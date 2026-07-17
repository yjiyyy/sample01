using System.Collections;
using UnityEngine;

public class EnemyImpact : MonoBehaviour
{
    private Coroutine impactRoutine;
    private Coroutine pushRoutine;

    // 스턴 게이지 동안(넉백→스턴 체인 포함) 들어오는 CC(넉백/스턴/CC푸시 등) 중복 적용을 막기 위한 잠금 시간
    private float stunCCLockUntilTime = 0f;

    private const float SOFT_KNOCK_DURATION = 0.12f;
    private const float SOFT_KNOCK_POWER_RATIO = 0.5f;
    private const float FACE_ANGLE_THRESHOLD = 30f;
    private const float EPS = 0.0001f;

    private static float ResolveTargetAnimationHold(WeaponDataSO weapon)
    {
        if (weapon == null) return 0f;
        if (weapon.targetHoldDuration > 0f) return weapon.targetHoldDuration;
        if (weapon.targetAnimationHoldDuration > 0f) return weapon.targetAnimationHoldDuration;
        return 0f;
    }

    private static float ResolveTargetStateHold(WeaponDataSO weapon)
    {
        if (weapon == null) return 0f;
        if (weapon.targetHoldDuration > 0f) return weapon.targetHoldDuration;
        return weapon.targetStateHoldDuration;
    }

    private static void ApplyTargetHoldsOnly(Enemy ctx, WeaponDataSO weapon, float impactScale)
    {
        if (ctx == null || weapon == null) return;

        float stateHold = ResolveTargetStateHold(weapon) * Mathf.Max(0f, impactScale);
        float animHold = ResolveTargetAnimationHold(weapon) * Mathf.Max(0f, impactScale);

        if (animHold > 0f) ctx.animCtrl?.StartAnimationHold(animHold);
        if (stateHold > 0f) ctx.StartStateHold(stateHold);
    }

    public void ApplyKnockback(Enemy ctx, Vector3 hitDir, WeaponDataSO weapon, float impactScale = 1f)
    {
        if (ctx == null || ctx.CurrentState == Enemy.EnemyState.Dead) return;

        // 스턴 게이지 잠금이 걸려있는 동안에는 HP만 들어가고 CC(넉백/스턴 등)는 중복 적용하지 않음.
        // 단, 타격감용 홀드(hitstop)는 허용.
        if (Time.time < stunCCLockUntilTime)
        {
            ApplyTargetHoldsOnly(ctx, weapon, impactScale);
            return;
        }

        // Stun 상태에서는 “HP만” 들어가고 넉백/스턴 CC 중복 적용 금지.
        // 단, 타격감용 홀드(hitstop)는 허용.
        if (ctx.CurrentState == Enemy.EnemyState.Stunned)
        {
            ApplyTargetHoldsOnly(ctx, weapon, impactScale);
            return;
        }

        float knockbackPower = weapon != null ? weapon.knockbackPower * impactScale : 0f;
        float knockbackDuration = weapon != null ? weapon.knockbackDuration * impactScale : 0.1f;
        float stunDuration = weapon != null ? weapon.stunDuration * impactScale : 0f;

        // CC 값이 모두 0이면 피격 반응(넉백 상태/모션) 없이 HP 처리만 허용한다.
        // 단, 타격감용 홀드(hitstop)는 기존대로 적용 가능.
        if (weapon != null &&
            knockbackPower <= EPS &&
            knockbackDuration <= EPS &&
            stunDuration <= EPS)
        {
            ApplyTargetHoldsOnly(ctx, weapon, impactScale);
            return;
        }

        if (impactRoutine != null)
        {
            StopCoroutine(impactRoutine);
            impactRoutine = null;
        }
        float targetStateHoldDuration = ResolveTargetStateHold(weapon);
        float targetAnimationHoldDuration = ResolveTargetAnimationHold(weapon);

        // Super-armor check: consider both shield (EnemyHealth) and manual super armor (Enemy)
        bool hasSuperArmor = ctx.HasAnySuperArmor();

        if (hasSuperArmor)
        {
            float softDuration = SOFT_KNOCK_DURATION * Mathf.Max(impactScale, 0f);
            impactRoutine = StartCoroutine(SoftKnockRoutine(ctx, hitDir, knockbackPower, softDuration));
            return;
        }

        // 새 스턴 CC를 시작하는 경우에만 잠금 시간 설정(넉백→스턴 체인 포함)
        if (stunDuration > 0f)
        {
            float kbDurForLock = Mathf.Max(knockbackDuration, EPS);
            float stunDurForLock = Mathf.Max(0f, stunDuration);
            stunCCLockUntilTime = Time.time + kbDurForLock + stunDurForLock;
        }

        // 데미지 단계에서 lethal이면 Dead로 이미 전환됐으므로 FaceHit가 호출되지 않음.
        // 여기서는 기존 흐름 유지(비치명 때만 회전)
        FaceHit(ctx, hitDir);
        var ai = ctx.GetComponent<EnemyAI>();
        if (ai != null) ai.SkipFindGoToCombat();
        impactRoutine = StartCoroutine(KnockbackThenStunRoutine(ctx, hitDir, knockbackPower, knockbackDuration, stunDuration, targetAnimationHoldDuration, targetStateHoldDuration));
    }

    public void ApplyPush(Enemy ctx, Vector3 hitDir, WeaponDataSO weapon, float impactScale = 1f)
    {
        if (ctx == null || ctx.CurrentState == Enemy.EnemyState.Dead) return;

        // 스턴 게이지 잠금이 걸려있는 동안에는 CC푸시도 적용하지 않음.
        // 단, 타격감용 홀드(hitstop)는 허용.
        if (Time.time < stunCCLockUntilTime)
        {
            ApplyTargetHoldsOnly(ctx, weapon, impactScale);
            return;
        }

        // Stun 상태에서는 밀림(Push)도 적용하지 않음.
        // 단, 타격감용 홀드(hitstop)는 허용.
        if (ctx.CurrentState == Enemy.EnemyState.Stunned)
        {
            ApplyTargetHoldsOnly(ctx, weapon, impactScale);
            return;
        }

        float pushPower = weapon != null ? weapon.knockbackPower * impactScale : 0f;
        float pushDuration = weapon != null ? weapon.knockbackDuration * impactScale : 0.1f;

        // Push 기반 CC 값이 모두 0이면 밀림 처리 없이 HP 처리만 허용한다.
        // 단, 타격감용 홀드(hitstop)는 기존대로 적용 가능.
        if (weapon != null &&
            pushPower <= EPS &&
            pushDuration <= EPS)
        {
            ApplyTargetHoldsOnly(ctx, weapon, impactScale);
            return;
        }

        if (pushRoutine != null)
        {
            StopCoroutine(pushRoutine);
            pushRoutine = null;
        }
        float targetStateHoldDuration = ResolveTargetStateHold(weapon);
        float targetAnimationHoldDuration = ResolveTargetAnimationHold(weapon);

        // Push는 밀림 + hold만 적용한다. Peace/발견 상태를 Combat으로 깨우지 않는다.
        pushRoutine = StartCoroutine(PushRoutine(ctx, hitDir, pushPower, pushDuration, targetAnimationHoldDuration, targetStateHoldDuration));
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

    private IEnumerator KnockbackThenStunRoutine(Enemy ctx, Vector3 hitDir, float power, float knockDuration, float stunDuration, float targetAnimationHoldDuration, float targetStateHoldDuration)
    {
        if (ctx == null) yield break;

        ctx.SetState(Enemy.EnemyState.Knockback, true);
        ctx.animCtrl?.PlayKnockback();

        // Apply holds after Knockback state transition so animator.speed reset does not cancel hold.
        if (targetAnimationHoldDuration > 0f)
            ctx.animCtrl?.StartAnimationHold(targetAnimationHoldDuration);
        if (targetStateHoldDuration > 0f)
            ctx.StartStateHold(targetStateHoldDuration);

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
            if (ctx.IsStateHoldActive)
            {
                yield return new WaitForFixedUpdate();
                continue;
            }

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
            // Stun 시작 시점에 이미 실행 중인 Push가 있다면 끊어서
            // 스턴 게이지 동안 밀림이 계속 들어가지 않게 함.
            if (pushRoutine != null)
            {
                StopCoroutine(pushRoutine);
                pushRoutine = null;
            }

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

    private IEnumerator PushRoutine(Enemy ctx, Vector3 hitDir, float power, float duration, float animationHoldDuration, float stateHoldDuration)
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
        if (stateHoldDuration > 0f)
            ctx.StartStateHold(stateHoldDuration);

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