using System.Collections;
using UnityEngine;

public partial class EnemyAttackController
{
    /* Ranged */
    private Coroutine rangedRoutine;
    private Transform rangedTarget;
    private int runningRangedIndex = -1;
    private bool rangedProjectileFired = false;

    private void StartRanged(RangedAttackData data, Transform target, int index)
    {
        MarkExecuted();
        ClearHold();

        if (rangedRoutine != null)
        {
            StopCoroutine(rangedRoutine);
            rangedRoutine = null;
        }

        runningRangedIndex = index;
        rangedTarget = target;

        enemy.SetState(Enemy.EnemyState.Attack);
        if (data.grantSuperArmor) enemy.AddSuperArmor(SuperArmorSource.Attack);
        else enemy.RemoveSuperArmor(SuperArmorSource.Attack);

        rangedRoutine = StartCoroutine(RangedRoutine(data));
        Log($"RANGED START idx={index} prep={data.prepareTime:F2} atk={data.attackTime:F2} fireAt={data.fireAtTime:F2}");
    }

    private IEnumerator RangedRoutine(RangedAttackData data)
    {
        // PREPARE
        if (data.prepareTime > 0f)
        {
            float prepClipLen = data.prepareClip != null ? data.prepareClip.length : 0f;
            bool willFreeze = prepClipLen > 0f && data.prepareTime > prepClipLen;
            float elapsed = 0f;
            bool freezed = false;

            if (enemy.animator && data.prepareClip != null)
            {
                enemy.animator.speed = 1f;
                enemy.animator.Play(data.prepareClip.name, 0, 0f);
            }

            while (elapsed < data.prepareTime)
            {
                if (enemy.CurrentState != Enemy.EnemyState.Attack ||
                    enemy.CurrentState == Enemy.EnemyState.ShieldBreak)
                {
                    Log("RANGED PREPARE INTERRUPT noCooldown");
                    CancelRangedNoCooldown();
                    yield break;
                }

                FaceTarget(rangedTarget);

                if (willFreeze && !freezed && enemy.animator != null && elapsed >= prepClipLen)
                {
                    enemy.animator.speed = 0f;
                    freezed = true;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (enemy.animator) enemy.animator.speed = 1f;
        }

        // ATTACK
        rangedProjectileFired = false;

        float atkReq = data.attackTime > 0f ? data.attackTime : 0.8f;
        float atkClipLen = GetRangedAttackClipLength(data);
        bool atkWillFreeze = atkClipLen > 0f && atkReq > atkClipLen;
        float fireTime = Mathf.Clamp(data.fireAtTime, 0f, atkReq);

        float atkElapsed = 0f;
        bool atkFreezed = false;

        if (enemy.animator)
        {
            enemy.animator.speed = 1f;
            if (data.attackClip != null)
                enemy.animator.Play(data.attackClip.name, 0, 0f);
            else
                enemy.animator.Play(data.attackName, 0, 0f);
        }

        while (atkElapsed < atkReq)
        {
            if (enemy.CurrentState != Enemy.EnemyState.Attack ||
                enemy.CurrentState == Enemy.EnemyState.ShieldBreak)
            {
                Log("RANGED ATTACK INTERRUPT noCooldown");
                CancelRangedNoCooldown();
                yield break;
            }

            FaceTarget(rangedTarget);

            if (!rangedProjectileFired && atkElapsed >= fireTime)
            {
                FireProjectile(data);
                rangedProjectileFired = true;
            }

            if (atkWillFreeze && !atkFreezed && enemy.animator != null && atkElapsed >= atkClipLen)
            {
                enemy.animator.speed = 0f;
                atkFreezed = true;
            }

            atkElapsed += Time.deltaTime;
            yield return null;
        }

        if (enemy.animator) enemy.animator.speed = 1f;

        FinishRanged(data, true);
    }

    private float GetRangedAttackClipLength(RangedAttackData data)
    {
        if (data.attackClip != null) return data.attackClip.length;
        if (enemy?.animator?.runtimeAnimatorController != null)
        {
            var clips = enemy.animator.runtimeAnimatorController.animationClips;
            foreach (var c in clips)
                if (c.name == data.attackName) return c.length;
        }
        return data.attackTime > 0f ? data.attackTime : 0.8f;
    }

    private void FireProjectile(RangedAttackData data)
    {
        if (data.projectilePrefab == null)
        {
            Log("RANGED projectilePrefab null");
            return;
        }

        Transform firePoint = FindChildRecursive(transform, data.firePointName);
        if (firePoint == null) firePoint = transform;

        Vector3 targetPos = rangedTarget != null ? rangedTarget.position : (firePoint.position + transform.forward * 5f);

        Vector3 shootDir = (targetPos - firePoint.position);
        if (shootDir.sqrMagnitude < 0.0001f) shootDir = transform.forward;
        shootDir.y = 0f;
        shootDir.Normalize();

        GameObject proj = Instantiate(
            data.projectilePrefab,
            firePoint.position,
            Quaternion.LookRotation(shootDir)
        );

        if (proj.TryGetComponent<HitBox_Enemy_Projectile>(out var hb))
        {
            hb.Initialize(
                data.damage,
                data.projectileSpeed,
                data.projectileLifetime,
                data.knockbackPower,
                data.knockbackDuration,
                data.stunDuration,
                data.allowDuplicateHit,
                data.duplicateHitInterval,
                data.movementType,
                firePoint.position,
                targetPos,
                data.arcHeight,
                data.faceToMovement,
                data.spinWhileFlying,
                data.spinAxis,
                data.spinSpeed,
                data.destroyOnObstacle,
                data.obstacleLayers
            );
        }

        Log($"RANGED FIRE proj@{firePoint.name} ¡æ target {targetPos} type={data.movementType}");
    }

    private void FinishRanged(RangedAttackData data, bool success)
    {
        if (success)
        {
            ApplyPerAttackCooldown(runningRangedIndex, data.cooldown);
            ApplyGlobalCooldown();
            Log($"RANGED END SUCCESS idx={runningRangedIndex}");
        }
        else
        {
            Log($"RANGED END CANCEL idx={runningRangedIndex}");
        }

        enemy.RemoveSuperArmor(SuperArmorSource.Attack);
        runningRangedIndex = -1;

        if (enemy.CurrentState == Enemy.EnemyState.Attack && !IsHardCrowdControlled())
            enemy.SetState(Enemy.EnemyState.Chase);

        if (rangedRoutine != null)
        {
            StopCoroutine(rangedRoutine);
            rangedRoutine = null;
        }
    }

    private void CancelRangedNoCooldown()
    {
        enemy.RemoveSuperArmor(SuperArmorSource.Attack);
        runningRangedIndex = -1;

        if (enemy.animator) enemy.animator.speed = 1f;

        if (enemy.CurrentState == Enemy.EnemyState.Attack && !IsHardCrowdControlled())
            enemy.SetState(Enemy.EnemyState.Chase);

        if (rangedRoutine != null)
        {
            StopCoroutine(rangedRoutine);
            rangedRoutine = null;
        }
    }

    private void InterruptRangedIfNeeded()
    {
        if (rangedRoutine != null)
        {
            Log("INTERRUPT ranged -> cancel");
            CancelRangedNoCooldown();
        }
    }
}