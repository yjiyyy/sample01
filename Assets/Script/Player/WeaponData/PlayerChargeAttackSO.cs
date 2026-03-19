using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Player/ChargeAttack")]
public class PlayerChargeAttackSO : ScriptableObject { 
    [Header("차지 성공 조건")]
    [Tooltip("공격 버튼을 누른(즉발 시점) 후, 이 시간 동안 계속 홀드하면 차지 성공")]
    public float holdSuccessTime = 1.5f;
    [Header("애니메이션")]
    [Tooltip("지정 시 이 클립 이름으로 재생(우선)")]
    public AnimationClip chargedClip;
    [Tooltip("클립이 비어 있으면 이 스테이트 이름으로 재생")]
    public string chargedStateName = "Attack_Charged01";
    [Header("히트박스 프리팹")]
    public GameObject hitBoxPrefab;
    [Header("전투 스탯(차지 전용)")]
    public float damage = 120f;
    public float duration = 0.8f;
    public float range = 2.5f;
    public float hitBoxLifetime = 0.15f;
    [Header("넉백/스턴(EnemyImpact에서 SO로 읽음)")]
    public float knockbackPower = 5f;
    public float knockbackDuration = 0.3f;
    public float stunDuration = 0f;
    /* ───────── Push 옵션 (WeaponDataSO와 동일 스펙으로 추가) ───────── */
    [Header("Push(밀림) 옵션")]
    [Tooltip("체크하면 이 차지 공격은 상태 변화(넉백) 대신 단순 Push로 동작합니다.")]
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
    [Header("발동 무적 (A안: 차지 성공 즉시부터 적용)")]
    public float invincibilityDuration = 0.3f;
    [Header("스폰 포인트")]
    [Tooltip("없으면 플레이어 Transform 기준. 기본은 Root_dummy")]
    public string meleeSpawnPointName = "Root_dummy";
    [Header("히트박스 스폰 딜레이 (절대시간, 차지 성공 시점 기준)")]
    [Tooltip("spawnCount로 개수 조절. spawnDelays에 절대시간(초)을 입력하세요. OnValidate에서 자동 정렬됩니다.")]
    public int spawnCount = 1;
    public List<float> spawnDelays = new List<float>() { 0f };
    [Header("AoE DoT(틱 모드)")]
    [Tooltip("켜면 라이프타임 동안 주기적으로 피해를 줍니다(즉발 1회 타격 없음).")]
    public bool enableAreaDot = false;
    [Tooltip("틱마다 주는 피해량")]
    public float dotDamagePerTick = 10f;
    [Tooltip("틱 주기(초)")]
    public float dotTickInterval = 0.2f;
    // ---------------- 처치 연출 (WeaponDataSO와 동일 필드) ----------------
    [Header("처치 연출 선택")]
    public DeathMode deathMode = DeathMode.Animation;
    [Header("Ragdoll 임펄스(죽음이 Ragdoll일 때만 사용)")]
    public float ragdollImpulse = 5f;
    public float ragdollUpImpulse = 0f;
    public float ragdollSpinTorque = 0f;
    [Header("Slice(본 분리)")]
    public List<SliceTarget> sliceTargets = new List<SliceTarget>();
    public float sliceImpulse = 0f;
    // ---------- New: Continuous / Movement / SuperArmor options ----------
    [Header("연속 차지 옵션")]
    [Tooltip("차지 성공 후 버튼을 떼거나 취소될 때까지 사이클을 반복 실행합니다.")]
    public bool continuousWhileHeld = false;
    [Header("공격 중 이동 (스크립트 이동)")]
    [Tooltip("Attack Duration 동안 플레이어를 스크립트로 이동시킵니다.")]
    public bool moveDuringAttack = false;
    [Range(0f, 1f)]
    [Tooltip("공격 중 이동/회전 배율(0=정지/회전 없음, 1=기본 속도). FixedUpdate 기반으로 적용됩니다.")]
    public float moveSpeedDuringAttack = 1f;
    [Header("플레이어 슈퍼아머")]
    [Tooltip("차지 성공 시 슈퍼아머를 부여하여 넉백/스턴에 의한 취소를 방지합니다.")]
    public bool grantSuperArmor = false;
    [Tooltip("슈퍼아머 지속시간(초). 0이면 한 사이클 동안만 적용.")]
    public float superArmorDuration = 0.0f;
    // --------------------------------------------------------------------

    // ---------- New: face nearest option ----------
    [Header("연속 차지 추가 옵션")]
    [Tooltip("체크하면 연속 차지 중 가장 가까운 몬스터를 항상 바라보며 주변을 돈다. 범위는 'range' 필드를 사용합니다.")]
    public bool faceNearestWhileHeld = false;
    private void OnValidate()
    {
        holdSuccessTime = Mathf.Max(0f, holdSuccessTime);
        duration = Mathf.Max(0f, duration);
        range = Mathf.Max(0f, range);
        hitBoxLifetime = Mathf.Max(0.01f, hitBoxLifetime);
        knockbackDuration = Mathf.Max(0f, knockbackDuration);
        stunDuration = Mathf.Max(0f, stunDuration);
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
        invincibilityDuration = Mathf.Max(0f, invincibilityDuration);
        spawnCount = Mathf.Max(1, spawnCount);
        dotDamagePerTick = Mathf.Max(0f, dotDamagePerTick);
        dotTickInterval = Mathf.Max(0.01f, dotTickInterval);
        superArmorDuration = Mathf.Max(0f, superArmorDuration);
    }
}