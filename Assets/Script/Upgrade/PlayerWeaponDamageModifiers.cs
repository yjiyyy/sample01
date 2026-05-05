using UnityEngine;

/// <summary>
/// 업그레이드 등으로 누적된 무기 카테고리별 데미지 보정을 보관합니다.
/// 최종 피해 = max(0, baseDamage * (1 + 퍼센트 합) + 플랫 합)
/// </summary>
[DisallowMultipleComponent]
public class PlayerWeaponDamageModifiers : MonoBehaviour
{
    private const int CategoryCount = 4;
    private const bool EnableChainDebugLog = true;

    public struct ChainShotsConfig
    {
        public int bounceCount;
        public float searchRadius;
        public float damageMultiplier;
        public float chainTargetHoldDuration;
    }

    public struct BonusShotConfig
    {
        public float chance;
        public float lateralOffsetMeters;
        public float delayUnscaledSeconds;
    }

    private readonly float[] percentBonusSum = new float[CategoryCount];
    private readonly float[] flatBonusSum = new float[CategoryCount];
    private Upgrade cachedUpgrade;
    private PlayerHealth cachedPlayerHealth;

    /// <summary>
    /// owner 루트(플레이어) 기준으로 카테고리별 보정을 적용합니다. 컴포넌트가 없으면 baseDamage 그대로.
    /// </summary>
    public static float ScaleOutgoingDamage(GameObject ownerRoot, WeaponCategory category, float baseDamage)
    {
        if (ownerRoot == null)
            return baseDamage;

        var mods = ownerRoot.GetComponentInChildren<PlayerWeaponDamageModifiers>(true);
        if (mods == null)
        {
            // fallback: 장착 무기 루트가 플레이어 루트가 아닌 프리팹 구조에서도
            // 단일 플레이 기준으로 보정을 찾을 수 있게 지원합니다.
            mods = Object.FindFirstObjectByType<PlayerWeaponDamageModifiers>();
        }
        return mods != null ? mods.Apply(category, baseDamage) : baseDamage;
    }

    /// <summary>
    /// 실제 적중 피해량 기준으로 흡혈 효과를 적용합니다.
    /// </summary>
    public static void TryApplyVampiricPunchOnHit(GameObject ownerRoot, WeaponDataSO weapon, float dealtDamage)
    {
        if (ownerRoot == null || weapon == null || dealtDamage <= 0f)
            return;

        var mods = ownerRoot.GetComponentInChildren<PlayerWeaponDamageModifiers>(true);
        if (mods == null)
            mods = Object.FindFirstObjectByType<PlayerWeaponDamageModifiers>();
        if (mods == null)
            return;

        float lifeStealPercent = mods.GetVampiricPunchPercent(weapon);
        if (lifeStealPercent <= 0f)
            return;

        if (mods.cachedPlayerHealth == null)
            mods.cachedPlayerHealth = ResolvePlayerHealth(mods.cachedUpgrade);
        if (mods.cachedPlayerHealth == null)
            return;

        float healAmount = Mathf.Max(0f, dealtDamage) * lifeStealPercent;
        if (healAmount > 0f)
            mods.cachedPlayerHealth.Heal(healAmount);
    }

    /// <summary>
    /// 적중 시 출혈 효과를 적용합니다. 이미 출혈 중이면 재적용하지 않습니다.
    /// </summary>
    public static void TryApplyBleedingPunchOnHit(GameObject ownerRoot, WeaponDataSO weapon, EnemyHealth target)
    {
        if (ownerRoot == null || weapon == null || target == null)
            return;

        var mods = ownerRoot.GetComponentInChildren<PlayerWeaponDamageModifiers>(true);
        if (mods == null)
            mods = Object.FindFirstObjectByType<PlayerWeaponDamageModifiers>();
        if (mods == null)
            return;

        if (!mods.TryGetBleedingPunchConfig(weapon, out float chance, out float duration, out float tickInterval, out float damagePerTick, out GameObject bleedTickEffectPrefab))
            return;

        if (Random.value > chance)
            return;

        target.TryApplyBleedOnce(duration, tickInterval, damagePerTick, bleedTickEffectPrefab);
    }

    /// <summary>
    /// 적중 시 확률적으로 넉백→스턴 흐름용 프록시 무기 데이터를 생성합니다.
    /// true를 반환하면 호출 측에서 기존 push 분기 대신 ApplyKnockback(proxy)를 사용합니다.
    /// </summary>
    public static bool TryBuildStunningPunchProxyOnHit(GameObject ownerRoot, WeaponDataSO weapon, out WeaponDataSO proxyWeapon)
    {
        proxyWeapon = null;
        if (ownerRoot == null || weapon == null)
            return false;

        var mods = ownerRoot.GetComponentInChildren<PlayerWeaponDamageModifiers>(true);
        if (mods == null)
            mods = Object.FindFirstObjectByType<PlayerWeaponDamageModifiers>();
        if (mods == null)
            return false;

        if (!mods.TryGetStunningPunchConfig(
                weapon,
                out float chance,
                out float bonusKbDuration,
                out float bonusKbPower,
                out float bonusJerkIntensity,
                out float bonusJerkDuration,
                out float bonusStunDuration))
            return false;

        if (Random.value > chance)
            return false;

        var proxy = ScriptableObject.CreateInstance<WeaponDataSO>();
        proxy.hideFlags = HideFlags.HideAndDontSave;
        proxy.knockbackDuration = Mathf.Max(0f, weapon.knockbackDuration + bonusKbDuration);
        proxy.knockbackPower = Mathf.Max(0f, weapon.knockbackPower + bonusKbPower);
        proxy.jerkIntensity = Mathf.Max(0f, weapon.jerkIntensity + bonusJerkIntensity);
        proxy.jerkDuration = Mathf.Max(0f, weapon.jerkDuration + bonusJerkDuration);
        proxy.stunDuration = Mathf.Max(0f, weapon.stunDuration + bonusStunDuration);
        proxy.usePushInsteadOfKnockback = false;
        proxyWeapon = proxy;
        return true;
    }

    /// <summary>
    /// ProjectileGun 데미지 타입 공격에 적용되는 ChainShots 설정을 조회합니다.
    /// 여러 슬롯이 있으면 수치가 합산됩니다.
    /// </summary>
    public static bool TryGetChainShotsConfig(GameObject ownerRoot, WeaponDataSO weapon, out ChainShotsConfig config)
    {
        config = default;
        if (ownerRoot == null || weapon == null)
            return false;

        var mods = ownerRoot.GetComponentInChildren<PlayerWeaponDamageModifiers>(true);
        if (mods == null)
            mods = Object.FindFirstObjectByType<PlayerWeaponDamageModifiers>();
        if (mods == null)
            return false;

        bool ok = mods.TryGetChainShotsConfigInternal(weapon, out config);
        if (EnableChainDebugLog)
        {
            if (ok)
            {
                Debug.Log($"[ChainShots] ConfigResolved | weapon:{weapon.name} bounce:{config.bounceCount} radius:{config.searchRadius:F2} dmgMul:{config.damageMultiplier:F2} hold:{config.chainTargetHoldDuration:F2}");
            }
            else
            {
                Debug.Log($"[ChainShots] ConfigMissing | weapon:{weapon.name} damageType:{weapon.damageType} category:{weapon.category}");
            }
        }
        return ok;
    }

    /// <summary>
    /// 원본 프로젝타일 발사 시 적용할 추가 관통 수치를 조회합니다.
    /// 슬롯에 같은 업그레이드가 여러 개면 합산됩니다.
    /// </summary>
    public static int GetAdditionalProjectilePierceCount(GameObject ownerRoot, WeaponDataSO weapon)
    {
        if (ownerRoot == null || weapon == null)
            return 0;

        var mods = ownerRoot.GetComponentInChildren<PlayerWeaponDamageModifiers>(true);
        if (mods == null)
            mods = Object.FindFirstObjectByType<PlayerWeaponDamageModifiers>();
        if (mods == null)
            return 0;

        return mods.GetPiercingShotsBonus(weapon);
    }

    /// <summary>
    /// 퀵 리로드 업그레이드 합산을 반영한 리로드 소요 시간(초)입니다.
    /// 슬롯별 reloadTimeReductionFraction 합산 후 최대 0.5(50% 단축)로 제한하고, 단축이 적용되면 결과는 최소 0.5초입니다.
    /// </summary>
    public static float GetReloadTimeWithQuickReload(GameObject ownerRoot, WeaponDataSO weapon, float baseReloadTimeSeconds)
    {
        float b = Mathf.Max(0f, baseReloadTimeSeconds);
        if (b <= 0f || ownerRoot == null || weapon == null)
            return b;

        var mods = ownerRoot.GetComponentInChildren<PlayerWeaponDamageModifiers>(true);
        if (mods == null)
            mods = Object.FindFirstObjectByType<PlayerWeaponDamageModifiers>();

        float sum = mods != null ? mods.GetQuickReloadReductionSum(weapon) : 0f;
        sum = Mathf.Clamp(sum, 0f, 0.5f);
        if (sum <= 0f)
            return b;

        float t = b * (1f - sum);
        return Mathf.Max(0.5f, t);
    }

    /// <summary>
    /// 확장 탄창 업그레이드로 더해지는 탄 수 합계입니다. 슬롯마다 합산, 상한 없음.
    /// </summary>
    public static int GetExtendedMagazineBonusCount(GameObject ownerRoot, WeaponDataSO weapon)
    {
        if (ownerRoot == null || weapon == null)
            return 0;

        var mods = ownerRoot.GetComponentInChildren<PlayerWeaponDamageModifiers>(true);
        if (mods == null)
            mods = Object.FindFirstObjectByType<PlayerWeaponDamageModifiers>();
        if (mods == null)
            return 0;

        return mods.GetExtendedMagazineBonusSum(weapon);
    }

    /// <summary>
    /// 보너스 샷(무료 추가 탄환) 설정을 조회합니다. 슬롯마다 확률·오프셋·지연을 합산합니다.
    /// </summary>
    public static bool TryGetBonusShotConfig(GameObject ownerRoot, WeaponDataSO weapon, out BonusShotConfig config)
    {
        config = default;
        if (ownerRoot == null || weapon == null)
            return false;

        var mods = ownerRoot.GetComponentInChildren<PlayerWeaponDamageModifiers>(true);
        if (mods == null)
            mods = Object.FindFirstObjectByType<PlayerWeaponDamageModifiers>();
        if (mods == null)
            return false;

        return mods.TryGetBonusShotConfigInternal(weapon, out config);
    }

    public void Clear()
    {
        for (int i = 0; i < CategoryCount; i++)
        {
            percentBonusSum[i] = 0f;
            flatBonusSum[i] = 0f;
        }
    }

    /// <summary>
    /// Upgrade 슬롯을 읽어 패시브 데미지 보정을 다시 계산합니다.
    /// </summary>
    public void RebuildFromUpgradeSlots(Upgrade upgrade)
    {
        Clear();
        if (upgrade == null)
            return;
        cachedUpgrade = upgrade;
        cachedPlayerHealth = ResolvePlayerHealth(upgrade);

        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            UpgradeEffectSO slot = upgrade.GetSlot(i);
            if (slot is Upgrade_01_02_PowerUp dmgUp)
            {
                if (dmgUp.affectedCategories == null || dmgUp.affectedCategories.Count == 0)
                    continue;

                for (int c = 0; c < dmgUp.affectedCategories.Count; c++)
                {
                    WeaponCategory cat = dmgUp.affectedCategories[c];
                    int idx = (int)cat;
                    if (idx < 0 || idx >= CategoryCount)
                        continue;

                    percentBonusSum[idx] += dmgUp.additivePercentDamage;
                    flatBonusSum[idx] += dmgUp.flatBonusDamage;
                }
            }
        }
    }

    private static PlayerHealth ResolvePlayerHealth(Upgrade upgrade)
    {
        if (upgrade == null)
            return null;

        Transform root = upgrade.transform.root;
        if (root == null)
            return null;

        var health = root.GetComponentInChildren<PlayerHealth>(true);
        if (health == null)
            health = Object.FindFirstObjectByType<PlayerHealth>();

        return health;
    }

    private float GetMissingHpRatio()
    {
        if (cachedPlayerHealth == null && cachedUpgrade != null)
            cachedPlayerHealth = ResolvePlayerHealth(cachedUpgrade);

        if (cachedPlayerHealth == null)
            return 0f;

        float max = Mathf.Max(0.0001f, cachedPlayerHealth.GetMaxHP());
        float current = Mathf.Clamp(cachedPlayerHealth.GetCurrentHP(), 0f, max);
        return 1f - (current / max);
    }

    private float GetDynamicBloodRagePercent(WeaponCategory category)
    {
        if (cachedUpgrade == null)
            return 0f;

        float missingHpRatio = GetMissingHpRatio();
        if (missingHpRatio <= 0f)
            return 0f;

        float sum = 0f;
        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            UpgradeEffectSO slot = cachedUpgrade.GetSlot(i);
            if (slot is not Upgrade_01_03_BloodRage bloodRage)
                continue;

            if (bloodRage.affectedCategories == null || bloodRage.affectedCategories.Count == 0)
                continue;

            bool containsCategory = false;
            for (int c = 0; c < bloodRage.affectedCategories.Count; c++)
            {
                if (bloodRage.affectedCategories[c] == category)
                {
                    containsCategory = true;
                    break;
                }
            }

            if (!containsCategory)
                continue;

            sum += missingHpRatio * Mathf.Max(0f, bloodRage.maxBonusPercentAtZeroHp);
        }

        return sum;
    }

    private float GetVampiricPunchPercent(WeaponDataSO weapon)
    {
        if (cachedUpgrade == null)
            return 0f;

        float sum = 0f;
        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            UpgradeEffectSO slot = cachedUpgrade.GetSlot(i);
            if (slot is not Upgrade_02_01_VampiricPunch vamp)
                continue;

            if (!ContainsDamageType(vamp.allowedDamageTypes, weapon.damageType))
                continue;

            if (!ContainsCategory(vamp.affectedCategories, weapon.category))
                continue;

            sum += Mathf.Max(0f, vamp.lifeStealPercent);
        }

        return sum;
    }

    private bool TryGetBleedingPunchConfig(
        WeaponDataSO weapon,
        out float chance,
        out float duration,
        out float tickInterval,
        out float damagePerTick,
        out GameObject bleedTickEffectPrefab)
    {
        chance = 0f;
        duration = 0f;
        tickInterval = 0f;
        damagePerTick = 0f;
        bleedTickEffectPrefab = null;

        if (cachedUpgrade == null)
            return false;

        bool found = false;
        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            UpgradeEffectSO slot = cachedUpgrade.GetSlot(i);
            if (slot is not Upgrade_02_02_BleedingPunch bleed)
                continue;

            if (!ContainsDamageType(bleed.allowedDamageTypes, weapon.damageType))
                continue;

            if (!ContainsCategory(bleed.affectedCategories, weapon.category))
                continue;

            found = true;
            chance += Mathf.Clamp01(bleed.bleedApplyChance);
            duration += Mathf.Max(0f, bleed.duration);
            tickInterval += Mathf.Max(0f, bleed.tickInterval);
            damagePerTick += Mathf.Max(0f, bleed.damagePerTick);
            if (bleedTickEffectPrefab == null && bleed.bleedTickEffectPrefab != null)
                bleedTickEffectPrefab = bleed.bleedTickEffectPrefab;
        }

        chance = Mathf.Clamp01(chance);
        if (!found)
            return false;

        if (duration <= 0f || tickInterval <= 0f || damagePerTick <= 0f || chance <= 0f)
            return false;

        return true;
    }

    private bool TryGetStunningPunchConfig(
        WeaponDataSO weapon,
        out float chance,
        out float knockbackDuration,
        out float knockbackPower,
        out float jerkIntensity,
        out float jerkDuration,
        out float stunDuration)
    {
        chance = 0f;
        knockbackDuration = 0f;
        knockbackPower = 0f;
        jerkIntensity = 0f;
        jerkDuration = 0f;
        stunDuration = 0f;

        if (cachedUpgrade == null)
            return false;

        bool found = false;
        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            UpgradeEffectSO slot = cachedUpgrade.GetSlot(i);
            if (slot is not Upgrade_02_03_StunningPunch stun)
                continue;

            if (!ContainsDamageType(stun.allowedDamageTypes, weapon.damageType))
                continue;

            if (!ContainsCategory(stun.affectedCategories, weapon.category))
                continue;

            found = true;
            chance += Mathf.Clamp01(stun.stunApplyChance);
            knockbackDuration += Mathf.Max(0f, stun.bonusKnockbackDuration);
            knockbackPower += Mathf.Max(0f, stun.bonusKnockbackPower);
            jerkIntensity += Mathf.Max(0f, stun.bonusJerkIntensity);
            jerkDuration += Mathf.Max(0f, stun.bonusJerkDuration);
            stunDuration += Mathf.Max(0f, stun.bonusStunDuration);
        }

        chance = Mathf.Clamp01(chance);
        if (!found)
            return false;

        if (chance <= 0f)
            return false;

        return knockbackDuration > 0f || knockbackPower > 0f || stunDuration > 0f;
    }

    private bool TryGetChainShotsConfigInternal(WeaponDataSO weapon, out ChainShotsConfig config)
    {
        config = default;

        if (cachedUpgrade == null)
            return false;

        int bounceSum = 0;
        float radiusSum = 0f;
        float damageMulSum = 0f;
        float holdSum = 0f;
        bool found = false;

        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            UpgradeEffectSO slot = cachedUpgrade.GetSlot(i);
            if (slot is not Upgrade_04_01_ChainShots chain)
                continue;

            if (!ContainsDamageType(chain.allowedDamageTypes, weapon.damageType))
                continue;

            if (!ContainsCategory(chain.affectedCategories, weapon.category))
                continue;

            found = true;
            bounceSum += Mathf.Max(0, chain.bounceCount);
            radiusSum += Mathf.Max(0f, chain.searchRadius);
            damageMulSum += Mathf.Max(0f, chain.damageMultiplier);
            holdSum += Mathf.Max(0f, chain.chainTargetHoldDuration);
        }

        if (!found)
            return false;

        if (bounceSum <= 0 || radiusSum <= 0f || damageMulSum <= 0f)
            return false;

        config = new ChainShotsConfig
        {
            bounceCount = bounceSum,
            searchRadius = radiusSum,
            damageMultiplier = damageMulSum,
            chainTargetHoldDuration = holdSum
        };
        return true;
    }

    private int GetPiercingShotsBonus(WeaponDataSO weapon)
    {
        if (cachedUpgrade == null || weapon == null)
            return 0;

        int sum = 0;
        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            UpgradeEffectSO slot = cachedUpgrade.GetSlot(i);
            if (slot is not Upgrade_04_02_PiercingShots piercing)
                continue;

            if (!ContainsDamageType(piercing.allowedDamageTypes, weapon.damageType))
                continue;

            if (!ContainsCategory(piercing.affectedCategories, weapon.category))
                continue;

            sum += Mathf.Max(0, piercing.additionalPierceCount);
        }

        return Mathf.Max(0, sum);
    }

    private float GetQuickReloadReductionSum(WeaponDataSO weapon)
    {
        if (cachedUpgrade == null || weapon == null)
            return 0f;

        float sum = 0f;
        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            UpgradeEffectSO slot = cachedUpgrade.GetSlot(i);
            if (slot is not Upgrade_04_04_QuickReload quick)
                continue;

            if (!ContainsDamageType(quick.allowedDamageTypes, weapon.damageType))
                continue;

            if (!ContainsCategory(quick.affectedCategories, weapon.category))
                continue;

            sum += Mathf.Max(0f, quick.reloadTimeReductionFraction);
        }

        return Mathf.Max(0f, sum);
    }

    private int GetExtendedMagazineBonusSum(WeaponDataSO weapon)
    {
        if (cachedUpgrade == null || weapon == null)
            return 0;

        int sum = 0;
        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            UpgradeEffectSO slot = cachedUpgrade.GetSlot(i);
            if (slot is not Upgrade_04_05_ExtendedMag ext)
                continue;

            if (!ContainsDamageType(ext.allowedDamageTypes, weapon.damageType))
                continue;

            if (!ContainsCategory(ext.affectedCategories, weapon.category))
                continue;

            sum += Mathf.Max(0, ext.additionalMagazineRounds);
        }

        return Mathf.Max(0, sum);
    }

    private bool TryGetBonusShotConfigInternal(WeaponDataSO weapon, out BonusShotConfig config)
    {
        config = default;

        if (cachedUpgrade == null || weapon == null)
            return false;

        float chanceSum = 0f;
        float lateralSum = 0f;
        float delaySum = 0f;
        bool found = false;

        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            UpgradeEffectSO slot = cachedUpgrade.GetSlot(i);
            if (slot is not Upgrade_04_03_BonusShot bonus)
                continue;

            if (!ContainsDamageType(bonus.allowedDamageTypes, weapon.damageType))
                continue;

            if (!ContainsCategory(bonus.affectedCategories, weapon.category))
                continue;

            found = true;
            chanceSum += Mathf.Clamp01(bonus.bonusShotChance);
            lateralSum += Mathf.Max(0f, bonus.lateralOffsetMeters);
            delaySum += Mathf.Max(0f, bonus.delayUnscaledSeconds);
        }

        if (!found)
            return false;

        config = new BonusShotConfig
        {
            chance = Mathf.Clamp01(chanceSum),
            lateralOffsetMeters = lateralSum,
            delayUnscaledSeconds = Mathf.Min(5f, delaySum)
        };

        return config.chance > 0f;
    }

    private static bool ContainsCategory(System.Collections.Generic.List<WeaponCategory> categories, WeaponCategory target)
    {
        if (categories == null || categories.Count == 0)
            return false;

        for (int i = 0; i < categories.Count; i++)
        {
            if (categories[i] == target)
                return true;
        }

        return false;
    }

    private static bool ContainsDamageType(System.Collections.Generic.List<AttackDamageType> types, AttackDamageType target)
    {
        if (types == null || types.Count == 0)
            return false;

        for (int i = 0; i < types.Count; i++)
        {
            if (types[i] == target)
                return true;
        }

        return false;
    }

    public float Apply(WeaponCategory category, float baseDamage)
    {
        int idx = (int)category;
        if (idx < 0 || idx >= CategoryCount)
            return Mathf.Max(0f, baseDamage);

        float dynamicBloodRage = GetDynamicBloodRagePercent(category);
        float mul = 1f + percentBonusSum[idx] + dynamicBloodRage;
        float v = baseDamage * mul + flatBonusSum[idx];
        return Mathf.Max(0f, v);
    }
}
