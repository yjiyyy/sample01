using UnityEngine;

[CreateAssetMenu(menuName = "Enemy/Attack/RushAttackData")]
public class RushAttackData : ScriptableObject
{
    [Header("준비 단계")]
    public float prepareTime = 1.5f;  // 준비 시간

    [Header("돌진 단계")]
    public float rushSpeed = 10.0f;   // 돌진 속도
    public float rushTime = 2.0f;     // 돌진 시간

    [Header("피해 효과")]
    public float damage = 20.0f;      // 충돌 시 플레이어 대미지
    public float knockbackPower = 5.0f; // 넉백 파워
    public float knockbackDuration = 0.5f; // 넉백 지속시간
    public float stunDuration = 0.2f; // 스턴 지속시간

    [Header("쿨다운")]
    public float cooldown = 8.0f;     // 재사용 대기시간
}