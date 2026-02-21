using System.Collections;
using UnityEngine;

/// <summary>
/// EnemyHealth
/// - Manages HP and optional shield.
/// - Shield now entirely manages the "super-armor" state: HasSuperArmor == (currentShield > 0).
/// - Exposes compatibility API used by HitBox and UI code (ApplyDamage, GetCurrentHP/GetMaxHP, UseShield()).
/// </summary>
[DisallowMultipleComponent]
public class EnemyHealth : MonoBehaviour
{
    [Header("체력")]
    public float maxHP = 100f;
    private float currentHP;

    [Header("실드 사용")]
    public bool useShield = false;
    public float maxShield = 150f;
    private float currentShield = 0f;

    [Tooltip("실드 브레이크(그로기) 지속 시간")]
    public float shieldBreakDuration = 2f;

    [Tooltip("그로기 종료 후 재충전까지 추가 대기 (끝나면 즉시 Full 회복)")]
    public float shieldRechargeDelay = 3f;

    [Header("디버그")]
    public bool showShieldLogs = false;

    // Cached refs
    private Enemy enemy;
    private Animator animator;

    // Animator parameter hashes (optional - existing project may use them)
    private int hashShieldCharged;

    // shield break state
    private bool isShieldBreak = false;
    private Coroutine shieldRechargeRoutine;

    // Public read-only property to indicate super-armor state: shield > 0 => super-armor
    public bool HasSuperArmor => useShield && currentShield > 0f;

    private void Awake()
    {
        currentHP = maxHP;
        currentShield = useShield ? maxShield : 0f;
        enemy = GetComponent<Enemy>();
        animator = enemy != null ? enemy.animator : GetComponent<Animator>();

        if (useShield && currentShield > 0f)
        {
            if (showShieldLogs) Debug.Log($"[EnemyHealth] Starting with shield ({currentShield}/{maxShield}) - super-armor active.");
        }
    }

    // -------------------------------------------------------
    // Public compatibility API (used by existing HitBox/UI code)
    // -------------------------------------------------------
    public void ApplyDamage(float amount) => ApplyDamage(amount, Vector3.zero, null, 1f, null);
    public void ApplyDamage(float amount, WeaponDataSO weapon) => ApplyDamage(amount, Vector3.zero, weapon, 1f, null);
    public void ApplyDamage(float amount, Vector3 hitDir, WeaponDataSO weapon) => ApplyDamage(amount, hitDir, weapon, 1f, null);

    // Main damage entry point (preserves existing behavior: shield absorbs first, shieldBreak triggers, then HP)
    public void ApplyDamage(float amount, Vector3 hitDir, WeaponDataSO weapon, float impactScale, System.Nullable<Vector3> hitPoint = null)
    {
        if (amount <= 0f || currentHP <= 0f) return;

        WeaponDataSO.TrySpawnHitEffectAt(weapon, hitPoint);

        float remaining = amount;

        // Shield absorption (when not shield broken)
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
                // all absorbed by shield
                return;
            }
        }

        // Apply remaining damage to HP
        currentHP -= remaining;
        if (showShieldLogs)
            Debug.Log($"{gameObject.name} HP -{remaining:F1} → {currentHP:F1}/{maxHP:F1}");

        if (currentHP <= 0f)
        {
            currentHP = 0f;
            Die(hitDir, weapon, impactScale);
        }
    }

    // -------------- Shield / Shield-break logic --------------
    private void TriggerShieldBreak(Vector3 hitDir, WeaponDataSO weapon, float impactScale)
    {
        if (!useShield) return;
        if (isShieldBreak) return;

        isShieldBreak = true;
        if (showShieldLogs) Debug.Log("[Shield] Break started. Super-armor disabled.");

        if (shieldRechargeRoutine != null) StopCoroutine(shieldRechargeRoutine);
        shieldRechargeRoutine = StartCoroutine(ShieldRechargeCoroutine());

        // Optionally, you might want to also interrupt enemy actions on shield break:
        // enemy?.SetState(Enemy.EnemyState.ShieldBreak, true);
    }

    private IEnumerator ShieldRechargeCoroutine()
    {
        // Wait additional delay before full recharge
        yield return new WaitForSeconds(shieldRechargeDelay);

        // End shield break state
        isShieldBreak = false;

        // Fully recharge shield
        currentShield = maxShield;

        if (showShieldLogs) Debug.Log("[Shield] Recharged after break. Super-armor re-enabled.");

        shieldRechargeRoutine = null;
    }

    // Public helper: reduce shield (e.g., external effects)
    public void ReduceShield(float amount)
    {
        if (!useShield || isShieldBreak || amount <= 0f) return;

        float prev = currentShield;
        currentShield = Mathf.Max(0f, currentShield - amount);

        if (showShieldLogs)
            Debug.Log($"[Shield] Reduced {amount:F1} ({prev:F1} -> {currentShield:F1})");

        if (currentShield <= 0f)
        {
            TriggerShieldBreak(Vector3.zero, null, 1f);
        }
    }

    private void Die(Vector3 hitDir, WeaponDataSO weapon, float impactScale = 1f)
    {
        if (enemy != null)
            enemy.Die(hitDir, weapon, impactScale);
    }

    // Optional helper: expose current shield value (read-only)
    public float GetCurrentShield() => currentShield;
    public float GetMaxShield() => maxShield;

    // Compatibility methods expected by UI / other code
    public float GetCurrentHP() => currentHP;
    public float GetMaxHP() => maxHP;
    public bool UseShield() => useShield;

    // Optional: force set shield (editor/test)
    public void SetShield(float v)
    {
        if (!useShield) return;
        currentShield = Mathf.Clamp(v, 0f, maxShield);
        if (showShieldLogs) Debug.Log($"[Shield] Set to {currentShield}/{maxShield}");
    }
}