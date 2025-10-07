using System.Collections;
using UnityEngine;

/// <summary>
/// EnemyAttackController
/// (설명 생략 – 기존 주석 유지)
/// B안 적용:
///  - ResetToM 트리거/AnyState 전역 복귀 제거
///  - 공격/러시 종료 시 상태가 Attack이면 SetState(Chase)만 호출 (Run 애니는 Enemy.SetState가 PlayRun 처리)
///  - 넉백/인터럽트 중 Run으로 뛰어가는 레이스 컨디션 제거
///
/// 2025-10-07 업데이트:
///  - RushAttackRoutine 에 러시 도중 목표(플레이어) 추적 보간 로직 추가
///
/// 2025-10-07 추가 수정:
///  - attackPatterns 배열에서 null / 지원하지 않는 SO 제거(CleanPatterns)
///  - 옵션1: Clean 후 패턴이 0개면 경고 로그 출력
/// </summary>
public class EnemyAttackController : MonoBehaviour
{
    [Header("패턴 배열 (MeleeAttackData / RushAttackData)")]
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

    [Header("디버그")]
    public bool debugDecisionLogs = true;

    public bool IsMeleeExecuting => attackInProgress && !IsRushing;
    public bool IsAttackExecuting => IsMeleeExecuting || IsRushing || rushPrepareCoroutine != null;

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
            return null;
        }
    }

    private void Awake()
    {
        // 1) 패턴 클린
        CleanPatterns();

        // 2) 기본 초기화
        enemy = GetComponent<Enemy>();
        int n = AttackCount;
        readyTimes = n > 0 ? new float[n] : System.Array.Empty<float>();
        for (int i = 0; i < n; i++) readyTimes[i] = -Mathf.Infinity;
        globalReadyTime = Time.time;

        // 3) 로그
        if (n == 0)
        {
            Debug.LogWarning("[EnemyAttackController] 등록된 유효 공격 패턴이 0개입니다. (공격 비활성 상태)");
        }
        Log($"INIT (validPatterns={n})");
    }

    /// <summary>
    /// null 또는 지원하지 않는 타입의 슬롯 제거
    /// </summary>
    private void CleanPatterns()
    {
        if (attackPatterns == null || attackPatterns.Length == 0) return;

        var list = new System.Collections.Generic.List<ScriptableObject>(attackPatterns.Length);
        int removedNull = 0;
        int removedUnsupported = 0;

        foreach (var p in attackPatterns)
        {
            if (p == null)
            {
                removedNull++;
                continue;
            }

            if (p is MeleeAttackData || p is RushAttackData)
            {
                list.Add(p);
            }
            else
            {
                removedUnsupported++;
                Debug.LogWarning($"[EnemyAttackController] 지원하지 않는 패턴 타입 무시: {p.GetType().Name}");
            }
        }

        if (removedNull > 0 || removedUnsupported > 0)
        {
            Debug.LogWarning($"[EnemyAttackController] 패턴 정리: null {removedNull}개, 미지원 {removedUnsupported}개 제거 → 최종 {list.Count}개");
        }

        attackPatterns = list.ToArray();
    }

    private void Update()
    {
        if (attackInProgress && !IsRushing)
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

        if (holdActive && !IsAttackExecuting && Time.time >= holdExpireTime)
        {
            Log($"HOLD TIMEOUT idx={pendingAttackIndex} -> cancel (no cooldown)");
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
        return 0f;
    }
    public float GetAttackCooldown(int index)
    {
        if (index < 0 || index >= AttackCount) return 0f;
        if (attackPatterns[index] is MeleeAttackData m) return m.cooldown;
        if (attackPatterns[index] is RushAttackData r) return r.cooldown;
        return 0f;
    }
    #endregion

    #region 선택 & 시작
    public int SelectAttackIndex(float distance)
    {
        if (IsAttackExecuting || IsGlobalCooling()) return -1;

        if (pendingAttackIndex >= 0)
        {
            if (!holdActive) return -1;
            float pr = GetAttackRange(pendingAttackIndex);
            if (distance <= pr && IsOffCooldown(pendingAttackIndex)) return pendingAttackIndex;
            return -1;
        }

        for (int i = 0; i < AttackCount; i++)
        {
            if (!IsOffCooldown(i)) continue;
            float r = GetAttackRange(i);
            PreparePending(i);
            if (distance <= r) return i;
            break;
        }
        return -1;
    }

    public bool TryStartAttack(int index, Transform target)
    {
        if (IsAttackExecuting || IsGlobalCooling() || !IsOffCooldown(index)) return false;
        if (attackPatterns == null || index < 0 || index >= attackPatterns.Length) return false;

        if (pendingAttackIndex != index) PreparePending(index);

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
        return false;
    }
    #endregion

    #region 패턴 홀드
    private void PreparePending(int index)
    {
        pendingAttackIndex = index;
        holdActive = true;
        float hold = ComputeHoldDuration(index);
        holdExpireTime = Time.time + hold;
        Log($"SELECT idx={index} hold={hold:F2}s");
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

    private void ClearHold()
    {
        if (holdActive) Log("HOLD CLEARED");
        holdActive = false;
        pendingAttackIndex = -1;
    }

    private void CancelPendingHold()
    {
        holdActive = false;
        pendingAttackIndex = -1;
    }
    #endregion

    #region Melee
    private void StartMelee(MeleeAttackData data, int index)
    {
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
            {
                if (c.name == data.attackName)
                    return c.length;
            }
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
        if (pendingAttackIndex >= 0)
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