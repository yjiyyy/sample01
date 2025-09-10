using UnityEngine;

[CreateAssetMenu(fileName = "MeleeAttackData", menuName = "Enemy/Attack/MeleeAttackData")]
public class MeleeAttackData : ScriptableObject
{
    [Header("기본 공격")]
    public float damage = 15.0f;      // 기본 공격 대미지
    public float attackRange = 2.0f;  // 공격 사거리
    public float attackRadius = 1.0f; // 공격 반경

    [Header("피해 효과")]
    public float knockbackPower = 3.0f; // 넉백 파워
    public float knockbackDuration = 0.3f; // 넉백 지속시간
    public float stunDuration = 0.1f; // 스턴 지속시간

    [Header("쿨다운")]
    public float cooldown = 2.0f;     // 재사용 대기시간
}