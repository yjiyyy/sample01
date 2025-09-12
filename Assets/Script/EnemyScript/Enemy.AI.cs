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

        // 쿨다운 체크를 Chase/Attack 상태에서만!
        if (
            (ctx.CurrentState == Enemy.EnemyState.Chase || ctx.CurrentState == Enemy.EnemyState.Attack) &&
            ctx.attackCtrl != null && ctx.attackCtrl.IsCooldownActive()
        )
        {
            // 쿨다운 중이므로 아무것도 안 함 (Idle 유지)
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
                // Knockback, Stunned 등에서는 아무것도 안 함
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
            ctx.attackCtrl.NotifyAttack(attackIdx);
            ctx.attackCtrl.BeginCooldown(attackIdx);

            ctx.SetState(Enemy.EnemyState.Attack);
            inAttackAnim = true;
            return;
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
            // 쿨다운 코루틴에서 자동 전환하게 맡김
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