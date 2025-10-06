using System.Collections;
using UnityEngine;

/// <summary>
/// EnemyAttackController
/// - 글로벌/개별 쿨타임
/// - 패턴 홀드(사거리 외 접근 대기)
/// - Rush 준비/실행/인터럽트
/// - Melee AttackHit 애니메이션 이벤트로 히트박스 1회 스폰
/// - ShieldBreak / Stun 중 상태 복구 가드
/// - Melee: attackTime < 클립 -> 지정 시간까지만 재생 후 즉시 종료 (애니 그대로 '잘림') / attackTime > 클립 -> 클립 끝나면 마지막 프레임 동결 후 남은 시간 기다렸다 종료
/// </summary>
public class EnemyAttackController : MonoBehaviour
{
    [Header("패턴 배열 (MeleeAttackData / RushAttackData)")]
    public ScriptableObject[] attackPatterns;
    public int AttackCount => attackPatterns != null ? attackPatterns.Length : 0;

    /* 공통 상태 */
    private ScriptableObject currentAttack;
    private int currentAttackIndex = -1;

    /* Melee */
    private bool attackInProgress = false;
    private float attackEndTime;
    private bool meleeHitboxSpawned = false;

    // ── 추가: Melee 타이밍 제어 변수 ──
    private float meleeRequestedDuration;
    private float meleeClipLength;
    private float meleeFreezeStartTime;
    private bool meleeWillFreeze;      // 클립보다 길어서 프리즈 예정인지
    private bool meleeFrozenApplied;   // speed=0 적용 여부

    /* Rush */
    public bool IsRushing { get; private set; } = false;
    private Coroutine rushPrepareCoroutine;
    private Coroutine rushCoroutine;
    private GameObject spawnedRushHitbox;
    private int runningRushIndex = -1;
    private Transform rushTarget;

    /* 참조 */
    private Enemy enemy;

    /* 개별 쿨타임 */
    private float[] readyTimes;

    /* 글로벌 쿨타임 */
    [Header("글로벌쿨타임 (성공 종료 후)")]
    public float 글로벌쿨타임 = 0.35f;
    private float globalReadyTime;

    /* 패턴 홀드 */
    [Header("패턴 홀드")]
    public float defaultPatternHoldDuration = 1.0f;
    public bool enablePerPatternHoldOverride = true;

    private bool holdActive = false;
    private float holdExpireTime;
    private int pendingAttackIndex = -1;

    /* 디버그 */
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
        enemy = GetComponent<Enemy>();
        int n = AttackCount;
        readyTimes = n > 0 ? new float[n] : System.Array.Empty<float>();
        for (int i = 0; i < n; i++) readyTimes[i] = -Mathf.Infinity;
        globalReadyTime = Time.time;
        Log("INIT");
    }

    private void Update()
    {
        // ───────── Melee 진행/프리즈 처리 ─────────
        if (attackInProgress && !IsRushing)
        {
            // 프리즈 조건: (요청시간 > 클립길이) && 아직 프리즈 안 했고 && 클립 재생 구간 지나감
            if (meleeWillFreeze && !meleeFrozenApplied && Time.time >= meleeFreezeStartTime)
            {
                if (enemy?.animator != null)
                {
                    enemy.animator.speed = 0f; // 마지막 프레임 고정
                }
                meleeFrozenApplied = true;
            }

            // 요청 시간이 다 되면 종료
            if (Time.time >= attackEndTime)
            {
                FinishMelee(true);
            }
        }

        // 패턴 홀드 만료
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

        // 요청 시간
        meleeRequestedDuration = data.attackTime > 0f ? data.attackTime : 0.8f;

        // 실제 클립 길이
        meleeClipLength = GetMeleeClipLength(data);

        // 분기 설정
        if (meleeRequestedDuration > meleeClipLength)
        {
            // 클립 재생 후 남은 시간 프리즈
            meleeWillFreeze = true;
            meleeFreezeStartTime = Time.time + meleeClipLength;
            attackEndTime = Time.time + meleeRequestedDuration;
        }
        else
        {
            // 요청 시간이 더 짧음: 그 시점에서 바로 종료 (클립은 잘리게 됨)
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
            // 속도는 항상 1 (요구사항: 속도 조절 X)
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
        // 찾지 못하면 요청 시간 사용 (혹은 기본값)
        return data.attackTime > 0f ? data.attackTime : 0.8f;
    }

    private void FinishMelee(bool success)
    {
        // 항상 speed 복구 (프리즈 상태에서 종료 가능)
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

            if (enemy.animator && !IsHardCrowdControlled())
                enemy.animator.SetTrigger("ResetToM");
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

        while (elapsed < data.rushTime)
        {
            transform.position += rushDir.normalized * data.rushSpeed * Time.deltaTime;

            if (enemy.CurrentState != Enemy.EnemyState.Attack ||
                enemy.CurrentState == Enemy.EnemyState.ShieldBreak)
            {
                Log("RUSH INTERRUPT noCooldown");
                StopRushCoroutines();
                IsRushing = false;
                CancelRushNoCooldown();
                yield break;
            }

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
            enemy.animator.SetTrigger("ResetToM");
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
            enemy.animator.SetTrigger("ResetToM");
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
                enemy.animator.SetTrigger("ResetToM");
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