using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class EnemyImpact : MonoBehaviour
{
    private Coroutine impactRoutine;

    // Soft Knock(슈퍼아머 중) 고정 짧은 시간
    private const float SOFT_KNOCK_DURATION = 0.12f; // Option C
    private const float SOFT_KNOCK_POWER_RATIO = 0.5f;

    public void ApplyKnockback(Enemy ctx, Vector3 hitDir, WeaponDataSO weapon, float impactScale = 1f)
    {
        if (ctx == null || ctx.CurrentState == Enemy.EnemyState.Dead) return;

        // 기존 코루틴 중단
        if (impactRoutine != null)
        {
            StopCoroutine(impactRoutine);
            impactRoutine = null;
        }

        float damage = weapon != null ? weapon.damage : 0f;
        if (ctx.TryGetComponent(out EnemyHealth health))
        {
            health.ApplyDamage(damage, hitDir, weapon, impactScale);
            // 사망 or ShieldBreak 진입 후 즉시 넉백/스턴 처리 스킵
            if (ctx.CurrentState == Enemy.EnemyState.Dead ||
                ctx.CurrentState == Enemy.EnemyState.ShieldBreak)
            {
                return;
            }
        }

        float knockbackPower = weapon != null ? weapon.knockbackPower * impactScale : 0f;
        float knockbackDuration = weapon != null ? weapon.knockbackDuration : 0.1f;
        float stunDuration = weapon != null ? weapon.stunDuration : 0f;

        // SuperArmor 또는 ShieldBreak 중이면 Hard 상태 전환 없이 SoftKnock
        if (ctx.HasSuperArmor)
        {
            impactRoutine = StartCoroutine(SoftKnockRoutine(ctx, hitDir, knockbackPower));
            return;
        }

        // 정상 넉백+스턴 루틴
        impactRoutine = StartCoroutine(KnockbackThenStunRoutine(ctx, hitDir, knockbackPower, knockbackDuration, stunDuration));
    }

    private IEnumerator SoftKnockRoutine(Enemy ctx, Vector3 hitDir, float power)
    {
        Vector3 dir = hitDir.normalized;
        dir.y = 0f;
        float elapsed = 0f;

        // 이동만 (공격/AI 유지, 상태 전환 없음)
        while (elapsed < SOFT_KNOCK_DURATION && ctx != null && ctx.CurrentState != Enemy.EnemyState.Dead)
        {
            if (ctx.agent != null && ctx.agent.isOnNavMesh)
            {
                float t = elapsed / SOFT_KNOCK_DURATION;
                float current = Mathf.Lerp(power * SOFT_KNOCK_POWER_RATIO, 0f, t);
                ctx.agent.isStopped = true; // 순간 정지 후 속도를 직접 부여
                ctx.agent.velocity = dir * current;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (ctx != null && ctx.agent != null && ctx.agent.isOnNavMesh &&
            ctx.CurrentState != Enemy.EnemyState.Dead &&
            ctx.CurrentState != Enemy.EnemyState.ShieldBreak)
        {
            ctx.agent.isStopped = false;
            ctx.agent.velocity = Vector3.zero;
        }

        impactRoutine = null;
    }

    private IEnumerator KnockbackThenStunRoutine(Enemy ctx, Vector3 hitDir, float power, float knockDuration, float stunDuration)
    {
        if (ctx == null) yield break;

        ctx.SetState(Enemy.EnemyState.Knockback, true);
        ctx.animCtrl?.PlayKnockback();

        float timer = 0f;
        Vector3 knockDir = hitDir.normalized;
        knockDir.y = 0f;

        while (timer < knockDuration && ctx.CurrentState == Enemy.EnemyState.Knockback)
        {
            if (ctx.agent != null && ctx.agent.isOnNavMesh)
            {
                ctx.agent.isStopped = true;
                ctx.agent.velocity = knockDir * power * (1f - timer / Mathf.Max(knockDuration, 0.01f));
            }
            timer += Time.deltaTime;
            yield return null;
        }

        if (ctx.CurrentState == Enemy.EnemyState.Dead ||
            ctx.CurrentState == Enemy.EnemyState.ShieldBreak)
        {
            impactRoutine = null;
            yield break;
        }

        if (stunDuration > 0f)
        {
            ctx.SetState(Enemy.EnemyState.Stunned, true);
            ctx.animCtrl?.PlayStun(true);
            if (ctx.agent != null && ctx.agent.isOnNavMesh)
            {
                ctx.agent.isStopped = true;
                ctx.agent.velocity = Vector3.zero;
            }
            yield return new WaitForSeconds(stunDuration);
            ctx.animCtrl?.PlayStun(false);
            ctx.animCtrl?.Animator.SetTrigger("ResetToM");
        }
        else
        {
            ctx.animCtrl?.Animator.SetTrigger("ResetToM");
        }

        if (ctx.CurrentState != Enemy.EnemyState.Dead &&
            ctx.CurrentState != Enemy.EnemyState.ShieldBreak)
        {
            ctx.SetState(Enemy.EnemyState.Chase);
            if (ctx.agent != null && ctx.agent.isOnNavMesh)
            {
                ctx.agent.isStopped = false;
                ctx.agent.velocity = Vector3.zero;
            }
        }

        impactRoutine = null;
    }
}