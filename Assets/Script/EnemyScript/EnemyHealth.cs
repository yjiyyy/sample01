using System;
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
    private bool isBleeding = false;
    private Coroutine bleedingRoutine;
    private EnemyPoisonDebuffRuntime poisonDebuffRuntime;

    /// <summary>HP가 0이 되어 Die가 확정될 때 1회 호출 (enemy.Die 직전).</summary>
    public event Action OnDeath;

    private bool deathInvoked;
    private bool isSpawnInvincible;

    public bool IsDeadProcessed() => deathInvoked;
    public bool IsSpawnInvincible => isSpawnInvincible;

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

        EnsurePoisonDebuffRuntimeReference();
    }

    private void EnsurePoisonDebuffRuntimeReference()
    {
        if (poisonDebuffRuntime != null)
            return;

        poisonDebuffRuntime = GetComponent<EnemyPoisonDebuffRuntime>() ??
                              GetComponentInChildren<EnemyPoisonDebuffRuntime>(true) ??
                              GetComponentInParent<EnemyPoisonDebuffRuntime>();

        if (poisonDebuffRuntime == null)
        {
            poisonDebuffRuntime = gameObject.AddComponent<EnemyPoisonDebuffRuntime>();
        }
    }

    // -------------------------------------------------------
    // Public compatibility API (used by existing HitBox/UI code)
    // -------------------------------------------------------
    public void ApplyDamage(float amount) => ApplyDamage(amount, Vector3.zero, null, 1f, null);
    public void ApplyDamage(float amount, WeaponDataSO weapon) => ApplyDamage(amount, Vector3.zero, weapon, 1f, null);
    public void ApplyDamage(float amount, Vector3 hitDir, WeaponDataSO weapon) => ApplyDamage(amount, hitDir, weapon, 1f, null);

    public void SetSpawnInvincible(bool value) => isSpawnInvincible = value;

    /// <summary>
    /// 자폭 타이머 등 외부 사망. 스폰 무적·실드 흡수 없이 즉시 애니메이션 사망 처리.
    /// </summary>
    public void ForceDieForSelfDestruct()
    {
        if (deathInvoked || currentHP <= 0f) return;
        currentHP = 0f;
        Die(Vector3.zero, null, 1f);
    }

    // Main damage entry point (preserves existing behavior: shield absorbs first, shieldBreak triggers, then HP)
    public void ApplyDamage(float amount, Vector3 hitDir, WeaponDataSO weapon, float impactScale, System.Nullable<Vector3> hitPoint = null)
    {
        if (currentHP <= 0f || deathInvoked)
            return;

        if (isSpawnInvincible)
            return;

        if (amount <= 0f)
        {
            if (weapon != null && weapon.isPoisonAttack && weapon.poisonOnHitStatus != null)
                ApplyPoisonStatus(weapon.poisonOnHitStatus);
            return;
        }

        WeaponDataSO.TrySpawnHitEffectAt(weapon, hitPoint);

        float remaining = amount;
        bool poisonHit = weapon != null && weapon.isPoisonAttack;
        bool bypassShield = poisonHit;

        // Shield absorption (when not shield broken). 독 피해는 실드를 우회해 HP에만 적용.
        if (!bypassShield && useShield && currentShield > 0f && !isShieldBreak)
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

        if (!deathInvoked && poisonHit && weapon.poisonOnHitStatus != null)
        {
            EnsurePoisonDebuffRuntimeReference();
            poisonDebuffRuntime?.RegisterPoisonHit(weapon.poisonOnHitStatus);
        }

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
        if (isSpawnInvincible) return;
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
        if (deathInvoked) return;
        deathInvoked = true;

        if (bleedingRoutine != null)
        {
            StopCoroutine(bleedingRoutine);
            bleedingRoutine = null;
        }
        isBleeding = false;

        TryClearPoisonDebuff();

        OnDeath?.Invoke();

        if (enemy != null)
            enemy.Die(hitDir, weapon, impactScale);
    }

    public bool IsBleeding => isBleeding;

    /// <summary>독 피해 없이 중독 상태만 걸거나 갱신합니다.</summary>
    public void ApplyPoisonStatus(PoisonStatusConfigSO config)
    {
        if (isSpawnInvincible) return;
        if (config == null || currentHP <= 0f || deathInvoked)
            return;

        EnsurePoisonDebuffRuntimeReference();
        poisonDebuffRuntime?.RegisterPoisonHit(config);
    }

    private void TryClearPoisonDebuff()
    {
        EnsurePoisonDebuffRuntimeReference();
        poisonDebuffRuntime?.ClearPoisonState();
    }

    /// <summary>
    /// 출혈을 1회만 적용합니다. 이미 출혈 중이면 무시합니다.
    /// </summary>
    public bool TryApplyBleedOnce(float duration, float tickInterval, float damagePerTick, GameObject bleedTickEffectPrefab = null)
    {
        if (isSpawnInvincible) return false;
        if (currentHP <= 0f || deathInvoked)
            return false;

        if (isBleeding)
            return false;

        if (duration <= 0f || tickInterval <= 0f || damagePerTick <= 0f)
            return false;

        isBleeding = true;
        bleedingRoutine = StartCoroutine(BleedRoutine(duration, tickInterval, damagePerTick, bleedTickEffectPrefab));
        return true;
    }

    private IEnumerator BleedRoutine(float duration, float tickInterval, float damagePerTick, GameObject bleedTickEffectPrefab)
    {
        float tick = Mathf.Max(0.05f, tickInterval);
        float endTime = Time.time + Mathf.Max(0.05f, duration);
        var wait = new WaitForSeconds(tick);

        while (Time.time < endTime)
        {
            yield return wait;

            if (currentHP <= 0f || deathInvoked)
                break;

            if (bleedTickEffectPrefab != null)
            {
                Transform root = transform.root != null ? transform.root : transform;
                UnityEngine.Object.Instantiate(bleedTickEffectPrefab, root.position, Quaternion.identity, root);
            }

            ApplyDamage(damagePerTick, Vector3.zero, null, 1f, null);
        }

        isBleeding = false;
        bleedingRoutine = null;
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