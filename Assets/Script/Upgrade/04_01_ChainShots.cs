using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "04_01_ChainShots",
    menuName = "Game/Upgrade/Effect/04_01_ChainShots",
    order = 9)]
public class Upgrade_04_01_ChainShots : UpgradeEffectSO
{
    [Header("효과를 적용할 공격 타입 (복수 선택)")]
    [Tooltip("요구사항 기준 기본값은 ProjectileGun만 선택됩니다.")]
    public List<AttackDamageType> allowedDamageTypes = new List<AttackDamageType> { AttackDamageType.ProjectileGun };

    [Header("효과를 적용할 무기 카테고리 (복수 선택)")]
    public List<WeaponCategory> affectedCategories = new List<WeaponCategory> { WeaponCategory.Primary };

    [Header("체인 설정")]
    [Tooltip("연쇄로 추가 생성될 탄환 횟수")]
    [Min(1)]
    public int bounceCount = 1;

    [Tooltip("다음 타겟 탐색 반경(미터). 피격된 적 루트 중심에서 탐색합니다.")]
    [Min(0.1f)]
    public float searchRadius = 5f;

    [Tooltip("체인탄 데미지 배율. 예: 0.8 = 원본 피해의 80%")]
    [Min(0f)]
    public float damageMultiplier = 1f;

    [Tooltip("체인탄 전용 타겟 홀드 시간(초). 0이면 홀드 비활성")]
    [Min(0f)]
    public float chainTargetHoldDuration = 0f;
}
