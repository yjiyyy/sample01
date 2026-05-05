using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "04_03_BonusShot",
    menuName = "Game/Upgrade/Effect/04_03_BonusShot",
    order = 11)]
public class Upgrade_04_03_BonusShot : UpgradeEffectSO
{
    [Header("효과를 적용할 공격 타입 (복수 선택)")]
    [Tooltip("탄환 무기는 AttackDamageType.ProjectileGun 을 사용합니다.")]
    public List<AttackDamageType> allowedDamageTypes = new List<AttackDamageType> { AttackDamageType.ProjectileGun };

    [Header("효과를 적용할 무기 카테고리 (복수 선택)")]
    public List<WeaponCategory> affectedCategories = new List<WeaponCategory> { WeaponCategory.Primary };

    [Header("보너스 발사")]
    [Tooltip("한 번 발사 트리거당 보너스 탄이 나갈 확률. 슬롯마다 합산 후 100%로 클램프.")]
    [Range(0f, 1f)]
    public float bonusShotChance = 0.15f;

    [Tooltip("보너스 탄 스폰 위치를 진행 방향에 수직인 XZ 평면 오른쪽으로 밀 거리(미터). 슬롯마다 합산.")]
    [Min(0f)]
    public float lateralOffsetMeters = 0.04f;

    [Tooltip("원본 탄 스폰 후 보너스 탄까지의 대기 시간(초). Time.timeScale 과 무관(unscaled). 슬롯마다 합산, 상한 5초.")]
    [Min(0f)]
    public float delayUnscaledSeconds = 0.1f;
}
