using UnityEngine;
using System.Collections;

/// <summary>
/// 러시 공격 전용 비헤이비어 (런타임에/컨트롤러 내부 시스템에서 new 로 생성해 주입하는 방식으로 동작)
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
        if (data == null)
        {
            yield break;
        }

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

        var agent = enemy != null ? enemy.agent : null;
        if (agent && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.ResetPath();
        }

        // 준비 모션
        callbacks.SetAnimatorBool("IsRushPrepare", true);
        callbacks.SetAnimatorBool("IsRush", false);
        callbacks.PlayAnimation("RushPrepare", useTrigger: false);

        float prepEnd = Time.time + data.prepareTime;
        while (Time.time < prepEnd && executing)
        {
            if (cachedTarget != null && enemy != null)
            {
                Vector3 dir = cachedTarget.position - enemy.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                    enemy.transform.rotation = Quaternion.LookRotation(dir.normalized);
            }
            yield return null;
        }

        if (!executing)
        {
            CancelNoCooldown(callbacks);
            yield break;
        }

        // 러시 본 동작
        callbacks.SetAnimatorBool("IsRushPrepare", false);
        callbacks.SetAnimatorBool("IsRush", true);
        callbacks.PlayAnimation("Rush", useTrigger: false);

        // 히트박스 스폰 (필드: hitBoxPrefab)
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

            // 진행 중 방향 미세 조정 (allowDirectionDeviation 활성 시)
            if (data.allowDirectionDeviation && cachedTarget != null)
            {
                Vector3 toTarget = cachedTarget.position - enemy.transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    Vector3 desired = toTarget.normalized;
                    // directionDeviationAmount 를 '보간 속도'로 사용
                    rushDir = Vector3.Lerp(rushDir, desired, Mathf.Clamp01(data.directionDeviationAmount * Time.deltaTime));
                    enemy.transform.rotation = Quaternion.LookRotation(rushDir);
                }
            }

            enemy.transform.position += rushDir.normalized * data.rushSpeed * Time.deltaTime;
            rushElapsed += Time.deltaTime;
            yield return null;
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

        if (enemy != null && enemy.agent && enemy.agent.isOnNavMesh)
            enemy.agent.isStopped = false;

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

        if (enemy != null && enemy.agent && enemy.agent.isOnNavMesh)
            enemy.agent.isStopped = false;

        callbacks.RequestCancel(this);
        runtimeCallbacks = null;
        enemy = null;
    }

}