using System.Collections;
using UnityEngine;

public partial class EnemyAttackController
{
    /* Melee */
    private bool attackInProgress = false;
    private bool meleeHitboxSpawned = false;

    private float meleeRequestedDuration;
    private float meleeClipLength;
    private float meleeFreezeStartElapsed;
    private float meleeElapsed;
    private bool meleeWillFreeze;
    private bool meleeFrozenApplied;
    private Coroutine meleeHitDelayRoutine;
    private Coroutine meleeMoveRoutine;
    private Coroutine attackHitDeferredRoutine;

    // --- Combo / override helpers ---
    // When true, FinishMelee will NOT apply per-attack cooldown. Used by combo mode.
    private bool suppressPerAttackCooldown = false;
    // If >= 0, overrides the MeleeAttackData.range when spawning hitboxes (used by combo to provide global range).
    private float overrideRange = -1f;

    /// <summary>
    /// Called by combo routine to enter combo-mode (suppress per-slot cooldowns, optionally override range).
    /// Note: combo no longer manages super-armor; Melee slots manage grantSuperArmor themselves.
    /// </summary>
    public void BeginComboMode(float comboRange = -1f)
    {
        suppressPerAttackCooldown = true;
        overrideRange = comboRange;
    }

    /// <summary>
    /// Cleanup combo-mode flags (restore normal behavior).
    /// </summary>
    public void EndComboMode()
    {
        suppressPerAttackCooldown = false;
        overrideRange = -1f;
    }

    // 매 프레임 Update()에서 호출 (원래 Update에 melee 진행/프리즈/종료 로직을 위임)
    private void TickMeleeUpdate()
    {
        if (attackInProgress && !IsRushing && rangedRoutine == null && !IsJumping)
        {
            // Hold is handled at EnemyAttackController.Update() level (early return),
            // so this elapsed timer only advances while gameplay logic is running.
            meleeElapsed += Time.deltaTime;

            if (meleeWillFreeze && !meleeFrozenApplied && meleeElapsed >= meleeFreezeStartElapsed)
            {
                if (enemy?.animator != null)
                    enemy.animator.speed = 0f;
                meleeFrozenApplied = true;
            }

            if (meleeElapsed >= meleeRequestedDuration)
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

        if (enemy != null && enemy.IsStateHoldActive)
        {
            if (attackHitDeferredRoutine != null)
                StopCoroutine(attackHitDeferredRoutine);
            attackHitDeferredRoutine = StartCoroutine(DeferredAttackHitSpawn(data));
            return;
        }

        SpawnMeleeHitbox(data);
    }
    #endregion

    #region Melee Hitbox Spawn (Delay via SO)
    private IEnumerator DelayedMeleeHitbox(MeleeAttackData data)
    {
        float delay = (data != null && data.hitboxSpawnDelay > 0f) ? data.hitboxSpawnDelay : 0f;
        if (delay > 0f)
        {
            float waited = 0f;
            while (waited < delay)
            {
                if (enemy != null && enemy.IsStateHoldActive)
                {
                    yield return null;
                    continue;
                }

                if (!attackInProgress) { meleeHitDelayRoutine = null; yield break; }
                if (currentAttack != data) { meleeHitDelayRoutine = null; yield break; }

                waited += Time.deltaTime;
                yield return null;
            }
        }

        while (enemy != null && enemy.IsStateHoldActive)
            yield return null;

        if (!attackInProgress) { meleeHitDelayRoutine = null; yield break; }
        if (currentAttack != data) { meleeHitDelayRoutine = null; yield break; }
        if (meleeHitboxSpawned) { meleeHitDelayRoutine = null; yield break; }

        SpawnMeleeHitbox(data);
        meleeHitDelayRoutine = null;
    }

    private IEnumerator DeferredAttackHitSpawn(MeleeAttackData data)
    {
        while (enemy != null && enemy.IsStateHoldActive)
            yield return null;

        if (!attackInProgress) { attackHitDeferredRoutine = null; yield break; }
        if (currentAttack != data) { attackHitDeferredRoutine = null; yield break; }
        if (meleeHitboxSpawned) { attackHitDeferredRoutine = null; yield break; }

        SpawnMeleeHitbox(data);
        attackHitDeferredRoutine = null;
    }

    private void SpawnMeleeHitbox(MeleeAttackData data)
    {
        if (data == null || data.hitBoxPrefab == null)
        {
            Log("MELEE HITBOX prefab null");
            return;
        }

        // Decide use range: if combo overrideRange set (>=0), use it; else use data.range
        float useRange = (overrideRange >= 0f) ? overrideRange : data.range;

        // attachHitboxToEnemy true이면 적의 자식으로 붙여서 이후 적이 움직이면 히트박스도 따라다니게 함.
        // false이면 월드에 고정해서 스폰 순간의 enemy.transform.position/rotation을 사용해 배치(이후 적 이동과 무관).
        GameObject go;
        if (data.attachHitboxToEnemy)
        {
            go = Instantiate(data.hitBoxPrefab, transform.position, transform.rotation, transform);
        }
        else
        {
            go = Instantiate(data.hitBoxPrefab, transform.position, transform.rotation);
        }

        meleeHitboxSpawned = true;

        if (go.TryGetComponent<HitBox_Enemy>(out var hb))
        {
            float life = data.hitBoxLifetime > 0f ? data.hitBoxLifetime : 0.1f;
            hb.Initialize(
                data.damage,
                useRange,
                data.knockbackPower,
                data.knockbackDuration,
                life,
                data.stunDuration,
                data.allowDuplicateHit,
                data.duplicateHitInterval,
                WeaponDataSO.CreatePlayerDeathProxy(data.deathMode, data.ragdollImpulse, data.ragdollUpImpulse, data.ragdollSpinTorque, data.sliceTargets, data.sliceImpulse),
                data.targetHoldDuration,
                data.usePushInsteadOfKnockback,
                data.attackerHoldDuration
            );
        }
        else
        {
            if (go.TryGetComponent<HitBox_PC>(out var hbpc))
            {
                hbpc.SetWeapon(null);
                hbpc.Initialize(data.damage, useRange, data.knockbackPower, Mathf.Max(0.01f, data.hitBoxLifetime));
            }
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
        if (attackHitDeferredRoutine != null)
        {
            StopCoroutine(attackHitDeferredRoutine);
            attackHitDeferredRoutine = null;
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
        meleeElapsed = 0f;

        if (meleeRequestedDuration > meleeClipLength)
        {
            meleeWillFreeze = true;
            meleeFreezeStartElapsed = meleeClipLength;
        }
        else
        {
            meleeWillFreeze = false;
            meleeFreezeStartElapsed = meleeRequestedDuration;
        }
        meleeFrozenApplied = false;

        enemy.SetState(Enemy.EnemyState.Attack);
        // Melee slot is responsible for grantSuperArmor now
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
            // Remove super armor that this melee granted (slot manages its own grant)
            enemy.RemoveSuperArmor(SuperArmorSource.Attack);

            if (success)
            {
                if (!suppressPerAttackCooldown)
                {
                    ApplyPerAttackCooldown(currentAttackIndex, data.cooldown);
                    ApplyGlobalCooldown();
                    Log($"MELEE END SUCCESS idx={currentAttackIndex}");
                }
                else
                {
                    // per-slot cooldown suppressed for combo mode; combo will apply its cooldown later
                    Log($"MELEE END SUCCESS idx={currentAttackIndex} (per-slot cooldown suppressed due to combo)");
                }
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
        meleeElapsed = 0f;

        if (enemy.CurrentState == Enemy.EnemyState.Attack && !IsHardCrowdControlled())
            enemy.SetState(Enemy.EnemyState.Chase);

        if (enemy != null)
            enemy.UnlockLookDirection();
    }
    #endregion

    #region Melee moving attack coroutine (MeleeMovingRoutine)
    private IEnumerator MeleeMovingRoutine(MeleeAttackData data)
    {
        if (data == null) yield break;

        Transform playerT = GameObject.FindWithTag("Player")?.transform;

        // Two possible lock timings:
        // - OnAnimationStart: capture target at coroutine start
        // - JustBeforeImpulse: capture right before applying force (we'll wait until applyAt)
        Vector3 targetPos = transform.position + transform.forward * 1f; // fallback
        if (data.lockTiming == MeleeAttackData.MovementLockTiming.OnAnimationStart)
        {
            if (playerT != null) targetPos = playerT.position;
        }

        float waitToApply = Mathf.Max(0f, data.forceApplyTime);
        float waitElapsed = 0f;

        // Wait until applyAt (if > now). If lockTiming is JustBeforeImpulse keep updating targetPos.
        while (waitElapsed < waitToApply)
        {
            if (enemy != null && enemy.IsStateHoldActive)
            {
                yield return null;
                continue;
            }

            if (!attackInProgress) yield break;
            if (enemy.CurrentState != Enemy.EnemyState.Attack || enemy.CurrentState == Enemy.EnemyState.ShieldBreak) yield break;

            if (data.lockTiming == MeleeAttackData.MovementLockTiming.JustBeforeImpulse)
            {
                if (playerT != null) targetPos = playerT.position;
            }

            waitElapsed += Time.deltaTime;
            yield return null;
        }

        // Finalize target snapshot for JustBeforeImpulse
        if (data.lockTiming == MeleeAttackData.MovementLockTiming.JustBeforeImpulse)
        {
            if (playerT != null) targetPos = playerT.position;
        }

        // Buffer (완충거리) before exact target where we switch to damped residual movement
        const float buffer = 0.1f; // 10cm

        // initial direction and distance
        Vector3 toTargetInit = targetPos - transform.position;
        toTargetInit.y = 0f;
        float initialDistance = toTargetInit.magnitude;

        Vector3 dir = toTargetInit.sqrMagnitude < 0.0001f ? transform.forward : toTargetInit.normalized;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;

        // optionally lock look direction shortly (handled by caller or elsewhere)
        if (data.lockTiming == MeleeAttackData.MovementLockTiming.JustBeforeImpulse)
        {
            float remaining = Mathf.Max(0f, meleeRequestedDuration - meleeElapsed);
            enemy.LockLookDirection(dir, remaining);
        }

        if (enemy == null || !enemy.IsLookLocked)
        {
            transform.rotation = Quaternion.LookRotation(dir);
        }

        // consider Rigidbody mass if present (velocity scale)
        float massMul = 1f;
        Rigidbody rb = enemy.GetComponent<Rigidbody>();
        if (rb != null) massMul = Mathf.Max(0.0001f, rb.mass);
        float initialSpeed = Mathf.Abs(data.moveForce) / massMul;

        float dur = Mathf.Max(0f, data.moveDuration);
        // ensure very small positive duration to allow loop logic
        if (dur <= 0f)
        {
            // do a single small displacement proportional to one frame (fallback)
            Vector3 tiny = dir * (initialSpeed * Time.fixedDeltaTime);
            try { enemy.MoveFilteredDisplacement(tiny); } catch { try { enemy.MovePhysicsDisplacement(tiny); } catch { transform.position += tiny; } }
            yield break;
        }

        float elapsed = 0f;
        bool reachedBufferPhase = false;
        Vector3 arrivalDir = dir;

        // Movement loop: approach until within buffer, then apply damped residual movement for remaining time
        while (elapsed < dur)
        {
            if (enemy != null && enemy.IsStateHoldActive)
            {
                yield return null;
                continue;
            }

            if (!attackInProgress) break;
            if (enemy.CurrentState != Enemy.EnemyState.Attack || enemy.CurrentState == Enemy.EnemyState.ShieldBreak) break;

            // Recompute remaining distance to target each step
            Vector3 toTarget = targetPos - transform.position;
            toTarget.y = 0f;
            float remainingDist = toTarget.magnitude;

            Vector3 stepDir;
            if (!reachedBufferPhase)
            {
                if (remainingDist > buffer)
                {
                    stepDir = toTarget.normalized;
                }
                else
                {
                    // switch to buffer/damped phase
                    reachedBufferPhase = true;
                    arrivalDir = (toTarget.sqrMagnitude > 0.0001f) ? toTarget.normalized : dir;
                    stepDir = arrivalDir;
                }
            }
            else
            {
                stepDir = arrivalDir;
            }

            // compute current speed (decaying over duration)
            float tNorm = Mathf.Clamp01(elapsed / dur);
            // use decaying profile (linear decay) for predictable behavior across platforms:
            float currentSpeed = initialSpeed * (1f - tNorm);
            Vector3 disp = stepDir * currentSpeed * Time.fixedDeltaTime;

            if (!reachedBufferPhase)
            {
                // allowed distance to move before entering buffer
                float allowed = Mathf.Max(0f, remainingDist - buffer);
                if (allowed <= 0f)
                {
                    // already inside buffer; switch to damped phase without moving
                    reachedBufferPhase = true;
                    arrivalDir = stepDir;
                    // continue to next iteration to apply damped movement
                    elapsed += Time.fixedDeltaTime;
                    yield return new WaitForFixedUpdate();
                    continue;
                }

                if (disp.magnitude >= allowed)
                {
                    // move exactly to buffer boundary
                    Vector3 toMove = stepDir * allowed;
                    try
                    {
                        enemy.MoveFilteredDisplacement(toMove);
                    }
                    catch
                    {
                        try { enemy.MovePhysicsDisplacement(toMove); } catch { transform.position += toMove; }
                    }

                    // approximate time used for this sub-frame move
                    float timeUsed = currentSpeed > 1e-6f ? (allowed / currentSpeed) : Time.fixedDeltaTime;
                    timeUsed = Mathf.Min(timeUsed, Time.fixedDeltaTime);
                    elapsed += timeUsed;

                    reachedBufferPhase = true;
                    arrivalDir = stepDir;
                    // continue loop to apply damped residual movement
                    yield return new WaitForFixedUpdate();
                    continue;
                }
                else
                {
                    // normal approach step
                    try
                    {
                        enemy.MoveFilteredDisplacement(disp);
                    }
                    catch
                    {
                        try { enemy.MovePhysicsDisplacement(disp); } catch { transform.position += disp; }
                    }
                    elapsed += Time.fixedDeltaTime;
                }
            }
            else
            {
                // Damped residual movement after reaching buffer:
                // do not overshoot the actual target
                if (remainingDist <= 0f) break;

                if (disp.magnitude >= remainingDist)
                {
                    Vector3 toMove = stepDir * remainingDist;
                    try
                    {
                        enemy.MoveFilteredDisplacement(toMove);
                    }
                    catch
                    {
                        try { enemy.MovePhysicsDisplacement(toMove); } catch { transform.position += toMove; }
                    }
                    // reached exact target; still allow time to elapse but no further motion
                    elapsed += Time.fixedDeltaTime;
                    break;
                }
                else
                {
                    try
                    {
                        enemy.MoveFilteredDisplacement(disp);
                    }
                    catch
                    {
                        try { enemy.MovePhysicsDisplacement(disp); } catch { transform.position += disp; }
                    }
                    elapsed += Time.fixedDeltaTime;
                }
            }

            yield return new WaitForFixedUpdate();
        }

        meleeMoveRoutine = null;
        yield break;
    }
    #endregion

    private void InterruptMeleeIfNeeded()
    {
        if (!attackInProgress) return;
        Log("INTERRUPT melee -> cancel");
        FinishMelee(false);
    }
}