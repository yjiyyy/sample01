using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyImpact : MonoBehaviour
{
    private Coroutine knockbackRoutine;
    private Coroutine stunRoutine;

    public void OnDamage(Enemy ctx, Vector3 hitDir, WeaponDataSO weapon, float scale)
    {
        // 기존 루틴 중단 및 초기화
        if (knockbackRoutine != null)
        {
            ctx.StopCoroutine(knockbackRoutine);
            knockbackRoutine = null;
        }
        if (stunRoutine != null)
        {
            ctx.StopCoroutine(stunRoutine);
            stunRoutine = null;
        }

        // 새로운 넉백 + 스턴 루틴 시작
        knockbackRoutine = ctx.StartCoroutine(KnockbackThenStunRoutine(ctx, hitDir, weapon, scale));

        // 저크 효과 (옵션)
        if (weapon != null && weapon.jerkIntensity > 0f)
        {
            if (ctx.TryGetComponent(out MultiBoneJerkController jerk))
                jerk.TriggerJerk(weapon.jerkIntensity, weapon.jerkDuration);
        }
    }

    private IEnumerator KnockbackThenStunRoutine(Enemy ctx, Vector3 direction, WeaponDataSO weapon, float scale)
    {
        // 넉백 루틴 실행
        yield return ctx.StartCoroutine(KnockbackRoutine(ctx, direction, weapon, scale));

        // 스턴 루틴 실행
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

        // 애니메이션 재생
        if (ctx.animator != null)
        {
            int randomKnockbackIndex = Random.Range(1, 4); // Knockback01, Knockback02, Knockback03 중 랜덤
            string animationName = $"Knockback0{randomKnockbackIndex}";
            ctx.animator.Play(animationName, 0, 0f);
            Debug.Log($"[KnockbackRoutine] 애니메이션 재생: {animationName}");
        }

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

        // 애니메이션 재생
        if (ctx.animator != null)
        {
            ctx.animator.Play("Stun", 0, 0f);
            Debug.Log("[StunRoutine] 스턴 애니메이션 재생");
        }

        yield return new WaitForSeconds(duration);

        if (ctx.CurrentState != Enemy.EnemyState.Dead)
            ctx.SetState(Enemy.EnemyState.Chase);

        stunRoutine = null;
    }
}