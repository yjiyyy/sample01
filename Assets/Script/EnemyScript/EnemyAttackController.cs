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
    public bool TryStartAttack(int index, Transform target)
    {
        if (index < 0 || index >= AttackCount) return false;
        if (attackPatterns == null) return false;
        var so = attackPatterns[index];
        if (so == null) return false;
        if (!IsOffCooldown(index)) return false;
        if (enemy != null && enemy.CurrentState == Enemy.EnemyState.ShieldBreak) return false; // 그로기 중 공격 금지

        lockedAttackIndex = index;

        if (so is MeleeAttackData)
        {
            NotifyAttack(index);
            enemy.SetState(Enemy.EnemyState.Attack);
            return true;
        }

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
            BeginCooldown(currentAttackIndex);
        }
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
        if (so is RushAttackData rush) return rush.range;
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
            lastUsedTimes[i] = -Mathf.Infinity;

        if (enemy != null && enemy.CurrentState != Enemy.EnemyState.Dead && enemy.CurrentState != Enemy.EnemyState.ShieldBreak)
            enemy.SetState(Enemy.EnemyState.Chase);

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
        if (enemy != null && enemy.CurrentState != Enemy.EnemyState.Dead && enemy.CurrentState != Enemy.EnemyState.ShieldBreak)
            enemy.SetState(Enemy.EnemyState.Chase);
        cooldownRoutine = null;
    }

    // ------ 랜덤 공격 선택 ------
    public int SelectAttackIndex(float distance)
    {
        if (lockedAttackIndex >= 0 && IsOffCooldown(lockedAttackIndex))
            return lockedAttackIndex;

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

        int chosen = available[Random.Range(0, available.Count)];
        lockedAttackIndex = chosen;
        return chosen;
    }

    // ───────────────── Rush ─────────────────
    private void StartRushAttack(RushAttackData data, Transform target, int index)
    {
        StopRushCoroutines();

        runningRushIndex = index;
        rushTarget = target;

        enemy.SetState(Enemy.EnemyState.Attack);

        // Rush SuperArmor 부여
        enemy.AddSuperArmor(SuperArmorSource.Rush);

        rushPrepareCoroutine = StartCoroutine(RushPrepareRoutine(data));
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
                Vector3 dir = (rushTarget.position - transform.position);
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    dir.Normalize();
                    transform.rotation = Quaternion.LookRotation(dir);
                }
            }

            elapsed += Time.deltaTime;
            yield return null;

            if (enemy.CurrentState != Enemy.EnemyState.Attack ||
                enemy.CurrentState == Enemy.EnemyState.ShieldBreak)
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

        if (enemy.animator != null)
        {
            enemy.animator.SetBool("IsRushPrepare", false);
            enemy.animator.SetBool("IsRush", true);
            enemy.animator.Play("Rush");
        }

        if (enemy.agent != null && enemy.agent.isOnNavMesh)
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

            transform.position += rushDir.normalized * data.rushSpeed * Time.deltaTime;

            elapsed += Time.deltaTime;
            yield return null;

            if (enemy.CurrentState != Enemy.EnemyState.Attack ||
                enemy.CurrentState == Enemy.EnemyState.ShieldBreak)
            {
                // ShieldBreak 등으로 중단
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
            enemy.animator.SetTrigger("ResetToM");
        }

        DespawnRushHitbox();
        rushCoroutine = null;

        if (runningRushIndex >= 0)
        {
            BeginCooldown(runningRushIndex); // Rush 완료 시 쿨다운
        }

        enemy.RemoveSuperArmor(SuperArmorSource.Rush);

        if (enemy.CurrentState != Enemy.EnemyState.ShieldBreak &&
            enemy.CurrentState != Enemy.EnemyState.Dead)
            enemy.SetState(Enemy.EnemyState.Chase);

        runningRushIndex = -1;
        rushTarget = null;
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

        // Rush SuperArmor 제거
        enemy.RemoveSuperArmor(SuperArmorSource.Rush);

        runningRushIndex = -1;
        rushTarget = null;
    }

    // ShieldBreak에서 호출 (실패 취급 → 쿨다운 없음)
    public void StopRushExternally(bool noCooldown)
    {
        if (IsRushing || rushPrepareCoroutine != null)
        {
            if (!noCooldown && runningRushIndex >= 0)
            {
                BeginCooldown(runningRushIndex);
            }
            StopRushCoroutines();
        }
    }
}