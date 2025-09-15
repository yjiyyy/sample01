using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class EnemyImpact : MonoBehaviour
{
    private Coroutine impactRoutine;

    public void ApplyKnockback(Enemy ctx, Vector3 hitDir, WeaponDataSO weapon, float impactScale = 1f)
    {
        // 기존 코루틴 강제 중단
        if (impactRoutine != null)
        {
            StopCoroutine(impactRoutine);
            impactRoutine = null;
        }

        ctx.attackCtrl?.InterruptCooldown();
        ctx.ai?.InterruptAttack();

        float damage = weapon != null ? weapon.damage : 0f;
        if (ctx.TryGetComponent(out EnemyHealth health))
        {
            health.ApplyDamage(damage, hitDir, weapon, impactScale);
        }

        float knockbackPower = weapon != null ? weapon.knockbackPower * impactScale : 0f;
        float knockbackDuration = weapon != null ? weapon.knockbackDuration : 0.1f;
        float stunDuration = weapon != null ? weapon.stunDuration : 0f;

        impactRoutine = StartCoroutine(KnockbackThenStunRoutine(ctx, hitDir, knockbackPower, knockbackDuration, stunDuration));
    }

    private IEnumerator KnockbackThenStunRoutine(Enemy ctx, Vector3 hitDir, float power, float knockDuration, float stunDuration)
    {
        // 넉백 시작
        ctx.SetState(Enemy.EnemyState.Knockback, true);
        ctx.animCtrl?.PlayKnockback();

        float timer = 0f;
        Vector3 knockDir = hitDir.normalized;
        knockDir.y = 0f;

        while (timer < knockDuration)
        {
            if (ctx.agent != null && ctx.agent.isOnNavMesh)
            {
                ctx.agent.isStopped = true;
                ctx.agent.velocity = knockDir * power * (1f - timer / Mathf.Max(knockDuration, 0.01f));
            }
            timer += Time.deltaTime;
            yield return null;
        }

        // 넉백 끝난 후
        if (stunDuration > 0f)
        {
            // 스턴 시작
            ctx.SetState(Enemy.EnemyState.Stunned, true);
            ctx.animCtrl?.PlayStun(true);
            if (ctx.agent != null && ctx.agent.isOnNavMesh)
            {
                ctx.agent.isStopped = true;
                ctx.agent.velocity = Vector3.zero;
            }
            yield return new WaitForSeconds(stunDuration);
            ctx.animCtrl?.PlayStun(false);

            // ⭐ 스턴 끝에 트리거 발동 (Any State → Run으로 복귀)
            ctx.animCtrl?.Animator.SetTrigger("ResetToM");
        }
        else
        {
            // ⭐ 스턴이 없으면 넉백 끝에 트리거 발동
            ctx.animCtrl?.Animator.SetTrigger("ResetToM");
        }

        // 정상 상태 복귀
        ctx.SetState(Enemy.EnemyState.Chase);
        if (ctx.agent != null && ctx.agent.isOnNavMesh)
        {
            ctx.agent.isStopped = false;
            ctx.agent.velocity = Vector3.zero;
        }

        impactRoutine = null;
    }
}