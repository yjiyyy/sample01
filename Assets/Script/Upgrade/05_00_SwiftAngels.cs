using UnityEngine;

[CreateAssetMenu(
    fileName = "05_00_SwiftAngels",
    menuName = "Game/Upgrade/Effect/05_00_SwiftAngels",
    order = 49)]
public class Upgrade_05_00_SwiftAngels : UpgradeEffectSO
{
    [Header("보조무기 공격 주기")]
    [Tooltip("05_ 계열 보조무기의 공격 쿨타임 감소 비율. 예: 0.1 = 10% 감소. 슬롯별 합산.")]
    [Min(0f)]
    public float cooldownReductionFraction = 0.1f;

#if UNITY_EDITOR
    private void OnValidate()
    {
        cooldownReductionFraction = Mathf.Clamp(cooldownReductionFraction, 0f, 0.5f);
    }
#endif
}
