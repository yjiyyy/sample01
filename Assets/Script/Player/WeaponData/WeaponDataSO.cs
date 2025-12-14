using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization;

/// <summary>
/// Weapon data ScriptableObject (common for most weapons)
/// - DeathMode 및 Ragdoll/Slice 필드 추가.
/// </summary>

public enum DamageTargetType
{
    EnemyOnly,
    PlayerOnly,
    Both
}

// 죽음 방식 선택
public enum DeathMode
{
    Animation,
    Ragdoll
}

// 새로 추가: 슬라이스 타겟(본 그룹)
public enum SliceTarget
{
    Head,       // Bip001 Head
    LeftArm,    // Bip001 L UpperArm
    RightArm,   // Bip001 R UpperArm
    LeftLeg,    // Bip001 L Thigh
    RightLeg,   // Bip001 R Thigh
    All         // 위 모든 파트 전체 분리
}

[CreateAssetMenu(menuName = "Weapon/WeaponDataSO")]
public class WeaponDataSO : ScriptableObject
{
    [Header("식별/표시")]
    public string id;
    public string weaponName = "NewWeapon";
    public Sprite icon;
    public WeaponCategory category = WeaponCategory.Primary;

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

    // renamed from hitstopTime -> animationHoldDuration
    [FormerlySerializedAs("hitstopTime")]
    public float animationHoldDuration = 0f;

    [Header("처치 연출 선택")]
    [Tooltip("죽음 방식 선택: Animation(애니메이션) 또는 Ragdoll(물리 랙돌)")]
    public DeathMode deathMode = DeathMode.Animation;

    [Header("Ragdoll 임펄스(죽음이 Ragdoll일 때만 사용)")]
    [Tooltip("수평(히트 방향) 속도 변화(m/s). ForceMode.VelocityChange로 적용.")]
    public float ragdollImpulse = 5f;

    [Tooltip("위로 띄우는 속도 변화(m/s). ForceMode.VelocityChange로 추가.")]
    public float ragdollUpImpulse = 0f;

    [Tooltip("회전 토크(ForceMode.VelocityChange). 전체 분배 기준값(힙=1.0, 머리=0.8, 기타=0.5).")]
    public float ragdollSpinTorque = 0f;

    /* ───────── Slice(본 분리) 옵션 ───────── */
    [Header("Slice(본 분리)")]
    [Tooltip("슬라이스 대상 본 목록. 여러 개 지정 시 균등 확률로 하나를 선택합니다. All을 지정하면 전체 분리합니다.")]
    public List<SliceTarget> sliceTargets = new List<SliceTarget>();

    [Tooltip("슬라이스된 파츠(선택된 본과 모든 하위 본)에만 적용할 힘(단일 값). 거리와 높이 모두 이 값 하나로 사용합니다. ForceMode.VelocityChange로 적용.")]
    public float sliceImpulse = 0f;

    [Header("Charge Attack (무기별 선택 적용)")]
    public PlayerChargeAttackSO chargeSlot;

    [Header("리코일(자기 반동)")]
    public float recoilStartDelay = 0f;
    public float recoilPower = 0f;
    public float recoilDuration = 0f;

    [Header("Mount / Socket names (priority order)")]
    public List<string> socketNames = new List<string>() { "R_Hand_Weapon" };

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

        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning($"WeaponDataSO '{name}' has empty id. Please set a unique id for inventory/DB usage.");
        }
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