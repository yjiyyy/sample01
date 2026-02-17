using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class EnemyAttackController
{
    public bool IsSuicideExecuting => suicideCoroutine != null || suicidePrepareCoroutine != null;

    private Coroutine suicidePrepareCoroutine = null;
    private Coroutine suicideCoroutine = null;

    private int runningSuicideIndex = -1;
    private Transform suicideTarget;

    private bool suicideExploded = false;

    private float suicideStartTime = -999f;

    private float lastRetargetTime = -999f;
    private Vector3 cachedDesiredDir = Vector3.forward;
    private Vector3 lastMoveDir = Vector3.forward;

    private static readonly Collider[] s_overlap = new Collider[256];

    private void StartSuicide(SuicideAttackData data, Transform target, int index)
    {
        if (data == null || target == null) return;

        MarkExecuted();
        ClearHold();

        StopSuicideCoroutines();

        runningSuicideIndex = index;
        suicideTarget = target;
        suicideExploded = false;

        suicideStartTime = Time.time;

        enemy.SetState(Enemy.EnemyState.Attack);

        suicidePrepareCoroutine = StartCoroutine(SuicidePrepareRoutine(data));
        if (data.debugLogs) Log($"SUICIDE PREPARE START idx={index} prep={data.prepareDuration:F2}");
    }

    private IEnumerator SuicidePrepareRoutine(SuicideAttackData data)
    {
        if (enemy.animator && data.prepareClip != null)
        {
            enemy.animator.speed = 1f;
            enemy.animator.Play(data.prepareClip.name, 0, 0f);
        }

        float elapsed = 0f;
        while (elapsed < data.prepareDuration)
        {
            if (IsOwnerDead())
            {
                if (data.debugLogs) Log("SUICIDE owner dead during prepare -> drop bomb");
                DropBombAndStop(data);
                yield break;
            }

            if (suicideTarget != null)
            {
                Vector3 dir = suicideTarget.position - transform.position;
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
                if (data.debugLogs) Log("SUICIDE PREPARE INTERRUPT noCooldown");
                CancelSuicideNoCooldown();
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        suicidePrepareCoroutine = null;
        suicideCoroutine = StartCoroutine(SuicideChaseRoutine(data));
    }

    private IEnumerator SuicideChaseRoutine(SuicideAttackData data)
    {
        if (enemy.animator && data.chaseLoopClip != null)
        {
            enemy.animator.speed = 1f;
            enemy.animator.Play(data.chaseLoopClip.name, 0, 0f);
        }

        float startTime = Time.time;

        Vector3 moveDir = transform.forward;
        moveDir.y = 0f;
        if (moveDir.sqrMagnitude < 0.0001f) moveDir = Vector3.forward;
        moveDir.Normalize();

        cachedDesiredDir = moveDir;
        lastMoveDir = moveDir;
        lastRetargetTime = -999f;

        while (!suicideExploded)
        {
            if (IsOwnerDead())
            {
                if (data.debugLogs) Log("SUICIDE owner dead during chase -> drop bomb");
                DropBombAndStop(data);
                yield break;
            }

            if (enemy.CurrentState != Enemy.EnemyState.Attack ||
                enemy.CurrentState == Enemy.EnemyState.ShieldBreak)
            {
                if (data.debugLogs) Log("SUICIDE CHASE INTERRUPT noCooldown");
                StopSuicideCoroutines();
                CancelSuicideNoCooldown();
                yield break;
            }

            if (data.maxChaseTime > 0f && Time.time - startTime >= data.maxChaseTime)
            {
                if (data.debugLogs) Log("SUICIDE explode: timeout");
                ExplodeNow(data);
                break;
            }

            if (suicideTarget != null)
            {
                float dist = Vector3.Distance(transform.position, suicideTarget.position);
                if (dist <= data.explodeDistance)
                {
                    if (data.debugLogs) Log("SUICIDE explode: distance reached");
                    ExplodeNow(data);
                    break;
                }
            }

            bool shouldRetarget = (data.retargetInterval <= 0f) || (Time.time - lastRetargetTime >= data.retargetInterval);
            if (shouldRetarget && suicideTarget != null)
            {
                Vector3 desired = suicideTarget.position - transform.position;
                desired.y = 0f;
                if (desired.sqrMagnitude > 0.0001f)
                {
                    cachedDesiredDir = desired.normalized;
                    lastRetargetTime = Time.time;
                }
            }

            if (data.allowDirectionDeviation && data.directionDeviationAmount > 0f && suicideTarget != null)
            {
                float baseWeight = Mathf.Clamp01(data.directionDeviationAmount);
                float dtWeight = 1f - Mathf.Pow(1f - baseWeight, Time.fixedDeltaTime * 60f);
                moveDir = Vector3.Slerp(moveDir, cachedDesiredDir, dtWeight).normalized;

                if (moveDir.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(moveDir);
            }

            Vector3 disp = moveDir * data.chaseSpeed * Time.fixedDeltaTime;
            enemy.MoveFilteredDisplacement(disp);

            lastMoveDir = moveDir;

            yield return new WaitForFixedUpdate();
        }

        StopSuicideCoroutines();
    }

    private bool IsOwnerDead()
    {
        if (enemy == null) return true;
        if (enemy.CurrentState == Enemy.EnemyState.Dead) return true;

        var eh = enemy.GetComponent<EnemyHealth>();
        if (eh != null && eh.GetCurrentHP() <= 0f) return true;

        return false;
    }

    private Vector3 GetSuicideExplodeCenter(SuicideAttackData data)
    {
        Vector3 fwd = transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        fwd.Normalize();

        return transform.position + fwd * data.explodeDistance;
    }

    private void DropBombAndStop(SuicideAttackData data)
    {
        if (suicideExploded) return;
        suicideExploded = true;

        StopSuicideCoroutines();

        if (data == null || data.droppedBombPrefab == null)
        {
            if (data != null && data.debugLogs) Log("SUICIDE drop bomb skipped: droppedBombPrefab is null");
            return;
        }

        float explodeAt = Time.time;
        if (data.maxChaseTime > 0f && suicideStartTime > -900f)
        {
            explodeAt = suicideStartTime + data.maxChaseTime;
            if (explodeAt < Time.time) explodeAt = Time.time;
        }

        // ✅ 스폰 높이 오프셋 적용
        Vector3 spawnPos = transform.position + Vector3.up * data.droppedBombSpawnHeightOffset;

        GameObject bomb = Instantiate(data.droppedBombPrefab, spawnPos, Quaternion.identity);

        if (data.debugLogs) Log($"SUICIDE drop bomb spawned: {bomb.name} explodeAt={explodeAt:F2} now={Time.time:F2}");

        // ✅ 스폰 시 위로 속도/스핀 부여 (Rigidbody 있을 때만)
        var rb = bomb.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            if (data.droppedBombUpVelocity > 0f)
                rb.AddForce(Vector3.up * data.droppedBombUpVelocity, ForceMode.VelocityChange);

            if (data.droppedBombSpinVelocity.sqrMagnitude > 0f)
                rb.AddTorque(data.droppedBombSpinVelocity, ForceMode.VelocityChange);
        }
        else
        {
            // Rigidbody 없으면 "회전 오프셋"으로 1회 적용(보기용)
            if (data.droppedBombSpinVelocity.sqrMagnitude > 0f)
                bomb.transform.rotation = Quaternion.Euler(data.droppedBombSpinVelocity);
        }

        if (bomb.TryGetComponent<SuicideDroppedBomb>(out var dropped))
        {
            dropped.Initialize(data, explodeAt);
        }
        else
        {
            dropped = bomb.AddComponent<SuicideDroppedBomb>();
            dropped.Initialize(data, explodeAt);
        }
    }

    private void ExplodeNow(SuicideAttackData data)
    {
        if (suicideExploded) return;
        suicideExploded = true;

        DoExplosionDamage(data);
        ForceKillOwner(data);

        ApplyPerAttackCooldown(runningSuicideIndex, data.cooldown);
        ApplyGlobalCooldown();
    }

    private void DoExplosionDamage(SuicideAttackData data)
    {
        Vector3 center = GetSuicideExplodeCenter(data); // ✅ 정상 자폭만 앞쪽 폭발
        float radius = data.explosionRadius;

        if (data.spawnDebugSphereOnExplode)
            SpawnDebugSphere(center, radius, data.debugSphereLifetime);

        int count = Physics.OverlapSphereNonAlloc(center, radius, s_overlap, ~0, QueryTriggerInteraction.Collide);

        var hitSeen = new HashSet<object>();

        for (int i = 0; i < count; i++)
        {
            var col = s_overlap[i];
            if (col == null) continue;

            var eh = col.GetComponentInParent<EnemyHealth>() ?? col.GetComponent<EnemyHealth>();
            if (eh != null)
            {
                var ownerEH = enemy != null ? enemy.GetComponent<EnemyHealth>() : null;
                if (ownerEH != null && ownerEH == eh)
                    continue;
            }

            Vector3 targetPos = col.bounds.center;
            float dist = Vector3.Distance(center, targetPos);
            if (dist > radius) continue;

            float t = radius > 0f ? dist / radius : 1f;
            t = Mathf.Clamp01(t);
            float mul = Mathf.Lerp(1f, data.edgeDamageMultiplier, t);

            float actualDamage = data.damage * mul;

            Vector3 hitDir = (targetPos - center);
            hitDir.y = 0f;
            if (hitDir.sqrMagnitude < 0.0001f) hitDir = Vector3.forward;
            hitDir.Normalize();

            var ph = col.GetComponentInParent<PlayerHealth>() ?? col.GetComponent<PlayerHealth>();
            if (ph != null)
            {
                if (!hitSeen.Contains(ph))
                {
                    hitSeen.Add(ph);
                    ApplyExplosionToPlayer(ph, actualDamage, hitDir, data, mul);
                }
                continue;
            }

            if (data.explosionTargets == SuicideAttackData.SuicideExplosionTargetType.PlayerAndEnemies && eh != null)
            {
                if (!hitSeen.Contains(eh))
                {
                    hitSeen.Add(eh);
                    ApplyExplosionToEnemy(eh, actualDamage, hitDir, data, mul);
                }
            }
        }
    }

    // 이하 ApplyExplosionToPlayer / ApplyExplosionToEnemy / ForceKillOwner / SpawnDebugSphere / Cancel/Stop/Interrupt는
    // 네 기존 파일 내용 그대로 두면 되고, ForceKillOwner의 hitDir만 explodeCenter 기준으로 계산하는 게 가장 자연스러움.
    // (현재 대화에서 이미 반영해둔 버전이면 유지해도 OK)

    private void ApplyExplosionToPlayer(PlayerHealth ph, float dmg, Vector3 hitDir, SuicideAttackData data, float mul)
    {
        if (ph == null) return;

        var pwc = ph.GetComponentInParent<PlayerWeaponController>() ?? ph.GetComponent<PlayerWeaponController>();
        if (pwc != null && pwc.IsInvincible())
            return;

        float kbPower = data.knockbackPower * mul;
        float kbDur = data.knockbackDuration * mul;
        float stun = data.stunDuration * mul;

        var deathProxy = WeaponDataSO.CreatePlayerDeathProxy(data.deathMode, data.ragdollImpulse, data.ragdollUpImpulse, data.ragdollSpinTorque, data.sliceTargets, data.sliceImpulse);
        ph.ApplyDamage(dmg, hitDir, deathProxy, 1f);

        if (ph.GetCurrentHP() <= 0f)
            return;

        if (pwc != null)
        {
            pwc.ForceApplyKnockback(hitDir, kbPower, kbDur, stun);
            return;
        }

        var pm = ph.GetComponentInParent<PlayerMovement>() ?? ph.GetComponent<PlayerMovement>();
        if (pm != null)
            pm.ApplyKnockback(hitDir, kbPower, kbDur, enemy != null ? enemy.transform : null);
    }

    private void ApplyExplosionToEnemy(EnemyHealth eh, float dmg, Vector3 hitDir, SuicideAttackData data, float mul)
    {
        if (eh == null) return;

        WeaponDataSO proxy = null;
        try
        {
            proxy = ScriptableObject.CreateInstance<WeaponDataSO>();
            proxy.hideFlags = HideFlags.HideAndDontSave;

            proxy.deathMode = data.deathMode;
            proxy.ragdollImpulse = data.ragdollImpulse;
            proxy.ragdollUpImpulse = data.ragdollUpImpulse;
            proxy.ragdollSpinTorque = data.ragdollSpinTorque;

            proxy.sliceTargets = data.sliceTargets != null ? new List<SliceTarget>(data.sliceTargets) : new List<SliceTarget>();
            proxy.sliceImpulse = data.sliceImpulse;

            proxy.animationHoldDuration = data.animationHoldDuration;
            proxy.usePushInsteadOfKnockback = data.usePushInsteadOfKnockback;

            proxy.knockbackPower = data.knockbackPower * mul;
            proxy.knockbackDuration = data.knockbackDuration * mul;
            proxy.stunDuration = data.stunDuration * mul;

            proxy.jerkIntensity = data.jerkIntensity;
            proxy.jerkDuration = data.jerkDuration;

            eh.ApplyDamage(dmg, hitDir, proxy, 1f);

            var e = eh.GetComponentInParent<Enemy>();
            if (e != null && e.CurrentState != Enemy.EnemyState.Dead)
            {
                if (proxy.usePushInsteadOfKnockback) e.ApplyPush(hitDir, proxy, 1f);
                else e.ApplyKnockback(hitDir, proxy, 1f);
            }
        }
        finally
        {
            if (proxy != null) Object.Destroy(proxy);
        }
    }

    private void ForceKillOwner(SuicideAttackData data)
    {
        if (enemy == null) return;

        var eh = enemy.GetComponent<EnemyHealth>();
        if (eh == null) return;

        WeaponDataSO proxy = ScriptableObject.CreateInstance<WeaponDataSO>();
        proxy.hideFlags = HideFlags.HideAndDontSave;

        proxy.deathMode = data.deathMode;
        proxy.ragdollImpulse = data.ragdollImpulse;
        proxy.ragdollUpImpulse = data.ragdollUpImpulse;
        proxy.ragdollSpinTorque = data.ragdollSpinTorque;

        proxy.sliceTargets = data.sliceTargets != null ? new List<SliceTarget>(data.sliceTargets) : new List<SliceTarget>();
        proxy.sliceImpulse = data.sliceImpulse;

        proxy.animationHoldDuration = data.animationHoldDuration;
        proxy.usePushInsteadOfKnockback = data.usePushInsteadOfKnockback;
        proxy.jerkIntensity = data.jerkIntensity;
        proxy.jerkDuration = data.jerkDuration;

        Vector3 center = GetSuicideExplodeCenter(data);
        Vector3 ownerPos = enemy.transform.position;

        Vector3 hitDir = (ownerPos - center);
        hitDir.y = 0f;
        if (hitDir.sqrMagnitude < 0.0001f) hitDir = -transform.forward;
        hitDir.y = 0f;
        if (hitDir.sqrMagnitude < 0.0001f) hitDir = Vector3.back;
        hitDir.Normalize();

        const float KILL_DAMAGE = 999999f;
        eh.ApplyDamage(KILL_DAMAGE, hitDir, proxy, 1f);

        Object.Destroy(proxy);
    }

    private void SpawnDebugSphere(Vector3 pos, float radius, float lifeTime)
    {
        GameObject dbg = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dbg.transform.position = pos;
        dbg.transform.localScale = Vector3.one * radius * 2f;

        var col = dbg.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        Object.Destroy(dbg, lifeTime);
    }

    private void CancelSuicideNoCooldown()
    {
        StopSuicideCoroutines();
        runningSuicideIndex = -1;
        suicideExploded = false;

        if (enemy.CurrentState == Enemy.EnemyState.Attack && !IsHardCrowdControlled())
            enemy.SetState(Enemy.EnemyState.Chase);
    }

    private void StopSuicideCoroutines()
    {
        if (suicidePrepareCoroutine != null) StopCoroutine(suicidePrepareCoroutine);
        if (suicideCoroutine != null) StopCoroutine(suicideCoroutine);
        suicidePrepareCoroutine = null;
        suicideCoroutine = null;
    }

    private void InterruptSuicideIfNeeded()
    {
        if (suicidePrepareCoroutine == null && suicideCoroutine == null)
            return;

        if (IsOwnerDead())
        {
            SuicideAttackData data = null;
            if (attackPatterns != null &&
                runningSuicideIndex >= 0 &&
                runningSuicideIndex < attackPatterns.Length)
            {
                data = attackPatterns[runningSuicideIndex] as SuicideAttackData;
            }

            Log("INTERRUPT suicide (owner dead) -> drop bomb");
            DropBombAndStop(data);
            return;
        }

        Log("INTERRUPT suicide -> cancel");
        CancelSuicideNoCooldown();
    }
}