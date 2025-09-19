using UnityEngine;

[CreateAssetMenu(fileName = "RushAttack", menuName = "Enemy/Attack/RushAttackData")]
public class RushAttackData : ScriptableObject
{
    [Header("기본")]
    public bool grantSuperArmor = false;
    public float range = 5f;
    public float prepareTime = 0f;
    public float rushTime = 2f;
    public float rushSpeed = 8f;
    public float damage = 20f;
    public float knockbackPower = 5f;
    public float knockbackDuration = 0.3f;
    public float stunDuration = 1f;
    public float cooldown = 3f;

    [Header("방향 조절")]
    public bool allowDirectionDeviation = false;
    public float directionDeviationAmount = 0.5f;

    [Header("히트박스")]
    public GameObject hitBoxPrefab;
    public float hitBoxLifetime = 1f;

    [Header("중복 데미지 옵션")]
    public bool allowDuplicateHit = false;
    public float duplicateHitInterval = 0.1f;
}