using System.Collections;
using UnityEngine;

/// <summary>
/// 개별 공격 타입(근접, 러쉬, 투사체 등)을 추상화.
/// Controller는 이 인터페이스만 알고 동작.
/// </summary>
public interface IEnemyAttackBehavior
{
    int Id { get; }
    string AttackName { get; }
    float Range { get; }
    float BaseCooldown { get; }
    float AttackTime { get; }          // 총 지속(전역 GCD 시작 이전 종료 기준)
    bool GrantsSuperArmor { get; }

    bool IsExecuting { get; }
    bool IsOnCooldown(float now);      // per-attack (최소1초 반영은 구현 내부)

    bool CanExecute(Enemy enemy, Transform target, float distance);
    float GetPriorityScore(Enemy enemy, Transform target, float distance); // 단순 랜덤이면 1

    IEnumerator Execute(Enemy enemy, Transform target, IEnemyAttackCallbacks callbacks);
    void OnAnimationEvent(string evtName);
    void Interrupt(bool hard);         // hard: ShieldBreak/넉백 등
    void StampCooldown(float now);     // 종료 시 쿨다운 스탬프
}