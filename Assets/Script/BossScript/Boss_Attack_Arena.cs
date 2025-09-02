public class Boss_Attack_Arena : BossAttackPattern
{
    public Boss_Attack_Arena()
    {
        cooldown = 15.0f;
    }

    public override void Execute(Boss boss)
    {
        boss.PlayAnimation("Attack_Arena");
        boss.StartCoroutine(boss.ResumeChaseAfterDelay(4.0f));
        MarkUsed();
    }
}
