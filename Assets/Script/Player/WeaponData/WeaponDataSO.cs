using UnityEngine;
using System.Collections.Generic;

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

/// <summary>근접 히트박스 방식. 둘 중 하나만 선택.</summary>
public enum MeleeHitboxMode
{
    [Tooltip("스폰 포인트에 프리팹 생성 (충격파, 범위형 등)")]
    SpawnPrefab,
    [Tooltip("무기 프리팹에 붙은 HitBox 콜라이더 활성화")]
    WeaponCollider
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

    [Header("무기 트레일 (단타, 콤보 없을 때만)")]
    [Tooltip("공격 시작 후 트레일 기록 시작까지 지연(초). trailEmitDuration>0일 때만 사용.")]
    public float trailEmitStartDelay = 0f;
    [Tooltip("트레일 기록 유지 시간(초). 0 이하면 트레일 미사용. 무기 프리팹에 WeaponTrailController 필요.")]
    public float trailEmitDuration = 0f;

    [Header("넉백 / 저크")]
    public float knockbackDuration = 0.2f;
    public float knockbackPower = 0f;
    public float jerkIntensity = 1f;
    public float jerkDuration = 0.2f;

    [Header("스턴")]
    public float stunDuration = 0f;

    [Header("Push(밀림) 옵션")]
    public bool usePushInsteadOfKnockback = false;

    [Header("Time Control (Hit Stop)")]
    [Tooltip("피격 대상 홀드 시간(초). 상태/애니메이션 모두 동일하게 적용. 0이면 비활성")]
    public float targetHoldDuration = 0f;
    [Tooltip("공격자 홀드 시간(초). 상태/애니메이션 모두 동일하게 적용. 0이면 비활성")]
    public float attackerHoldDuration = 0f;

    // Legacy split fields kept for data migration only.
    [HideInInspector] public float targetStateHoldDuration = 0f;
    [HideInInspector] public float targetAnimationHoldDuration = 0f;
    [HideInInspector] public float attackerStateHoldDuration = 0f;
    [HideInInspector] public float attackerAnimationHoldDuration = 0f;

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
    [Tooltip("양수: 뒤로 밀림, 음수: 앞으로 밀림")]
    public float recoilPower = 0f;
    public float recoilDuration = 0f;

    [Header("Mount / Socket names (priority order)")]
    public List<string> socketNames = new List<string>() { "R_Hand_Weapon" };

    // ---------------- 완전 이관: Spawn points & prefabs ----------------
    [Header("Attack Spawn Points (완전 이관)")]
    [Tooltip("원거리 스폰 포인트(1). 비워두면 무기(프리팹) 내부에서 'Fire_Point'를 자동 사용.\n" +
             "값을 넣으면 무기(프리팹) 기준으로 해당 이름/경로를 찾아 사용합니다.")]
    public string projectileSpawnPointPathOrName = "";

    [Header("Attack Prefabs (완전 이관)")]
    [Tooltip("근접 히트박스 방식. SpawnPrefab=스폰 포인트에 프리팹 생성, WeaponCollider=무기에 붙은 콜라이더 활성화")]
    public MeleeHitboxMode meleeHitboxMode = MeleeHitboxMode.SpawnPrefab;
    [Tooltip("meleeHitboxMode가 SpawnPrefab일 때 사용. 비어있으면 경고.")]
    public GameObject meleeHitboxPrefab;

    /// <summary>무기 콜라이더 사용 여부. meleeHitboxMode 또는 레거시 useWeaponCollider 기준.</summary>
    public bool UseWeaponCollider => _useWeaponColliderLegacy || meleeHitboxMode == MeleeHitboxMode.WeaponCollider;
    [SerializeField, HideInInspector]
    [UnityEngine.Serialization.FormerlySerializedAs("useWeaponCollider")]
    private bool _useWeaponColliderLegacy = false;
    public GameObject projectilePrefab;
    public GameObject shotgunSectorPrefab;

    [Tooltip("피격 시 타겟 표면에 스폰할 이펙트 프리팹. 비어있으면 이펙트 없음. ClosestPoint 기준 위치에 생성됨.")]
    public GameObject hitEffectPrefab;

    [Header("공격 FX (Attack Prefabs 아래)")]
    [Tooltip("공격 시 스폰할 FX 목록. attachRoot, prefab, startDelay 지정.")]
    public List<AttackFXEntry> attackFX = new List<AttackFXEntry>();

    // ---------------- Dual Wield ----------------
    [Header("Dual Wield (양손 옵션)")]
    [Tooltip("true면 1회 공격에서 스폰을 최대 2번(1번/2번) ���도합니다.\n" +
             "근접은 항상 'Root_dummy' 기준, 원거리는 projectileSpawnPoint2 사용.")]
    public bool dualWield = false;

    [Tooltip("2번째 스폰 딜레이(초). 0이면 거의 동시에 나갑니다.")]
    public float hitboxSpawnDelay2 = 0f;

    [Header("Dual Wield - Second Spawn Points")]
    [Tooltip("원거리 스폰 포인트(2). 비워두면 2번째 원거리 스폰은 안 나갑니다. (무기 내부 기준 이름/경로)")]
    public string projectileSpawnPoint2PathOrName = "";

    /// <summary>
    /// 무기 SO의 hitEffectPrefab을 hitPoint 위치에 스폰. 비어있으면 무시.
    /// </summary>
    public static void TrySpawnHitEffectAt(WeaponDataSO weapon, System.Nullable<Vector3> hitPoint)
    {
        if (weapon == null || weapon.hitEffectPrefab == null || !hitPoint.HasValue) return;
        Vector3 pos = hitPoint.Value + Random.insideUnitSphere * 0.05f;
        Object.Instantiate(weapon.hitEffectPrefab, pos, Quaternion.identity);
    }

    /// <summary>
    /// 몬스터 공격 데이터의 처치 연출(Death Mode, 랙돌, 슬라이스)로 런타임 프록시 WeaponDataSO 생성.
    /// HitBox/ApplyDamage 호출 시 인라인 필드만 넘길 때 사용.
    /// </summary>
    public static WeaponDataSO CreatePlayerDeathProxy(
        DeathMode deathMode,
        float ragdollImpulse,
        float ragdollUpImpulse,
        float ragdollSpinTorque,
        List<SliceTarget> sliceTargets,
        float sliceImpulse)
    {
        var so = CreateInstance<WeaponDataSO>();
        so.hideFlags = HideFlags.HideAndDontSave;
        so.deathMode = deathMode;
        so.ragdollImpulse = ragdollImpulse;
        so.ragdollUpImpulse = ragdollUpImpulse;
        so.ragdollSpinTorque = ragdollSpinTorque;
        so.sliceTargets = sliceTargets != null ? new List<SliceTarget>(sliceTargets) : new List<SliceTarget>();
        so.sliceImpulse = sliceImpulse;
        return so;
    }

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

        // Migration: if unified fields are empty, pull from legacy split values.
        if (targetHoldDuration <= 0f)
            targetHoldDuration = Mathf.Max(targetStateHoldDuration, targetAnimationHoldDuration);
        if (attackerHoldDuration <= 0f)
            attackerHoldDuration = Mathf.Max(attackerStateHoldDuration, attackerAnimationHoldDuration);

        targetHoldDuration = Mathf.Max(0f, targetHoldDuration);
        attackerHoldDuration = Mathf.Max(0f, attackerHoldDuration);

        targetStateHoldDuration = Mathf.Max(0f, targetStateHoldDuration);
        targetAnimationHoldDuration = Mathf.Max(0f, targetAnimationHoldDuration);
        attackerStateHoldDuration = Mathf.Max(0f, attackerStateHoldDuration);
        attackerAnimationHoldDuration = Mathf.Max(0f, attackerAnimationHoldDuration);

        ragdollImpulse = Mathf.Max(0f, ragdollImpulse);
        ragdollUpImpulse = Mathf.Max(0f, ragdollUpImpulse);
        ragdollSpinTorque = Mathf.Max(0f, ragdollSpinTorque);

        sliceImpulse = Mathf.Max(0f, sliceImpulse);

        hitboxSpawnDelay2 = Mathf.Max(0f, hitboxSpawnDelay2);

        trailEmitStartDelay = Mathf.Max(0f, trailEmitStartDelay);
        if (trailEmitDuration < 0f)
            trailEmitDuration = 0f;

        // 레거시 useWeaponCollider → meleeHitboxMode 마이그레이션
        if (_useWeaponColliderLegacy)
        {
            meleeHitboxMode = MeleeHitboxMode.WeaponCollider;
            _useWeaponColliderLegacy = false;
        }

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