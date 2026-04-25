using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "02_03_StunningPunch",
    menuName = "Game/Upgrade/Effect/02_03_StunningPunch",
    order = 8)]
public class Upgrade_02_03_StunningPunch : UpgradeEffectSO
{
    [Header("효과를 적용할 공격 타입 (복수 선택)")]
    [Tooltip("기본값은 Unarmed만 선택됩니다.")]
    public List<AttackDamageType> allowedDamageTypes = new List<AttackDamageType> { AttackDamageType.Unarmed };

    [Header("효과를 적용할 무기 카테고리 (복수 선택)")]
    public List<WeaponCategory> affectedCategories = new List<WeaponCategory> { WeaponCategory.Primary };

    [Header("스턴 적용 확률")]
    [Range(0f, 1f)]
    public float stunApplyChance = 0.2f;

    [Header("넉백 / 저크 보너스")]
    [Min(0f)]
    [UnityEngine.Serialization.FormerlySerializedAs("knockbackDuration")]
    public float bonusKnockbackDuration = 0.5f;
    [Min(0f)]
    [UnityEngine.Serialization.FormerlySerializedAs("knockbackPower")]
    public float bonusKnockbackPower = 3f;
    [Min(0f)]
    [UnityEngine.Serialization.FormerlySerializedAs("jerkIntensity")]
    public float bonusJerkIntensity = 0f;
    [Min(0f)]
    [UnityEngine.Serialization.FormerlySerializedAs("jerkDuration")]
    public float bonusJerkDuration = 0f;

    [Header("스턴 보너스")]
    [Min(0f)]
    [UnityEngine.Serialization.FormerlySerializedAs("stunDuration")]
    public float bonusStunDuration = 0.5f;
}
