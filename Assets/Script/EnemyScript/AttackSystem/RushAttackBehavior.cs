using UnityEngine;
using System.Collections;

public class RushAttackBehavior : EnemyAttackBehaviorBase
{
    private readonly RushAttackData data;
    private Transform cachedTarget;
    private IEnemyAttackCallbacks runtimeCallbacks;

    public RushAttackBehavior(int id, RushAttackData d) : base(id)
    {
        data = d;
    }

    public override string AttackName => data.attackName;
    public override float Range => data.range;
    public override float BaseCooldown => data.cooldown;
    public override bool GrantsSuperArmor => data.grantSuperArmor;
    public override float AttackTime => data.prepareTime + data.rushTime; // 합산

    public override bool CanExecute(Enemy enemy, Transform target, float distance)
    {
        return distance <= Range; // 필요 시 최소거리 조건 추가 가능
    }

    public override IEnumerator Execute(Enemy enemy, Transform target, IEnemyAttackCallbacks callbacks)
    {
        executing = true;
        runtimeCallbacks = callbacks;
        cachedTarget = target;

        callbacks.OnBehaviorStarted(this);
        // 준비 애니
        callbacks.SetAnimatorBool("IsRushPrepare", true);
        callbacks.SetAnimatorBool("IsRush", false);
        callbacks.PlayAnimation("RushPrepare", useTrigger: false);

        float prepEnd = Time.time + data.prepareTime;
        while (Time.time < prepEnd && executing)
        {
            if (cachedTarget != null)
            {
                Vector3 dir = cachedTarget.position - enemy.transform.position;
                dir.y = 0;
                if (dir.sqrMagnitude > 0.0001f)
                    enemy.transform.rotation = Quaternion.LookRotation(dir.normalized);
            }
            yield return null;
        }

        if (!executing)
        {
            runtimeCallbacks = null;
            yield break;
        }

        // Rush 시작
        callbacks.SetAnimatorBool("IsRushPrepare", false);
        callbacks.SetAnimatorBool("IsRush", true);
        callbacks.PlayAnimation("Rush", useTrigger: false);

        // Rush 히트박스 (지속형)
        if (data.hitBoxPrefab != null)
        {
            runtimeCallbacks.SpawnHitbox(
                data.hitBoxPrefab,
                data.hitBoxLifetime > 0 ? data.hitBoxLifetime : data.rushTime,
                hb =>
                {
                    hb.Initialize(
                        data.damage,
                        0f,
                        data.knockbackPower,
                        data.knockbackDuration,
                        data.hitBoxLifetime > 0 ? data.hitBoxLifetime : data.rushTime,
                        data.stunDuration,
                        data.allowDuplicateHit,
                        data.duplicateHitInterval
                    );
                });
        }

        float rushElapsed = 0f;
        Vector3 rushDir = enemy.transform.forward;
        rushDir.y = 0;

        while (rushElapsed < data.rushTime && executing)
        {
            if (data.allowDirectionDeviation && cachedTarget != null)
            {
                Vector3 toPlayer = cachedTarget.position - enemy.transform.position;
                toPlayer.y = 0;
                if (toPlayer.sqrMagnitude > 0.0001f)
                {
                    Vector3 desiredDir = toPlayer.normalized;
                    rushDir = Vector3.Lerp(rushDir, desiredDir, data.directionDeviationAmount * Time.deltaTime);
                    enemy.transform.rotation = Quaternion.LookRotation(rushDir);
                }
            }

            enemy.transform.position += rushDir.normalized * data.rushSpeed * Time.deltaTime;
            rushElapsed += Time.deltaTime;
            yield return null;
        }

        callbacks.SetAnimatorBool("IsRush", false);
        executing = false;
        runtimeCallbacks = null;
        callbacks.RequestFinish(this);
    }

    public override void Interrupt(bool hard)
    {
        base.Interrupt(hard);
        // hard 인터럽트 시 추가 패널티 도입 가능 (현재 정책: 쿨다운 리셋 → Controller가 전역 GCD도 해제)
    }
}