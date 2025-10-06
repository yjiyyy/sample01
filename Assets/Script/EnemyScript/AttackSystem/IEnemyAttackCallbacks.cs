using UnityEngine;

public interface IEnemyAttackCallbacks
{
    // 패턴(Behavior) 공통 라이프사이클
    void OnBehaviorStarted(IEnemyAttackBehavior behavior);
    void RequestFinish(IEnemyAttackBehavior behavior);    // 성공(쿨다운/글로벌쿨타임 적용)
    void RequestCancel(IEnemyAttackBehavior behavior);    // 취소(노쿨) - 새로 추가

    // 애니메이션 / 상태
    void PlayAnimation(string animTriggerOrState, bool useTrigger = true);
    void SetAnimatorBool(string name, bool value);

    // 힛박스 스폰
    void SpawnHitbox(GameObject prefab, float lifetime, System.Action<HitBox_Enemy> init);
}