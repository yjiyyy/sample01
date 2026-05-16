using UnityEngine;

/// <summary>
/// 플레이어 중독 디버프: 지속·틱 데미지는 머지 규칙, 틱 간격·FX·HP바 색은 마지막 갱신 SO 기준.
/// 무적 중에는 틱 피해 없음(시간은 감소). 사망·부활 대기 진입 시 해제.
/// </summary>
[DisallowMultipleComponent]
public class PlayerPoisonDebuffRuntime : MonoBehaviour
{
    private PlayerHealth playerHealth;
    private WeaponDataSO poisonTickWeapon;

    private float mergedDurationCap;
    private float mergedDamagePerTick;
    private float remainingDuration;
    private float tickAccumulator;
    private Color lastHpBarTint;
    private bool hasLastTint;

    /// <summary>마지막으로 <see cref="RegisterPoisonHit"/>에 넘어온 SO(틱 간격·FX·스케일·색 표시).</summary>
    private PoisonStatusConfigSO activePresentationConfig;

    private GameObject activeLoopFx;
    private GameObject loopFxPrefabSource;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = GetComponentInParent<PlayerHealth>();

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
        poisonTickWeapon.name = "PoisonDebuffTickProxy";
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

    /// <summary>독 피격으로 중독을 걸거나 갱신합니다. 무적/사망 처리는 호출 전에 걸러야 합니다.</summary>
    public void RegisterPoisonHit(PoisonStatusConfigSO config)
    {
        if (config == null || playerHealth == null || playerHealth.IsDeadProcessed())
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
        if (playerHealth == null)
            return;

        if (playerHealth.IsDeadProcessed())
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
            if (mergedDamagePerTick > 0f && !playerHealth.IsInvulnerableNow())
                playerHealth.ApplyDamage(mergedDamagePerTick, Vector3.zero, poisonTickWeapon, 1f, null);
        }

        if (remainingDuration <= 0f)
            ClearPoisonState();
    }

    private void RefreshLoopFxForPresentation()
    {
        if (playerHealth == null)
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

        Transform root = playerHealth.transform.root;
        if (root == null)
            return;

        activeLoopFx = Instantiate(wantPrefab, root);
        loopFxPrefabSource = wantPrefab;
        ApplyLoopFxTransform(activePresentationConfig);
    }

    private void ApplyLoopFxTransform(PoisonStatusConfigSO cfg)
    {
        if (activeLoopFx == null || playerHealth == null || cfg == null)
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
