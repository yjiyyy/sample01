using System.Collections;
using UnityEngine;

public partial class EnemyAttackController
{
    /* Melee */
    private bool attackInProgress = false;
    private float attackEndTime;
    private bool meleeHitboxSpawned = false;

    private float meleeRequestedDuration;
    private float meleeClipLength;
    private float meleeFreezeStartTime;
    private bool meleeWillFreeze;
    private bool meleeFrozenApplied;
    private Coroutine meleeHitDelayRoutine;
    private Coroutine meleeMoveRoutine;

    // 중앙 Update()에서 호출됨 (기존 Update의 melee 파트와 동작 동일)
    private void TickMeleeUpdate()
    {
        if (attackInProgress && !IsRushing && rangedRoutine == null && !IsJumping)
        {
            if (meleeWillFreeze && !meleeFrozenApplied && Time.time >= meleeFreezeStartTime)
            {
                if (enemy?.animator != null)
                    enemy.animator.speed = 0f;
                meleeFrozenApplied = true;
            }

            if (Time.time >= attackEndTime)
            {
                FinishMelee(true);
            }
        }
    }

    #region AnimationEvent (Melee)
    public void AttackHit()
    {
        if (!attackInProgress) return;
        if (!(currentAttack is MeleeAttackData data)) return;
        if (meleeHitboxSpawned) return;
        SpawnMeleeHitbox(data);
    }
    #endregion

    #region Melee Hitbox Spawn (Delay via SO)
    private IEnumerator DelayedMeleeHitbox(MeleeAttackData data)
    {
        float delay = (data != null && data.hitboxSpawnDelay > 0f) ? data.hitboxSpawnDelay : 0f;
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (!attackInProgress) { meleeHitDelayRoutine = null; yield break; }
        if (currentAttack != data) { meleeHitDelayRoutine = null; yield break; }
        if (meleeHitboxSpawned) { meleeHitDelayRoutine = null; yield break; }

        SpawnMeleeHitbox(data);
        meleeHitDelayRoutine = null;
    }

    private void SpawnMeleeHitbox(MeleeAttackData data)
    {
        if (data == null || data.hitBoxPrefab == null)
        {
            Log("MELEE HITBOX prefab null");
            return;
        }

        GameObject go = Instantiate(data.hitBoxPrefab, transform.position, transform.rotation, transform);
        meleeHitboxSpawned = true;

        if (go.TryGetComponent<HitBox_Enemy>(out var hb))
        {
            float life = data.hitBoxLifetime > 0f ? data.hitBoxLifetime : 0.1f;
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
        }
        Log("MELEE HITBOX spawned");
    }
    #endregion

    #region Melee
    private void StartMelee(MeleeAttackData data, int index)
    {
        MarkExecuted();
        ClearHold();

        if (meleeHitDelayRoutine != null)
        {
            StopCoroutine(meleeHitDelayRoutine);
            meleeHitDelayRoutine = null;
        }

        if (meleeMoveRoutine != null)
        {
            StopCoroutine(meleeMoveRoutine);
            meleeMoveRoutine = null;
        }

        attackInProgress = true;
        meleeHitboxSpawned = false;

        currentAttack = data;
        currentAttackIndex = index;

        meleeRequestedDuration = data.attackTime > 0f ? data.attackTime : 0.8f;
        meleeClipLength = GetMeleeClipLength(data);

        if (meleeRequestedDuration > meleeClipLength)
        {
            meleeWillFreeze = true;
            meleeFreezeStartTime = Time.time + meleeClipLength;
            attackEndTime = Time.time + meleeRequestedDuration;
        }
        else
        {
            meleeWillFreeze = false;
            attackEndTime = Time.time + meleeRequestedDuration;
        }
        meleeFrozenApplied = false;

        enemy.SetState(Enemy.EnemyState.Attack);
        if (data.grantSuperArmor) enemy.AddSuperArmor(SuperArmorSource.Attack);
        else enemy.RemoveSuperArmor(SuperArmorSource.Attack);

        if (enemy.animator)
        {
            SafeSetBool("IsRush", false);
            SafeSetBool("IsRushPrepare", false);
            enemy.animator.speed = 1f;

            if (data.clip != null)
                enemy.animator.Play(data.clip.name, 0, 0f);
            else
                enemy.animator.Play(data.attackName, 0, 0f);

            if (data.lockTiming == MeleeAttackData.MovementLockTiming.OnAnimationStart)
            {
                Transform playerT = GameObject.FindWithTag("Player")?.transform;
                Vector3 dirToTarget = playerT != null ? (playerT.position - transform.position) : transform.forward;
                dirToTarget.y = 0f;
                if (dirToTarget.sqrMagnitude < 1e-6f) dirToTarget = transform.forward;
                enemy.LockLookDirection(dirToTarget.normalized, meleeRequestedDuration);
            }
        }

        meleeHitDelayRoutine = StartCoroutine(DelayedMeleeHitbox(data));

        if (data.isMovingAttack)
        {
            meleeMoveRoutine = StartCoroutine(MeleeMovingRoutine(data));
        }

        Log($"MELEE START idx={index} req={meleeRequestedDuration:F3}s clip={meleeClipLength:F3}s freeze={(meleeWillFreeze ? "Y" : "N")}, hitDelay={data.hitboxSpawnDelay:F3}s");
    }

    private float GetMeleeClipLength(MeleeAttackData data)
    {
        if (data.clip != null) return data.clip.length;
        if (enemy?.animator?.runtimeAnimatorController != null)
        {
            var clips = enemy.animator.runtimeAnimatorController.animationClips;
            foreach (var c in clips)
                if (c.name == data.attackName) return c.length;
        }
        return data.attackTime > 0f ? data.attackTime : 0.8f;
    }

    private void FinishMelee(bool success)
    {
        if (enemy?.animator != null)
            enemy.animator.speed = 1f;

        if (meleeHitDelayRoutine != null)
        {
            StopCoroutine(meleeHitDelayRoutine);
            meleeHitDelayRoutine = null;
        }

        if (meleeMoveRoutine != null)
        {
            StopCoroutine(meleeMoveRoutine);
            meleeMoveRoutine = null;
        }

        attackInProgress = false;
        if (currentAttack is MeleeAttackData data)
        {
            enemy.RemoveSuperArmor(SuperArmorSource.Attack);
            if (success)
            {
                ApplyPerAttackCooldown(currentAttackIndex, data.cooldown);
                ApplyGlobalCooldown();
                Log($"MELEE END SUCCESS idx={currentAttackIndex}");
            }
            else
            {
                Log($"MELEE END CANCEL idx={currentAttackIndex} noCooldown");
            }
        }

        currentAttack = null;
        currentAttackIndex = -1;
        meleeHitboxSpawned = false;
        meleeWillFreeze = false;
        meleeFrozenApplied = false;

        if (enemy.CurrentState == Enemy.EnemyState.Attack && !IsHardCrowdControlled())
            enemy.SetState(Enemy.EnemyState.Chase);

        if (enemy != null)
            enemy.UnlockLookDirection();
    }
    #endregion

    #region Melee moving attack coroutine
    private IEnumerator MeleeMovingRoutine(MeleeAttackData data)
    {
        if (data == null) yield break;

        Transform playerT = GameObject.FindWithTag("Player")?.transform;

        Vector3 targetPos = playerT != null ? playerT.position : transform.position + transform.forward * 1f;
        if (data.lockTiming == MeleeAttackData.MovementLockTiming.OnAnimationStart)
        {
            targetPos = playerT != null ? playerT.position : targetPos;
        }

        float applyAt = Time.time + Mathf.Max(0f, data.forceApplyTime);

        while (Time.time < applyAt)
        {
            if (!attackInProgress) yield break;
            if (enemy.CurrentState != Enemy.EnemyState.Attack || enemy.CurrentState == Enemy.EnemyState.ShieldBreak) yield break;

            if (data.lockTiming == MeleeAttackData.MovementLockTiming.JustBeforeImpulse)
            {
                if (playerT != null)
                    targetPos = playerT.position;
            }

            yield return null;
        }

        Vector3 dir = targetPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();

        if (data.lockTiming == MeleeAttackData.MovementLockTiming.JustBeforeImpulse)
        {
            float remaining = Mathf.Max(0f, attackEndTime - Time.time);
            enemy.LockLookDirection(dir, remaining);
        }

        if (enemy == null || !enemy.IsLookLocked)
        {
            transform.rotation = Quaternion.LookRotation(dir);
        }

        float massMul = 1f;
        Rigidbody rb = enemy.GetComponent<Rigidbody>();
        if (rb != null) massMul = Mathf.Max(0.0001f, rb.mass);
        float initialSpeed = Mathf.Abs(data.moveForce) / massMul;

        float dur = Mathf.Max(0f, data.moveDuration);
        float elapsed = 0f;

        while (elapsed < dur)
        {
            if (!attackInProgress) break;
            if (enemy.CurrentState != Enemy.EnemyState.Attack || enemy.CurrentState == Enemy.EnemyState.ShieldBreak) break;

            float t = Mathf.Clamp01(elapsed / dur);
            float currentSpeed = initialSpeed * (1f - t);
            Vector3 disp = dir * currentSpeed * Time.fixedDeltaTime;

            enemy.MoveFilteredDisplacement(disp);

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        meleeMoveRoutine = null;
    }
    #endregion

    private void InterruptMeleeIfNeeded()
    {
        if (!attackInProgress) return;
        Log("INTERRUPT melee -> cancel");
        FinishMelee(false);
    }
}