using UnityEngine;
using System.Collections;

public class RushAttackBehavior : EnemyAttackBehaviorBase
{
    private readonly RushAttackData data;
    private Transform cachedTarget;
    private IEnemyAttackCallbacks runtimeCallbacks;
    private Enemy enemy;
    private bool superArmorGranted = false;
    private bool executingRush = false;

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
        return distance <= Range;
    }

    public override IEnumerator Execute(Enemy enemy, Transform target, IEnemyAttackCallbacks callbacks)
    {
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

        // Prepare
        callbacks.SetAnimatorBool("IsRushPrepare", true);
        callbacks.SetAnimatorBool("IsRush", false);
        callbacks.PlayAnimation("RushPrepare", useTrigger: false);

        float prepEnd = Time.time + data.prepareTime;
        while (Time.time < prepEnd && executing)
        {
            if (cachedTarget != null)
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

        // Rush º»µ¿ÀÛ
        executingRush = true;
        callbacks.SetAnimatorBool("IsRushPrepare", false);
        callbacks.SetAnimatorBool("IsRush", true);
        callbacks.PlayAnimation("Rush", useTrigger: false);

        if (data.hitboxPrefab != null)
        {
            float life = (data.hitBoxLifetime > 0 ? data.hitBoxLifetime : data.rushTime);
            callbacks.SpawnHitbox(
                data.hitboxPrefab,
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
        Vector3 rushDir = enemy.transform.forward;
        rushDir.y = 0f;

        while (rushElapsed < data.rushTime && executing)
        {
            if (data.allowDirectionDeviation && cachedTarget != null)
            {
                Vector3 toTarget = cachedTarget.position - enemy.transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    Vector3 desired = toTarget.normalized;
                    rushDir = Vector3.Lerp(rushDir, desired, data.directionDeviationAmount * Time.deltaTime);
                    enemy.transform.rotation = Quaternion.LookRotation(rushDir);
                }
            }

            enemy.transform.position += rushDir.normalized * data.rushSpeed * Time.deltaTime;
            rushElapsed += Time.deltaTime;
            yield return null;
        }

        executing = false;
        executingRush = false;

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

        callbacks.RequestFinish(this); // ¼º°ø ¡æ Äð´Ù¿î / ±Û·Î¹úÄðÅ¸ÀÓ Àû¿ë
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

        callbacks.RequestCancel(this); // Ãë¼Ò ¡æ ³ëÄð
        runtimeCallbacks = null;
        enemy = null;
    }
}