using UnityEngine;

[CreateAssetMenu(
    fileName = "01_05_SwiftRecovery",
    menuName = "Game/Upgrade/Effect/01_05_SwiftRecovery",
    order = 5)]
public class Upgrade_01_05_SwiftRecovery : UpgradeEffectSO
{
    [Header("스태미나 회복속도 추가 배율(합산)")]
    [Tooltip("예: 0.25 = +25%, 슬롯 중복 시 합산됩니다.")]
    [Min(0f)]
    public float additiveStaminaRegenPercent = 0.25f;

    [Header("스태미나 회복 지연 감소(초, 합산)")]
    [Tooltip("예: 1.0 = 소비 후 회복 시작 지연을 1초 감소. 슬롯 중복 시 합산됩니다.")]
    [Min(0f)]
    public float staminaRechargeDelayReduction = 1f;
}
