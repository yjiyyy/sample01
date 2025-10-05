using UnityEngine;

[CreateAssetMenu(fileName = "RushAttack", menuName = "Enemy/Attack/RushAttackData")]
public class RushAttackData : ScriptableObject
{
    [Header("기본")]
    public string attackName = "Rush_Attack";
    public bool grantSuperArmor = true;

    [Header("사거리 / 쿨다운")]
    public float range = 5f;
    [Tooltip("0 < cooldown < 1 이면 실제 적용 1초, 0이면 전역 GCD 없음")]
    public float cooldown = 1f;

    [Header("준비 & 돌진")]
    public float prepareTime = 0.8f;
    public float rushTime = 1.2f;
    public float rushSpeed = 6f;

    [Header("방향 보정")]
    public bool allowDirectionDeviation = false;
    [Range(0f, 1f)] public float directionDeviationAmount = 0.25f;

    [Header("데미지/상호작용")]
    public float damage = 10f;
    public float knockbackPower = 5f;
    public float knockbackDuration = 0.25f;
    public float stunDuration = 0f;

    [Header("히트박스")]
    public GameObject hitBoxPrefab;
    public float hitBoxLifetime = 0.5f;

    [Header("중복 히트 옵션")]
    public bool allowDuplicateHit = false;
    public float duplicateHitInterval = 0.1f;

    // AttackTime 개념: prepare + rush 합산을 Behavior에서 계산 (attackTime SO 필드 별도 두지 않음)
}