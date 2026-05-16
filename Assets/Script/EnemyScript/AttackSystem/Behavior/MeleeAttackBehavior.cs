using UnityEngine;
using System.Collections;

public class MeleeAttackBehavior : EnemyAttackBehaviorBase
{
    private readonly MeleeAttackData data;
    private readonly float animLengthFallback;
    private IEnemyAttackCallbacks runtimeCallbacks;

    public MeleeAttackBehavior(int id, MeleeAttackData d, float estimatedClipLength = 0.6f) : base(id)
    {
        data = d;
        animLengthFallback = estimatedClipLength;
    }

    public override string AttackName => data.attackName;
    public override float Range => data.range;
    public override float BaseCooldown => data.cooldown;
    public override bool GrantsSuperArmor => data.grantSuperArmor;
    public override float AttackTime => (data.attackTime <= 0f) ? animLengthFallback : data.attackTime;

    public override bool CanExecute(Enemy enemy, Transform target, float distance) => distance <= Range;

    public override IEnumerator Execute(Enemy enemy, Transform target, IEnemyAttackCallbacks callbacks)
    {
        executing = true;
        runtimeCallbacks = callbacks;

        callbacks.OnBehaviorStarted(this);
        callbacks.PlayAnimation("Attack", useTrigger: false);

        float end = Time.time + AttackTime;
        while (Time.time < end && executing)
            yield return null;

        executing = false;
        runtimeCallbacks = null;
        callbacks.RequestFinish(this);
    }

    public override void OnAnimationEvent(string evtName)
    {
        if (!executing || runtimeCallbacks == null) return;
        if (evtName == "AttackHit" && data.hitBoxPrefab != null)
        {
            runtimeCallbacks.SpawnHitbox(
                data.hitBoxPrefab,
                data.hitBoxLifetime > 0 ? data.hitBoxLifetime : 0.1f,
                hb =>
                {
                    hb.Initialize(
                        data.damage,
                        data.range,
                        data.knockbackPower,
                        data.knockbackDuration,
                        data.hitBoxLifetime,
                        data.stunDuration,
                        data.allowDuplicateHit,
                        data.duplicateHitInterval,
                        WeaponDataSO.CreatePlayerDeathProxy(data.deathMode, data.ragdollImpulse, data.ragdollUpImpulse, data.ragdollSpinTorque, data.sliceTargets, data.sliceImpulse, data.isPoisonAttack, data.poisonOnHitStatus),
                        data.targetHoldDuration,
                        data.usePushInsteadOfKnockback,
                        data.attackerHoldDuration
                    );
                }
            );
        }
    }

    public override void Interrupt(bool hard)
    {
        base.Interrupt(hard);
        // Melee?? ?????? ?? ??? �?? ???? (?????? ???? ???)
    }
}