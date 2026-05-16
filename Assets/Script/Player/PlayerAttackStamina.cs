using UnityEngine;

/// <summary>
/// 무기 공격·콤보 스텝의 스테미너 소모를 한곳에서 처리합니다. (회피의 EvadeDataSO는 별도)
/// </summary>
public static class PlayerAttackStamina
{
    /// <summary>콤보 스텝에 별도 값이 있으면 사용하고, 음수면 무기 기본값을 사용합니다.</summary>
    public static float GetEffectiveCost(WeaponDataSO weapon, MeleeComboStepSO step)
    {
        if (step != null && step.staminaCost >= 0f)
            return Mathf.Max(0f, step.staminaCost);
        if (weapon == null)
            return 0f;
        return Mathf.Max(0f, weapon.staminaCost);
    }

    public static bool CanPay(PlayerStats stats, float amount)
    {
        if (amount <= 0f)
            return true;
        if (stats == null)
            return false;
        return stats.CanUseStamina(amount);
    }

    /// <summary>스테미너가 부족하면 false. amount≤0 이면 true.</summary>
    public static bool TryPay(PlayerStats stats, float amount)
    {
        if (amount <= 0f)
            return true;
        if (stats == null)
            return false;
        return stats.UseStamina(amount);
    }
}
