using UnityEngine;
using System.Collections;

/// <summary>
/// 피격 반응(넉백 / 스턴 / SoftKnock / Push)
/// - NavMeshAgent 제거 버전: 모두 Transform.position 사용
/// - 이동은 Time.fixedDeltaTime + WaitForFixedUpdate
/// </summary>
[DisallowMultipleComponent]
public class EnemyImpact : MonoBehaviour
{
    private Coroutine impactRoutine;
    private Coroutine pushRoutine;

    private const float SOFT_KNOCK_DURATION = 0.12f;
    private const float SOFT_KNOCK_POWER_RATIO = 0.5f;
    private const float FACE_ANGLE_THRESHOLD = 30f;

    public void ApplyKnockback(Enemy ctx, Vector3 hitDir, WeaponDataSO weapon, float impactScale = 1f)
    {
        if (ctx == null || ctx.CurrentState == Enemy.EnemyState.Dead) return;

        if (impactRoutine != null)
        {
            StopCoroutine(impactRoutine);
            impactRoutine = null;
        }

        float knockbackPower = weapon != null ? weapon.knockbackPower * impactScale : 0f;
        float knockbackDuration = weapon != null ? weapon.knockbackDuration * impactScale : 0.1f;
        float stunDuration = weapon != null ? weapon.stunDuration * impactScale : 0f;

        if (ctx.HasSuperArmor)
        {
            float softDuration = SOFT_KNOCK_DURATION * Mathf.Max(impactScale, 0f);
            impactRoutine = StartCoroutine(SoftKnockRoutine(ctx, hitDir, knockbackPower, softDuration));
            return;
        }

        FaceHit(ctx, hitDir);
        impactRoutine = StartCoroutine(KnockbackThenStunRoutine(ctx, hitDir, knockbackPower, knockbackDuration, stunDuration));
    }

    public void ApplyPush(Enemy ctx, Vector3 hitDir, WeaponDataSO weapon, float impactScale = 1f)
    {
        if (ctx == null || ctx.CurrentState == Enemy.EnemyState.Dead) return;

        if (pushRoutine != null)
        {
            StopCoroutine(pushRoutine);
            pushRoutine = null;
        }

        float pushPower = weapon != null ? weapon.knockbackPower * impactScale : 0f;
        float pushDuration = weapon != null ? weapon.knockbackDuration * impactScale : 0.1f;
        float hitstop = weapon != null ? weapon.hitstopTime : 0f;

        pushRoutine = StartCoroutine(PushRoutine(ctx, hitDir, pushPower, pushDuration, hitstop));
    }

    private void FaceHit(Enemy ctx, Vector3 hitDir)
    {
        if (ctx == null || ctx.CurrentState == Enemy.EnemyState.Dead || ctx.HasSuperArmor) return;

        Vector3 look = -hitDir;
        look.y = 0f;
        if (look.sqrMagnitude < 0.0001f) return;
        look.Normalize();

        Vector3 currentFwd = ctx.transform.forward;
        currentFwd.y = 0f;
        if (currentFwd.sqrMagnitude < 0.0001f) currentFwd = Vector3.forward;

        float angle = Vector3.Angle(currentFwd, look);
        if (angle < FACE_ANGLE_THRESHOLD) return;

        ctx.transform.rotation = Quaternion.LookRotation(look, Vector3.up);
    }

    private IEnumerator SoftKnockRoutine(Enemy ctx, Vector3 hitDir, float power, float duration)
    {
        Vector3 dir = hitDir.normalized;
        dir.y = 0f;
        float elapsed = 0f;

        while (elapsed < duration && ctx != null && ctx.CurrentState != Enemy.EnemyState.Dead)
        {
            float t = elapsed / Mathf.Max(duration, 0.0001f);
            float current = Mathf.Lerp(power * SOFT_KNOCK_POWER_RATIO, 0f, t);
            ctx.transform.position += dir * current * Time.fixedDeltaTime;

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
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
            float t = Mathf.Clamp01(timer / Mathf.Max(knockDuration, 0.0001f));
            float currentSpeed = power * (1f - t);
            ctx.transform.position += knockDir * currentSpeed * Time.fixedDeltaTime;

            timer += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
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
        }

        impactRoutine = null;
    }

    private IEnumerator PushRoutine(Enemy ctx, Vector3 hitDir, float power, float duration, float hitstop)
    {
        if (ctx == null) yield break;

        float timer = 0f;
        Vector3 dir = hitDir.normalized;
        dir.y = 0f;

        // Hitstop
        float prevAnimSpeed = 1f;
        bool hitstopActive = hitstop > 0f;
        float hitstopEndTime = hitstopActive ? Time.time + hitstop : -1f;
        if (hitstopActive && ctx.animCtrl?.Animator != null)
        {
            prevAnimSpeed = ctx.animCtrl.Animator.speed;
            ctx.animCtrl.Animator.speed = 0f;
        }

        while (timer < duration && ctx != null && ctx.CurrentState != Enemy.EnemyState.Dead)
        {
            float t = Mathf.Clamp01(timer / Mathf.Max(duration, 0.0001f));
            float currentSpeed = power * (1f - t);
            ctx.transform.position += dir * currentSpeed * Time.fixedDeltaTime;

            if (hitstopActive && Time.time >= hitstopEndTime)
            {
                if (ctx.animCtrl?.Animator != null)
                    ctx.animCtrl.Animator.speed = prevAnimSpeed;
                hitstopActive = false;
            }

            timer += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        if (hitstopActive && ctx != null && ctx.animCtrl?.Animator != null)
            ctx.animCtrl.Animator.speed = prevAnimSpeed;

        pushRoutine = null;
    }
}