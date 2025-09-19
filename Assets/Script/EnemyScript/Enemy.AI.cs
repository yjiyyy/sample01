using UnityEngine;

[DisallowMultipleComponent]
public class EnemyAI : MonoBehaviour
{
    [Header("Fallback 사거리(패턴 없을 때만 사용)")]
    public float fallbackEngageRange = 2f;

    private bool inAttackAnim;

    public void Tick(Enemy ctx, Transform player)
    {
        if (ctx == null || player == null || ctx.agent == null || !ctx.agent.isOnNavMesh) return;
        if (ctx.CurrentState == Enemy.EnemyState.ShieldBreak) return; // 그로기 중 AI 중지
        if (ctx.CurrentState == Enemy.EnemyState.Dead) return;

        // 쿨다운 중이면 이동/공격 안함
        if (
            (ctx.CurrentState == Enemy.EnemyState.Chase || ctx.CurrentState == Enemy.EnemyState.Attack) &&
            ctx.attackCtrl != null && ctx.attackCtrl.IsCooldownActive()
        )
        {
            // 바라보기
            Vector3 dir = player.position - ctx.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                ctx.transform.rotation = Quaternion.LookRotation(dir);

            ctx.animCtrl.UpdateMovement(0f);
            return;
        }

        switch (ctx.CurrentState)
        {
            case Enemy.EnemyState.Chase:
                HandleChase(ctx, player);
                break;
            case Enemy.EnemyState.Attack:
                HandleAttack(ctx, player);
                break;
        }
    }

    private void HandleChase(Enemy ctx, Transform player)
    {
        Vector3 dir = player.position - ctx.transform.position;
        float distance = dir.magnitude;

        int attackIdx = -1;
        if (ctx.attackCtrl != null && ctx.attackCtrl.AttackCount > 0)
            attackIdx = ctx.attackCtrl.SelectAttackIndex(distance);

        float engageRange = (attackIdx >= 0)
            ? ctx.attackCtrl.GetAttackRange(attackIdx)
            : fallbackEngageRange;

        bool canAttack = (attackIdx >= 0);

        if (distance < engageRange && canAttack)
        {
            bool started = ctx.attackCtrl.TryStartAttack(attackIdx, player);
            if (started)
            {
                inAttackAnim = true;
                return;
            }
        }

        ctx.agent.isStopped = false;
        ctx.agent.SetDestination(player.position);
        ctx.animCtrl.UpdateMovement(ctx.agent.velocity.magnitude);

        dir.y = 0f;
        if (dir != Vector3.zero)
            ctx.transform.rotation = Quaternion.LookRotation(dir);
    }

    private void HandleAttack(Enemy ctx, Transform player)
    {
        if (ctx.attackCtrl != null && ctx.attackCtrl.IsRushing)
            return;

        Vector3 dir = player.position - ctx.transform.position;
        dir.y = 0f;
        if (dir != Vector3.zero)
            ctx.transform.rotation = Quaternion.LookRotation(dir);

        var info = ctx.animator != null ? ctx.animator.GetCurrentAnimatorStateInfo(0) : default;
        if (ctx.animator != null && info.IsName("Attack") && info.normalizedTime >= 0.95f)
        {
            inAttackAnim = false;
        }
    }

    public void OnAttackStarted(Enemy ctx)
    {
        inAttackAnim = true;
    }
    public void InterruptAttack()
    {
        inAttackAnim = false;
    }
}