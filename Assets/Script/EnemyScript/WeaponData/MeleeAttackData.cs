using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Enemy/Attack/MeleeAttackData", fileName = "MeleeAttackData_SO")]
public class MeleeAttackData : ScriptableObject
{
    [Header("공격 기본 정보")]
    public string attackName = "Melee_Attack";
    public GameObject hitBoxPrefab;

    [Header("전투 스탯")]
    public bool grantSuperArmor = false;
    public float damage = 10f;
    public float range = 2f;
    public float cooldown = 1f;

    [Header("지속 시간")]
    [Tooltip("공격 모션 시간. 0 이하이면 컨트롤러 fallback 사용.")]
    public float attackTime = 0f;

    [Header("애니메이션 클립")]
    [Tooltip("공격 모션 후보. 1개면 고정, 2개 이상이면 매 공격마다 그중 하나를 랜덤 재생합니다.")]
    public List<AnimationClip> variantClips = new List<AnimationClip>();

    [Header("히트박스 타이밍 및 지속")]
    public float hitboxSpawnDelay = 0f;
    public float hitBoxLifetime = 0.2f;

    [Header("공격 FX")]
    [Tooltip("페이즈별 공격 FX. 근접 단타는 Attack 페이즈를 주로 사용.")]
    public List<AttackFXPhaseSet> attackFXPhases = new List<AttackFXPhaseSet>();

    // New: whether spawned hitbox should be attached to the enemy (local) or left in world space
    [Header("히트박스 스폰 옵션")]
    [Tooltip("히트박스를 적의 자식으로 붙일지 여부. true이면 적의 자식으로 붙고, false이면 월드 고정으로 스폰됩니다.")]
    public bool attachHitboxToEnemy = true;

    [Header("넉백 / 저크")]
    public float knockbackPower = 4f;
    public float knockbackDuration = 0.25f;
    public float stunDuration = 0f;

    [Header("Push / Hitstop (플레이어 피격 시)")]
    [Tooltip("true면 넉백+스턴 대신 밀림(Push)만 적용. 플레이어 SO와 동일.")]
    public bool usePushInsteadOfKnockback = false;
    [Tooltip("피격 시 플레이어 Hitstop 시간(초). 0이면 비활성. 플레이어 SO targetHoldDuration과 동일.")]
    public float targetHoldDuration = 0f;
    [Tooltip("공격 적중 시 공격자(몬스터) Hitstop 시간(초). 플레이어 SO attackerHoldDuration과 동일.")]
    public float attackerHoldDuration = 0f;

    [Header("중복 데미지")]
    public bool allowDuplicateHit = false;
    public float duplicateHitInterval = 0.1f;

    [Header("처치 연출 (플레이어)")]
    public DeathMode deathMode = DeathMode.Animation;
    [Header("Ragdoll 임펄스(죽음이 Ragdoll일 때만 사용)")]
    public float ragdollImpulse = 5f;
    public float ragdollUpImpulse = 0f;
    public float ragdollSpinTorque = 0f;
    [Header("Slice(본 분리)")]
    public List<SliceTarget> sliceTargets = new List<SliceTarget>();
    public float sliceImpulse = 0f;

    [Header("독 (플레이어)")]
    [Tooltip("true일 때만 독 공격으로 처리합니다(배리어 우회 등). false이면 Poison On Hit Status가 있어도 적용되지 않습니다.")]
    public bool isPoisonAttack;
    [Tooltip("맞을 때 플레이어 중독 상태를 갱신할 설정. 비우면 독 규칙만 적용되고 중독 틱·연출은 없습니다.")]
    public PoisonStatusConfigSO poisonOnHitStatus;

    // -----------------------------
    // Moving attack 간소화된 필드
    // -----------------------------
    [Header("이동 공격 옵션")]
    [Tooltip("이동을 수행하는 공격인지 여부")]
    public bool isMovingAttack = false;

    [Tooltip("임펄스(초기 속도) 크기. 단위: m/s (리지드바디에 단발로 적용하는 초기 속도 개념)")]
    public float moveForce = 4f;

    [Tooltip("이동 감쇠 지속시간(초). 임펄스 이후 속도를 감쇠하여 정지시키는 시간.")]
    public float moveDuration = 0.3f;

    public enum MovementLockTiming
    {
        OnAnimationStart,    // 공격 애니메이션 시작 직후 위치를 고정
        JustBeforeImpulse    // 실제 힘을 주기 직전까지 위치를 계속 추적하고, 힘을 주는 순간 위치를 고정
    }

    [Tooltip("목표 위치 고정 타이밍")]
    public MovementLockTiming lockTiming = MovementLockTiming.JustBeforeImpulse;

    // 단일 커스텀 필드: 애니메이션 시작 기준으로 몇 초 후에 힘을 적용할지(0이면 즉시)
    [Tooltip("애니메이션 시작 기준으로 힘을 적용할 시간(초). 0이면 즉시 적용.")]
    public float forceApplyTime = 0f;

    /// <summary>
    /// variantClips에 등록된 클립 중 하나를 랜덤 선택. 없으면 null.
    /// </summary>
    public AnimationClip PickRandomVariantClip()
    {
        if (variantClips == null || variantClips.Count == 0)
            return null;

        int validCount = 0;
        for (int i = 0; i < variantClips.Count; i++)
        {
            if (variantClips[i] != null)
                validCount++;
        }

        if (validCount == 0)
            return null;

        if (validCount == 1)
        {
            for (int i = 0; i < variantClips.Count; i++)
            {
                if (variantClips[i] != null)
                    return variantClips[i];
            }
        }

        int pick = Random.Range(0, validCount);
        for (int i = 0; i < variantClips.Count; i++)
        {
            if (variantClips[i] == null) continue;
            if (pick == 0) return variantClips[i];
            pick--;
        }

        return null;
    }

    private void OnValidate()
    {
        hitboxSpawnDelay = Mathf.Max(0f, hitboxSpawnDelay);
        hitBoxLifetime = Mathf.Max(0f, hitBoxLifetime);
        knockbackDuration = Mathf.Max(0f, knockbackDuration);
        stunDuration = Mathf.Max(0f, stunDuration);
        targetHoldDuration = Mathf.Max(0f, targetHoldDuration);
        attackerHoldDuration = Mathf.Max(0f, attackerHoldDuration);
        range = Mathf.Max(0f, range);
        cooldown = Mathf.Max(0f, cooldown);

        // 이동 공격값 검증
        moveForce = Mathf.Max(0f, moveForce);
        moveDuration = Mathf.Max(0f, moveDuration);

        // forceApplyTime 검증
        forceApplyTime = Mathf.Max(0f, forceApplyTime);
    }
}