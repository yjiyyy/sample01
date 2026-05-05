using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "04_05_ExtendedMag",
    menuName = "Game/Upgrade/Effect/04_05_ExtendedMag",
    order = 13)]
public class Upgrade_04_05_ExtendedMag : UpgradeEffectSO
{
    [Header("효과를 적용할 공격 타입 (복수 선택)")]
    public List<AttackDamageType> allowedDamageTypes = new List<AttackDamageType> { AttackDamageType.ProjectileGun };

    [Header("효과를 적용할 무기 카테고리 (복수 선택)")]
    public List<WeaponCategory> affectedCategories = new List<WeaponCategory> { WeaponCategory.Primary };

    [Header("탄창")]
    [Tooltip("SO 기본 탄창 용량에 더해질 탄 수. 슬롯마다 합산(상한 없음). 슬롯 변경 시 탄창을 새 용량으로 가득 채웁니다.")]
    [Min(0)]
    public int additionalMagazineRounds = 5;
}
