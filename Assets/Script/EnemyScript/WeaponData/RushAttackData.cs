using UnityEngine;

[CreateAssetMenu(fileName = "RushAttack", menuName = "Enemy/Attack/RushAttackData")]
public class RushAttackData : ScriptableObject
{
    [Header("준비 단계")]
    public float range = 2f;

    public float prepareTime = 1f;
    [Header("러시 본동작")]
    public float rushTime = 2f;
    public float rushSpeed = 10f;

    [Header("전투 스탯")]
    public float damage = 20f;
    public float knockbackPower = 5f;
    public float knockbackDuration = 0.3f;
    public float stunDuration = 0f;
    public float cooldown = 3f;

    [Header("방향 보정(선택)")]
    public bool allowDirectionDeviation = false;
    [Range(0f, 5f)] public float directionDeviationAmount = 0.1f;

    [Header("히트박스 (SO에서 지정)")]
    [Tooltip("러시 동안 붙일 히트박스 프리팹 (Trigger Collider + HitBox_Enemy 권장)")]
    public GameObject hitBoxPrefab;

    [Tooltip("히트박스 유지 시간 (0 이하이면 rushTime 동안 유지)")]
    public float hitBoxLifetime = 0f;

}