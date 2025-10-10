using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EnemyAttackController
/// 실행(Executed) 개념 추가:
///  - 패턴이 선택(SELECT)되면 holdActive = true, holdExpireTime = now + holdDuration
///  - StartMelee / StartRush / StartRanged 진입 순간 MarkExecuted() → pendingExecuted = true
/// 랜덤 선택:
///  - 모든 Ready 후보 수집 후 균등 랜덤
/// 쿨다운:
///  - 성공 종료 시 개별+글로벌 적용
/// </summary>
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

    /* Rush */
    public bool IsRushing { get; private set; } = false;
    private Coroutine rushPrepareCoroutine;
    private Coroutine rushCoroutine;
    private GameObject spawnedRushHitbox;
    private int runningRushIndex = -1;
    private Transform rushTarget;

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
    public bool enablePerPatternHoldOverride = true;

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

        // Hold 만료 (실행 안됐고, 공격 수행 중 아님)
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

    private void SpawnMeleeHitbox(MeleeAttackData data)
    {
        if (data.hitBoxPrefab == null)
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

        // 기존 hold 유지 중 (아직 실행 안됨)
        if (pendingAttackIndex >= 0 && holdActive && !pendingExecuted)
        {
            float pr = GetAttackRange(pendingAttackIndex);
            if (distance <= pr && IsOffCooldown(pendingAttackIndex))
                return pendingAttackIndex;
            return -1; // 사거리 밖 → 계속 추격
        }

        // 새로운 선택 (Ready 후보 수집)
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

        // 다른 패턴이 갑자기 사거리 안에서 직접 호출될 수 있으므로 pending 동기화
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

        float hold = ComputeHoldDuration(index);
        holdExpireTime = Time.time + hold;
        Log($"[AttackFlow] SELECT idx={index} hold={hold:F2}s");
    }

    private float ComputeHoldDuration(int index)
    {
        if (!enablePerPatternHoldOverride) return defaultPatternHoldDuration;
        if (attackPatterns == null || index < 0 || index >= attackPatterns.Length) return defaultPatternHoldDuration;

        var so = attackPatterns[index];
        var f = so.GetType().GetField("holdOverride");
        if (f != null && f.FieldType == typeof(float))
        {
            float ov = (float)f.GetValue(so);
            if (ov > 0f) return ov;
        }
        return defaultPatternHoldDuration;
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
        pendingExecuted = false; // 다음 사이클 초기화
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
        // 실행 순간 표시 (Hold 성공 판정)
        MarkExecuted();
        ClearHold();

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
            enemy.animator.Play(data.attackName, 0, 0f);
        }

        Log($"MELEE START idx={index} req={meleeRequestedDuration:F3}s clip={meleeClipLength:F3}s freeze={(meleeWillFreeze ? "Y" : "N")}");
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
    }
    #endregion

    #region Rush
    private void StartRush(RushAttackData data, Transform target, int index)
    {
        // 실행 표시
        MarkExecuted();
        ClearHold();

        StopRushCoroutines();
        runningRushIndex = index;
        rushTarget = target;

        enemy.SetState(Enemy.EnemyState.Attack);
        if (data.grantSuperArmor) enemy.AddSuperArmor(SuperArmorSource.Attack);
        else enemy.RemoveSuperArmor(SuperArmorSource.Attack);

        rushPrepareCoroutine = StartCoroutine(RushPrepareRoutine(data));
        Log($"RUSH PREPARE START idx={index} prep={data.prepareTime:F2}");
    }

    private IEnumerator RushPrepareRoutine(RushAttackData data)
    {
        if (enemy.animator)
        {
            SafeSetBool("IsRushPrepare", true);
            SafeSetBool("IsRush", false);
            enemy.animator.Play("RushPrepare");
        }

        float elapsed = 0f;
        while (elapsed < data.prepareTime)
        {
            if (rushTarget != null)
            {
                Vector3 dir = rushTarget.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(dir.normalized);
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
            SafeSetBool("IsRushPrepare", false);
            SafeSetBool("IsRush", true);
            enemy.animator.Play("Rush");
        }
        if (enemy.agent && enemy.agent.isOnNavMesh)
        {
            enemy.agent.isStopped = true;
            enemy.agent.velocity = Vector3.zero;
            enemy.agent.ResetPath();
        }

        SpawnRushHitbox(data);

        float elapsed = 0f;
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

        while (elapsed < data.rushTime)
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
                    float dtWeight = 1f - Mathf.Pow(1f - baseWeight, Time.deltaTime * 60f);
                    rushDir = Vector3.Slerp(rushDir, desired, dtWeight).normalized;

                    if (rushDir.sqrMagnitude > 0.0001f)
                        transform.rotation = Quaternion.LookRotation(rushDir);
                }
            }

            transform.position += rushDir * data.rushSpeed * Time.deltaTime;

            elapsed += Time.deltaTime;
            yield return null;
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
        DespawnRushHitbox();

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
            float life = data.hitBoxLifetime > 0f ? data.hitBoxLifetime : data.rushTime;
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
        // 실행 표시
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
                    enemy.animator.speed = 0f; // 마지막 프레임 고정
                    freezed = true;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (enemy.animator) enemy.animator.speed = 1f; // 복구
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
                enemy.animator.speed = 0f; // 마지막 프레임 고정
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

        // firePointName 사용 (없으면 자기 transform)
        Transform firePoint = FindChildRecursive(transform, data.firePointName);
        if (firePoint == null) firePoint = transform;

        // 발사 순간 타겟 위치 스냅
        Vector3 targetPos = rangedTarget != null ? rangedTarget.position : (firePoint.position + transform.forward * 5f);

        // 직선 기준 방향(수평)
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

    private void FaceTarget(Transform t)
    {
        if (t == null) return;
        Vector3 dir = t.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    private Transform FindChildRecursive(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name) return t;
        }
        return null;
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