using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemyAttackController : MonoBehaviour
{
    [Header("공격 패턴 데이터 (SO 배열)")]
    public ScriptableObject[] attackPatterns;

    private ScriptableObject currentAttack;
    private int currentAttackIndex = -1;
    private float[] lastUsedTimes;
    public int AttackCount => attackPatterns != null ? attackPatterns.Length : 0;

    public bool IsRushing { get; private set; } = false;

    private bool isCooldown = false;
    private Coroutine cooldownRoutine;
    private Enemy enemy;

    // ───── Rush 내부 실행용 상태 ─────
    private Coroutine rushPrepareCoroutine;
    private Coroutine rushCoroutine;
    private GameObject spawnedRushHitbox;
    private int runningRushIndex = -1;
    private Transform rushTarget;

    // 🔒 공격 인덱스 고정용 상태
    private int lockedAttackIndex = -1;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        int n = AttackCount;
        lastUsedTimes = n > 0 ? new float[n] : System.Array.Empty<float>();
        for (int i = 0; i < lastUsedTimes.Length; i++)
            lastUsedTimes[i] = -Mathf.Infinity;
    }

    // ───────────────── 메인 진입점 ─────────────────
    // AI는 이 메서드만 호출하면 됨(프리팹에 추가 컴포넌트 불필요)
    public bool TryStartAttack(int index, Transform target)
    {
        if (index < 0 || index >= AttackCount) return false;
        if (attackPatterns == null) return false;
        var so = attackPatterns[index];
        if (so == null) return false;
        if (!IsOffCooldown(index)) return false;

        // 공격 시작 시 공격 인덱스 Lock
        lockedAttackIndex = index;

        // Melee: 기존 애니 이벤트 흐름 유지
        if (so is MeleeAttackData)
        {
            NotifyAttack(index);
            enemy.SetState(Enemy.EnemyState.Attack);
            return true;
        }

        // Rush: AttackController 내부 코루틴으로 처리
        if (so is RushAttackData rushData)
        {
            StartRushAttack(rushData, target, index);
            return true;
        }

        Debug.LogWarning($"[EnemyAttackController] 알 수 없는 SO 타입: {so.GetType().Name}");
        return false;
    }

    public void NotifyAttack(int index)
    {
        if (index < 0 || index >= AttackCount) { currentAttack = null; currentAttackIndex = -1; return; }
        currentAttack = attackPatterns[index];
        currentAttackIndex = index;
    }

    // 애니메이션 이벤트(밀리만 사용)
    public void AttackHit()
    {
        if (currentAttack == null || currentAttackIndex < 0) return;

        if (currentAttack is MeleeAttackData meleeData)
        {
            HandleMeleeAttack(meleeData);
            BeginCooldown(currentAttackIndex); // 밀리어택은 이벤트 시점에 쿨다운 시작
        }
        // Rush는 여기서 처리하지 않음(코루틴 종료 시 쿨다운)
    }

    private void HandleMeleeAttack(MeleeAttackData meleeData)
    {
        if (meleeData.hitBoxPrefab == null) return;

        GameObject go = Instantiate(meleeData.hitBoxPrefab, transform);

        if (go.TryGetComponent<HitBox_Enemy>(out var enemyHitBox))
        {
            enemyHitBox.Initialize(
                meleeData.damage,
                meleeData.range,
                meleeData.knockbackPower,
                meleeData.knockbackDuration,
                meleeData.hitBoxLifetime,
                meleeData.stunDuration,
                meleeData.allowDuplicateHit,
                meleeData.duplicateHitInterval
            );
        }
    }

    public float GetAttackCooldown(int index)
    {
        if (index < 0 || index >= AttackCount) return 1f;
        var so = attackPatterns[index];
        if (so is MeleeAttackData melee) return melee.cooldown;
        if (so is RushAttackData rush) return rush.cooldown;
        return 1f;
    }

    public float GetAttackRange(int index)
    {
        if (index < 0 || index >= AttackCount) return 2f;
        var so = attackPatterns[index];
        if (so is MeleeAttackData melee) return melee.range;
        if (so is RushAttackData rush) return rush.range; // ← RushAttackData의 range 필드 사용
        return 2f;
    }

    public bool IsOffCooldown(int index)
    {
        if (index < 0 || index >= AttackCount) return false;
        return Time.time >= lastUsedTimes[index] + GetAttackCooldown(index);
    }

    public void BeginCooldown(int index)
    {
        if (index < 0 || index >= AttackCount) return;
        lastUsedTimes[index] = Time.time;

        if (cooldownRoutine != null)
            StopCoroutine(cooldownRoutine);
        float cd = GetAttackCooldown(index);
        cooldownRoutine = StartCoroutine(CooldownRoutine(cd));

        // 쿨다운 시작시 lock 해제!
        lockedAttackIndex = -1;
    }

    public bool IsCooldownActive() => isCooldown;

    public void InterruptCooldown()
    {
        isCooldown = false;
        if (cooldownRoutine != null)
        {
            StopCoroutine(cooldownRoutine);
            cooldownRoutine = null;
        }

        for (int i = 0; i < lastUsedTimes.Length; i++)
        {
            lastUsedTimes[i] = -Mathf.Infinity;
        }
        if (enemy != null)
            enemy.SetState(Enemy.EnemyState.Chase);

        // 강제 lock 해제
        lockedAttackIndex = -1;
    }

    private IEnumerator CooldownRoutine(float duration)
    {
        isCooldown = true;
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            yield return null;
            if (!isCooldown) yield break;
        }
        isCooldown = false;
        if (enemy != null)
            enemy.SetState(Enemy.EnemyState.Chase);
        cooldownRoutine = null;
    }

    // ------ 핵심: 랜덤 공격 선택 (거리 무시!) ------
    public int SelectAttackIndex(float distance)
    {
        // lock이 걸려 있으면 그 인덱스만 반환
        if (lockedAttackIndex >= 0 && IsOffCooldown(lockedAttackIndex))
            return lockedAttackIndex;

        // 쿨타임이 끝난 공격 패턴만 후보로 수집
        List<int> available = new();
        for (int i = 0; i < AttackCount; i++)
        {
            if (attackPatterns == null || i >= attackPatterns.Length) break;
            if (attackPatterns[i] == null) continue;
            if (!IsOffCooldown(i)) continue;
            available.Add(i);
        }
        if (available.Count == 0)
            return -1;

        // 랜덤으로 1개 선택!
        int chosen = available[Random.Range(0, available.Count)];
        lockedAttackIndex = chosen;
        return chosen;
    }

    // ───────────────── Rush 내부 로직 ─────────────────

    private void StartRushAttack(RushAttackData data, Transform target, int index)
    {
        // 이미 다른 rush 진행 중이면 중단
        StopRushCoroutines();

        runningRushIndex = index;
        rushTarget = target;

        // 상태: Attack
        enemy.SetState(Enemy.EnemyState.Attack);

        rushPrepareCoroutine = StartCoroutine(RushPrepareRoutine(data));
    }

    private IEnumerator RushPrepareRoutine(RushAttackData data)
    {
        // 애니메이터 세팅(있으면)
        if (enemy.animator != null)
        {
            enemy.animator.SetBool("IsRushPrepare", true);
            enemy.animator.SetBool("IsRush", false);
            enemy.animator.Play("RushPrepare");
        }

        // 방향 조준
        Vector3 dir = Vector3.forward;
        float elapsed = 0f;

        while (elapsed < data.prepareTime)
        {
            if (rushTarget != null)
            {
                dir = (rushTarget.position - transform.position);
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    dir.Normalize();
                    transform.rotation = Quaternion.LookRotation(dir);
                }
            }

            elapsed += Time.deltaTime;
            yield return null;

            if (enemy.CurrentState != Enemy.EnemyState.Attack)
            {
                StopRushCoroutines();
                yield break;
            }
        }

        rushPrepareCoroutine = null;
        rushCoroutine = StartCoroutine(RushAttackRoutine(data));
    }

    private IEnumerator RushAttackRoutine(RushAttackData data)
    {
        IsRushing = true;

        // 애니메이터 전환
        if (enemy.animator != null)
        {
            enemy.animator.SetBool("IsRushPrepare", false);
            enemy.animator.SetBool("IsRush", true);
            enemy.animator.Play("Rush");
        }

        // NavMeshAgent 정지
        if (enemy.agent != null && enemy.agent.isOnNavMesh)
        {
            enemy.agent.isStopped = true;
            enemy.agent.velocity = Vector3.zero;
            enemy.agent.ResetPath();
        }

        // 히트박스 생성 및 초기화
        SpawnRushHitbox(data);

        float elapsed = 0f;

        // 돌진 방향: allowDirectionDeviation 체크
        Vector3 rushDir = transform.forward;
        rushDir.y = 0f;
        if (rushDir.sqrMagnitude < 0.0001f) rushDir = Vector3.forward;

        while (elapsed < data.rushTime)
        {
            if (data.allowDirectionDeviation && rushTarget != null)
            {
                Vector3 toPlayer = rushTarget.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.0001f)
                {
                    Vector3 desiredDir = toPlayer.normalized;
                    float lerpT = data.directionDeviationAmount * Time.deltaTime;
                    rushDir = Vector3.Lerp(rushDir, desiredDir, lerpT);
                    transform.rotation = Quaternion.LookRotation(rushDir);
                }
            }
            // 체크 해제 시 rushDir은 최초 방향 고정!

            transform.position += rushDir.normalized * data.rushSpeed * Time.deltaTime;

            elapsed += Time.deltaTime;
            yield return null;

            if (enemy.CurrentState != Enemy.EnemyState.Attack)
            {
                StopRushCoroutines();
                IsRushing = false;
                yield break;
            }
        }

        IsRushing = false;
        FinishRushAttack();
    }

    private void FinishRushAttack()
    {
        if (enemy.animator != null)
        {
            enemy.animator.SetBool("IsRush", false);
            enemy.animator.SetBool("IsRushPrepare", false);
            enemy.animator.SetTrigger("ResetToM"); // ⭐️ 러시 종료 후 즉시 AnyState→Run 트리거!
        }

        DespawnRushHitbox();
        rushCoroutine = null;

        if (runningRushIndex >= 0)
        {
            BeginCooldown(runningRushIndex); // Rush는 종료 시 쿨다운 시작
        }

        enemy.SetState(Enemy.EnemyState.Chase);
        runningRushIndex = -1;
        rushTarget = null;
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
        runningRushIndex = -1;
        rushTarget = null;
    }

    private void SpawnRushHitbox(RushAttackData data)
    {
        if (spawnedRushHitbox != null) return;

        if (data.hitBoxPrefab == null)
        {
            Debug.LogWarning("[EnemyAttackController] Rush hitBoxPrefab이 비었습니다.");
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
                0f, // range 미사용
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
}