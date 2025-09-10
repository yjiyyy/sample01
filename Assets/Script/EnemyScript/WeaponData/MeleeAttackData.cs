using UnityEngine;

[CreateAssetMenu(fileName = "MeleeAttackData", menuName = "Enemy/Attack/MeleeAttackData")]
public class MeleeAttackData : ScriptableObject
{
    [Header("공격 기본 정보")]
    public float damage = 15.0f;        // 공격 데미지
    public float range = 2.0f;          // 공격 사거리
    
    [Header("넉백 효과")]
    public float knockbackPower = 3.0f;     // 넉백 파워
    public float knockbackDuration = 0.3f;  // 넉백 지속시간
    public float stunDuration = 0.1f;       // 스턴 지속시간
    
    [Header("히트박스 설정")]
    public GameObject hitBoxPrefab;     // 히트박스 프리팹
    public float hitBoxLifetime = 0.2f; // 히트박스 지속시간
    
    [Header("쿨다운")]
    public float cooldown = 2.0f;       // 재사용 대기시간
}