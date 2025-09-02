using UnityEngine;

[CreateAssetMenu(menuName = "Enemy/AttackData")]
public class EnemyAttackData : ScriptableObject
{
    [Header("기본 정보")]
    public string attackName = "NewAttack";
    public GameObject hitBoxPrefab;
    public float damage = 10f;
    public float range = 2f;
    public float cooldown = 1f;

    [Header("넉백 / 스턴 효과")]
    [Tooltip("플레이어가 밀려나는 힘 (속도 계수)")]
    public float knockbackPower = 0f;

    [Tooltip("플레이어가 밀려나는 시간 (초)")]
    public float knockbackDuration = 0f;

    [Tooltip("플레이어 스턴 지속 시간 (초)")]
    public float stunDuration = 0f;

    [Header("히트박스")]
    public float hitBoxLifetime = 0.2f;
}
