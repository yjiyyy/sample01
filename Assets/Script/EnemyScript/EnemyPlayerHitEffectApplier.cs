using UnityEngine;

/// <summary>
/// 몬스터가 플레이어를 맞췄을 때 CC + 히트스톱 적용 순서 (플레이어 WeaponDataSO와 동일한 의미).
/// 1) 공격자(attacker) 홀드
/// 2) 피격자(target) 히트스톱 홀드
/// 3) Push 또는 ForceApplyKnockback
/// </summary>
public static class EnemyPlayerHitEffectApplier
{
    /// <summary>
    /// IronBody 추가 피해 규칙을 반영해 최종 피해량을 계산합니다.
    /// - 확률: 슬롯 합산 후 1로 클램프
    /// - 추가피해 비율: 슬롯 중 최댓값 사용
    /// </summary>
    public static float ApplyIronBodyExtraDamageIfNeeded(PlayerWeaponController playerWeaponController, float baseDamage)
    {
        if (baseDamage <= 0f)
            return 0f;

        if (!TryGetIronBodyConfig(
                playerWeaponController,
                out _,
                out _,
                out float extraDamageChance,
                out float extraDamagePercent))
            return baseDamage;

        if (extraDamageChance <= 0f || extraDamagePercent <= 0f)
            return baseDamage;

        if (Random.value > extraDamageChance)
            return baseDamage;

        return baseDamage * (1f + extraDamagePercent);
    }

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

        PlayerWeaponController pwcResolve = playerWeaponController;
        if (pwcResolve == null && playerMovement != null)
            pwcResolve = playerMovement.GetComponent<PlayerWeaponController>() ??
                         playerMovement.GetComponentInParent<PlayerWeaponController>();

        if (pwcResolve != null && pwcResolve.IsInvincible())
            return;

        PlayerWeaponController pwcEffective = pwcResolve ?? playerWeaponController;

        bool effectiveUsePush = usePushInsteadOfKnockback;
        bool allowPushDisplacement = true;
        bool allowTargetHitstop = true;

        if (TryGetIronBodyConfig(pwcEffective, out bool ironBodyPush, out bool ironBodyHitstop, out _, out _))
        {
            // IronBody 장착 시 공격 취소를 막기 위해 항상 Push 경로로 처리합니다.
            effectiveUsePush = true;
            allowPushDisplacement = ironBodyPush;
            allowTargetHitstop = ironBodyHitstop;
        }

        if (allowTargetHitstop && pwcEffective != null && targetHoldDuration > 0f)
        {
            pwcEffective.StartStateHold(targetHoldDuration);
            pwcEffective.StartAnimationHold(targetHoldDuration);
        }

        if (effectiveUsePush)
        {
            if (allowPushDisplacement && playerMovement != null)
                playerMovement.ApplyKnockback(hitDirection, knockbackPower, knockbackDuration, knockbackRelativeTransform, faceHitDirection: false);
        }
        else if (pwcEffective != null)
        {
            // 타격감 우선: target hold를 먼저 건 뒤 넉백을 적용한다.
            pwcEffective.ForceApplyKnockback(hitDirection, knockbackPower, knockbackDuration, stunDuration, clearExistingHolds: false);
        }
        else if (playerMovement != null)
        {
            playerMovement.ApplyKnockback(hitDirection, knockbackPower, knockbackDuration, knockbackRelativeTransform);
        }
    }

    private static bool TryGetIronBodyConfig(
        PlayerWeaponController playerWeaponController,
        out bool applyPushDisplacement,
        out bool applyTargetHitstop,
        out float extraDamageChance,
        out float extraDamagePercentMax)
    {
        applyPushDisplacement = true;
        applyTargetHitstop = true;
        extraDamageChance = 0f;
        extraDamagePercentMax = 0f;

        if (playerWeaponController == null)
            return false;

        Upgrade upgrade = playerWeaponController.GetComponent<Upgrade>();
        if (upgrade == null)
            upgrade = playerWeaponController.GetComponentInChildren<Upgrade>(true);
        if (upgrade == null)
            upgrade = playerWeaponController.GetComponentInParent<Upgrade>();

        Transform root = playerWeaponController.transform.root;
        if (upgrade == null && root != null)
            upgrade = root.GetComponentInChildren<Upgrade>(true);

        if (upgrade == null)
            upgrade = Object.FindFirstObjectByType<Upgrade>();

        if (upgrade == null)
            return false;

        bool found = false;
        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            UpgradeEffectSO slot = upgrade.GetSlot(i);
            if (slot is not Upgrade_01_06_IronBody ironBody)
                continue;

            found = true;
            applyPushDisplacement = applyPushDisplacement && ironBody.applyPushDisplacement;
            applyTargetHitstop = applyTargetHitstop && ironBody.applyTargetHitstop;
            extraDamageChance += Mathf.Clamp01(ironBody.extraDamageProcChance);
            extraDamagePercentMax = Mathf.Max(extraDamagePercentMax, Mathf.Max(0f, ironBody.extraDamageTakenPercent));
        }

        extraDamageChance = Mathf.Clamp01(extraDamageChance);

        return found;
    }

    private static void ApplyAttackerHold(Enemy attacker, float attackerHoldDuration)
    {
        if (attackerHoldDuration <= 0f || attacker == null) return;
        if (attacker.CurrentState == Enemy.EnemyState.Dead) return;

        attacker.StartStateHold(attackerHoldDuration);
        attacker.animCtrl?.StartAnimationHold(attackerHoldDuration);
    }
}
