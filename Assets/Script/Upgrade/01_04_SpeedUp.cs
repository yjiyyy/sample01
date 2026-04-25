using UnityEngine;

[CreateAssetMenu(
    fileName = "01_04_SpeedUp",
    menuName = "Game/Upgrade/Effect/01_04_SpeedUp",
    order = 4)]
public class Upgrade_01_04_SpeedUp : UpgradeEffectSO
{
    [Header("추가 이동속도 배율(합산)")]
    [Tooltip("예: 0.1 = +10%, 슬롯 중복 시 합산됩니다.")]
    [Min(0f)]
    public float additiveMoveSpeedPercent = 0.1f;
}
