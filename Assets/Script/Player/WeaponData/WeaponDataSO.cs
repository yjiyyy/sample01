using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 폭발/프로젝타일 등에서 데미지 판정 대상을 구분하기 위한 옵션
/// (Launcher 전용 데이터에서 사용)
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

    [Header("히트박스 타이밍 및 지속")]
    [Tooltip("공격 시작 후 몇 초 뒤 히트박스가 생성되는지")]
    public float hitboxSpawnDelay = 0f;
    public float hitBoxLifetime = 0.2f;

    [Header("넉백 / 저크")]
    public float knockbackDuration = 0.2f;
    public float knockbackPower = 0f;
    public float jerkIntensity = 1f;
    public float jerkDuration = 0.2f;

    [Header("스턴")]
    [Tooltip("0이면 스턴 없음, 값이 있으면 스턴 지속 시간 (초)")]
    public float stunDuration = 0f;

    [Header("랙돌/슬라이스 연출")]
    public float ragdollImpulse = 5f;
    public float upwardImpulse = 3f;
    public float torqueImpulse = 6f;
    public float sliceForce = 8f;

    [Header("처치 연출")]
    public EnemyDeathType deathType = EnemyDeathType.Default;
    public List<BodySliceType> possibleSliceParts = new();

    /* ───────── Per-Weapon Charge Attack Slot ───────── */
    [Header("Charge Attack (무기별 선택 적용)")]
    [Tooltip("이 무기에 사용할 플레이어 차지 공격 SO. 비어있으면 이 무기는 차지 비활성.")]
    public PlayerChargeAttackSO chargeSlot;
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