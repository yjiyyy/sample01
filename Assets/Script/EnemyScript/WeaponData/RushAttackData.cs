using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "RushAttack", menuName = "Enemy/Attack/RushAttackData")]
public class RushAttackData : EnemyAttackDataBase
{
    [Header("공격 기본 정보")]
    public string attackName = "Rush_Attack";
    public bool grantSuperArmor = true;

    [Header("사거리/쿨타임/데미지")]
    public float range = 5f;
    public float cooldown = 1f;
    public float damage = 5f;

    [Header("타이밍")]
    [FormerlySerializedAs("prepareTime")]
    [Tooltip("준비 단계 지속시간(초)")]
    public float prepareDuration = 1f;

    [FormerlySerializedAs("rushTime")]
    [Tooltip("돌진(공격) 단계 지속시간(초)")]
    public float attackDuration = 1f;

    [Tooltip("마무리(감속) 단계 지속시간(초)")]
    public float finishDuration = 0.3f;

    [Tooltip("돌진(공격) 단계에서 이동하는 속도(미터/초)")]
    public float rushSpeed = 5f;

    [Header("방향 보정 (돌진 중 타겟을 향해 꺾기)")]
    public bool allowDirectionDeviation = false;

    [Tooltip("1이면 즉시 타겟 방향으로 꺾고, 0이면 방향을 고정합니다. 0~1 사이 보간")]
    [Range(0f, 1f)] public float directionDeviationAmount = 0.5f;

    [Header("넉백/스턴")]
    public float knockbackPower = 5f;
    public float knockbackDuration = 0.3f;
    public float stunDuration = 0f;

    [Header("Push / Hitstop (플레이어 피격 시)")]
    [Tooltip("true면 넉백+스턴 대신 밀림(Push)만 적용. 플레이어 SO와 동일.")]
    public bool usePushInsteadOfKnockback = false;
    [Tooltip("피격 시 플레이어 Hitstop 시간(초). 0이면 비활성.")]
    public float targetHoldDuration = 0f;
    [Tooltip("공격 적중 시 공격자(몬스터) Hitstop 시간(초). 플레이어 SO attackerHoldDuration과 동일.")]
    public float attackerHoldDuration = 0f;

    [Header("히트박스")]
    [FormerlySerializedAs("hitboxPrefab")]
    public GameObject hitBoxPrefab;
    [Tooltip("히트박스 유지시간(초). 0 이하면 돌진 시간(attackDuration)과 같게 처리")]
    public float hitBoxLifetime = 0f;

    [Header("중복 피격")]
    public bool allowDuplicateHit = true;
    public float duplicateHitInterval = 0.1f;

    [Header("독 (플레이어)")]
    [Tooltip("true일 때만 독 공격으로 처리합니다(배리어 우회 등). false이면 Poison On Hit Status가 있어도 적용되지 않습니다.")]
    public bool isPoisonAttack;
    [Tooltip("맞을 때 플레이어 중독 상태를 갱신할 설정. 비우면 독 규칙만 적용되고 중독 틱·연출은 없습니다.")]
    public PoisonStatusConfigSO poisonOnHitStatus;

    [Header("애니메이션 클립 (선택)")]
    [Tooltip("준비 단계에서 재생할 클립(없으면 컨트롤러가 RushPrepare 상태 이름을 사용)")]
    public AnimationClip prepareClip;

    [Tooltip("돌진(공격) 단계에서 재생할 클립(없으면 attackName 또는 \"Rush\")")]
    public AnimationClip attackClip;

    [Tooltip("마무리(감속) 단계에서 재생할 클립(없으면 생략)")]
    public AnimationClip finishClip;

    [Header("Attack FX (Phase)")]
    [Tooltip("이 공격의 페이즈별 FX. Prepare/Attack/Finish 페이즈를 주로 사용합니다.")]
    public List<AttackFXPhaseSet> attackFXPhases = new List<AttackFXPhaseSet>();

    private void OnValidate()
    {
        range = Mathf.Max(0f, range);
        cooldown = Mathf.Max(0f, cooldown);
        damage = Mathf.Max(0f, damage);

        prepareDuration = Mathf.Max(0f, prepareDuration);
        attackDuration = Mathf.Max(0f, attackDuration);
        finishDuration = Mathf.Max(0f, finishDuration);
        rushSpeed = Mathf.Max(0f, rushSpeed);

        directionDeviationAmount = Mathf.Clamp01(directionDeviationAmount);

        knockbackDuration = Mathf.Max(0f, knockbackDuration);
        stunDuration = Mathf.Max(0f, stunDuration);

        hitBoxLifetime = Mathf.Max(0f, hitBoxLifetime);
        duplicateHitInterval = Mathf.Max(0f, duplicateHitInterval);
        targetHoldDuration = Mathf.Max(0f, targetHoldDuration);
        attackerHoldDuration = Mathf.Max(0f, attackerHoldDuration);
    }
}
