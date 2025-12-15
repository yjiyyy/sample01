using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAttackController : MonoBehaviour
{
    [Header("패턴 배열 (MeleeAttackData / RushAttackData / RangedAttackData)")]
    public ScriptableObject[] attackPatterns;
    public int AttackCount => attackPatterns != null ? attackPatterns.Length : 0;

    private ScriptableObject currentAttack;
    private int currentAttackIndex = -1;

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

    /* Rush */
    public bool IsRushing { get; private set; } = false;
    private Coroutine rushPrepareCoroutine;
    private Coroutine rushCoroutine;
    private GameObject spawnedRushHitbox;
    private int runningRushIndex = -1;
    private Transform rushTarget;
    // 마지막 돌진 방향(마무리 감속에 사용)
    private Vector3 lastRushDir = Vector3.forward;

    /* Ranged */
    private Coroutine rangedRoutine;
    private Transform rangedTarget;
    private int runningRangedIndex = -1;
    private bool rangedProjectileFired = false;

    private Enemy enemy;

    private float[] readyTimes;

    [Header("글로벌쿨타임 (성공 종료 후)")]
    public float 글로벌쿨타임 = 0.35f;
    private float globalReadyTime;

    [Header("패턴 홀드")]
    public float defaultPatternHoldDuration = 1.0f;

    private bool holdActive = false;
    private float holdExpireTime;
    private int pendingAttackIndex = -1;
    private bool pendingExecuted = false;

    [Header("디버그")]
    public bool debugDecisionLogs = true;

    public bool IsMeleeExecuting => attackInProgress && !IsRushing && rangedRoutine == null;
    public bool IsAttackExecuting => IsMeleeExecuting || IsRushing || rushPrepareCoroutine != null || rangedRoutine != null;

    public string CurrentAttackName
    {
        get
        {
            if (currentAttack is MeleeAttackData m) return m.attackName;
            if (IsRushing &&
                runningRushIndex >= 0 &&
                attackPatterns != null &&
                runningRushIndex < attackPatterns.Length &&
                attackPatterns[runningRushIndex] is RushAttackData r) return r.attackName;
            if (rangedRoutine != null &&
                runningRangedIndex >= 0 &&
                attackPatterns != null &&
                runningRangedIndex < attackPatterns.Length &&
                attackPatterns[runningRangedIndex] is RangedAttackData ra) return ra.attackName;
            return null;
        }
    }

    private void Awake()
    {
        CleanPatterns();

        enemy = GetComponent<Enemy>();
        int n = AttackCount;
        readyTimes = n > 0 ? new float[n] : System.Array.Empty<float>();
        for (int i = 0; i < n; i++) readyTimes[i] = -Mathf.Infinity;
        globalReadyTime = Time.time;

        if (n == 0)
            Debug.LogWarning("[EnemyAttackController] 등록된 유효 공격 패턴이 0개입니다. (공격 비활성 상태)");

        Log($"INIT (validPatterns={n})");
    }

    /// <summary> null / 미지원 타입 제거 </summary>
    private void CleanPatterns()
    {
        if (attackPatterns == null || attackPatterns.Length == 0) return;

        var list = new List<ScriptableObject>(attackPatterns.Length);
        int removedNull = 0;
        int removedUnsupported = 0;

        foreach (var p in attackPatterns)
        {
            if (p == null) { removedNull++; continue; }
            if (p is MeleeAttackData || p is RushAttackData || p is RangedAttackData) list.Add(p);
            else
            {
                removedUnsupported++;
                Debug.LogWarning($"[EnemyAttackController] 지원하지 않는 패턴 타입 무시: {p.GetType().Name}");
            }
        }

        if (removedNull > 0 || removedUnsupported > 0)
            Debug.LogWarning($"[EnemyAttackController] 패턴 정리: null {removedNull}개, 미지원 {removedUnsupported}개 제거 → 최종 {list.Count}개");

        attackPatterns = list.ToArray();
    }

    private void Update()
    {
        // Melee 진행 & 프리즈 처리
        if (attackInProgress && !IsRushing && rangedRoutine == null)
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

        // Hold 만료
        if (holdActive && !IsAttackExecuting && !pendingExecuted && Time.time >= holdExpireTime)
        {
            Log($"[AttackFlow] HOLD TIMEOUT idx={pendingAttackIndex}");
            CancelPendingHold();
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

    #region 외부 조회
    public bool IsGlobalCooling() => Time.time < globalReadyTime;
    public bool IsOffCooldown(int index)
    {
        if (index < 0 || index >= AttackCount) return false;
        return Time.time >= readyTimes[index];
    }
    public float GetAttackRange(int index)
    {
        if (index < 0 || index >= AttackCount) return 0f;
        if (attackPatterns[index] is MeleeAttackData m) return m.range;
        if (attackPatterns[index] is RushAttackData r) return r.range;
        if (attackPatterns[index] is RangedAttackData rg) return rg.range;
        return 0f;
    }
    public float GetAttackCooldown(int index)
    {
        if (index < 0 || index >= AttackCount) return 0f;
        if (attackPatterns[index] is MeleeAttackData m) return m.cooldown;
        if (attackPatterns[index] is RushAttackData r) return r.cooldown;
        if (attackPatterns[index] is RangedAttackData rg) return rg.cooldown;
        return 0f;
    }
    #endregion

    #region 선택 & 시작 (랜덤 + 실행 플래그)
    public int SelectAttackIndex(float distance)
    {
        if (IsAttackExecuting || IsGlobalCooling()) return -1;

        if (pendingAttackIndex >= 0 && holdActive && !pendingExecuted)
        {
            float pr = GetAttackRange(pendingAttackIndex);
            if (distance <= pr && IsOffCooldown(pendingAttackIndex))
                return pendingAttackIndex;
            return -1;
        }

        List<int> candidates = null;
        for (int i = 0; i < AttackCount; i++)
        {
            if (!IsOffCooldown(i)) continue;
            (candidates ??= new List<int>()).Add(i);
        }

        if (candidates == null || candidates.Count == 0)
            return -1;

        int chosen = candidates[Random.Range(0, candidates.Count)];
        PreparePending(chosen);

        float range = GetAttackRange(chosen);
        if (distance <= range)
            return chosen;

        return -1;
    }

    public bool TryStartAttack(int index, Transform target)
    {
        if (IsAttackExecuting || IsGlobalCooling() || !IsOffCooldown(index)) return false;
        if (attackPatterns == null || index < 0 || index >= attackPatterns.Length) return false;

        if (pendingAttackIndex != index)
            PreparePending(index);

        var so = attackPatterns[index];
        if (so is MeleeAttackData m)
        {
            StartMelee(m, index);
            return true;
        }
        if (so is RushAttackData r)
        {
            StartRush(r, target, index);
            return true;
        }
        if (so is RangedAttackData rg)
        {
            StartRanged(rg, target, index);
            return true;
        }
        return false;
    }
    #endregion

    #region 패턴 홀드 / 실행 플래그
    private void PreparePending(int index)
    {
        pendingAttackIndex = index;
        holdActive = true;
        pendingExecuted = false;

        float hold = defaultPatternHoldDuration;
        holdExpireTime = Time.time + hold;
        Log($"[AttackFlow] SELECT idx={index} hold={hold:F2}s");
    }

    private void MarkExecuted()
    {
        if (pendingExecuted) return;
        pendingExecuted = true;
        Log($"[AttackFlow] EXECUTE idx={pendingAttackIndex}");
    }

    private void ClearHold()
    {
        if (holdActive)
            Log("HOLD CLEARED");
        holdActive = false;
        pendingAttackIndex = -1;
        pendingExecuted = false;
    }

    private void CancelPendingHold()
    {
        holdActive = false;
        pendingAttackIndex = -1;
        pendingExecuted = false;
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

    #region Rush
    private void StartRush(RushAttackData data, Transform target, int index)
    {
        MarkExecuted();
        ClearHold();

        StopRushCoroutines();
        runningRushIndex = index;
        rushTarget = target;

        enemy.SetState(Enemy.EnemyState.Attack);
        if (data.grantSuperArmor) enemy.AddSuperArmor(SuperArmorSource.Attack);
        else enemy.RemoveSuperArmor(SuperArmorSource.Attack);

        rushPrepareCoroutine = StartCoroutine(RushPrepareRoutine(data));
        Log($"RUSH PREPARE START idx={index} prep={data.prepareDuration:F2}");
    }

    private IEnumerator RushPrepareRoutine(RushAttackData data)
    {
        if (enemy.animator)
        {
            // 파라미터가 없어도 Play만으로 동작하도록
            if (data.prepareClip != null)
            {
                enemy.animator.speed = 1f;
                enemy.animator.Play(data.prepareClip.name, 0, 0f);
            }
            else
            {
                // 클립 미지정 시 폴백(있으면): "RushPrepare"
                SafeSetBool("IsRushPrepare", true);
                SafeSetBool("IsRush", false);
                enemy.animator.Play("RushPrepare");
            }
        }

        float elapsed = 0f;
        while (elapsed < data.prepareDuration)
        {
            if (rushTarget != null)
            {
                Vector3 dir = rushTarget.position - transform.position;
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
                Log("RUSH PREPARE INTERRUPT noCooldown");
                CancelRushNoCooldown();
                yield break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        rushPrepareCoroutine = null;
        rushCoroutine = StartCoroutine(RushAttackRoutine(data));
    }

    private IEnumerator RushAttackRoutine(RushAttackData data)
    {
        IsRushing = true;

        if (enemy.animator)
        {
            // 공격 클립 우선, 없으면 attackName, 그도 없으면 "Rush"
            enemy.animator.speed = 1f;
            if (data.attackClip != null)
                enemy.animator.Play(data.attackClip.name, 0, 0f);
            else if (!string.IsNullOrEmpty(data.attackName))
                enemy.animator.Play(data.attackName, 0, 0f);
            else
                enemy.animator.Play("Rush", 0, 0f);
        }

        SpawnRushHitbox(data);

        float elapsed = 0f;
        // 초기 돌진 방향
        Vector3 rushDir = transform.forward;
        rushDir.y = 0f;
        if (rushDir.sqrMagnitude < 0.0001f) rushDir = Vector3.forward;

        bool useDeviation = false;
        float baseWeight = 0f;
        if (data != null)
        {
            useDeviation = data.allowDirectionDeviation;
            baseWeight = Mathf.Clamp01(data.directionDeviationAmount);
        }

        // FixedUpdate 기반 이동(플랫폼/프레임 독립)
        while (elapsed < data.attackDuration)
        {
            if (enemy.CurrentState != Enemy.EnemyState.Attack ||
                enemy.CurrentState == Enemy.EnemyState.ShieldBreak)
            {
                Log("RUSH INTERRUPT noCooldown");
                StopRushCoroutines();
                IsRushing = false;
                CancelRushNoCooldown();
                yield break;
            }

            if (useDeviation && baseWeight > 0f && rushTarget != null)
            {
                Vector3 desired = rushTarget.position - transform.position;
                desired.y = 0f;
                if (desired.sqrMagnitude > 0.0001f)
                {
                    desired.Normalize();
                    // 고정 프레임 기반 가중치
                    float dtWeight = 1f - Mathf.Pow(1f - baseWeight, Time.fixedDeltaTime * 60f);
                    rushDir = Vector3.Slerp(rushDir, desired, dtWeight).normalized;

                    if (rushDir.sqrMagnitude > 0.0001f)
                        transform.rotation = Quaternion.LookRotation(rushDir);
                }
            }

            Vector3 disp = rushDir * data.rushSpeed * Time.fixedDeltaTime;
            enemy.MoveFilteredDisplacement(disp);

            elapsed += Time.fixedDeltaTime;
            lastRushDir = rushDir;
            yield return new WaitForFixedUpdate();
        }

        // 공격 구간 종료 → 마무리 감속으로 넘어감 (히트박스는 공격 구간까지만)
        DespawnRushHitbox();

        // 마무리 루틴 실행(계속 IsRushing 유지)
        rushCoroutine = StartCoroutine(RushFinishRoutine(data, lastRushDir));
    }

    private IEnumerator RushFinishRoutine(RushAttackData data, Vector3 dir)
    {
        // 마무리 클립(선택) 재생
        if (enemy.animator && data.finishClip != null)
        {
            enemy.animator.speed = 1f;
            enemy.animator.Play(data.finishClip.name, 0, 0f);
        }

        float dur = Mathf.Max(0f, data.finishDuration);
        float elapsed = 0f;

        // 선형 감속: rushSpeed → 0
        float initialSpeed = Mathf.Max(0f, data.rushSpeed);

        Vector3 finishDir = dir;
        finishDir.y = 0f;
        if (finishDir.sqrMagnitude < 0.0001f) finishDir = transform.forward;
        finishDir.Normalize();

        while (elapsed < dur)
        {
            if (enemy.CurrentState != Enemy.EnemyState.Attack ||
                enemy.CurrentState == Enemy.EnemyState.ShieldBreak)
            {
                Log("RUSH FINISH INTERRUPT noCooldown");
                StopRushCoroutines();
                IsRushing = false;
                CancelRushNoCooldown();
                yield break;
            }

            float t = Mathf.Clamp01(elapsed / dur);
            float currentSpeed = initialSpeed * (1f - t);
            Vector3 disp = finishDir * currentSpeed * Time.fixedDeltaTime;

            // 마무리 중에는 방향 보정 없이 감속만
            enemy.MoveFilteredDisplacement(disp);

            // 시선은 마지막 방향 유지
            if (finishDir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(finishDir);

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        IsRushing = false;
        FinishRush(data, true);
    }

    private void FinishRush(RushAttackData data, bool success)
    {
        if (success)
        {
            ApplyPerAttackCooldown(runningRushIndex, data.cooldown);
            ApplyGlobalCooldown();
            Log($"RUSH END SUCCESS idx={runningRushIndex}");
        }
        else
        {
            Log($"RUSH END CANCEL idx={runningRushIndex}");
        }

        enemy.RemoveSuperArmor(SuperArmorSource.Attack);

        if (enemy.animator && !IsHardCrowdControlled())
        {
            SafeSetBool("IsRush", false);
            SafeSetBool("IsRushPrepare", false);
        }

        runningRushIndex = -1;

        if (enemy.CurrentState == Enemy.EnemyState.Attack && !IsHardCrowdControlled())
            enemy.SetState(Enemy.EnemyState.Chase);
    }

    private void CancelRushNoCooldown()
    {
        enemy.RemoveSuperArmor(SuperArmorSource.Attack);
        if (enemy.animator && !IsHardCrowdControlled())
        {
            SafeSetBool("IsRush", false);
            SafeSetBool("IsRushPrepare", false);
        }
        DespawnRushHitbox();
        runningRushIndex = -1;

        if (enemy.CurrentState == Enemy.EnemyState.Attack && !IsHardCrowdControlled())
            enemy.SetState(Enemy.EnemyState.Chase);
    }

    private void StopRushCoroutines()
    {
        if (rushPrepareCoroutine != null) StopCoroutine(rushPrepareCoroutine);
        if (rushCoroutine != null) StopCoroutine(rushCoroutine);
        rushPrepareCoroutine = null;
        rushCoroutine = null;
    }

    private void SpawnRushHitbox(RushAttackData data)
    {
        if (data.hitBoxPrefab == null) return;
        if (spawnedRushHitbox != null) return;

        spawnedRushHitbox = Instantiate(data.hitBoxPrefab, transform.position, transform.rotation, transform);

        if (spawnedRushHitbox.TryGetComponent<HitBox_Enemy>(out var hb))
        {
            float life = data.hitBoxLifetime > 0f ? data.hitBoxLifetime : data.attackDuration;
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

    private void DespawnRushHitbox()
    {
        if (spawnedRushHitbox != null)
            Destroy(spawnedRushHitbox);
        spawnedRushHitbox = null;
    }
    #endregion

    #region Ranged
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
        // (기존 로직 유지)
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

        Log($"RANGED FIRE proj@{firePoint.name} → target {targetPos} type={data.movementType}");
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
    #endregion

    #region 쿨타임 & 인터럽트
    private void ApplyPerAttackCooldown(int index, float baseCooldown)
    {
        if (index < 0 || index >= AttackCount) return;
        readyTimes[index] = Time.time + Mathf.Max(0f, baseCooldown);
    }
    private void ApplyGlobalCooldown() => globalReadyTime = Time.time + 글로벌쿨타임;

    public void OnInterrupted()
    {
        if (attackInProgress)
        {
            Log("INTERRUPT melee -> cancel");
            FinishMelee(false);
        }
        if (rushPrepareCoroutine != null || IsRushing)
        {
            Log("INTERRUPT rush -> cancel");
            StopRushCoroutines();
            IsRushing = false;
            CancelRushNoCooldown();
        }
        if (rangedRoutine != null)
        {
            Log("INTERRUPT ranged -> cancel");
            CancelRangedNoCooldown();
        }
        if (pendingAttackIndex >= 0 && !pendingExecuted)
        {
            Log("INTERRUPT pending cleared");
            CancelPendingHold();
        }
    }
    public void InterruptCooldown() => OnInterrupted();

    public void StopRushExternally(bool noCooldown)
    {
        if (!(IsRushing || rushPrepareCoroutine != null)) return;

        RushAttackData data = null;
        if (runningRushIndex >= 0 &&
            attackPatterns != null &&
            runningRushIndex < attackPatterns.Length)
            data = attackPatterns[runningRushIndex] as RushAttackData;

        Log(noCooldown ? "Rush External stop noCooldown" : "Rush External stop applyCooldown");
        StopRushCoroutines();
        IsRushing = false;

        if (noCooldown)
        {
            CancelRushNoCooldown();
        }
        else
        {
            if (data != null)
            {
                ApplyPerAttackCooldown(runningRushIndex, data.cooldown);
                ApplyGlobalCooldown();
            }
            enemy.RemoveSuperArmor(SuperArmorSource.Attack);
            if (enemy.animator && !IsHardCrowdControlled())
            {
                SafeSetBool("IsRush", false);
                SafeSetBool("IsRushPrepare", false);
            }
            runningRushIndex = -1;
            if (enemy.CurrentState == Enemy.EnemyState.Attack && !IsHardCrowdControlled())
                enemy.SetState(Enemy.EnemyState.Chase);
        }
    }
    #endregion

    #region 유틸
    private bool IsHardCrowdControlled()
    {
        return enemy != null &&
               (enemy.CurrentState == Enemy.EnemyState.Stunned ||
                enemy.CurrentState == Enemy.EnemyState.ShieldBreak);
    }

    private void FaceTarget(Transform t)
    {
        if (t == null) return;
        if (enemy != null && enemy.IsLookLocked) return;

        Vector3 dir = t.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    private Transform FindChildRecursive(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        foreach (var tr in root.GetComponentsInChildren<Transform>(true))
        {
            if (tr == null) continue;
            if (tr.name == name) return tr;
        }
        return null;
    }

    private bool HasParam(string p)
    {
        if (enemy?.animator == null) return false;
        foreach (var prm in enemy.animator.parameters)
            if (prm.name == p) return true;
        return false;
    }

    private void SafeSetBool(string p, bool v)
    {
        if (HasParam(p)) enemy.animator.SetBool(p, v);
    }

    private void Log(string msg)
    {
#if UNITY_EDITOR
        if (debugDecisionLogs) Debug.Log($"[EnemyAttackController] {msg}", this);
#else
        if (debugDecisionLogs) Debug.Log($"[EnemyAttackController] {msg}");
#endif
    }
    #endregion
}