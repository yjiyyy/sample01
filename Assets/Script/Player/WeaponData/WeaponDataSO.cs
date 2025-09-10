using UnityEngine;
using System.Collections.Generic;

public enum WeaponCategory
{
    None,       // 무기 없음 (맨손 공격)
    Bat,        // 근접
    Gun,        // 투사체
    Shotgun,    // 섹터(부채꼴) 판정 + 거리감쇠
    Launcher    // 폭발형 무기 (예: 로켓 런처)
}

/// <summary>
/// 폭발/프로젝타일 등에서 데미지 판정 대상을 구분하기 위한 옵션
/// </summary>
public enum DamageTargetType
{
    EnemyOnly,
    PlayerOnly,
    Both
}

[CreateAssetMenu(menuName = "Weapon/WeaponDataSO")]
public class WeaponDataSO : ScriptableObject
{
    [Header("공통 옵션")]
    public string weaponName = "NewWeapon";

    [Header("애니메이션 세트 (Animator Override Controller 방식)")]
    [Tooltip("무기별 애니메이션을 교체하려면 여기에 AOC를 등록")]
    public AnimatorOverrideController overrideController;

    [Header("전투 관련")]
    public float cooldown = 1.0f;
    public float damage = 10f;
    public float range = 2.5f;
    public int projectileCount = 1;
    public bool isMelee = true;
    public float AutoAttackDelay = 0.3f;
    public float hitBoxLifetime = 0.2f;
    public float criticalChance = 0f;
    public float aoeRadius = 0f;

    [Header("히트박스 타이밍")]
    [Tooltip("공격 시작 후 몇 초 뒤 히트박스가 생성되는지")]
    public float hitboxSpawnDelay = 0f;

    [Header("넉백 관련")]
    public float knockbackDuration = 0.2f;
    public float knockbackPower = 0f;
    public float jerkIntensity = 1f;
    public float jerkDuration = 0.2f;

    [Header("스턴 관련")]
    [Tooltip("0이면 스턴 없음, 값이 있으면 스턴 지속 시간 (초)")]
    public float stunDuration = 0f;

    [Header("투사체 관련")]
    public float projectileLifetime = 5f;
    public float projectileSpeed = 10f;
    public int pierceCount = 0;

    [Tooltip("로켓류면 true")]
    public bool isExplosiveProjectile = false;
    public float explosiveRadius = 3f;
    [Range(0f, 1f)] public float explosiveEdgeMul = 0.2f;

    [Header("감지(부채꼴) 설정")]
    public float viewAngle = 45f;
    public float viewDistance = 10f;

    [Header("랙돌 비행 세기")]
    public float ragdollImpulse = 5f;
    public float upwardImpulse = 3f;
    public float torqueImpulse = 6f;
    public float sliceForce = 8f;

    [Header("처치 연출")]
    public EnemyDeathType deathType = EnemyDeathType.Default;
    public List<BodySliceType> possibleSliceParts = new();

    [Header("무기 분류")]
    public WeaponCategory weaponCategory = WeaponCategory.Bat;

    [Header("샷건(섹터) 파라미터")]
    public float shotgunRadius = 5.0f;
    [Range(1f, 360f)] public float shotgunAngle = 30f;
    public bool shotgunUseDistanceFalloff = true;
    [Range(0f, 1f)] public float shotgunFalloffMin = 0.2f;

    [Header("샷건 섹터 시각화")]
    public bool shotgunDebugVisualize = true;
    public Color shotgunDebugColor = new Color(1f, 0.6f, 0f, 0.25f);
    public Color shotgunDebugActualColor = new Color(0f, 1f, 0f, 0.25f);

    [Header("데미지 판정 대상")]
    public DamageTargetType damageTargetType = DamageTargetType.EnemyOnly;
}

public enum EnemyDeathType
{
    Default,
    Ragdoll,
    Slice,
}

public enum BodySliceType
{
    None,
    Head,
    LeftArm,
    RightArm,
    LeftLeg,
    RightLeg,
    All,
}
