using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "RushAttack", menuName = "Enemy/Attack/RushAttackData")]
public class RushAttackData : ScriptableObject
{
    [Header("공격 기본 정보")]
    public string attackName = "Rush_Attack";
    public bool grantSuperArmor = true;

    [Header("범위/쿨타임/데미지")]
    public float range = 5f;
    public float cooldown = 1f;
    public float damage = 5f;

    [Header("타이밍")]
    public float prepareTime = 1f;
    public float rushTime = 1f;
    public float rushSpeed = 5f;

    [Header("방향 편차 (러시 중 추적 보간 등과 병행 시 주의)")]
    public bool allowDirectionDeviation = false;
    [Tooltip("허용 시 방향 랜덤/보간 등에 활용할 양(해석은 구현부 재량)")]
    public float directionDeviationAmount = 0.5f;

    [Header("넉백/스턴")]
    public float knockbackPower = 5f;
    public float knockbackDuration = 0.3f;
    public float stunDuration = 0f;

    [Header("히트박스")]
    [FormerlySerializedAs("hitboxPrefab")]
    public GameObject hitBoxPrefab;     // 에셋 YAML: hitBoxPrefab
    public float hitBoxLifetime = 5f;

    [Header("중복 히트")]
    public bool allowDuplicateHit = true;
    public float duplicateHitInterval = 0.1f;

   
}