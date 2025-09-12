using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyImpact : MonoBehaviour
{
    private Coroutine knockbackRoutine;
    private Coroutine stunRoutine;

    public void OnDamage(Enemy ctx, Vector3 hitDir, WeaponDataSO weapon, float scale)
    {
        if (stunRoutine != null) { ctx.StopCoroutine(stunRoutine); stunRoutine = null; }
        if (knockbackRoutine != null) { ctx.StopCoroutine(knockbackRoutine); }

        knockbackRoutine = ctx.StartCoroutine(KnockbackThenStunRoutine(ctx, hitDir, weapon, scale));

        if (weapon != null && weapon.jerkIntensity > 0f)
        {
            if (ctx.TryGetComponent(out MultiBoneJerkController jerk))
                jerk.TriggerJerk(weapon.jerkIntensity, weapon.jerkDuration);
        }
    }

    public void ApplyKnockback(Enemy ctx, Vector3 dir, WeaponDataSO weapon)
    {
        OnDamage(ctx, dir, weapon, 1f);
    }

    private IEnumerator KnockbackThenStunRoutine(Enemy ctx, Vector3 direction, WeaponDataSO weapon, float scale)
    {
        yield return ctx.StartCoroutine(KnockbackRoutine(ctx, direction, weapon, scale));

        if (weapon != null && weapon.stunDuration > 0f)
        {
            stunRoutine = ctx.StartCoroutine(StunRoutine(ctx, weapon.stunDuration));
        }
        else
        {
            ctx.SetState(Enemy.EnemyState.Chase);
        }
    }

    private IEnumerator KnockbackRoutine(Enemy ctx, Vector3 direction, WeaponDataSO weapon, float scale)
    {
        ctx.SetState(Enemy.EnemyState.Knockback);

        float duration = weapon != null ? weapon.knockbackDuration : 0.2f;
        float power = weapon != null ? weapon.knockbackPower * scale : 5f;

        float timer = 0f;
        Vector3 dir = direction; dir.y = 0f;
        if (dir == Vector3.zero) dir = Vector3.back;
        dir = dir.normalized;

        while (timer < duration && ctx.CurrentState != Enemy.EnemyState.Dead)
        {
            float t = timer / duration;
            float currentSpeed = Mathf.Lerp(power, 0f, t);
            ctx.transform.position += dir * currentSpeed * Time.deltaTime;

            timer += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator StunRoutine(Enemy ctx, float duration)
    {
        ctx.SetState(Enemy.EnemyState.Stunned);
        yield return new WaitForSeconds(duration);
        if (ctx.CurrentState != Enemy.EnemyState.Dead)
            ctx.SetState(Enemy.EnemyState.Chase);
        stunRoutine = null;
    }
}