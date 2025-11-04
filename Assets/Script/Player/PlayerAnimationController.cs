using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player 애니메이션 제어기
/// - UpperBody 레이어 토글/임시 비활성화(넉백 등 CC 시) 로직 포함
/// - Animator 파라미터가 없을 때 예외가 발생하지 않도록 안전하게 호출하도록 수정됨
/// - 트리거 안전 호출(SafeSetTrigger) 추가 및 Knockback/Stun 진입시 트리거 사용으로 수정
/// </summary>
public class PlayerAnimationController : MonoBehaviour
{
    private Animator animator;
    private PlayerMovement movement;

    [SerializeField] public WeaponBehavior weaponBehavior;

    // Animator 파라미터 해시
    private readonly int hashSpeed = Animator.StringToHash("Speed");
    private readonly int hashAttackIndex = Animator.StringToHash("AttackIndex");
    private readonly int hashIsAttacking = Animator.StringToHash("IsAttacking");
    private readonly int hashIsUpperAttacking = Animator.StringToHash("IsUpperAttacking"); // <-- 상체 전용
    private readonly int hashIsBackStep = Animator.StringToHash("IsBackStep"); // <-- AR 하체용 BackStep
    private readonly int hashIsDead = Animator.StringToHash("IsDead");
    private readonly int hashKnockback = Animator.StringToHash("Knockback"); // 트리거로 사용
    private readonly int hashKnockbackIndex = Animator.StringToHash("KnockbackIndex");
    private readonly int hashStun = Animator.StringToHash("Stun"); // 트리거로 사용
    private readonly int hashIsEvading = Animator.StringToHash("IsEvading"); // ✅ 회피 파라미터 추가

    // Lower-body 재생속도 전용 파라미터
    private readonly int hashLowerBodySpeed = Animator.StringToHash("LowerBodySpeed");

    // UpperBody 레이어 요청 플래그:
    private bool upperBodyRequestedEnabled = false;

    // UpperBody 레이어 이름(설정과 일치해야 함)
    private const string upperLayerName = "UpperBody";

    void Awake()
    {
        animator = GetComponent<Animator>();
        movement = GetComponent<PlayerMovement>();
    }

    void Update()
    {
        if (animator == null) return;
        float speed = (movement != null) ? movement.GetAnimatorSpeedEstimate() : 0f;
        SafeSetFloat(hashSpeed, speed);
    }

    /* ───────── 안전 호출 헬퍼 ───────── */

    // Animator에 해당 해시(파라미터)가 존재하는지 검사
    private bool HasParameter(int hash)
    {
        if (animator == null) return false;
        var pars = animator.parameters;
        for (int i = 0; i < pars.Length; ++i)
        {
            if (pars[i].nameHash == hash) return true;
        }
        return false;
    }

    private void SafeSetBool(int hash, bool value)
    {
        if (animator == null) return;
        if (HasParameter(hash))
        {
            animator.SetBool(hash, value);
        }
        else
        {
            Debug.LogWarning($"[PlayerAnim] SetBool skipped — parameter not found (hash:{hash})");
        }
    }

    private void SafeResetTrigger(int hash)
    {
        if (animator == null) return;
        if (HasParameter(hash))
        {
            animator.ResetTrigger(hash);
        }
        else
        {
            Debug.LogWarning($"[PlayerAnim] ResetTrigger skipped — parameter not found (hash:{hash})");
        }
    }

    private void SafeSetTrigger(int hash)
    {
        if (animator == null) return;
        if (HasParameter(hash))
        {
            animator.SetTrigger(hash);
        }
        else
        {
            Debug.LogWarning($"[PlayerAnim] SetTrigger skipped — parameter not found (hash:{hash})");
        }
    }

    private void SafeSetFloat(int hash, float value)
    {
        if (animator == null) return;
        if (HasParameter(hash))
        {
            animator.SetFloat(hash, value);
        }
        else
        {
            Debug.LogWarning($"[PlayerAnim] SetFloat skipped — parameter not found (hash:{hash})");
        }
    }

    /* ───────── 상태별 강제 애니메이션 전환 (블렌드 트리 대응) ───────── */

    public void ForceAnimationByState(PlayerState newState)
    {
        if (animator == null) return;

        // 상태에 맞춰 파라미터 리셋(안전호출)
        ResetAllAnimatorParams(newState);

        // ── UpperBody 레이어: CC 진입 시 임시 비활성, 복귀 시 요청 플래그에 따라 복구
        int upperLayer = GetUpperLayerIndex();
        if (upperLayer >= 0)
        {
            if (IsStateBlockingUpperBody(newState))
            {
                if (animator.GetLayerWeight(upperLayer) > 0f)
                {
                    animator.SetLayerWeight(upperLayer, 0f);
                    animator.Update(0f);
                    Debug.Log("[PlayerAnim] CC 진입 → UpperBody 레이어 임시 비활성화");
                }
            }
            else
            {
                float targetWeight = upperBodyRequestedEnabled ? 1f : 0f;
                if (!Mathf.Approximately(animator.GetLayerWeight(upperLayer), targetWeight))
                {
                    animator.SetLayerWeight(upperLayer, targetWeight);
                    animator.Update(0f);
                    Debug.Log($"[PlayerAnim] 상태 복귀 → UpperBody 레이어 weight 복구 target={targetWeight}");
                }
            }
        }

        switch (newState)
        {
            case PlayerState.Idle:
                SafeSetFloat(hashSpeed, 0f);
                animator.Play("Idle/Run", 0, 0f);
                Debug.Log("[PlayerAnim] 강제 전환 → Idle");
                break;

            case PlayerState.Move:
                SafeSetFloat(hashSpeed, 1f);
                animator.Play("Idle/Run", 0, 0f);
                Debug.Log("[PlayerAnim] 강제 전환 → Run");
                break;

            case PlayerState.Attack:
                Debug.Log("[PlayerAnim] Attack 상태 - PlayAttack() 별도 호출 필요");
                break;

            case PlayerState.Knockback:
                // Knockback은 트리거로 알리고, 재생(강제)도 실행
                float randomKnockbackIndex = UnityEngine.Random.Range(0, 3);
                SafeSetFloat(hashKnockbackIndex, randomKnockbackIndex);

                // 안전하게 트리거 설정
                SafeSetTrigger(hashKnockback);

                // 강제 재생 (fallback 혹은 immediate visual)
                animator.Play("Knockback_Blend Tree", 0, 0f);
                Debug.Log($"[PlayerAnim] 강제 전환 → Knockback (Index: {randomKnockbackIndex})");
                break;

            case PlayerState.Stun:
                // Stun은 트리거로 알림
                SafeSetTrigger(hashStun);
                // 즉시 재생(보장)
                animator.Play("Stun", 0, 0f);
                Debug.Log("[PlayerAnim] 강제 전환 → Stun");
                break;

            case PlayerState.Evade:
                SafeSetBool(hashIsEvading, true);
                animator.Update(0f);
                animator.Play("Evade", 0, 0f);
                Debug.Log("[PlayerAnim] 강제 전환 → Evade");
                break;

            case PlayerState.Dead:
                SafeSetBool(hashIsDead, true);
                animator.Play("Death", 0, 0f);
                Debug.Log("[PlayerAnim] 강제 전환 → Death");
                break;
        }
    }

    private bool IsStateBlockingUpperBody(PlayerState state)
    {
        return state == PlayerState.Knockback ||
               state == PlayerState.Stun ||
               state == PlayerState.Evade ||
               state == PlayerState.Dead;
    }

    private int GetUpperLayerIndex()
    {
        if (animator == null) return -1;
        return animator.GetLayerIndex(upperLayerName);
    }

    private void ResetAllAnimatorParams(PlayerState targetState = PlayerState.Idle)
    {
        if (animator == null) return;

        // 트리거 리셋
        SafeResetTrigger(hashKnockback);
        SafeResetTrigger(hashStun);

        // Bool 리셋
        SafeSetBool(hashIsAttacking, false);
        SafeSetBool(hashIsUpperAttacking, false);
        SafeSetBool(hashIsBackStep, false);
        SafeSetBool(hashIsDead, false);

        if (targetState != PlayerState.Evade)
        {
            SafeSetBool(hashIsEvading, false);
        }

        // Float 리셋 (하체 속도는 기본 1으로 복구)
        SafeSetFloat(hashAttackIndex, 0f);
        SafeSetFloat(hashKnockbackIndex, 0f);
        SafeSetFloat(hashLowerBodySpeed, 1f);

        Debug.Log("[PlayerAnim] 모든 애니메이터 파라미터 리셋 완료");
    }

    public void EndEvade()
    {
        SafeSetBool(hashIsEvading, false);
        Debug.Log("[PlayerAnim] 회피 애니메이션 종료");
    }

    /* ───────── 공격 실행 (upperBodyOnly 옵션 포함) ───────── */
    public void PlayAttack(WeaponDataSO weaponData, bool upperBodyOnly = false)
    {
        if (animator == null) return;

        float randomIndex = UnityEngine.Random.Range(0, 3);

        if (upperBodyOnly)
        {
            // 상체 전용 재생: 최초 진입/재시작 로직(이전 구현 유지)
            SafeSetFloat(hashAttackIndex, randomIndex);
            SafeSetBool(hashIsUpperAttacking, true);

            int upperLayer = GetUpperLayerIndex();
            if (upperLayer >= 0)
            {
                bool currentlyUpper = false;
                if (HasParameter(hashIsUpperAttacking))
                    currentlyUpper = animator.GetBool(hashIsUpperAttacking);

                if (!currentlyUpper)
                {
                    animator.Play("Attack_BlendTree", upperLayer, 0f);
                    Debug.Log($"[PlayerAnim] Upper-body Attack 시작(초기) → Index:{randomIndex}, 무기:{weaponData?.weaponName}");
                }
                else
                {
                    animator.CrossFade("Attack_BlendTree", 0f, upperLayer, 0f);
                    Debug.Log($"[PlayerAnim] Upper-body Attack 재시작 → Index:{randomIndex}, 무기:{weaponData?.weaponName}");
                }

                if (animator.GetLayerWeight(upperLayer) <= 0f)
                    animator.SetLayerWeight(upperLayer, 1f);

                animator.Update(0f);
            }
            else
            {
                ResetAllAnimatorParams();
                SafeSetFloat(hashAttackIndex, randomIndex);
                SafeSetBool(hashIsAttacking, true);
                animator.Play("Attack_BlendTree", 0, 0f);
                Debug.Log($"[PlayerAnim] UpperLayer 없음 → 전체 Attack 시작(fallback) Index:{randomIndex}, 무기:{weaponData?.weaponName}");
            }
        }
        else
        {
            ResetAllAnimatorParams();
            SafeSetFloat(hashAttackIndex, randomIndex);
            SafeSetBool(hashIsAttacking, true);
            animator.Play("Attack_BlendTree", 0, 0f);
            Debug.Log($"[PlayerAnim] Attack 시작 → Index:{randomIndex}, 무기:{weaponData?.weaponName}");
        }
    }

    public void EndAttack()
    {
        if (animator == null) return;
        SafeSetBool(hashIsAttacking, false);
        SafeSetBool(hashIsUpperAttacking, false);
        // 하체 속도 복구
        SafeSetFloat(hashLowerBodySpeed, 1f);
        Debug.Log("[PlayerAnim] Attack 종료 (쿨타임 종료)");

        // 애니메이션 종료 시점: pending 전환이 있으면 실행
        var pwc = GetComponentInParent<PlayerWeaponController>();
        if (pwc != null)
        {
            pwc.ExecutePendingSwitchIfAnyImmediate();
        }
    }

    public void SetUpperBodyLayerEnabled(bool enabled)
    {
        if (animator == null) return;

        int layerIndex = GetUpperLayerIndex();
        if (layerIndex < 0)
        {
            Debug.LogWarning("[PlayerAnim] UpperBody 레이어가 없습니다. SetUpperBodyLayerEnabled 무시됨.");
            return;
        }

        upperBodyRequestedEnabled = enabled;

        if (enabled)
        {
            animator.SetLayerWeight(layerIndex, 1f);
            Debug.Log("[PlayerAnim] UpperBody 레이어 활성화");
        }
        else
        {
            SafeSetBool(hashIsAttacking, false);
            SafeSetBool(hashIsUpperAttacking, false);
            SafeSetBool(hashIsBackStep, false);
            SafeSetFloat(hashLowerBodySpeed, 1f);
            animator.SetLayerWeight(layerIndex, 0f);
            animator.Update(0f);
            Debug.Log("[PlayerAnim] UpperBody 레이어 비활성화 및 공격 파라미터 리셋");
        }
    }

    // 하체 전용 재생속도 설정(외부에서 호출)
    public void SetLowerBodyPlaybackSpeed(float speed)
    {
        SafeSetFloat(hashLowerBodySpeed, speed);
        //Debug.Log($"[PlayerAnim] LowerBodySpeed -> {speed}");
    }

    // 하체 BackStep 플래그 설정(외부에서 호출)
    public void SetBackStep(bool enabled)
    {
        SafeSetBool(hashIsBackStep, enabled);
        Debug.Log($"[PlayerAnim] IsBackStep -> {enabled}");
    }

    public void PlayChargedAttack(string stateNameOrClip)
    {
        if (animator == null) return;

        ResetAllAnimatorParams();
        SafeSetBool(hashIsAttacking, true);

        string s = string.IsNullOrEmpty(stateNameOrClip) ? "Attack_Charged01" : stateNameOrClip;
        animator.Play(s, 0, 0f);

        Debug.Log($"[PlayerAnim] ChargedAttack 시작 → {s}");
    }

    /* 애니메이션 이벤트 */
    public void AttackHit()
    {
        Debug.Log("💥 [AnimEvent] AttackHit() 호출됨");
        weaponBehavior?.AttackHit();
    }

    public void OnAttackStart() => Debug.Log("🕒 [AnimEvent] OnAttackStart() 호출됨");
    public void OnAttackEnd() => Debug.Log("✅ [AnimEvent] OnAttackEnd() 호출됨");

    public Animator GetAnimator() => animator;
}