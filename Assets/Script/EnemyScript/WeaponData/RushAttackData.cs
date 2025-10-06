using UnityEngine;

[CreateAssetMenu(fileName = "RushAttack", menuName = "Enemy/Attack/RushAttackData")]
public class RushAttackData : ScriptableObject
{
    [Header("공격 기본 정보")]
    public string attackName = "Rush_Attack";

    [Header("준비 & 돌진")]
    public float prepareTime = 1.0f;
    public float rushTime = 0.6f;
    public float rushSpeed = 8f;

    [Header("쿨다운")]
    public float cooldown = 3f;

    [Header("공격 수치")]
    public GameObject hitboxPrefab;
    public float damage = 15f;
    public float range = 3f;

    [Header("넉백 & 제어")]
    public float knockbackPower = 8f;
    public float knockbackDuration = 0.25f;
    public float stunDuration = 0f;

    [Header("히트박스")]
    public float hitBoxLifetime = 0.2f;

    [Header("중복 히트")]
    public bool allowDuplicateHit = false;
    public float duplicateHitInterval = 0.15f;

    [Header("기타")]
    public bool grantSuperArmor = true;

    [Header("방향 보정 옵션")]
    public bool allowDirectionDeviation = false;
    public float directionDeviationAmount = 4f;

    [Header("패턴 홀드 Override")]
    public float holdOverride = -1f;
}