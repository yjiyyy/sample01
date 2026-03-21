using UnityEngine;

/// <summary>
/// 몬스터가 플레이어를 맞췄을 때 CC + 히트스톱 적용 순서 (플레이어 WeaponDataSO와 동일한 의미).
/// 1) 공격자(attacker) 홀드
/// 2) 피격자(target) 히트스톱 홀드
/// 3) Push 또는 ForceApplyKnockback
/// </summary>
public static class EnemyPlayerHitEffectApplier
{
    public static void ApplyCrowdControlAndTargetHitstop(
        PlayerWeaponController playerWeaponController,
        PlayerMovement playerMovement,
        Vector3 hitDirection,
        float knockbackPower,
        float knockbackDuration,
        float stunDuration,
        bool usePushInsteadOfKnockback,
        float targetHoldDuration,
        Transform knockbackRelativeTransform,
        Enemy attacker,
        float attackerHoldDuration)
    {
        ApplyAttackerHold(attacker, attackerHoldDuration);

        if (playerWeaponController != null && targetHoldDuration > 0f)
        {
            playerWeaponController.StartStateHold(targetHoldDuration);
            playerWeaponController.StartAnimationHold(targetHoldDuration);
        }

        if (usePushInsteadOfKnockback)
        {
            if (playerMovement != null)
                playerMovement.ApplyKnockback(hitDirection, knockbackPower, knockbackDuration, knockbackRelativeTransform, faceHitDirection: false);
        }
        else if (playerWeaponController != null)
        {
            // 타격감 우선: target hold를 먼저 건 뒤 넉백을 적용한다.
            playerWeaponController.ForceApplyKnockback(hitDirection, knockbackPower, knockbackDuration, stunDuration, clearExistingHolds: false);
        }
        else if (playerMovement != null)
        {
            playerMovement.ApplyKnockback(hitDirection, knockbackPower, knockbackDuration, knockbackRelativeTransform);
        }
    }

    private static void ApplyAttackerHold(Enemy attacker, float attackerHoldDuration)
    {
        if (attackerHoldDuration <= 0f || attacker == null) return;
        if (attacker.CurrentState == Enemy.EnemyState.Dead) return;

        attacker.StartStateHold(attackerHoldDuration);
        attacker.animCtrl?.StartAnimationHold(attackerHoldDuration);
    }
}
