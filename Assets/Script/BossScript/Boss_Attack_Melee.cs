public class Boss_Attack_Melee : BossAttackPattern
{
    public Boss_Attack_Melee()
    {
        cooldown = 2.0f;
    }

    public override void Execute(Boss boss)
    {
        boss.PlayAnimation("Attack_Melee");
        boss.StartCoroutine(boss.ResumeChaseAfterDelay(2.0f));
        MarkUsed();
    }
}
