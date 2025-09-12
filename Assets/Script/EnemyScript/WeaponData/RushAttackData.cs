using UnityEngine;

[CreateAssetMenu(fileName = "RushAttack", menuName = "Enemy/Attack/RushAttackData")]
public class RushAttackData : ScriptableObject
{
    [Header("준비(바람) 단계")]
    public float prepareTime = 0.5f;
    public float prepareSpeed = 0f;

    [Header("돌진 단계")]
    public float rushTime = 1.5f;
    public float rushSpeed = 10f;

    [Header("전투 스탯")]
    public float damage = 10f;
    public float knockbackPower = 5f;

    [Header("쿨다운")]
    public float cooldown = 3f;

    [Header("조준 보정")]
    public bool allowDirectionDeviation = false;
    [Range(0f, 1f)] public float directionDeviationAmount = 0.1f;
}