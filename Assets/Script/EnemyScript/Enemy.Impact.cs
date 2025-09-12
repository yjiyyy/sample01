using UnityEngine;
using System.Collections;

/// <summary>
/// 적의 넉백/데미지/스턴/임팩트 통합 처리 컴포넌트
/// - Enemy.cs에서 ApplyKnockback만 호출하면 모든 효과가 처리됨
/// </summary>
[DisallowMultipleComponent]
public class EnemyImpact : MonoBehaviour
{
    /// <summary>
    /// 적의 넉백 + 데미지 + 스턴 + 임팩트 연출을 한 번에 처리
    /// 공격 도중 피격 시 공격 상태/쿨다운/AI 공격 플래그/애니메이션까지 모두 강제 중단 후 넉백 상태로 전환
    /// </summary>
    /// <param name="ctx">적 본체(Enemy)</param>
    /// <param name="hitDir">피격 방향</param>
    /// <param name="weapon">무기 데이터</param>
    /// <param name="impactScale">임팩트 강도(거리감쇠, 특수효과 등)</param>
    public void ApplyKnockback(Enemy ctx, Vector3 hitDir, WeaponDataSO weapon, float impactScale = 1f)
    {
        // 1. 공격 상태·쿨다운·AI 공격 플래그·애니메이션 모두 강제 중단
        ctx.attackCtrl?.InterruptCooldown();   // 쿨다운/공격 코루틴 중단
        ctx.ai?.InterruptAttack();             // AI 공격 플래그 중단

        // 2. 넉백 상태로 강제 전환 (force=true)
        ctx.SetState(Enemy.EnemyState.Knockback, true);

        // 3. Knockback 애니메이션 강제 재생
        ctx.animCtrl?.PlayKnockback();

        // 4. 데미지 처리 (EnemyHealth에 전달)
        if (ctx.TryGetComponent(out EnemyHealth health))
        {
            float damage = weapon != null ? weapon.damage : 0f;
            health.ApplyDamage(damage, hitDir, weapon, impactScale);
        }

        // 5. 넉백 처리 (NavMeshAgent 이동)
        float knockbackPower = weapon != null ? weapon.knockbackPower * impactScale : 0f;
        float knockbackDuration = weapon != null ? weapon.knockbackDuration : 0.1f;
        if (knockbackPower > 0f && ctx.agent != null && ctx.agent.isOnNavMesh)
        {
            // 코루틴으로 넉백 적용
            StartCoroutine(KnockbackRoutine(ctx, hitDir, knockbackPower, knockbackDuration));
        }

        // 6. 스턴 처리 (넉백 후, stunDuration이 있다면 스턴 상태로 전환)
        float stunDuration = weapon != null ? weapon.stunDuration : 0f;
        if (stunDuration > 0f)
        {
            StartCoroutine(StunRoutine(ctx, stunDuration));
        }
    }

    /// <summary>
    /// 넉백 코루틴 (NavMeshAgent를 강제로 이동)
    /// </summary>
    private IEnumerator KnockbackRoutine(Enemy ctx, Vector3 hitDir, float power, float duration)
    {
        float timer = 0f;
        Vector3 knockDir = hitDir.normalized;
        knockDir.y = 0f;

        // 넉백 방향으로 일정 시간 이동
        while (timer < duration)
        {
            if (ctx.agent != null && ctx.agent.isOnNavMesh)
            {
                ctx.agent.isStopped = true;
                ctx.agent.velocity = knockDir * power * (1f - timer / duration);
            }
            timer += Time.deltaTime;
            yield return null;
        }

        // 넉백 종료: 상태 Chase로 복구 (스턴이 없을 경우)
        if (ctx.CurrentState == Enemy.EnemyState.Knockback)
        {
            ctx.SetState(Enemy.EnemyState.Chase);
            ctx.animCtrl?.PlayStun(false);
            if (ctx.agent != null && ctx.agent.isOnNavMesh)
            {
                ctx.agent.isStopped = false;
                ctx.agent.velocity = Vector3.zero;
            }
        }
    }

    /// <summary>
    /// 스턴 코루틴
    /// </summary>
    private IEnumerator StunRoutine(Enemy ctx, float duration)
    {
        ctx.SetState(Enemy.EnemyState.Stunned, true);
        ctx.animCtrl?.PlayStun(true);

        yield return new WaitForSeconds(duration);

        // 스턴 종료: 상태 Chase로 복구
        if (ctx.CurrentState == Enemy.EnemyState.Stunned)
        {
            ctx.SetState(Enemy.EnemyState.Chase);
            ctx.animCtrl?.PlayStun(false);
        }
    }
}