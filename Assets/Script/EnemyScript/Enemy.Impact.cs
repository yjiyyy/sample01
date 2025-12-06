using System.Collections;
using UnityEngine;

public class EnemyImpact : MonoBehaviour
{
    private Coroutine impactRoutine;
    private Coroutine pushRoutine;

    private const float SOFT_KNOCK_DURATION = 0.12f;
    private const float SOFT_KNOCK_POWER_RATIO = 0.5f;
    private const float FACE_ANGLE_THRESHOLD = 30f;
    private const float EPS = 0.0001f;

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

        // Super-armor check: delegated to EnemyHealth (shield-based)
        var health = ctx.GetComponent<EnemyHealth>();
        bool hasSuperArmor = health != null && health.HasSuperArmor;

        if (hasSuperArmor)
        {
            float softDuration = SOFT_KNOCK_DURATION * Mathf.Max(impactScale, 0f);
            impactRoutine = StartCoroutine(SoftKnockRoutine(ctx, hitDir, knockbackPower, softDuration));
            return;
        }

        // 데미지 단계에서 lethal이면 Dead로 이미 전환됐으므로 FaceHit가 호출되지 않음.
        // 여기서는 기존 흐름 유지(비치명 때만 회전)
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
        float animationHoldDuration = weapon != null ? weapon.animationHoldDuration : 0f;

        pushRoutine = StartCoroutine(PushRoutine(ctx, hitDir, pushPower, pushDuration, animationHoldDuration));
    }

    private void FaceHit(Enemy ctx, Vector3 hitDir)
    {
        if (ctx == null || ctx.CurrentState == Enemy.EnemyState.Dead) return;

        // 여기서 추가적으로 EnemyHealth의 치명 여부를 볼 필요는 없음.
        // HitBox에서 '데미지 먼저 → Dead면 회전/넉백을 호출하지 않음'으로 방지했기 때문.

        Vector3 look = -hitDir;
        look.y = 0f;
        if (look.sqrMagnitude < 0.0001f) return;
        look.Normalize();

        Vector3 currentFwd = ctx.transform.forward;
        currentFwd.y = 0f;
        if (currentFwd.sqrMagnitude < 0.0001f) currentFwd = Vector3.forward;

        float angle = Vector3.Angle(currentFwd, look);
        if (angle < FACE_ANGLE_THRESHOLD) return;

        // 비치명 데미지에서는 기존처럼 방향 전환 유지
        ctx.transform.rotation = Quaternion.LookRotation(look, Vector3.up);
    }

    private IEnumerator SoftKnockRoutine(Enemy ctx, Vector3 hitDir, float power, float duration)
    {
        Vector3 dir = hitDir.normalized;
        dir.y = 0f;
        float elapsed = 0f;

        float massMul = 1f;
        var facade = ctx.GetComponent<EnemyFacade>();
        if (facade != null && facade.config != null) massMul = Mathf.Max(0.0001f, facade.config.mass);

        float initialSpeed = Mathf.Abs(power) * SOFT_KNOCK_POWER_RATIO / massMul;
        float dur = Mathf.Max(duration, EPS);

        while (elapsed < dur && ctx != null && ctx.CurrentState != Enemy.EnemyState.Dead)
        {
            float t = Mathf.Clamp01(elapsed / dur);
            float currentSpeed = initialSpeed * (1f - t);
            Vector3 disp = dir * currentSpeed * Time.fixedDeltaTime;
            ctx.MoveFilteredDisplacement(disp);

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

        float massMul = 1f;
        var facade = ctx.GetComponent<EnemyFacade>();
        if (facade != null && facade.config != null) massMul = Mathf.Max(0.0001f, facade.config.mass);

        float initialSpeed = Mathf.Abs(power) / massMul;
        float dur = Mathf.Max(knockDuration, EPS);

        while (timer < dur && ctx.CurrentState == Enemy.EnemyState.Knockback)
        {
            float t = Mathf.Clamp01(timer / dur);
            float currentSpeed = initialSpeed * (1f - t); // 60fps/30fps 동일한 이동량
            Vector3 disp = knockDir * currentSpeed * Time.fixedDeltaTime;
            ctx.MoveFilteredDisplacement(disp);

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

    private IEnumerator PushRoutine(Enemy ctx, Vector3 hitDir, float power, float duration, float animationHoldDuration)
    {
        if (ctx == null) yield break;

        float timer = 0f;
        Vector3 dir = hitDir.normalized;
        dir.y = 0f;

        float massMul = 1f;
        var facade = ctx.GetComponent<EnemyFacade>();
        if (facade != null && facade.config != null) massMul = Mathf.Max(0.0001f, facade.config.mass);

        float dur = Mathf.Max(duration, EPS);
        float initialSpeed = Mathf.Abs(power) / massMul;

        if (animationHoldDuration > 0f)
            ctx.animCtrl?.StartAnimationHold(animationHoldDuration);

        while (timer < dur && ctx != null && ctx.CurrentState != Enemy.EnemyState.Dead)
        {
            float t = Mathf.Clamp01(timer / dur);
            float currentSpeed = initialSpeed * (1f - t);
            Vector3 disp = dir * currentSpeed * Time.fixedDeltaTime;
            ctx.MoveFilteredDisplacement(disp);

            timer += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        pushRoutine = null;
    }
}