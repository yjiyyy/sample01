using System;
using UnityEngine;

/// <summary>
/// 보조무기(05_ 계열) 공격 쿨타임 보정 값을 관리합니다.
/// - 슬롯의 05_00_SwiftAngels를 합산해 감소율을 계산
/// - 합산 감소율은 최대 0.5(50%)로 제한
/// </summary>
[DisallowMultipleComponent]
public class PlayerCompanionCooldownModifiers : MonoBehaviour
{
    private const float MaxReduction = 0.5f;
    private float companionCooldownReductionSum;

    public void RebuildFromUpgradeSlots(Upgrade upgrade)
    {
        companionCooldownReductionSum = 0f;
        if (upgrade == null)
            return;

        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            UpgradeEffectSO slot = upgrade.GetSlot(i);
            if (slot is not Upgrade_05_00_SwiftAngels swift)
                continue;

            companionCooldownReductionSum += Mathf.Max(0f, swift.cooldownReductionFraction);
        }

        companionCooldownReductionSum = Mathf.Clamp(companionCooldownReductionSum, 0f, MaxReduction);
    }

    public static float ApplyCompanionCooldown(GameObject ownerRoot, string sourceUpgradeId, float baseCooldown, float minCooldown = 0.05f)
    {
        float safeBase = Mathf.Max(0f, baseCooldown);
        float safeMin = Mathf.Max(0f, minCooldown);
        if (!IsCompanionUpgradeId(sourceUpgradeId))
            return Mathf.Max(safeMin, safeBase);

        var mods = Resolve(ownerRoot);
        float reduction = mods != null ? Mathf.Clamp(mods.companionCooldownReductionSum, 0f, MaxReduction) : 0f;
        float scaled = safeBase * (1f - reduction);
        return Mathf.Max(safeMin, scaled);
    }

    private static bool IsCompanionUpgradeId(string upgradeId)
    {
        if (string.IsNullOrEmpty(upgradeId))
            return false;

        return upgradeId.StartsWith("05_", StringComparison.Ordinal);
    }

    private static PlayerCompanionCooldownModifiers Resolve(GameObject ownerRoot)
    {
        if (ownerRoot != null)
        {
            var mods = ownerRoot.GetComponentInChildren<PlayerCompanionCooldownModifiers>(true);
            if (mods != null)
                return mods;
        }

        return UnityEngine.Object.FindFirstObjectByType<PlayerCompanionCooldownModifiers>();
    }
}
