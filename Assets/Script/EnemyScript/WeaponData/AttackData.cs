using UnityEngine;

// 공격 데이터의 기본 클래스
public abstract class AttackData : ScriptableObject
{
    [Header("공격 기본 정보")]
    public string attackName = "Attack";

    [Header("전투 스탯")]
    public float damage = 10f;
    public float range = 2f;
    public float cooldown = 1f;

    [Header("넉백 관련")]
    public float knockbackPower = 5f;
    public float knockbackDuration = 0.2f;
    public float stunDuration = 0f;
}