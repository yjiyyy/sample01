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
        // 1. 애니메이션 트리거 실행 + 플레이어 위치 저장
        targetPosition = boss.GetPlayerPosition();
        boss.PlayAnimation("Attack_Jump");

        // 2. 0.2초 후 이동 시작, 0.7초간 이동
        boss.StartCoroutine(DelayedJumpMove(boss, 0.2f, 0.8f));

        // 3. 전체 연출 후 추적 복귀 (1.0초 + 여유)
        boss.StartCoroutine(boss.ResumeChaseAfterDelay(3.0f));

        MarkUsed();
    }

    private System.Collections.IEnumerator DelayedJumpMove(Boss boss, float delayBeforeMove, float moveDuration)
    {
        yield return new WaitForSeconds(delayBeforeMove);

        Vector3 start = boss.transform.position;
        Vector3 end = targetPosition;
        float elapsed = 0f;

        // 보스가 이동 방향을 플레이어 쪽으로 정렬
        Vector3 direction = (end - start).normalized;
        boss.transform.rotation = Quaternion.LookRotation(direction);

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / moveDuration;
            boss.transform.position = Vector3.Lerp(start, end, t);
            yield return null;
        }

        // 도착 후 타격은 애니메이션 이벤트에서 처리
    }
}
