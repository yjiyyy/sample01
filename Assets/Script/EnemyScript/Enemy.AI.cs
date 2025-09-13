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

        // 쿨다운 중이 아닐 때만 공격 시도
        int attackIdx = -1;
        bool canAttack = false;
        
        if (ctx.attackCtrl != null && ctx.attackCtrl.AttackCount > 0 && !ctx.attackCtrl.IsCooldownActive())
        {
            attackIdx = ctx.attackCtrl.SelectAttackIndex(distance);
            canAttack = (attackIdx >= 0);
            
            if (ctx.debugMode && attackIdx < 0)
                Debug.Log($"[EnemyAI] No available attacks at distance {distance:F2}");
        }
        else if (ctx.debugMode && ctx.attackCtrl.IsCooldownActive())
        {
            Debug.Log("[EnemyAI] Attack blocked - cooldown active");
        }

        float engageRange = (attackIdx >= 0)
            ? ctx.attackCtrl.GetAttackRange(attackIdx)
            : fallbackEngageRange;

        if (distance < engageRange && canAttack)
        {
            if (ctx.debugMode)
                Debug.Log($"[EnemyAI] Starting attack {attackIdx} at distance {distance:F2}");
                
            ctx.attackCtrl.NotifyAttack(attackIdx);
            ctx.attackCtrl.BeginCooldown(attackIdx);

            ctx.SetState(Enemy.EnemyState.Attack);
            inAttackAnim = true;
            return;
        }

        // 항상 플레이어를 추적 (쿨다운 중에도)
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