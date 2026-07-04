/// <summary>
/// M_Face 표정 전환에 쓰는 게임 상황.
/// Animator 상태 이름과 1:1이 아니며, 바디 쪽에서 상황을 판정해 전달합니다.
/// </summary>
public enum EnemyFaceSituation
{
    Peace,
    Find,
    Combat,
    Attack,
    Knockback,
    Stun,
    ShieldBreak,
    Dead,
}
