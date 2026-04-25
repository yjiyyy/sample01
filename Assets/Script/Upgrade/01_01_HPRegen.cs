using UnityEngine;

[CreateAssetMenu(
    fileName = "01_01_HPRegen",
    menuName = "Game/Upgrade/Effect/01_01_HPRegen",
    order = 1)]
public class Upgrade_01_01_HPRegen : UpgradeEffectSO
{
    [Header("회복 주기(초)")]
    [Min(0.05f)]
    public float tickInterval = 1f;

    [Header("주기마다 회복량")]
    [Min(0f)]
    public float healAmountPerTick = 1f;
}
