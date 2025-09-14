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

        // 쿨다운 중이면 아무 것도 안 함
        if (
            (ctx.CurrentState == Enemy.EnemyState.Chase || ctx.CurrentState == Enemy.EnemyState.Attack) &&
            ctx.attackCtrl != null && ctx.attackCtrl.IsCooldownActive()
        )
        {
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
        {
            attackIdx = ctx.attackCtrl.SelectAttackIndex(distance);
        }

        float engageRange = (attackIdx >= 0)
            ? ctx.attackCtrl.GetAttackRange(attackIdx)
            : fallbackEngageRange;

        bool canAttack = (attackIdx >= 0);

        if (distance < engageRange && canAttack)
        {
            // AttackController가 내부에서 Melee/Rush 모두 처리 (쿨다운 시점 포함)
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
        Vector3 dir = player.position - ctx.transform.position;
        dir.y = 0f;
        if (dir != Vector3.zero)
            ctx.transform.rotation = Quaternion.LookRotation(dir);

        var info = ctx.animator != null ? ctx.animator.GetCurrentAnimatorStateInfo(0) : default;
        if (ctx.animator != null && info.IsName("Attack") && info.normalizedTime >= 0.95f)
        {
            inAttackAnim = false;
        }

        if (!inAttackAnim)
        {
            // 쿨다운 루틴이 상태 전환 담당
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