using UnityEngine;

[CreateAssetMenu(fileName = "RushAttack", menuName = "Enemy/Attack/RushAttackData")]
public class RushAttackData : ScriptableObject
{
    [Header("준비 단계 설정")]
    public float prepareTime = 1f;     // 돌진 전 준비 시간
    public float prepareSpeed = 0f;    // 준비 중 이동 속도

    [Header("돌진 설정")]
    public float rushTime = 2f;        // 돌진 지속 시간
    public float rushSpeed = 10f;      // 돌진 속도

    [Header("공격력")]
    public float damage = 20f;         // 충돌 시 데미지
    public float knockbackPower = 5f;  // 넉백 힘

    [Header("쿨다운")]
    public float cooldown = 3f;        // 재사용 대기시간

    [Header("방향 변경 설정")]
    public bool allowDirectionDeviation = false;  // 돌진 중 방향 변경 허용 여부
    public float directionDeviationAmount = 0.1f; // 변경 가능한 최대 각도 (0~1)
}