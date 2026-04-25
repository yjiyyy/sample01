using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "01_03_BloodRage",
    menuName = "Game/Upgrade/Effect/01_03_BloodRage",
    order = 3)]
public class Upgrade_01_03_BloodRage : UpgradeEffectSO
{
    [Header("보정을 적용할 무기 카테고리 (복수 선택)")]
    public List<WeaponCategory> affectedCategories = new List<WeaponCategory> { WeaponCategory.Primary };

    [Header("HP 0%일 때 최대 추가 배율")]
    [Tooltip("선형 증가. 예: 0.5 = 잃은 체력 비율 100%에서 +50%")]
    [Min(0f)]
    public float maxBonusPercentAtZeroHp = 0.5f;
}
