using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization;

/// <summary>
/// Weapon data ScriptableObject (common for most weapons)
/// - 기존 필드 + (WeaponBehavior 완전 이관용) 스폰포인트/프리팹 필드
/// - 듀얼(2번째 스폰포인트/딜레이) 필드
/// - PlayerAnimationTester용 weaponPrefab 필드
/// </summary>

public enum DamageTargetType
{
    EnemyOnly,
    PlayerOnly,
    Both
}

public enum DeathMode
{
    Animation,
    Ragdoll
}

public enum SliceTarget
{
    Head,
    LeftArm,
    RightArm,
    LeftLeg,
    RightLeg,
    All
}

[CreateAssetMenu(menuName = "Weapon/WeaponDataSO")]
public class WeaponDataSO : ScriptableObject
{
    [Header("식별/표시")]
    public string id;
    public string weaponName = "NewWeapon";
    public Sprite icon;
    public WeaponCategory category = WeaponCategory.Primary;

    [Header("장착 프리팹 (테스트/장착용)")]
    [Tooltip("이 WeaponDataSO를 장착할 때 사용할 무기 프리팹.\n" +
             "PlayerAnimationTester(에디터 테스트)에서 이 값을 사용해 장착합니다.")]
    public GameObject weaponPrefab;

    [Header("애니메이션 세트 (Animator Override Controller 방식)")]
    public AnimatorOverrideController overrideController;

    [Header("전투 관련")]
    public float cooldown = 1.0f;
    public float damage = 10f;
    public float range = 2.5f;

    [Header("히트박스 타이밍 및 지속")]
    public float hitboxSpawnDelay = 0f;
    public float hitBoxLifetime = 0.2f;

    [Header("넉백 / 저크")]
    public float knockbackDuration = 0.2f;
    public float knockbackPower = 0f;
    public float jerkIntensity = 1f;
    public float jerkDuration = 0.2f;

    [Header("스턴")]
    public float stunDuration = 0f;

    [Header("Push(밀림) 옵션")]
    public bool usePushInsteadOfKnockback = false;

    [FormerlySerializedAs("hitstopTime")]
    public float animationHoldDuration = 0f;

    [Header("처치 연출 선택")]
    public DeathMode deathMode = DeathMode.Animation;

    [Header("Ragdoll 임펄스(죽음이 Ragdoll일 때만 사용)")]
    public float ragdollImpulse = 5f;
    public float ragdollUpImpulse = 0f;
    public float ragdollSpinTorque = 0f;

    [Header("Slice(본 분리)")]
    public List<SliceTarget> sliceTargets = new List<SliceTarget>();
    public float sliceImpulse = 0f;

    [Header("Charge Attack (무기별 선택 적용)")]
    public PlayerChargeAttackSO chargeSlot;

    [Header("Melee Combo (무기별)")]
    [Tooltip("근접 콤보가 있는 경우 MeleeComboSO를 연결하세요. 비어있으면 콤보 비활성.")]
    public MeleeComboSO comboSlot;

    [Header("리코일(자기 반동)")]
    public float recoilStartDelay = 0f;
    public float recoilPower = 0f;
    public float recoilDuration = 0f;

    [Header("Mount / Socket names (priority order)")]
    public List<string> socketNames = new List<string>() { "R_Hand_Weapon" };

    // ---------------- 완전 이관: Spawn points & prefabs ----------------
    [Header("Attack Spawn Points (완전 이관)")]
    [Tooltip("근접 히트박스 스폰 포인트(1). 비워두면 플레이어 루트에서 'Root_dummy'를 자동 사용.\n" +
             "값을 넣으면 플레이어 루트 기준으로 해당 이름/경로를 찾아 사용합니다.")]
    public string meleeSpawnPointPathOrName = "";

    [Tooltip("원거리 스폰 포인트(1). 비워두면 무기(프리팹) 내부에서 'Fire_Point'를 자동 사용.\n" +
             "값을 넣으면 무기(프리팹) 기준으로 해당 이름/경로를 찾아 사용합니다.")]
    public string projectileSpawnPointPathOrName = "";

    [Header("Attack Prefabs (완전 이관)")]
    public GameObject meleeHitboxPrefab;
    public GameObject projectilePrefab;
    public GameObject shotgunSectorPrefab;

    // ---------------- Dual Wield ----------------
    [Header("Dual Wield (양손 옵션)")]
    [Tooltip("true면 1회 공격에서 스폰을 최대 2번(1번/2번) ���도합니다.\n" +
             "2번 스폰은 2번째 스폰포인트가 입력된 경우에만 나갑니다.")]
    public bool dualWield = false;

    [Tooltip("2번째 스폰 딜레이(초). 0이면 거의 동시에 나갑니다.")]
    public float hitboxSpawnDelay2 = 0f;

    [Header("Dual Wield - Second Spawn Points")]
    [Tooltip("근접 스폰 포인트(2). 비워두면 2번째 근접 스폰은 안 나갑니다. (플레이어 루트 기준 이름/경로)")]
    public string meleeSpawnPoint2PathOrName = "";

    [Tooltip("원거리 스폰 포인트(2). 비워두면 2번째 원거리 스폰은 안 나갑니다. (무기 내부 기준 이름/경로)")]
    public string projectileSpawnPoint2PathOrName = "";

#if UNITY_EDITOR
    private void OnValidate()
    {
        cooldown = Mathf.Max(0f, cooldown);
        hitboxSpawnDelay = Mathf.Max(0f, hitboxSpawnDelay);
        hitBoxLifetime = Mathf.Max(0.01f, hitBoxLifetime);

        knockbackDuration = Mathf.Max(0f, knockbackDuration);
        jerkDuration = Mathf.Max(0f, jerkDuration);
        stunDuration = Mathf.Max(0f, stunDuration);

        recoilStartDelay = Mathf.Max(0f, recoilStartDelay);
        recoilDuration = Mathf.Max(0f, recoilDuration);

        animationHoldDuration = Mathf.Max(0f, animationHoldDuration);

        ragdollImpulse = Mathf.Max(0f, ragdollImpulse);
        ragdollUpImpulse = Mathf.Max(0f, ragdollUpImpulse);
        ragdollSpinTorque = Mathf.Max(0f, ragdollSpinTorque);

        sliceImpulse = Mathf.Max(0f, sliceImpulse);

        hitboxSpawnDelay2 = Mathf.Max(0f, hitboxSpawnDelay2);

        if (string.IsNullOrEmpty(id))
            Debug.LogWarning($"WeaponDataSO '{name}' has empty id. Please set a unique id for inventory/DB usage.");
    }
#endif
}

public enum WeaponCategory
{
    Primary,
    Secondary,
    Special,
    Throwable
}