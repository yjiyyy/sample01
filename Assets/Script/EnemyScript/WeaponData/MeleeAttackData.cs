using UnityEngine;

[CreateAssetMenu(fileName = "MeleeAttack", menuName = "Enemy/Attack/MeleeAttackData")]
public class MeleeAttackData : ScriptableObject
{
    [Header("공격 기본 정보")]
    public string attackName = "Melee_Attack";
    public GameObject hitBoxPrefab;

    [Header("전투 스탯")]
    public bool grantSuperArmor = false;
    public float damage = 10f;
    public float range = 2f;

    [Tooltip("원본 쿨다운(초). 0 < cooldown < 1 이면 '실제 적용'은 1초로 상향. 0이면 전역 GCD도 발생하지 않음.")]
    public float cooldown = 1f;

    [Header("공격 지속(AttackTime)")]
    [Tooltip("공격 시작 ~ '공격이 완전히 끝났다'고 간주하는 시간.\n0 이하이면 애니메이션 클립 길이를 사용.\n애니보다 길면 끝 프레임 Freeze(추후 구현), 짧으면 조기 종료(추후 구현).")]
    public float attackTime = 0f;

    [Header("넉백 관련")]
    public float knockbackPower = 5f;
    public float knockbackDuration = 0.2f;
    public float stunDuration = 0f;

    [Header("히트박스 설정")]
    public float hitBoxLifetime = 0.1f;

    [Header("중복 데미지 옵션")]
    public bool allowDuplicateHit = false;
    public float duplicateHitInterval = 0.1f;
}