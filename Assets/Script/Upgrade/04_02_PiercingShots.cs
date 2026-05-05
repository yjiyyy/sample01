using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "04_02_PiercingShots",
    menuName = "Game/Upgrade/Effect/04_02_PiercingShots",
    order = 10)]
public class Upgrade_04_02_PiercingShots : UpgradeEffectSO
{
    [Header("효과를 적용할 공격 타입 (복수 선택)")]
    [Tooltip("탄환 무기는 AttackDamageType.ProjectileGun 을 사용합니다.")]
    public List<AttackDamageType> allowedDamageTypes = new List<AttackDamageType> { AttackDamageType.ProjectileGun };

    [Header("효과를 적용할 무기 카테고리 (복수 선택)")]
    public List<WeaponCategory> affectedCategories = new List<WeaponCategory> { WeaponCategory.Primary };

    [Header("관통 추가 수치")]
    [Tooltip("원본 프로젝타일의 관통 횟수에 더해지는 값. 슬롯 간 합산됩니다.")]
    [Min(1)]
    public int additionalPierceCount = 1;
}
