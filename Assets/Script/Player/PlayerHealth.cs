using UnityEngine;

/// <summary>
/// 플레이어 전용 체력 관리 시스템
/// - 레벨업/경험 관련 로직은 PlayerStats로 이동하였습니다.
/// - 이 컴포넌트는 체력 관련만 담당합니다.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("기본 체력")]
    public float maxHP = 100f;
    private float currentHP;

    [Header("피격 반응 (무게)")]
    [Tooltip("값이 클수록 넉백에 덜 밀림")]
    public float weight = 1f;

    // ✅ 사망 처리 중복 방지 플래그 (HP 0이 여러 번 들어오거나, 피격이 연속으로 들어와도 1회만 처리)
    private bool deadProcessed = false;

    void Awake()
    {
        currentHP = maxHP;
        deadProcessed = false;
    }

    /* ───────── 피해 처리 ───────── */
    public void ApplyDamage(float amount)
    {
        ApplyDamage(amount, Vector3.zero, null, 1f);
    }

    public void ApplyDamage(float amount, WeaponDataSO weapon)
    {
        ApplyDamage(amount, Vector3.zero, weapon, 1f);
    }

    public void ApplyDamage(float amount, Vector3 hitDir, WeaponDataSO weapon)
    {
        ApplyDamage(amount, hitDir, weapon, 1f);
    }

    public void ApplyDamage(float amount, Vector3 hitDir, WeaponDataSO weapon, float impactScale)
    {
        // 이미 죽었으면 추가 데미지/넉백/로그 등 모두 무시
        if (deadProcessed) return;

        currentHP -= amount;
        Debug.Log($"플레이어가 {amount:F1} 피해! scale:{impactScale:F2} | HP: {currentHP:F1}");

        if (currentHP <= 0f)
        {
            currentHP = 0f;
            Die(hitDir, weapon, impactScale);
        }
    }

    /* ───────── 회복 처리 ───────── */
    public void Heal(float amount)
    {
        if (amount <= 0f) return;
        if (deadProcessed) return; // 죽은 상태에서는 회복 금지(부활은 나중에 별도 처리)

        currentHP += amount;
        currentHP = Mathf.Clamp(currentHP, 0f, maxHP);

        Debug.Log($"플레이어가 {amount:F1} 회복됨 → 현재 HP: {currentHP:F1}");
    }

    /* ───────── 사망 처리 ───────── */
    private void Die(Vector3 hitDir, WeaponDataSO weapon, float impactScale = 1f)
    {
        if (deadProcessed) return;
        deadProcessed = true;

        Debug.Log("플레이어 사망 (HP 0) → 즉시 Dead 상태 진입");

        // 1) 상태(논리) 먼저 Dead로 만들어서 "넉백 후 die" 같은 흐름을 차단
        //    (PlayerWeaponController가 Dead면 ForceApplyKnockback이 무시되도록 이미 방어 로직이 존재함)
        var weaponCtrl = GetComponent<PlayerWeaponController>();
        if (weaponCtrl != null)
        {
            // 프로젝트 내부 구현에 따라 SetState가 public일 수도/아닐 수도 있어서
            // 여기서는 "있으면 호출" 방식으로 안전하게 처리.
            // (만약 컴파일 에러가 나면, 다음 단계에서 PlayerWeaponController의 공개 API에 맞춰서 조정하면 됨)
            try
            {
                // 리플렉션을 쓰는 이유: 지금 대화에서 PlayerWeaponController의 public API를 확정 못 했기 때문
                // (컴파일 에러 없이 최대한 안전하게 적용)
                var m = weaponCtrl.GetType().GetMethod("SetState",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (m != null)
                {
                    m.Invoke(weaponCtrl, new object[] { PlayerState.Dead });
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PlayerHealth] weaponCtrl SetState(PlayerState.Dead) 호출 시도 실패: {ex.Message}");
            }
        }

        // 2) 애니메이터 IsDead를 최우선으로 확정
        //    (PlayerAnimationController는 ForceAnimationByState(Dead)에서 IsDead=true 처리함)
        var animCtrl = GetComponent<PlayerAnimationController>();
        if (animCtrl != null)
        {
            animCtrl.ForceAnimationByState(PlayerState.Dead);
        }
        else
        {
            // 혹시 PlayerAnimationController가 없으면, Animator 직접 세팅(안전장치)
            var animator = GetComponent<Animator>();
            if (animator != null)
            {
                animator.SetBool("IsDead", true);
            }
        }

        // 3) 입력/행동 차단: 핵심 컨트롤 스크립트 비활성화
        //    (애니메이션은 재생돼야 하므로 Animator/PlayerAnimationController는 끄지 않음)
        var move = GetComponent<PlayerMovement>();
        if (move != null) move.enabled = false;

        if (weaponCtrl != null) weaponCtrl.enabled = false;

        var evade = GetComponent<PlayerEvadeController>();
        if (evade != null) evade.enabled = false;

        // 4) 죽음 상태 충돌 OFF: 루트 기준 모든 Collider 비활성화 (자식 포함)
        var root = transform.root;
        if (root != null)
        {
            var cols = root.GetComponentsInChildren<Collider>(true);
            foreach (var c in cols)
            {
                if (c != null) c.enabled = false;
            }
        }

        // 5) A안: 물리도 완전 정지 (Rigidbody 정지 + Kinematic)
        //    Root motion이 없으므로 애니메이션과 충돌하지 않음.
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null && transform.root != null)
            rb = transform.root.GetComponent<Rigidbody>();

        if (rb != null)
        {
#if UNITY_6000_0_OR_NEWER
            // Unity6: Rigidbody.velocity 대신 linearVelocity가 권장/사용되는 프로젝트가 있음
            rb.linearVelocity = Vector3.zero;
#else
            rb.velocity = Vector3.zero;
#endif
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        // 6) 5초 후 플레이어 프리팹 루트 삭제
        if (transform.root != null)
        {
            Destroy(transform.root.gameObject, 5f);
        }
        else
        {
            Destroy(gameObject, 5f);
        }
    }

    /* ───────── 유틸 ───────── */
    public void SetHealth(float value)
    {
        currentHP = Mathf.Clamp(value, 0f, maxHP);

        // 에디터/디버그로 강제로 0을 넣는 경우도 있으니, 여기서도 사망 처리 보강
        if (!deadProcessed && currentHP <= 0f)
        {
            currentHP = 0f;
            Die(Vector3.zero, null, 1f);
        }
    }

    public float GetCurrentHP() => currentHP;
    public float GetMaxHP() => maxHP;
    public float GetWeight() => weight;
}