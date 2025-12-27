using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 중앙 관리 + 공통 유틸만 두고, 공격별 구현은 partial 파일로 분리한다.
// 프리팹에는 이 EnemyAttackController 컴포넌트 1개만 붙이면 된다.
public partial class EnemyAttackController : MonoBehaviour
{
    [Header("패턴 배열 (MeleeAttackData / RushAttackData / RangedAttackData / JumpAttackData / AoEAttackData / ComboAttackData / TimeProjectileAttackData)")]
    public ScriptableObject[] attackPatterns;
    public int AttackCount => attackPatterns != null ? attackPatterns.Length : 0;

    // Melee에서만 쓰는 현재 공격(기존 구조 유지)
    private ScriptableObject currentAttack;
    private int currentAttackIndex = -1;

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

    // TimeProjectile 전용 상태를 main에 선언 (TPA partial에서 참조)
    private Coroutine timeProjectileRoutine;
    private int runningTimeProjectileIndex = -1;

    // 기존 상태 플래그들과 AoE 통합 (AoE는 partial 파일에서 aoeCoroutine 변수로 관리)
    public bool IsMeleeExecuting => attackInProgress && !IsRushing && rangedRoutine == null && !IsJumping;
    // 콤보가 생겼으니 comboCoroutine은 partial 쪽에 선언됨; partial 합쳐질 때 같이 평가됩니다.
    public bool IsAttackExecuting => IsMeleeExecuting || IsRushing || rushPrepareCoroutine != null || rangedRoutine != null || IsJumping || aoeCoroutine != null || comboCoroutine != null || timeProjectileRoutine != null;

    public string CurrentAttackName
    {
        get
        {
            if (currentAttack is MeleeAttackData m) return m.attackName;
            if (currentAttack is ComboAttackData cb) return cb.attackName;
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
            if (IsJumping &&
                runningJumpIndex >= 0 &&
                attackPatterns != null &&
                runningJumpIndex < attackPatterns.Length &&
                attackPatterns[runningJumpIndex] is JumpAttackData ja) return ja.attackName;
            if (aoeCoroutine != null &&
                runningAoEIndex >= 0 &&
                attackPatterns != null &&
                runningAoEIndex < attackPatterns.Length &&
                attackPatterns[runningAoEIndex] is AoEAttackData a) return a.attackName;
            if (timeProjectileRoutine != null &&
                runningTimeProjectileIndex >= 0 &&
                attackPatterns != null &&
                runningTimeProjectileIndex < attackPatterns.Length &&
                attackPatterns[runningTimeProjectileIndex] is TimeProjectileAttackData t) return t.attackName;
            return null;
        }
    }

    private void Awake()
    {
        CleanPatterns();

        enemy = GetComponent<Enemy>();

        // readyTimes 초기화 (길이 동기화 및 기본값 세팅)
        SyncReadyTimes(initialFill: true);

        globalReadyTime = Time.time;

        if (AttackCount == 0)
            Debug.LogWarning("[EnemyAttackController] 등록된 유효 공격 패턴이 0개입니다. (공격 비활성 상태)");

        Log($"INIT (validPatterns={AttackCount})");
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
            if (p is MeleeAttackData || p is RushAttackData || p is RangedAttackData || p is JumpAttackData || p is AoEAttackData || p is ComboAttackData || p is TimeProjectileAttackData) list.Add(p);
            else
            {
                removedUnsupported++;
                Debug.LogWarning($"[EnemyAttackController] 지원하지 않는 패턴 타입 무시: {p.GetType().Name}");
            }
        }

        if (removedNull > 0 || removedUnsupported > 0)
            Debug.LogWarning($"[EnemyAttackController] 패턴 정리: null {removedNull}개, 미지원 {removedUnsupported}개 제거 → 최종 {list.Count}개");

        attackPatterns = list.ToArray();

        // 패턴 정리 후 readyTimes 길이 동기화 (현재 값 보존)
        SyncReadyTimes(initialFill: false);
    }

    /// <summary>
    /// readyTimes 배열을 AttackCount에 맞춰 동기화합니다.
    /// initialFill = true 면 새로 생성된 인덱스는 -Infinity로 채웁니다.
    /// initialFill = false 면 기존 readyTimes 값은 보존되고 새로 생긴 인덱스만 -Infinity로 채웁니다.
    /// </summary>
    private void SyncReadyTimes(bool initialFill)
    {
        int n = AttackCount;
        var old = readyTimes;
        if (old == null || old.Length != n)
        {
            var nw = n > 0 ? new float[n] : System.Array.Empty<float>();
            for (int i = 0; i < nw.Length; i++)
            {
                if (!initialFill && old != null && i < old.Length)
                    nw[i] = old[i];
                else
                    nw[i] = -Mathf.Infinity;
            }
            readyTimes = nw;
        }
    }

    private void Update()
    {
        // 기존 Update에 있던 Melee 진행/프리즈/종료 로직을 그대로 위임 호출
        TickMeleeUpdate();

        // Hold 만료 (기존 로직 유지)
        if (holdActive && !IsAttackExecuting && !pendingExecuted && Time.time >= holdExpireTime)
        {
            Log($"[AttackFlow] HOLD TIMEOUT idx={pendingAttackIndex}");
            CancelPendingHold();
        }

        // partial 쪽에서 필요하면 TickTimeProjectileUpdate 같은 헬퍼를 호출하도록 설계되어 있음.
        TickTimeProjectileUpdate();
    }

    #region 외부 조회
    public bool IsGlobalCooling() => Time.time < globalReadyTime;
    public bool IsOffCooldown(int index)
    {
        if (index < 0) return false;
        if (readyTimes == null || index >= readyTimes.Length) return false;
        return Time.time >= readyTimes[index];
    }
    public float GetAttackRange(int index)
    {
        if (index < 0 || index >= AttackCount) return 0f;
        if (attackPatterns[index] is MeleeAttackData m) return m.range;
        if (attackPatterns[index] is RushAttackData r) return r.range;
        if (attackPatterns[index] is RangedAttackData rg) return rg.range;
        if (attackPatterns[index] is JumpAttackData j) return j.range;
        if (attackPatterns[index] is AoEAttackData a) return a.spawnRadius; // AoE: use spawnRadius as effective range
        if (attackPatterns[index] is ComboAttackData c) return c.range;
        if (attackPatterns[index] is TimeProjectileAttackData t) return t.range;
        return 0f;
    }
    public float GetAttackCooldown(int index)
    {
        if (index < 0 || index >= AttackCount) return 0f;
        if (attackPatterns[index] is MeleeAttackData m) return m.cooldown;
        if (attackPatterns[index] is RushAttackData r) return r.cooldown;
        if (attackPatterns[index] is RangedAttackData rg) return rg.cooldown;
        if (attackPatterns[index] is JumpAttackData j) return j.cooldown;
        if (attackPatterns[index] is AoEAttackData a) return a.cooldown;
        if (attackPatterns[index] is ComboAttackData c) return c.cooldown;
        if (attackPatterns[index] is TimeProjectileAttackData t) return t.cooldown;
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
        if (so is ComboAttackData c)
        {
            StartCombo(c, target, index);
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
        if (so is JumpAttackData j)
        {
            StartJump(j, target, index);
            return true;
        }
        if (so is AoEAttackData a)
        {
            StartAoE(a, target, index);
            return true;
        }
        if (so is TimeProjectileAttackData tpa)
        {
            StartTimeProjectile(tpa, target, index);
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

    #region 쿨타임 & 인터럽트
    private void ApplyPerAttackCooldown(int index, float baseCooldown)
    {
        if (index < 0) return;
        if (readyTimes == null || index >= readyTimes.Length) return;
        readyTimes[index] = Time.time + Mathf.Max(0f, baseCooldown);
    }
    private void ApplyGlobalCooldown() => globalReadyTime = Time.time + 글로벌쿨타임;

    public void OnInterrupted()
    {
        // 기존 동작을 유지하되, 공격별 정리 코드를 공격 파일로 분리
        InterruptMeleeIfNeeded();
        InterruptRushIfNeeded();
        InterruptRangedIfNeeded();
        InterruptJumpIfNeeded();
        InterruptAoEIfNeeded();
        InterruptComboIfNeeded();

        if (pendingAttackIndex >= 0 && !pendingExecuted)
        {
            Log("INTERRUPT pending cleared");
            CancelPendingHold();
        }
    }
    public void InterruptCooldown() => OnInterrupted();
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