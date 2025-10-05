using UnityEngine;
using System.Collections;

/// <summary>
/// 공통 유틸: 쿨다운 스탬프, 최소쿨 계산, AttackTime 보정(필요 시)
/// </summary>
public abstract class EnemyAttackBehaviorBase : IEnemyAttackBehavior
{
    public int Id { get; private set; }

    protected float lastUsedTime = -Mathf.Infinity;
    protected bool executing = false;

    public bool IsExecuting => executing;

    // 서브클래스에서 필수 구현/Overridable 속성
    public abstract string AttackName { get; }
    public abstract float Range { get; }
    public abstract float BaseCooldown { get; }
    public abstract float AttackTime { get; }
    public abstract bool GrantsSuperArmor { get; }

    protected EnemyAttackBehaviorBase(int id)
    {
        Id = id;
    }

    public virtual bool IsOnCooldown(float now)
    {
        float cd = BaseCooldown;
        if (cd > 0 && cd < 1f) cd = 1f;
        return now < lastUsedTime + cd;
    }

    public virtual void StampCooldown(float now)
    {
        lastUsedTime = now;
    }

    public virtual float GetPriorityScore(Enemy enemy, Transform target, float distance) => 1f;

    public abstract bool CanExecute(Enemy enemy, Transform target, float distance);

    public abstract IEnumerator Execute(Enemy enemy, Transform target, IEnemyAttackCallbacks callbacks);

    public virtual void OnAnimationEvent(string evtName) { }

    public virtual void Interrupt(bool hard)
    {
        // 기본: 즉시 종료 신호만 (Controller가 RequestFinish 호출)
        executing = false;
    }
}