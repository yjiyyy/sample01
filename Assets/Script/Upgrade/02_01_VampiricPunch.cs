using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "02_01_VampiricPunch",
    menuName = "Game/Upgrade/Effect/02_01_VampiricPunch",
    order = 6)]
public class Upgrade_02_01_VampiricPunch : UpgradeEffectSO
{
    [Header("효과를 적용할 공격 타입 (복수 선택)")]
    [Tooltip("기본값은 Unarmed만 선택됩니다.")]
    public List<AttackDamageType> allowedDamageTypes = new List<AttackDamageType> { AttackDamageType.Unarmed };

    [Header("효과를 적용할 무기 카테고리 (복수 선택)")]
    public List<WeaponCategory> affectedCategories = new List<WeaponCategory> { WeaponCategory.Primary };

    [Header("흡혈 비율(합산)")]
    [Tooltip("최종 가한 피해량 기준 회복 비율. 예: 0.05 = 피해의 5% 회복")]
    [Min(0f)]
    public float lifeStealPercent = 0.05f;
}
