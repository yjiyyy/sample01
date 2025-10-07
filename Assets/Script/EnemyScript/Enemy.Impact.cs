using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class EnemyImpact : MonoBehaviour
{
    private Coroutine impactRoutine;

    // Soft Knock(슈퍼아머 중) 고정 짧은 시간
    private const float SOFT_KNOCK_DURATION = 0.12f;
    private const float SOFT_KNOCK_POWER_RATIO = 0.5f;

    // 회전 정책 상수
    //  - 슈퍼아머: 회전 제외
    //  - 사망: 회전 제외
    //  - Hard Knockback(=SuperArmor 아님) 시 Rush/Attack 등 어떤 상태였든 각도 차이 30° 이상이면 즉시 스냅 회전
    private const float FACE_ANGLE_THRESHOLD = 30f;

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

            // 데미지로 인해 사망 또는 실드브레이크 진입했다면 추가 처리 불필요
            if (ctx.CurrentState == Enemy.EnemyState.Dead ||
                ctx.CurrentState == Enemy.EnemyState.ShieldBreak)
            {
                return;
            }
        }

        float knockbackPower = weapon != null ? weapon.knockbackPower * impactScale : 0f;
        float knockbackDuration = weapon != null ? weapon.knockbackDuration : 0.1f;
        float stunDuration = weapon != null ? weapon.stunDuration : 0f;

        // SuperArmor(SoftKnock) 처리: 회전 제외
        if (ctx.HasSuperArmor)
        {
            impactRoutine = StartCoroutine(SoftKnockRoutine(ctx, hitDir, knockbackPower));
            return;
        }

        // Hard Knockback: 회전 조건 충족 시 1회 스냅 회전
        FaceHit(ctx, hitDir);

        // 정상 넉백+스턴 루틴
        impactRoutine = StartCoroutine(KnockbackThenStunRoutine(ctx, hitDir, knockbackPower, knockbackDuration, stunDuration));
    }

    /// <summary>
    /// Hard Knockback 시(슈퍼아머 아님) 공격자 방향을 바라보도록 회전.
    /// hitDir 은 "공격자 → 적" 방향이므로 적이 공격자를 바라보려면 -hitDir 사용.
    /// </summary>
    private void FaceHit(Enemy ctx, Vector3 hitDir)
    {
        if (ctx == null) return;
        if (ctx.CurrentState == Enemy.EnemyState.Dead) return;
        if (ctx.HasSuperArmor) return; // 정책: 슈퍼아머 시 회전 제외

        // 수평 평면에서만 방향 계산
        Vector3 look = -hitDir;
        look.y = 0f;

        if (look.sqrMagnitude < 0.0001f)
            return;

        look.Normalize();

        Vector3 currentFwd = ctx.transform.forward;
        currentFwd.y = 0f;
        if (currentFwd.sqrMagnitude < 0.0001f)
            currentFwd = Vector3.forward;

        float angle = Vector3.Angle(currentFwd, look);
        if (angle < FACE_ANGLE_THRESHOLD)
            return; // 30도 미만이면 회전 생략

        ctx.transform.rotation = Quaternion.LookRotation(look, Vector3.up);
    }

    private IEnumerator SoftKnockRoutine(Enemy ctx, Vector3 hitDir, float power)
    {
        Vector3 dir = hitDir.normalized;
        dir.y = 0f;
        float elapsed = 0f;

        while (elapsed < SOFT_KNOCK_DURATION && ctx != null && ctx.CurrentState != Enemy.EnemyState.Dead)
        {
            if (ctx.agent != null && ctx.agent.isOnNavMesh)
            {
                float t = elapsed / SOFT_KNOCK_DURATION;
                float current = Mathf.Lerp(power * SOFT_KNOCK_POWER_RATIO, 0f, t);
                ctx.agent.isStopped = true;
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
            ctx.animCtrl?.Animator.SetTrigger("ResetToM"); // (구조 개편 전 잔존 트리거)
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