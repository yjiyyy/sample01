using System.Collections;
using UnityEngine;

/// <summary>
/// EnemyAttackController (통합 개선 버전)
/// - 글로벌쿨타임
/// - 패턴 홀드
/// - Rush prepare = 실행 간주
/// - Interrupt / ShieldBreak 시 노쿨 취소
/// - AnimationEvent 'AttackHit'로 Melee 힛박스 스폰
/// - 호환 메서드: IsGlobalCooling, StopRushExternally, InterruptCooldown
/// </summary>
public class EnemyAttackController : MonoBehaviour
{
    [Header("패턴 배열 (MeleeAttackData / RushAttackData)")]
    public ScriptableObject[] attackPatterns;
    public int AttackCount => attackPatterns != null ? attackPatterns.Length : 0;

    /* ====== 공통 진행 상태 ====== */
    private ScriptableObject currentAttack;
    private int currentAttackIndex = -1;

    /* ====== Melee 진행 ====== */
    private bool attackInProgress = false;
    private float attackStartTime;
    private float attackEndTime;
    private float effectiveAttackDuration;
    private bool meleeHitboxSpawned = false;   // AnimationEvent 기반 1회 스폰 제어

    /* ====== Rush 진행 ====== */
    public bool IsRushing { get; private set; } = false;
    private Coroutine rushPrepareCoroutine;
    private Coroutine rushCoroutine;
    private GameObject spawnedRushHitbox;
    private int runningRushIndex = -1;
    private Transform rushTarget;

    /* ====== 참조 ====== */
    private Enemy enemy;

    /* ====== Per-Attack Cooldowns ====== */
    private float[] readyTimes;

    /* ====== 글로벌 쿨타임 ====== */
    [Header("글로벌쿨타임 (성공 종료 후)")]
    public float 글로벌쿨타임 = 0.35f;
    private float globalReadyTime = 0f;

    /* ====== 패턴 홀드 ====== */
    [Header("패턴 홀드")]
    public float defaultPatternHoldDuration = 1.0f;
    public bool enablePerPatternHoldOverride = true;

    private bool holdActive = false;
    private float holdExpireTime = 0f;
    private int pendingAttackIndex = -1;
    private float pendingSelectTime = 0f;

    /* ====== 디버그 ====== */
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
        for (int i = 0; i < n; i++)
            readyTimes[i] = -Mathf.Infinity;

        globalReadyTime = Time.time;
        Log("[INIT]");
    }

    private void Update()
    {
        if (attackInProgress && !IsRushing)
        {
            if (Time.time >= attackEndTime)
            {
                FinishMelee(success: true);
            }
        }

        if (holdActive && !IsAttackExecuting && Time.time >= holdExpireTime)
        {
            Log($"[HOLD TIMEOUT] idx={pendingAttackIndex} -> cancel (no cooldown)");
            CancelPendingHold();
        }
    }

    #region AnimationEvent (Melee 히트)
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
            Log("[MELEE HITBOX] prefab null");
            return;
        }

        GameObject go = Instantiate(
            data.hitBoxPrefab,
            transform.position,
            transform.rotation,
            transform
        );
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

        Log("[MELEE HITBOX] spawned");
    }
    #endregion

    #region 공개 / AI 연동
    public bool IsGlobalCooling() => Time.time < globalReadyTime;
    public float GetGlobalCooldownRemaining() => Mathf.Max(0f, globalReadyTime - Time.time);

    public bool IsOffCooldown(int index)
    {
        if (index < 0 || index >= AttackCount) return false;
        return Time.time >= readyTimes[index];
    }

    public float GetAttackRange(int index)
    {
        if (index < 0 || index >= AttackCount) return 0f;
        var so = attackPatterns[index];
        if (so is MeleeAttackData m) return m.range;
        if (so is RushAttackData r) return r.range;
        return 0f;
    }

    public float GetAttackCooldown(int index)
    {
        if (index < 0 || index >= AttackCount) return 0f;
        var so = attackPatterns[index];
        if (so is MeleeAttackData m) return m.cooldown;
        if (so is RushAttackData r) return r.cooldown;
        return 0f;
    }

    public int SelectAttackIndex(float distance)
    {
        if (IsAttackExecuting) return -1;
        if (IsGlobalCooling()) return -1;

        if (pendingAttackIndex >= 0)
        {
            if (!holdActive) return -1;
            float range = GetAttackRange(pendingAttackIndex);
            if (distance <= range && IsOffCooldown(pendingAttackIndex))
                return pendingAttackIndex;
            return -1;
        }

        for (int i = 0; i < AttackCount; i++)
        {
            if (!IsOffCooldown(i)) continue;
            float range = GetAttackRange(i);

            if (distance <= range)
            {
                PreparePending(i);
                return i;
            }
            else
            {
                PreparePending(i);
                break;
            }
        }
        return -1;
    }

    public bool TryStartAttack(int index, Transform target)
    {
        if (IsAttackExecuting) return false;
        if (IsGlobalCooling()) return false;
        if (!IsOffCooldown(index)) return false;

        if (pendingAttackIndex != index)
            PreparePending(index);

        if (attackPatterns == null || index < 0 || index >= attackPatterns.Length)
            return false;

        var so = attackPatterns[index];
        if (so is MeleeAttackData m)
        {
            StartMelee(m, index);
            return true;
        }
        else if (so is RushAttackData r)
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
        pendingSelectTime = Time.time;
        float hold = ComputeHoldDuration(index);
        holdActive = true;
        holdExpireTime = Time.time + hold;
        Log($"[SELECT] idx={index} hold={hold:F2}s");
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
        if (holdActive) Log("[HOLD CLEARED]");
        holdActive = false;
        pendingAttackIndex = -1;
        pendingSelectTime = 0f;
    }

    private void CancelPendingHold()
    {
        holdActive = false;
        pendingAttackIndex = -1;
        pendingSelectTime = 0f;
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
        attackStartTime = Time.time;
        effectiveAttackDuration = data.attackTime > 0f ? data.attackTime : 0.8f;
        attackEndTime = attackStartTime + effectiveAttackDuration;

        enemy.SetState(Enemy.EnemyState.Attack);

        if (data.grantSuperArmor) enemy.AddSuperArmor(SuperArmorSource.Attack);
        else enemy.RemoveSuperArmor(SuperArmorSource.Attack);

        if (enemy.animator)
        {
            enemy.animator.SetBool("IsRush", false);
            enemy.animator.SetBool("IsRushPrepare", false);
            enemy.animator.Play(data.attackName);
        }

        Log($"[MELEE START] idx={index}");
    }

    private void FinishMelee(bool success)
    {
        attackInProgress = false;

        if (currentAttack is MeleeAttackData data)
        {
            if (enemy.animator) enemy.animator.SetTrigger("ResetToM");
            enemy.RemoveSuperArmor(SuperArmorSource.Attack);

            if (success)
            {
                ApplyPerAttackCooldown(currentAttackIndex, data.cooldown);
                ApplyGlobalCooldown();
                Log($"[MELEE END SUCCESS] idx={currentAttackIndex}");
            }
            else
            {
                Log($"[MELEE END CANCEL] idx={currentAttackIndex} (no cooldown)");
            }
        }

        currentAttack = null;
        currentAttackIndex = -1;
        meleeHitboxSpawned = false;

        if (enemy.CurrentState == Enemy.EnemyState.Attack)
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
        Log($"[RUSH PREPARE START] idx={index} prep={data.prepareTime:F2}");
    }

    private IEnumerator RushPrepareRoutine(RushAttackData data)
    {
        if (enemy.animator)
        {
            enemy.animator.SetBool("IsRushPrepare", true);
            enemy.animator.SetBool("IsRush", false);
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
                Log("[RUSH PREPARE INTERRUPT] cancel(no cooldown)");
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
            enemy.animator.SetBool("IsRushPrepare", false);
            enemy.animator.SetBool("IsRush", true);
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
                Log("[RUSH INTERRUPT] cancel(no cooldown)");
                StopRushCoroutines();
                IsRushing = false;
                CancelRushNoCooldown();
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        IsRushing = false;
        FinishRush(data, success: true);
    }

    private void FinishRush(RushAttackData data, bool success)
    {
        if (enemy.animator)
        {
            enemy.animator.SetBool("IsRush", false);
            enemy.animator.SetBool("IsRushPrepare", false);
            enemy.animator.SetTrigger("ResetToM");
        }

        DespawnRushHitbox();

        if (success)
        {
            ApplyPerAttackCooldown(runningRushIndex, data.cooldown);
            ApplyGlobalCooldown();
            Log($"[RUSH END SUCCESS] idx={runningRushIndex}");
        }
        else
        {
            Log($"[RUSH END CANCEL] idx={runningRushIndex}");
        }

        enemy.RemoveSuperArmor(SuperArmorSource.Attack);
        runningRushIndex = -1;

        if (enemy.CurrentState == Enemy.EnemyState.Attack)
            enemy.SetState(Enemy.EnemyState.Chase);
    }

    private void CancelRushNoCooldown()
    {
        if (enemy.animator)
        {
            enemy.animator.SetBool("IsRush", false);
            enemy.animator.SetBool("IsRushPrepare", false);
            enemy.animator.SetTrigger("ResetToM");
        }
        DespawnRushHitbox();
        enemy.RemoveSuperArmor(SuperArmorSource.Attack);
        runningRushIndex = -1;
        if (enemy.CurrentState == Enemy.EnemyState.Attack)
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
        if (data.hitboxPrefab == null) return;
        if (spawnedRushHitbox != null) return;

        spawnedRushHitbox = Instantiate(data.hitboxPrefab, transform.position, transform.rotation, transform);
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
        {
            Destroy(spawnedRushHitbox);
            spawnedRushHitbox = null;
        }
    }
    #endregion

    #region 쿨타임
    private void ApplyPerAttackCooldown(int index, float baseCooldown)
    {
        if (index < 0 || index >= AttackCount) return;
        readyTimes[index] = Time.time + Mathf.Max(0f, baseCooldown);
    }

    private void ApplyGlobalCooldown()
    {
        globalReadyTime = Time.time + 글로벌쿨타임;
    }
    #endregion

    #region Interrupt / 외부 중단
    public void OnInterrupted()
    {
        if (attackInProgress)
        {
            Log("[INTERRUPT] melee -> cancel(no cooldown)");
            FinishMelee(success: false);
        }

        if (rushPrepareCoroutine != null || IsRushing)
        {
            Log("[INTERRUPT] rush -> cancel(no cooldown)");
            StopRushCoroutines();
            IsRushing = false;
            CancelRushNoCooldown();
        }

        if (pendingAttackIndex >= 0)
        {
            Log("[INTERRUPT] pending cleared");
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
        {
            data = attackPatterns[runningRushIndex] as RushAttackData;
        }

        Log(noCooldown ? "[Rush] External stop (noCooldown)" : "[Rush] External stop (apply cooldown)");

        StopRushCoroutines();
        IsRushing = false;

        if (noCooldown)
        {
            CancelRushNoCooldown();
        }
        else
        {
            if (enemy.animator)
            {
                enemy.animator.SetBool("IsRush", false);
                enemy.animator.SetBool("IsRushPrepare", false);
                enemy.animator.SetTrigger("ResetToM");
            }

            if (data != null)
            {
                ApplyPerAttackCooldown(runningRushIndex, data.cooldown);
                ApplyGlobalCooldown();
            }

            enemy.RemoveSuperArmor(SuperArmorSource.Attack);
            runningRushIndex = -1;
            if (enemy.CurrentState == Enemy.EnemyState.Attack)
                enemy.SetState(Enemy.EnemyState.Chase);
        }
    }
    #endregion

    #region 로깅
    private void Log(string msg)
    {
        if (debugDecisionLogs)
            Debug.Log($"[EnemyAttackController] {msg}");
    }
    #endregion
}