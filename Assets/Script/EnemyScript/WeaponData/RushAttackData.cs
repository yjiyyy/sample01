using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "RushAttack", menuName = "Enemy/Attack/RushAttackData")]
public class RushAttackData : ScriptableObject
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
    [Tooltip("준비 구간 지속시간(초)")]
    public float prepareDuration = 1f;

    [FormerlySerializedAs("rushTime")]
    [Tooltip("공격(돌진) 구간 지속시간(초)")]
    public float attackDuration = 1f;

    [Tooltip("마무리(감속) 구간 지속시간(초)")]
    public float finishDuration = 0.3f;

    [Tooltip("공격(돌진) 구간에서의 이동 속도(유닛/초)")]
    public float rushSpeed = 5f;

    [Header("방향 보정 (공격/마무리 중 실시간 보정 비율)")]
    public bool allowDirectionDeviation = false;

    [Tooltip("1이면 즉시 타겟 방향으로 붙고, 0이면 시작 방향 유지. 0~1 사이 권장")]
    [Range(0f, 1f)] public float directionDeviationAmount = 0.5f;

    [Header("넉백/스턴")]
    public float knockbackPower = 5f;
    public float knockbackDuration = 0.3f;
    public float stunDuration = 0f;

    [Header("히트박스")]
    [FormerlySerializedAs("hitboxPrefab")]
    public GameObject hitBoxPrefab;
    [Tooltip("히트박스 수명(초). 0 이하이면 공격(attackDuration)과 동일하게 유지")]
    public float hitBoxLifetime = 0f;

    [Header("중복 히트")]
    public bool allowDuplicateHit = true;
    public float duplicateHitInterval = 0.1f;

    [Header("애니메이션 클립 (선택)")]
    [Tooltip("준비 구간에서 재생할 클립(없으면 재생 생략 또는 기존 이름 폴백)")]
    public AnimationClip prepareClip;

    [Tooltip("공격(돌진) 구간에서 재생할 클립(없으면 attackName 또는 \"Rush\")")]
    public AnimationClip attackClip;

    [Tooltip("마무리(감속) 구간에서 재생할 클립(없으면 생략)")]
    public AnimationClip finishClip;

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
    }
}