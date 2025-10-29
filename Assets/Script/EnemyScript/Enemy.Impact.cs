using UnityEngine;
using System.Collections;

/// <summary>
/// Enemy의 피격(임팩트) 처리 전담 컴포넌트
/// - 넉백/스턴/SoftKnock 등 기존 로직 유지
/// - Push(밀림) 관련 루틴 추가 (상태 변화 없음, AI/공격 중단 없음)
/// </summary>
[DisallowMultipleComponent]
public class EnemyImpact : MonoBehaviour
{
    private Coroutine impactRoutine;
    private Coroutine pushRoutine; // Push 처리용

    // Soft Knock(슈퍼아머 중) 기본 시간
    private const float SOFT_KNOCK_DURATION = 0.12f;
    private const float SOFT_KNOCK_POWER_RATIO = 0.5f;

    // 회전 정책 상수
    //  - 슈퍼아머: 회전 제외
    //  - 사망: 회전 제외
    //  - Hard Knockback(=SuperArmor 아님) 시 Rush/Attack 등 어떤 상태였든 각도 차이 30° 이상이면 즉시 스냅 회전
    private const float FACE_ANGLE_THRESHOLD = 30f;

    /// <summary>
    /// ApplyKnockback: 기존 넉백/스턴 로직 (상태 변화 포함)
    /// </summary>
    public void ApplyKnockback(Enemy ctx, Vector3 hitDir, WeaponDataSO weapon, float impactScale = 1f)
    {
        if (ctx == null || ctx.CurrentState == Enemy.EnemyState.Dead) return;

        // 기존 코루틴 중단
        if (impactRoutine != null)
        {
            StopCoroutine(impactRoutine);
            impactRoutine = null;
        }

        // ✅ 데미지는 여기서 적용하지 않음(중복 방지). 힛박스/프로젝타일에서 1회만 적용.

        // ✅ 거리 감쇠(weight) 적용: 파워/지속/스턴 모두
        float knockbackPower = weapon != null ? weapon.knockbackPower * impactScale : 0f;
        float knockbackDuration = weapon != null ? weapon.knockbackDuration * impactScale : 0.1f;
        float stunDuration = weapon != null ? weapon.stunDuration * impactScale : 0f;

        // SuperArmor(SoftKnock) 처리: 회전 제외, 시간/파워도 감쇠 반영
        if (ctx.HasSuperArmor)
        {
            float softDuration = SOFT_KNOCK_DURATION * Mathf.Max(impactScale, 0f);
            impactRoutine = StartCoroutine(SoftKnockRoutine(ctx, hitDir, knockbackPower, softDuration));
            return;
        }

        // Hard Knockback: 회전 조건 충족 시 1회 스냅 회전
        FaceHit(ctx, hitDir);

        // 정상 넉백+스턴 루틴
        impactRoutine = StartCoroutine(KnockbackThenStunRoutine(ctx, hitDir, knockbackPower, knockbackDuration, stunDuration));
    }

    /// <summary>
    /// ApplyPush: 상태 변화 없이 단순히 뒤로 밀림만 적용 (요청: AI/공격 중단 없음)
    /// - 동일 적에게 push가 연속으로 들어오면 이전 push를 덮어씁니다.
    /// - 넉백/스턴 루틴과는 별개로 동작(동시 적용 가능).
    /// </summary>
    public void ApplyPush(Enemy ctx, Vector3 hitDir, WeaponDataSO weapon, float impactScale = 1f)
    {
        if (ctx == null || ctx.CurrentState == Enemy.EnemyState.Dead) return;

        // 이미 pushRoutine이 있다면 덮어쓰기
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

    /// <summary>
    /// FaceHit: Hard Knock 시(슈퍼아머 아님) 공격자 방향을 바라보도록 회전.
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

    /// <summary>
    /// SoftKnockRoutine: SuperArmor 상태일 때 간단한 감쇠 넉백
    /// (기존 구현을 유지)
    /// </summary>
    private IEnumerator SoftKnockRoutine(Enemy ctx, Vector3 hitDir, float power, float duration)
    {
        Vector3 dir = hitDir.normalized;
        dir.y = 0f;
        float elapsed = 0f;

        while (elapsed < duration && ctx != null && ctx.CurrentState != Enemy.EnemyState.Dead)
        {
            if (ctx.agent != null && ctx.agent.isOnNavMesh)
            {
                float t = elapsed / Mathf.Max(duration, 0.0001f);
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

    /// <summary>
    /// KnockbackThenStunRoutine: 기존 넉백+스턴 루틴
    /// (기존 구현을 유지)
    /// </summary>
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

    /// <summary>
    /// PushRoutine: 간단한 Non-interrupt 밀림 구현
    /// - agent.isStopped는 변경하지 않음 (AI/공격 유지)
    /// - agent.Move(delta) 사용하여 외부 이동을 적용 (NavMeshAgent의 보정과 병행됨)
    /// - 애니메이터 히트스탑은 대상(적)만 Animator.speed로 처리
    /// </summary>
    private IEnumerator PushRoutine(Enemy ctx, Vector3 hitDir, float power, float duration, float hitstop)
    {
        if (ctx == null) yield break;

        float timer = 0f;
        Vector3 dir = hitDir.normalized;
        dir.y = 0f;

        // Hitstop 처리: 애니메이터 속도 저장/0으로 설정 후 복원
        float prevAnimSpeed = 1f;
        bool hitstopActive = hitstop > 0f;
        float hitstopEndTime = hitstopActive ? Time.time + hitstop : -1f;
        if (hitstopActive)
        {
            if (ctx.animCtrl?.Animator != null)
            {
                prevAnimSpeed = ctx.animCtrl.Animator.speed;
                ctx.animCtrl.Animator.speed = 0f;
            }
        }

        while (timer < duration && ctx != null && ctx.CurrentState != Enemy.EnemyState.Dead)
        {
            float t = Mathf.Clamp01(timer / Mathf.Max(duration, 0.0001f));
            float currentSpeed = power * (1f - t); // simple linear falloff

            if (ctx.agent != null && ctx.agent.isOnNavMesh)
            {
                // agent.Move는 NavMeshAgent의 내부 보정과 함께 외부 이동을 적용하므로
                // AI 동작을 중단하지 않으면서 위치를 밀어낼 수 있음.
                Vector3 delta = dir * currentSpeed * Time.deltaTime;
                ctx.agent.Move(delta);
            }
            else
            {
                ctx.transform.position += dir * currentSpeed * Time.deltaTime;
            }

            if (hitstopActive && Time.time >= hitstopEndTime)
            {
                if (ctx.animCtrl?.Animator != null)
                    ctx.animCtrl.Animator.speed = prevAnimSpeed;
                hitstopActive = false;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        // 보장 복구
        if (hitstopActive && ctx != null && ctx.animCtrl?.Animator != null)
        {
            ctx.animCtrl.Animator.speed = prevAnimSpeed;
        }

        pushRoutine = null;
    }
}