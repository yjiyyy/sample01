using UnityEngine;
using System.Collections;

/// <summary>
/// NavMeshAgent 제거 / 고정 타임스텝 버전 러시 비헤이비어
/// - 준비/러시 모두 Time.fixedDeltaTime + WaitForFixedUpdate
/// - 방향 편차: 기존과 비슷하게 directionDeviationAmount * Time.fixedDeltaTime
/// - Push/Knockback과 누적 허용
/// </summary>
public class RushAttackBehavior : EnemyAttackBehaviorBase
{
    private readonly RushAttackData data;
    private Transform cachedTarget;
    private IEnemyAttackCallbacks runtimeCallbacks;
    private Enemy enemy;

    private bool superArmorGranted = false;

    public RushAttackBehavior(int id, RushAttackData d) : base(id)
    {
        data = d;
    }

    public override string AttackName => data.attackName;
    public override float Range => data.range;
    public override float BaseCooldown => data.cooldown;
    public override bool GrantsSuperArmor => data.grantSuperArmor;
    public override float AttackTime => data.prepareTime + data.rushTime;

    public override bool CanExecute(Enemy enemy, Transform target, float distance)
    {
        if (data == null) return false;
        return distance <= Range;
    }

    public override IEnumerator Execute(Enemy enemy, Transform target, IEnemyAttackCallbacks callbacks)
    {
        if (data == null) yield break;

        executing = true;
        this.enemy = enemy;
        cachedTarget = target;
        runtimeCallbacks = callbacks;

        callbacks.OnBehaviorStarted(this);

        if (GrantsSuperArmor && enemy != null)
        {
            enemy.AddSuperArmor(SuperArmorSource.Attack);
            superArmorGranted = true;
        }

        // 준비
        callbacks.SetAnimatorBool("IsRushPrepare", true);
        callbacks.SetAnimatorBool("IsRush", false);
        callbacks.PlayAnimation("RushPrepare", useTrigger: false);

        float prepElapsed = 0f;
        while (prepElapsed < data.prepareTime && executing)
        {
            if (cachedTarget != null && enemy != null)
            {
                Vector3 dir = cachedTarget.position - enemy.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                    enemy.transform.rotation = Quaternion.LookRotation(dir.normalized);
            }

            prepElapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        if (!executing)
        {
            CancelNoCooldown(callbacks);
            yield break;
        }

        // 러시 시작
        callbacks.SetAnimatorBool("IsRushPrepare", false);
        callbacks.SetAnimatorBool("IsRush", true);
        callbacks.PlayAnimation("Rush", useTrigger: false);

        if (data.hitBoxPrefab != null)
        {
            float life = (data.hitBoxLifetime > 0 ? data.hitBoxLifetime : data.rushTime);
            callbacks.SpawnHitbox(
                data.hitBoxPrefab,
                life,
                hb =>
                {
                    hb.Initialize(
                        data.damage,
                        data.range,
                        data.knockbackPower,
                        data.knockbackDuration,
                        life,
                        data.stunDuration,
                        data.allowDuplicateHit,
                        data.duplicateHitInterval
                    );
                });
        }

        float rushElapsed = 0f;
        Vector3 rushDir = (enemy != null ? enemy.transform.forward : Vector3.forward);
        rushDir.y = 0f;
        if (rushDir.sqrMagnitude < 0.0001f) rushDir = Vector3.forward;

        while (rushElapsed < data.rushTime && executing)
        {
            if (enemy == null) break;

            if (data.allowDirectionDeviation && cachedTarget != null && data.directionDeviationAmount > 0f)
            {
                Vector3 toTarget = cachedTarget.position - enemy.transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    Vector3 desired = toTarget.normalized;
                    float stepW = Mathf.Clamp01(data.directionDeviationAmount * Time.fixedDeltaTime);
                    rushDir = Vector3.Lerp(rushDir, desired, stepW).normalized;
                    enemy.transform.rotation = Quaternion.LookRotation(rushDir);
                }
            }

            enemy.transform.position += rushDir.normalized * data.rushSpeed * Time.fixedDeltaTime;

            rushElapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        executing = false;

        callbacks.SetAnimatorBool("IsRush", false);
        FinishSuccess(callbacks);
    }

    public override void Interrupt(bool hard)
    {
        if (!executing) return;

        base.Interrupt(hard);
        executing = false;

        if (runtimeCallbacks != null)
        {
            runtimeCallbacks.SetAnimatorBool("IsRushPrepare", false);
            runtimeCallbacks.SetAnimatorBool("IsRush", false);
            runtimeCallbacks.PlayAnimation("ResetToM", useTrigger: true);
            CancelNoCooldown(runtimeCallbacks);
        }
    }

    private void FinishSuccess(IEnemyAttackCallbacks callbacks)
    {
        if (superArmorGranted && enemy != null)
        {
            enemy.RemoveSuperArmor(SuperArmorSource.Attack);
            superArmorGranted = false;
        }

        callbacks.RequestFinish(this);
        runtimeCallbacks = null;
        enemy = null;
    }

    private void CancelNoCooldown(IEnemyAttackCallbacks callbacks)
    {
        if (superArmorGranted && enemy != null)
        {
            enemy.RemoveSuperArmor(SuperArmorSource.Attack);
            superArmorGranted = false;
        }

        callbacks.RequestCancel(this);
        runtimeCallbacks = null;
        enemy = null;
    }
}