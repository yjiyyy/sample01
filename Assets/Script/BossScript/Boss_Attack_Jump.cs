using UnityEngine;

public class Boss_Attack_Jump : BossAttackPattern
{
    private Vector3 targetPosition;

    public Boss_Attack_Jump()
    {
        cooldown = 10.0f;
    }

    public override void Execute(Boss boss)
    {
        // 1. �ִϸ��̼� Ʈ���� ���� + �÷��̾� ��ġ ����
        targetPosition = boss.GetPlayerPosition();
        boss.PlayAnimation("Attack_Jump");

        // 2. 0.2�� �� �̵� ����, 0.7�ʰ� �̵�
        boss.StartCoroutine(DelayedJumpMove(boss, 0.2f, 0.8f));

        // 3. ��ü ���� �� ���� ���� (1.0�� + ����)
        boss.StartCoroutine(boss.ResumeChaseAfterDelay(3.0f));

        MarkUsed();
    }

    private System.Collections.IEnumerator DelayedJumpMove(Boss boss, float delayBeforeMove, float moveDuration)
    {
        yield return new WaitForSeconds(delayBeforeMove);

        Vector3 start = boss.transform.position;
        Vector3 end = targetPosition;
        float elapsed = 0f;

        // ������ �̵� ������ �÷��̾� ������ ����
        Vector3 direction = (end - start).normalized;
        boss.transform.rotation = Quaternion.LookRotation(direction);

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / moveDuration;
            boss.transform.position = Vector3.Lerp(start, end, t);
            yield return null;
        }

        // ���� �� Ÿ���� �ִϸ��̼� �̺�Ʈ���� ó��
    }
}
