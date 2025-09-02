public class BossAttackDecider
{
    private BossAttackPattern melee = new Boss_Attack_Melee();
    private BossAttackPattern jump = new Boss_Attack_Jump();
    private BossAttackPattern arena = new Boss_Attack_Arena();

    public BossAttackPattern ChoosePattern(float distance)
    {
        if (distance < 3f && melee.IsOffCooldown())
            return melee;
        else if (distance < 7f && jump.IsOffCooldown())
            return jump;
        else if (arena.IsOffCooldown())
            return arena;

        return null; // 아무 쿨타임도 안됐을 경우
    }
}
