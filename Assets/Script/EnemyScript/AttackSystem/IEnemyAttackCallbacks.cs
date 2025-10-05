using UnityEngine;

public interface IEnemyAttackCallbacks
{
    void OnBehaviorStarted(IEnemyAttackBehavior behavior);
    void RequestFinish(IEnemyAttackBehavior behavior);
    void PlayAnimation(string animTriggerOrState, bool useTrigger = true);
    void SetAnimatorBool(string name, bool value);
    void SpawnHitbox(GameObject prefab, float lifetime, System.Action<HitBox_Enemy> init);
}