using UnityEngine;
using System.Collections;

/// <summary>
/// 적 전용 체력 + 실드 + 슈퍼아머 연동
/// </summary>
public class EnemyHealth : MonoBehaviour
{
    [Header("체력")]
    public float maxHP = 100f;
    private float currentHP;

    [Header("실드 사용")]
    public bool useShield = false;
    public float maxShield = 150f;

    [Tooltip("실드 브레이크(그로기) 지속 시간")]
    public float shieldBreakDuration = 2f;

    [Tooltip("그로기 종료 후 재충전까지 추가 대기 (끝나면 즉시 Full 회복)")]
    public float shieldRechargeDelay = 3f;

    [Header("디버그")]
    public bool showShieldLogs = true;

    private float currentShield;
    private bool isShieldBreak = false;
    private Coroutine shieldBreakRoutine;

    private Enemy enemy;
    private Animator animator;

    private readonly int hashIsShieldBreak = Animator.StringToHash("IsShieldBreak");
    private readonly int hashShieldCharged = Animator.StringToHash("ShieldCharged");

    void Awake()
    {
        currentHP = maxHP;
        currentShield = useShield ? maxShield : 0f;
        enemy = GetComponent<Enemy>();
        animator = enemy != null ? enemy.animator : GetComponent<Animator>();

        // 초기 실드가 있다면 슈퍼아머 부여
        if (useShield && currentShield > 0f && enemy != null)
        {
            enemy.AddSuperArmor(SuperArmorSource.Shield);
        }
    }

    private void OnDisable()
    {
        if (shieldBreakRoutine != null)
        {
            StopCoroutine(shieldBreakRoutine);
            shieldBreakRoutine = null;
        }
    }

    public bool IsShieldBreak() => isShieldBreak;

    // -------------- 피해 처리 --------------
    public void ApplyDamage(float amount) => ApplyDamage(amount, Vector3.zero, null, 1f);
    public void ApplyDamage(float amount, WeaponDataSO weapon) => ApplyDamage(amount, Vector3.zero, weapon, 1f);
    public void ApplyDamage(float amount, Vector3 hitDir, WeaponDataSO weapon) => ApplyDamage(amount, hitDir, weapon, 1f);

    public void ApplyDamage(float amount, Vector3 hitDir, WeaponDataSO weapon, float impactScale)
    {
        if (amount <= 0f || currentHP <= 0f) return;

        float remaining = amount;

        // 실드 흡수 (브레이크 중이 아닐 때만)
        if (useShield && currentShield > 0f && !isShieldBreak)
        {
            float absorb = Mathf.Min(currentShield, remaining);
            currentShield -= absorb;
            remaining -= absorb;

            if (showShieldLogs)
                Debug.Log($"[Shield] -{absorb:F1} (Remain {currentShield:F1}/{maxShield:F1})");

            if (currentShield <= 0f)
            {
                TriggerShieldBreak(hitDir, weapon, impactScale);
            }

            if (remaining <= 0f)
            {
                // HP에 피해 없음
                return;
            }
        }

        // 실드 없거나 남은 데미지 HP 적용
        currentHP -= remaining;
        if (showShieldLogs)
            Debug.Log($"{gameObject.name} HP -{remaining:F1} → {currentHP:F1}/{maxHP:F1}");

        if (currentHP <= 0f)
        {
            Die(hitDir, weapon, impactScale);
        }
    }

    // -------------- 실드 브레이크 --------------
    private void TriggerShieldBreak(Vector3 hitDir, WeaponDataSO weapon, float impactScale)
    {
        if (isShieldBreak) return;
        if (enemy != null && enemy.CurrentState == Enemy.EnemyState.Dead) return;

        isShieldBreak = true;
        currentShield = 0f;

        // Rush 중단 & 쿨다운 미부여 (실패 처리)
        if (enemy != null && enemy.attackCtrl != null && enemy.attackCtrl.IsRushing)
        {
            enemy.attackCtrl.StopRushExternally(noCooldown: true);
        }

        // Shield SuperArmor 제거
        enemy?.RemoveSuperArmor(SuperArmorSource.Shield);

        // 상태 전환
        enemy?.SetState(Enemy.EnemyState.ShieldBreak, true);

        if (animator != null)
        {
            animator.SetBool(hashIsShieldBreak, true);
        }

        if (showShieldLogs)
        {
            Debug.Log($"[ShieldBreak] Enter (Groggy {shieldBreakDuration:F2}s, RechargeDelay {shieldRechargeDelay:F2}s)");
        }

        if (shieldBreakRoutine != null)
        {
            StopCoroutine(shieldBreakRoutine);
        }
        shieldBreakRoutine = StartCoroutine(ShieldBreakFlow());
    }

    private IEnumerator ShieldBreakFlow()
    {
        // 그로기 (공격/AI 정지)
        yield return new WaitForSeconds(shieldBreakDuration);

        ExitShieldBreak(); // Chase 복귀

        // 재충전 지연
        if (shieldRechargeDelay > 0f)
            yield return new WaitForSeconds(shieldRechargeDelay);

        FullyRechargeShield();
        shieldBreakRoutine = null;
    }

    private void ExitShieldBreak()
    {
        if (!isShieldBreak) return;
        isShieldBreak = false;

        if (animator != null) animator.SetBool(hashIsShieldBreak, false);

        if (enemy != null && enemy.CurrentState == Enemy.EnemyState.ShieldBreak)
        {
            enemy.SetState(Enemy.EnemyState.Chase);
        }

        if (showShieldLogs)
        {
            Debug.Log($"[ShieldBreak] Exit (→ Chase, wait recharge {shieldRechargeDelay:F2}s)");
        }
    }

    private void FullyRechargeShield()
    {
        if (!useShield) return;
        currentShield = maxShield;
        enemy?.AddSuperArmor(SuperArmorSource.Shield);

        if (animator != null)
        {
            animator.SetTrigger(hashShieldCharged);
        }

        if (showShieldLogs)
            Debug.Log($"[ShieldCharged] Full ({currentShield}/{maxShield})");
    }

    // -------------- 사망 처리 --------------
    private void Die(Vector3 hitDir, WeaponDataSO weapon, float impactScale = 1f)
    {
        if (showShieldLogs) Debug.Log($"{gameObject.name} 사망");
        if (shieldBreakRoutine != null)
        {
            StopCoroutine(shieldBreakRoutine);
            shieldBreakRoutine = null;
        }
        enemy?.Die(hitDir, weapon, impactScale);
    }

    // -------------- 유틸 / Getter --------------
    public void SetHealth(float value) => currentHP = Mathf.Clamp(value, 0f, maxHP);
    public float GetCurrentHP() => currentHP;
    public float GetMaxHP() => maxHP;

    public float GetCurrentShield() => currentShield;
    public float GetMaxShield() => maxShield;
    public bool UseShield() => useShield;
}