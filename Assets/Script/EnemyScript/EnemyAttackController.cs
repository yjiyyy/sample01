using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 최소 사양 버전 (Decision Delay + Per-Attack Cooldown)
/// + 실패(pending 타임아웃) 시 해당 공격 full 쿨다운 소모 패치.
///
/// 동작 개요:
/// 1. 공격 성공(실행) 후: nextDecisionTime = Time.time + (baseCooldown) + decisionDelay
/// 2. 공격 선택(pending) 후 decisionDelay 안에 사거리 진입 못해서 실행 실패(타임아웃):
///      - 이제 full 쿨다운을 소비 (readyTimes 갱신)
///      - nextDecisionTime = Time.time + (baseCooldown) + decisionDelay
/// 3. 선택이 없고(nextDecisionTime 지나고) 쿨다운이 끝난 공격들 중 랜덤 1개 pick → pendingAttackIndex
///    (거리 상관 없이 pick; 사거리 밖이면 대기/시도)
///
/// 주의:
/// - baseCooldown == 0 인 공격은 실패/성공 모두 즉시 재사용 가능(설정값 그대로).
/// - 실패 시에도 쿨을 소모하기 때문에 근접이 계속 사거리 밖일 경우 한동안 그 패턴이 후보에서 사라지고
///   다른 패턴(예: Rush)이 나올 확률이 크게 증가.
/// </summary>
public class EnemyAttackController : MonoBehaviour
{
    [Header("공격 패턴 데이터 (SO 배열)")]
    public ScriptableObject[] attackPatterns;
    public int AttackCount => attackPatterns != null ? attackPatterns.Length : 0;

    /* ===== 진행 상태 ===== */
    private ScriptableObject currentAttack;
    private int currentAttackIndex = -1;

    // Melee
    private bool attackInProgress = false;
    private float attackStartTime;
    private float attackEndTime;
    private float effectiveAttackDuration;

    // Rush
    public bool IsRushing { get; private set; } = false;
    private Coroutine rushPrepareCoroutine;
    private Coroutine rushCoroutine;
    private GameObject spawnedRushHitbox;
    private int runningRushIndex = -1;
    private Transform rushTarget;

    private Enemy enemy;

    /* ===== Per Attack Cooldown =====
     * readyTimes[i] = i번 공격이 다시 '선택 가능'해지는 시각
     */
    private float[] readyTimes;

    /* ===== Decision Delay ===== */
    [Header("결정 딜레이")]
    [Tooltip("공격 종료/실패 후 다음 공격 선택 전에 추가로 기다릴 시간")]
    public float decisionDelay = 0.35f;

    private float nextDecisionTime = 0f;

    // 선택되었지만 아직 실행되지 못한 공격
    private int pendingAttackIndex = -1;
    private float pendingSelectTime = 0f;

    /* ===== 디버그 ===== */
    [Header("디버그")]
    public bool debugDecisionLogs = true;

    public float DecisionDelayRemaining => Mathf.Max(0f, nextDecisionTime - Time.time);
    public bool IsMeleeExecuting => attackInProgress && !IsRushing;
    public float MeleeElapsed => attackInProgress ? Mathf.Clamp(Time.time - attackStartTime, 0f, effectiveAttackDuration) : 0f;
    public float MeleeDuration => attackInProgress ? effectiveAttackDuration : 0f;
    public string CurrentAttackName
    {
        get
        {
            if (currentAttack is MeleeAttackData m) return m.attackName;
            if (IsRushing && runningRushIndex >= 0 &&
                attackPatterns != null &&
                runningRushIndex < attackPatterns.Length &&
                attackPatterns[runningRushIndex] is RushAttackData) return "Rush";
            return null;
        }
    }
    public float GetPerAttackCooldownRemaining(int index)
    {
        if (index < 0 || index >= AttackCount) return 0f;
        return Mathf.Max(0f, readyTimes[index] - Time.time);
    }

    /* ===== Unity ===== */
    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        int n = AttackCount;
        readyTimes = n > 0 ? new float[n] : System.Array.Empty<float>();
        for (int i = 0; i < n; i++) readyTimes[i] = -Mathf.Infinity;

        nextDecisionTime = Time.time; // 시작 즉시 선택 가능
        Log("[INIT] nextDecisionTime=now");
    }

    private void Update()
    {
        if (attackInProgress && !IsRushing)
        {
            if (Time.time >= attackEndTime)
            {
                FinishMeleeAttackAndSchedule();
            }
            else
            {
                if (currentAttack is MeleeAttackData mData && mData.attackTime > 0f)
                {
                    var info = enemy?.animator != null ? enemy.animator.GetCurrentAnimatorStateInfo(0) : default;
                    if (info.IsName("Attack") &&
                        info.normalizedTime >= 0.99f &&
                        Time.time < attackEndTime)
                    {
                        if (enemy?.animator) enemy.animator.speed = 1f;
                    }
                }
            }
        }
    }

    /* ===== Public / Cooldown ===== */
    public bool IsGlobalCooling() => false; // 전역 GCD 없음
    public bool IsOffCooldown(int index)
    {
        if (index < 0 || index >= AttackCount) return false;
        return Time.time >= readyTimes[index];
    }

    private void ApplyPerAttackCooldown(int index, float baseCooldown)
    {
        if (index < 0 || index >= AttackCount) return;
        readyTimes[index] = Time.time + Mathf.Max(0f, baseCooldown);
    }

    public float GetAttackCooldown(int index)
    {
        if (index < 0 || index >= AttackCount) return 0f;
        var so = attackPatterns[index];
        if (so is MeleeAttackData melee) return melee.cooldown;
        if (so is RushAttackData rush) return rush.cooldown;
        return 0f;
    }

    public float GetAttackRange(int index)
    {
        if (index < 0 || index >= AttackCount) return 0f;
        var so = attackPatterns[index];
        if (so is MeleeAttackData melee) return melee.range;
        if (so is RushAttackData rush) return rush.range;
        return 0f;
    }

    /* ===== 선택 로직 =====
     * 반환: 즉시 TryStartAttack 가능한 인덱스 (사거리 OK) 또는 -1
     */
    public int SelectAttackIndex(float distance)
    {
        // 진행 중 공격 있으면 새 선택 X
        if (attackInProgress || IsRushing) return -1;

        // 1) Pending 상태
        if (pendingAttackIndex >= 0)
        {
            float range = GetAttackRange(pendingAttackIndex);
            if (distance <= range && IsOffCooldown(pendingAttackIndex))
            {
                // 사거리 들어왔으니 AI가 바로 TryStartAttack 호출
                return pendingAttackIndex;
            }

            // 타임아웃 체크 → 실패 시 full 쿨다운 소비
            float waited = Time.time - pendingSelectTime;
            if (waited >= decisionDelay)
            {
                int failIdx = pendingAttackIndex;
                float baseCd = GetAttackCooldown(failIdx);
                ApplyPerAttackCooldown(failIdx, baseCd); // 실패도 full 소모
                pendingAttackIndex = -1;
                nextDecisionTime = Time.time + baseCd + decisionDelay;
                LogPendingTimeout(failIdx, waited, baseCd);
            }
            return -1;
        }

        // 2) DecisionDelay 대기
        if (Time.time < nextDecisionTime) return -1;

        // 3) 후보 수집 (쿨다운만)
        List<int> candidates = new();
        for (int i = 0; i < AttackCount; i++)
        {
            if (attackPatterns == null || i >= AttackCount) break;
            if (attackPatterns[i] == null) continue;
            if (!IsOffCooldown(i)) continue;
            candidates.Add(i);
        }

        LogDecisionAttempt(candidates);

        if (candidates.Count == 0)
        {
            // 아무 것도 준비 안 됨 → 다음 시도 시간만 미뤄둠
            nextDecisionTime = Time.time + decisionDelay;
            return -1;
        }

        // 4) 랜덤 pick
        int pick = candidates[Random.Range(0, candidates.Count)];
        pendingAttackIndex = pick;
        pendingSelectTime = Time.time;

        float pickRange = GetAttackRange(pick);
        bool immediate = distance <= pickRange;

        LogDecisionPick(pick, distance, immediate, immediate ? "immediate" : "wait-range");
        return immediate ? pick : -1;
    }

    /* ===== TryStartAttack ===== */
    public bool TryStartAttack(int index, Transform target)
    {
        if (index < 0 || index >= AttackCount) return false;
        if (!IsOffCooldown(index)) return false;
        if (enemy != null && enemy.CurrentState == Enemy.EnemyState.ShieldBreak) return false;
        var so = attackPatterns[index];
        if (so == null) return false;

        // pending 보호 (외부 호출 경로 대비)
        if (pendingAttackIndex == -1)
        {
            pendingAttackIndex = index;
            pendingSelectTime = Time.time;
        }

        if (so is MeleeAttackData melee)
        {
            StartMeleeAttack(index, melee);
            return true;
        }
        if (so is RushAttackData rush)
        {
            StartRushAttack(rush, target, index);
            return true;
        }

        Debug.LogWarning($"[EnemyAttackController] 미지원 SO 타입: {so.GetType().Name}");
        return false;
    }

    private void StartMeleeAttack(int index, MeleeAttackData data)
    {
        currentAttackIndex = index;
        currentAttack = data;

        if (data.grantSuperArmor) enemy.AddSuperArmor(SuperArmorSource.Attack);
        else enemy.RemoveSuperArmor(SuperArmorSource.Attack);

        enemy.SetState(Enemy.EnemyState.Attack);

        float raw = data.attackTime;
        float clipLen = EstimateAttackClipLength();
        if (raw <= 0f) raw = clipLen;
        effectiveAttackDuration = raw;

        attackStartTime = Time.time;
        attackEndTime = attackStartTime + effectiveAttackDuration;
        attackInProgress = true;

        if (enemy.animator) enemy.animator.speed = 1f;

        pendingAttackIndex = -1; // 실행 성공
        Log($"start melee idx={index} name={data.attackName} time={Time.time:F2}");
    }

    private float EstimateAttackClipLength()
    {
        if (enemy == null || enemy.animator == null) return 0.5f;
        var clips = enemy.animator.GetCurrentAnimatorClipInfo(0);
        if (clips.Length > 0 && clips[0].clip != null) return clips[0].clip.length;
        return 0.5f;
    }

    private void FinishMeleeAttackAndSchedule()
    {
        if (enemy.animator) enemy.animator.speed = 1f;

        float cd = 0f;
        if (currentAttackIndex >= 0)
        {
            cd = GetAttackCooldown(currentAttackIndex);
            ApplyPerAttackCooldown(currentAttackIndex, cd);
        }

        enemy.RemoveSuperArmor(SuperArmorSource.Attack);

        if (enemy.CurrentState != Enemy.EnemyState.Dead &&
            enemy.CurrentState != Enemy.EnemyState.ShieldBreak)
        {
            enemy.SetState(Enemy.EnemyState.Chase);
        }

        attackInProgress = false;
        currentAttack = null;
        currentAttackIndex = -1;

        nextDecisionTime = Time.time + cd + decisionDelay;
        LogAfterFinish("Melee", cd, nextDecisionTime);
    }

    public void AttackHit()
    {
        if (!attackInProgress) return;
        if (currentAttack is MeleeAttackData melee) SpawnMeleeHitBox(melee);
    }

    private void SpawnMeleeHitBox(MeleeAttackData data)
    {
        if (data.hitBoxPrefab == null) return;
        GameObject go = Instantiate(data.hitBoxPrefab, transform);
        if (go.TryGetComponent<HitBox_Enemy>(out var hb))
        {
            hb.Initialize(
                data.damage,
                data.range,
                data.knockbackPower,
                data.knockbackDuration,
                data.hitBoxLifetime,
                data.stunDuration,
                data.allowDuplicateHit,
                data.duplicateHitInterval
            );
        }
    }

    /* ===== Rush ===== */
    private void StartRushAttack(RushAttackData data, Transform target, int index)
    {
        StopRushCoroutines();

        runningRushIndex = index;
        rushTarget = target;

        enemy.SetState(Enemy.EnemyState.Attack);

        if (data.grantSuperArmor) enemy.AddSuperArmor(SuperArmorSource.Attack);
        else enemy.RemoveSuperArmor(SuperArmorSource.Attack);

        rushPrepareCoroutine = StartCoroutine(RushPrepareRoutine(data));
        pendingAttackIndex = -1;
        Log($"start rush idx={index} time={Time.time:F2}");
    }

    private IEnumerator RushPrepareRoutine(RushAttackData data)
    {
        if (enemy.animator != null)
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
                StopRushCoroutines();
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

        if (enemy.animator != null)
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
                StopRushCoroutines();
                IsRushing = false;
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        IsRushing = false;
        FinishRushAttack(data);
    }

    private void FinishRushAttack(RushAttackData data)
    {
        if (enemy.animator != null)
        {
            enemy.animator.SetBool("IsRush", false);
            enemy.animator.SetBool("IsRushPrepare", false);
            enemy.animator.SetTrigger("ResetToM");
        }

        DespawnRushHitbox();
        rushCoroutine = null;

        float cd = 0f;
        if (runningRushIndex >= 0)
        {
            cd = data.cooldown;
            ApplyPerAttackCooldown(runningRushIndex, cd);
        }

        enemy.RemoveSuperArmor(SuperArmorSource.Attack);

        if (enemy.CurrentState != Enemy.EnemyState.ShieldBreak &&
            enemy.CurrentState != Enemy.EnemyState.Dead)
            enemy.SetState(Enemy.EnemyState.Chase);

        runningRushIndex = -1;
        rushTarget = null;

        nextDecisionTime = Time.time + cd + decisionDelay;
        LogAfterFinish("Rush", cd, nextDecisionTime);
    }

    private void SpawnRushHitbox(RushAttackData data)
    {
        if (spawnedRushHitbox != null) return;
        if (data.hitBoxPrefab == null)
        {
            Debug.LogWarning("[EnemyAttackController] Rush hitBoxPrefab 비어있음");
            return;
        }

        spawnedRushHitbox = Instantiate(data.hitBoxPrefab, transform);
        spawnedRushHitbox.transform.localPosition = Vector3.zero;
        spawnedRushHitbox.transform.localRotation = Quaternion.identity;

        if (spawnedRushHitbox.TryGetComponent<HitBox_Enemy>(out var hb))
        {
            float life = data.hitBoxLifetime > 0f ? data.hitBoxLifetime : data.rushTime;
            hb.Initialize(
                data.damage,
                0f,
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

    private void StopRushCoroutines()
    {
        if (rushPrepareCoroutine != null)
        {
            StopCoroutine(rushPrepareCoroutine);
            rushPrepareCoroutine = null;
        }
        if (rushCoroutine != null)
        {
            StopCoroutine(rushCoroutine);
            rushCoroutine = null;
        }
        DespawnRushHitbox();
        IsRushing = false;

        enemy.RemoveSuperArmor(SuperArmorSource.Attack);
        runningRushIndex = -1;
        rushTarget = null;

        Log("[Rush] Forced stop");
    }

    public void StopRushExternally(bool noCooldown)
    {
        if (IsRushing || rushPrepareCoroutine != null)
        {
            if (!noCooldown && runningRushIndex >= 0)
            {
                float cd = GetAttackCooldown(runningRushIndex);
                ApplyPerAttackCooldown(runningRushIndex, cd);
                nextDecisionTime = Time.time + cd + decisionDelay;
                Log($"[Rush] External stop with cooldown cd={cd:F2}, nextDecision={nextDecisionTime:F2}");
            }
            else
            {
                Log("[Rush] External stop (noCooldown)");
            }
            StopRushCoroutines();
        }
    }

    /* ===== Interrupt ===== */
    public void InterruptCooldown()
    {
        attackInProgress = false;
        if (enemy?.animator) enemy.animator.speed = 1f;

        if (IsRushing || rushPrepareCoroutine != null || rushCoroutine != null)
        {
            StopRushCoroutines();
            IsRushing = false;
        }

        currentAttackIndex = -1;
        currentAttack = null;
        pendingAttackIndex = -1;

        enemy?.RemoveSuperArmor(SuperArmorSource.Attack);

        if (enemy != null &&
            enemy.CurrentState != Enemy.EnemyState.Dead &&
            enemy.CurrentState != Enemy.EnemyState.ShieldBreak)
        {
            enemy.SetState(Enemy.EnemyState.Chase);
        }

        // 쿨다운들은 유지 (소비한 것 유지)
        nextDecisionTime = Time.time; // 즉시 재선택 가능
        Log("[Interrupt] all reset, nextDecision=now (cooldowns kept)");
    }

    /* ===== 로그 유틸 ===== */
    private string AttackName(int idx)
    {
        if (idx < 0 || idx >= AttackCount) return "None";
        var so = attackPatterns[idx];
        if (so is MeleeAttackData m) return m.attackName;
        if (so is RushAttackData) return "Rush";
        return so ? so.name : "NullSO";
    }

    private void Log(string msg)
    {
        if (!debugDecisionLogs) return;
        Debug.Log($"[AttackDecision] {msg}", this);
    }

    private void LogDecisionAttempt(List<int> candidates)
    {
        if (!debugDecisionLogs) return;
        var listStr = candidates.Count == 0 ? "-" : string.Join(",", candidates);
        Log($"attempt t={Time.time:F2} (candidates={listStr})");
    }

    private void LogDecisionPick(int pick, float distance, bool immediate, string mode)
    {
        if (!debugDecisionLogs) return;
        float range = GetAttackRange(pick);
        Log($"pick t={Time.time:F2} idx={pick} name={AttackName(pick)} dist={distance:F2} range={range:F2} mode={mode} immediateStart={immediate}");
    }

    private void LogPendingTimeout(int idx, float waited, float baseCd)
    {
        if (!debugDecisionLogs) return;
        Log($"pending-timeout t={Time.time:F2} idx={idx} name={AttackName(idx)} waited={waited:F2}s decisionDelay={decisionDelay:F2}s -> FULL CD consume {baseCd:F2}s, nextDecision={nextDecisionTime:F2}");
    }

    private void LogAfterFinish(string type, float cd, float next)
    {
        if (!debugDecisionLogs) return;
        Log($"finished {type} t={Time.time:F2} usedCd={cd:F2} nextDecision={next:F2}");
    }
}