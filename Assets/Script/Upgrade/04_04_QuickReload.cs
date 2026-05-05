using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "04_04_QuickReload",
    menuName = "Game/Upgrade/Effect/04_04_QuickReload",
    order = 12)]
public class Upgrade_04_04_QuickReload : UpgradeEffectSO
{
    [Header("효과를 적용할 공격 타입 (복수 선택)")]
    public List<AttackDamageType> allowedDamageTypes = new List<AttackDamageType> { AttackDamageType.ProjectileGun };

    [Header("효과를 적용할 무기 카테고리 (복수 선택)")]
    public List<WeaponCategory> affectedCategories = new List<WeaponCategory> { WeaponCategory.Primary };

    [Header("리로드 시간")]
    [Tooltip("리로드 소요 시간 단축 비율. 예: 0.1이면 기본 시간의 90%로 단축. 슬롯마다 합산 후 전체 합은 최대 0.5(50% 단축)로 제한.")]
    [Min(0f)]
    public float reloadTimeReductionFraction = 0.1f;

#if UNITY_EDITOR
    private void OnValidate()
    {
        reloadTimeReductionFraction = Mathf.Clamp(reloadTimeReductionFraction, 0f, 0.5f);
    }
#endif
}
