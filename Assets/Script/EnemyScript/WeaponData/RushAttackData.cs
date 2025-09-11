using UnityEngine;

[CreateAssetMenu(fileName = "RushAttack", menuName = "Enemy/Attack/RushAttackData")]
public class RushAttackData : ScriptableObject
{
    [Header("준비 단계")]
    public float prepareTime = 1.5f;  // 준비 시간
    public float prepareSpeed = 0.0f; // 준비 중 이동 속도 (보류)

    [Header("돌진 단계")]
    public float rushTime = 2.0f;     // 돌진 시간
    public float rushSpeed = 10.0f;   // 돌진 속도

    [Header("피해 효과")]
    public float damage = 20.0f;      // 충돌 시 플레이어 데미지
    public float knockbackPower = 5.0f; // 넉백 파워

    [Header("쿨다운")]
    public float cooldown = 3.0f;     // 쿨다운 시간

    [Header("옵션")]
    public bool allowDirectionDeviation = false; // 돌진 방향에 변화 추가
    [Range(0f, 1f)] public float directionDeviationAmount = 0.1f; // 변화량 
}