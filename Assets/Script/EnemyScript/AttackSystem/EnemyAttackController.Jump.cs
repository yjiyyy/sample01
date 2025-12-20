using System.Collections;
using UnityEngine;

public partial class EnemyAttackController
{
    /* Jump (점프 공격) */
    public bool IsJumping { get; private set; } = false;
    private Coroutine jumpPrepareCoroutine;
    private Coroutine jumpCoroutine;
    private GameObject spawnedJumpHitbox;
    private int runningJumpIndex = -1;
    private Transform jumpTarget;

    private bool debugJumpTrajectory = true;

    private void StartJump(JumpAttackData data, Transform target, int index)
    {
        MarkExecuted();
        ClearHold();

        StopJumpCoroutines();
        runningJumpIndex = index;
        jumpTarget = target;

        enemy.SetState(Enemy.EnemyState.Attack);
        if (data.grantSuperArmor) enemy.AddSuperArmor(SuperArmorSource.Attack);
        else enemy.RemoveSuperArmor(SuperArmorSource.Attack);

        jumpPrepareCoroutine = StartCoroutine(JumpPrepareRoutine(data));
        Log($"JUMP PREPARE START idx={index} prep={data.prepareDuration:F2}");
    }

    private IEnumerator JumpPrepareRoutine(JumpAttackData data)
    {
        if (enemy.animator)
        {
            if (data.prepareClip != null)
            {
                enemy.animator.speed = 1f;
                enemy.animator.Play(data.prepareClip.name, 0, 0f);
            }
            else
            {
                // 폴백: "JumpPrepare"
                SafeSetBool("IsJumpPrepare", true);
                SafeSetBool("IsJump", false);
                enemy.animator.Play("JumpPrepare");
            }
        }

        float elapsed = 0f;
        while (elapsed < data.prepareDuration)
        {
            if (jumpTarget != null)
            {
                Vector3 dir = jumpTarget.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    if (enemy == null || !enemy.IsLookLocked)
                        transform.rotation = Quaternion.LookRotation(dir.normalized);
                }
            }

            if (enemy.CurrentState != Enemy.EnemyState.Attack ||
                enemy.CurrentState == Enemy.EnemyState.ShieldBreak)
            {
                Log("JUMP PREPARE INTERRUPT noCooldown");
                CancelJumpNoCooldown();
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        jumpPrepareCoroutine = null;
        jumpCoroutine = StartCoroutine(JumpAttackRoutine(data));
    }

    private IEnumerator JumpAttackRoutine(JumpAttackData data)
    {
        IsJumping = true;

        // start / target positions
        Vector3 startPos = transform.position;
        Vector3 landingPos = startPos;
        if (jumpTarget != null)
        {
            // use player's position at the end of Prepare (no prediction)
            landingPos = jumpTarget.position;
        }

        // Calculate transform Y so that capsule bottom sits on landingPos.y
        float landingTransformY = transform.position.y; // default
        CapsuleCollider cap = GetComponent<CapsuleCollider>();
        if (cap != null)
        {
            float scaleY = cap.transform.lossyScale.y;
            // transformY = landingPos.y - center.y*scaleY + (height*0.5f*scaleY)
            landingTransformY = landingPos.y - (cap.center.y * scaleY) + (cap.height * 0.5f * scaleY);
            landingTransformY += 0.01f; // small epsilon
        }
        else
        {
            landingTransformY = landingPos.y;
        }

        // Play loop/jump animation
        if (enemy.animator)
        {
            enemy.animator.speed = 1f;
            if (data.loopClip != null)
                enemy.animator.Play(data.loopClip.name, 0, 0f);
            else if (!string.IsNullOrEmpty(data.attackName))
                enemy.animator.Play(data.attackName, 0, 0f);
            else
                enemy.animator.Play("JumpLoop", 0, 0f);
        }

        // Trajectory parameters
        float tTotal = Mathf.Max(0.0001f, data.duration);
        float elapsed = 0f;

        // Precompute vertical difference and arc height adjustment
        float verticalDiff = landingTransformY - startPos.y;
        float arcH = data.height;
        if (verticalDiff > 0f)
        {
            // Increase arc so that absolute peak = startY + verticalDiff + data.height
            arcH = data.height + 0.5f * verticalDiff;
        }

        // simulatedPos is our internal authoritative position used to compute displacements.
        // This avoids depending on transform.position which may be modified externally.
        Vector3 simulatedPos = transform.position;

        if (debugJumpTrajectory)
        {
            Debug.Log($"[JumpDebug] startY={startPos.y:F3} landingY={landingTransformY:F3} verticalDiff={verticalDiff:F3} arcH={arcH:F3} duration={tTotal:F3}");
        }

        // Use FixedUpdate sync for physics-consistent movement
        while (elapsed < tTotal)
        {
            if (enemy.CurrentState != Enemy.EnemyState.Attack ||
                enemy.CurrentState == Enemy.EnemyState.ShieldBreak)
            {
                Log("JUMP ATTACK INTERRUPT");
                CancelJumpNoCooldown();
                yield break;
            }

            yield return new WaitForFixedUpdate();
            float dt = Time.fixedDeltaTime;
            elapsed += dt;
            float u = Mathf.Clamp01(elapsed / tTotal);

            // horizontal (XZ) linear interpolation
            Vector3 targetXZ = new Vector3(landingPos.x, 0f, landingPos.z);
            Vector3 startXZ = new Vector3(startPos.x, 0f, startPos.z);
            Vector3 posXZ = Vector3.Lerp(startXZ, targetXZ, u);

            // vertical (Y): base lerp plus arc term (peak at u=0.5)
            float baseY = Mathf.Lerp(startPos.y, landingTransformY, u);
            float arc = 4f * arcH * u * (1f - u); // peaks at u=0.5 -> arcH
            float posY = baseY + arc;

            Vector3 nextPos = new Vector3(posXZ.x, posY, posXZ.z);

            // compute displacement from simulatedPos (not transform.position)
            Vector3 disp = nextPos - simulatedPos;

            // Safety clamp: prevent going below landingTransformY
            float predictedNextY = simulatedPos.y + disp.y;
            bool willClamp = false;
            if (predictedNextY < landingTransformY)
            {
                disp.y = landingTransformY - simulatedPos.y;
                willClamp = true;
            }

            // Optional debug draw/log
            if (debugJumpTrajectory)
            {
                Debug.DrawLine(simulatedPos, simulatedPos + disp, Color.cyan, 2f);
                Debug.Log($"[JumpDebug] u={u:F3} nextY={posY:F3} simulatedY={simulatedPos.y:F3} dispY={disp.y:F3} willClamp={willClamp}");
            }

            // Apply movement using Enemy's movement API for consistent physics handling
            try
            {
                enemy.MoveFilteredDisplacement(disp);
            }
            catch
            {
                // fallback if API not present
                transform.position += disp;
            }

            // Advance our internal simulated position by the same displacement we applied
            simulatedPos += disp;

            if (willClamp) break;
        }

        // Final snap to landing position (ensure exact), use simulatedPos for delta
        Vector3 desiredPos = new Vector3(landingPos.x, landingTransformY, landingPos.z);
        Vector3 finalDisp = desiredPos - simulatedPos;
        if (finalDisp.sqrMagnitude > 0.000001f)
        {
            if (debugJumpTrajectory)
                Debug.Log($"[JumpDebug] finalSnap desiredY={desiredPos.y:F3} simulatedY={simulatedPos.y:F3} finalDispY={finalDisp.y:F3}");

            try
            {
                enemy.MoveFilteredDisplacement(finalDisp);
            }
            catch
            {
                transform.position = desiredPos;
            }
            simulatedPos += finalDisp;
        }

        // END: play end animation & spawn hitbox at landing position
        if (enemy.animator)
        {
            enemy.animator.speed = 1f;
            if (data.endClip != null)
                enemy.animator.Play(data.endClip.name, 0, 0f);
            else
                enemy.animator.Play("JumpEnd", 0, 0f);
        }

        // Camera shake
        if (data.cameraShake != null)
        {
            var shaker = CameraShakePlayer.Instance;
            if (shaker != null)
                shaker.PlayShake(data.cameraShake, 1f);
        }

        // Spawn hitbox at landing location
        SpawnJumpHitboxAtPosition(data, desiredPos);

        // Wait for endDuration (allow animation to play)
        float waited = 0f;
        while (waited < data.endDuration)
        {
            if (enemy.CurrentState != Enemy.EnemyState.Attack ||
                enemy.CurrentState == Enemy.EnemyState.ShieldBreak)
            {
                break;
            }
            yield return null;
            waited += Time.deltaTime;
        }

        DespawnJumpHitbox();

        // finalize
        if (enemy.animator && !IsHardCrowdControlled())
        {
            SafeSetBool("IsJump", false);
            SafeSetBool("IsJumpPrepare", false);
        }

        IsJumping = false;
        runningJumpIndex = -1;
        jumpCoroutine = null;

        enemy.RemoveSuperArmor(SuperArmorSource.Attack);

        if (enemy.CurrentState == Enemy.EnemyState.Attack && !IsHardCrowdControlled())
            enemy.SetState(Enemy.EnemyState.Chase);
    }

    // Helper that spawns the hitbox at a specific world position (landing)
    private void SpawnJumpHitboxAtPosition(JumpAttackData data, Vector3 worldPos)
    {
        if (data.hitBoxPrefab == null) return;
        if (spawnedJumpHitbox != null) return;

        if (data.attachHitboxToEnemy)
        {
            // attach to enemy, but still place at landing world pos (localization)
            spawnedJumpHitbox = Instantiate(data.hitBoxPrefab, worldPos, transform.rotation, transform);
        }
        else
        {
            spawnedJumpHitbox = Instantiate(data.hitBoxPrefab, worldPos, Quaternion.identity);
        }

        if (spawnedJumpHitbox.TryGetComponent<HitBox_Enemy>(out var hb))
        {
            float life = data.hitBoxLifetime > 0f ? data.hitBoxLifetime : data.endDuration;
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
    }

    private void SpawnJumpHitbox(JumpAttackData data)
    {
        // kept for backward compatibility in case other code expects this name
        // it will simply spawn at current transform position and follow attachHitboxToEnemy flag
        if (data == null || data.hitBoxPrefab == null) return;
        if (spawnedJumpHitbox != null) return;

        Transform parent = data.attachHitboxToEnemy ? transform : null;
        Vector3 spawnPos = transform.position;
        Quaternion spawnRot = transform.rotation;

        spawnedJumpHitbox = parent != null
            ? Instantiate(data.hitBoxPrefab, spawnPos, spawnRot, parent)
            : Instantiate(data.hitBoxPrefab, spawnPos, spawnRot);

        if (spawnedJumpHitbox.TryGetComponent<HitBox_Enemy>(out var hb))
        {
            float life = data.hitBoxLifetime > 0f ? data.hitBoxLifetime : data.endDuration;
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
    }

    private void DespawnJumpHitbox()
    {
        if (spawnedJumpHitbox != null)
            Destroy(spawnedJumpHitbox);
        spawnedJumpHitbox = null;
    }

    private void CancelJumpNoCooldown()
    {
        enemy.RemoveSuperArmor(SuperArmorSource.Attack);
        if (enemy.animator && !IsHardCrowdControlled())
        {
            SafeSetBool("IsJump", false);
            SafeSetBool("IsJumpPrepare", false);
        }
        DespawnJumpHitbox();
        runningJumpIndex = -1;

        if (enemy.CurrentState == Enemy.EnemyState.Attack && !IsHardCrowdControlled())
            enemy.SetState(Enemy.EnemyState.Chase);
    }

    private void StopJumpCoroutines()
    {
        if (jumpPrepareCoroutine != null) StopCoroutine(jumpPrepareCoroutine);
        if (jumpCoroutine != null) StopCoroutine(jumpCoroutine);
        jumpPrepareCoroutine = null;
        jumpCoroutine = null;
    }

    private void InterruptJumpIfNeeded()
    {
        if (jumpPrepareCoroutine != null || IsJumping)
        {
            Log("INTERRUPT jump -> cancel");
            StopJumpCoroutines();
            IsJumping = false;
            CancelJumpNoCooldown();
        }
    }
}