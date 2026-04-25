using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "01_02_PowerUp",
    menuName = "Game/Upgrade/Effect/01_02_PowerUp",
    order = 2)]
public class Upgrade_01_02_PowerUp : UpgradeEffectSO
{
    [Header("보정을 적용할 무기 카테고리 (복수 선택)")]
    public List<WeaponCategory> affectedCategories = new List<WeaponCategory> { WeaponCategory.Primary };

    [Tooltip("각 카테고리에 대해 곱해지는 추가 배율의 합. 예: 0.1 = +10%. 슬롯마다 합산.")]
    public float additivePercentDamage = 0.1f;

    [Tooltip("곱 연산 이후 더해지는 고정 피해. 슬롯마다 합산.")]
    public float flatBonusDamage = 0f;
}
