using UnityEngine;

/// <summary>
/// 몬스터 중독 디버프: 지속·틱 데미지는 머지 규칙, 틱 간격·FX·HP바 색은 마지막 갱신 SO 기준.
/// 독 피해는 실드(슈퍼아머)를 우회하고 HP에만 적용됩니다.
/// </summary>
[DisallowMultipleComponent]
public class EnemyPoisonDebuffRuntime : MonoBehaviour
{
    private EnemyHealth enemyHealth;
    private WeaponDataSO poisonTickWeapon;

    private float mergedDurationCap;
    private float mergedDamagePerTick;
    private float remainingDuration;
    private float tickAccumulator;
    private Color lastHpBarTint;
    private bool hasLastTint;

    private PoisonStatusConfigSO activePresentationConfig;

    private GameObject activeLoopFx;
    private GameObject loopFxPrefabSource;

    private void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        if (enemyHealth == null)
            enemyHealth = GetComponentInParent<EnemyHealth>();

        BuildTickWeaponProxy();
    }

    private void OnDestroy()
    {
        DestroyLoopFx();
        if (poisonTickWeapon != null)
            Destroy(poisonTickWeapon);
    }

    private void BuildTickWeaponProxy()
    {
        if (poisonTickWeapon != null)
            return;

        poisonTickWeapon = ScriptableObject.CreateInstance<WeaponDataSO>();
        poisonTickWeapon.hideFlags = HideFlags.HideAndDontSave;
        poisonTickWeapon.name = "EnemyPoisonDebuffTickProxy";
        poisonTickWeapon.isPoisonAttack = true;
        poisonTickWeapon.damageType = AttackDamageType.ProjectileGun;
        poisonTickWeapon.damage = 0f;
        poisonTickWeapon.knockbackDuration = 0f;
        poisonTickWeapon.knockbackPower = 0f;
        poisonTickWeapon.jerkIntensity = 0f;
        poisonTickWeapon.jerkDuration = 0f;
        poisonTickWeapon.stunDuration = 0f;
    }

    public bool IsPoisoned => remainingDuration > 0f;

    public bool TryGetPoisonHpBarTint(out Color tint)
    {
        if (remainingDuration > 0f && hasLastTint)
        {
            tint = lastHpBarTint;
            return true;
        }

        tint = default;
        return false;
    }

    /// <summary>독 피격으로 중독을 걸거나 갱신합니다. 사망 처리는 호출 전에 걸러야 합니다.</summary>
    public void RegisterPoisonHit(PoisonStatusConfigSO config)
    {
        if (config == null || enemyHealth == null || enemyHealth.IsDeadProcessed())
            return;

        mergedDurationCap = Mathf.Max(mergedDurationCap, config.poisonDurationSeconds);
        mergedDamagePerTick = Mathf.Max(mergedDamagePerTick, config.poisonDamagePerTick);
        remainingDuration = mergedDurationCap;

        activePresentationConfig = config;
        lastHpBarTint = config.hpBarFillWhilePoisoned;
        hasLastTint = true;

        if (poisonTickWeapon != null)
            poisonTickWeapon.damage = mergedDamagePerTick;

        RefreshLoopFxForPresentation();
    }

    public void ClearPoisonState()
    {
        remainingDuration = 0f;
        mergedDurationCap = 0f;
        mergedDamagePerTick = 0f;
        tickAccumulator = 0f;
        hasLastTint = false;
        activePresentationConfig = null;

        if (poisonTickWeapon != null)
            poisonTickWeapon.damage = 0f;

        DestroyLoopFx();
    }

    private void Update()
    {
        if (enemyHealth == null)
            return;

        if (enemyHealth.IsDeadProcessed())
        {
            if (remainingDuration > 0f || activeLoopFx != null)
                ClearPoisonState();
            return;
        }

        if (remainingDuration <= 0f)
            return;

        remainingDuration -= Time.deltaTime;

        float tick = Mathf.Max(0.05f, activePresentationConfig != null
            ? activePresentationConfig.poisonTickIntervalSeconds
            : 0.5f);

        tickAccumulator += Time.deltaTime;
        while (tickAccumulator >= tick && remainingDuration > 0f)
        {
            tickAccumulator -= tick;
            if (mergedDamagePerTick > 0f)
                enemyHealth.ApplyDamage(mergedDamagePerTick, Vector3.zero, poisonTickWeapon, 1f, null);
        }

        if (remainingDuration <= 0f)
            ClearPoisonState();
    }

    private void RefreshLoopFxForPresentation()
    {
        if (enemyHealth == null)
            return;

        GameObject wantPrefab = activePresentationConfig != null ? activePresentationConfig.poisonLoopFxPrefab : null;

        if (wantPrefab == null)
        {
            DestroyLoopFx();
            loopFxPrefabSource = null;
            return;
        }

        if (activeLoopFx != null && loopFxPrefabSource != wantPrefab)
            DestroyLoopFx();

        if (activeLoopFx != null)
        {
            ApplyLoopFxTransform(activePresentationConfig);
            return;
        }

        Transform root = enemyHealth.transform.root;
        if (root == null)
            return;

        activeLoopFx = Instantiate(wantPrefab, root);
        activeLoopFx.name = wantPrefab.name + "_PoisonLoop";
        loopFxPrefabSource = wantPrefab;
        ApplyLoopFxTransform(activePresentationConfig);
    }

    private void ApplyLoopFxTransform(PoisonStatusConfigSO cfg)
    {
        if (activeLoopFx == null || enemyHealth == null || cfg == null)
            return;

        activeLoopFx.transform.localPosition = Vector3.zero;
        activeLoopFx.transform.localRotation = Quaternion.identity;

        Vector3 prefabScale = loopFxPrefabSource != null ? loopFxPrefabSource.transform.localScale : Vector3.one;
        float mul = Mathf.Max(0.01f, cfg.poisonFxScaleMultiplier);
        activeLoopFx.transform.localScale = prefabScale * mul;
    }

    private void DestroyLoopFx()
    {
        if (activeLoopFx != null)
        {
            Destroy(activeLoopFx);
            activeLoopFx = null;
        }

        loopFxPrefabSource = null;
    }
}
