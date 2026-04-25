using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "02_02_BleedingPunch",
    menuName = "Game/Upgrade/Effect/02_02_BleedingPunch",
    order = 7)]
public class Upgrade_02_02_BleedingPunch : UpgradeEffectSO
{
    [Header("효과를 적용할 공격 타입 (복수 선택)")]
    [Tooltip("기본값은 Unarmed만 선택됩니다.")]
    public List<AttackDamageType> allowedDamageTypes = new List<AttackDamageType> { AttackDamageType.Unarmed };

    [Header("효과를 적용할 무기 카테고리 (복수 선택)")]
    public List<WeaponCategory> affectedCategories = new List<WeaponCategory> { WeaponCategory.Primary };

    [Header("출혈 적용 확률")]
    [Tooltip("0~1 범위. 예: 0.25 = 25% 확률")]
    [Range(0f, 1f)]
    public float bleedApplyChance = 0.25f;

    [Header("출혈 지속시간(초)")]
    [Min(0.05f)]
    public float duration = 3f;

    [Header("틱 간격(초)")]
    [Min(0.05f)]
    public float tickInterval = 0.5f;

    [Header("틱당 피해량")]
    [Min(0f)]
    public float damagePerTick = 1f;

    [Header("출혈 틱 이펙트 프리팹")]
    [Tooltip("출혈 틱마다 대상 몬스터 root에 생성할 이펙트 프리팹")]
    public GameObject bleedTickEffectPrefab;
}
