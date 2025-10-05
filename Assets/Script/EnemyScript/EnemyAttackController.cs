using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 확정 설계 반영:
/// - 전역 GCD(global 1s) : 어떤 공격이든 baseCooldown > 0 이면 공격 종료 시 1초간 모든 공격 차단
/// - baseCooldown < 1 → 실제 per-attack 쿨다운 1초로 상향 (전역 1초와 동일 시점에 풀림)
/// - baseCooldown == 0 → 전역 GCD 미발동, per-attack 쿨다운 없음
/// - AttackTime: 공격 시작 시 attackStartTime 기록, attackEndTime 도달 시 '공격 종료 처리' + 위 쿨다운 로직 적용
/// - 기존 isCooldown + CooldownRoutine 제거. (전역/개별 타이머 계산만)
/// - Interrupt 시: per-attack & global 모두 리셋(현행 동작 유지), 다음 프레임부터 즉시 다른 공격 가능
/// - AttackHit(): 히트박스 생성만 담당 (쿨다운 시작 X)
/// - Rush: 완료 시 동일 규칙(전역 GCD + per-attack 쿨) 적용
/// 
/// 추후 확장 예정 Scaffold:
/// - CooldownMicroState (Idle / Backstep / Move) : 아직 미사용, 나중에 전역 GCD 시간 동안 동작 다양화 가능
/// </summary>
public class EnemyAttackController : MonoBehaviour
{
    [Header("공격 패턴 데이터 (SO 배열)")]
    public ScriptableObject[] attackPatterns;

    private ScriptableObject currentAttack;
    private int currentAttackIndex = -1;

    private float[] lastUsedTimes;        // 개별 공격 시작(쿨다운) 기준 시간
    public int AttackCount => attackPatterns != null ? attackPatterns.Length : 0;

    // Rush 상태
    public bool IsRushing { get; private set; } = false;

    private Enemy enemy;

    // ─── AttackTime 관리 ───
    private bool attackInProgress = false;
    private float attackStartTime;
    private float attackEndTime;
    private float effectiveAttackDuration;

    // 전역 GCD
    private float globalAvailableTime = -Mathf.Infinity;  // Time.time < globalAvailableTime 이면 전역 쿨다운 중

    // 공격 인덱스 고정
    private int lockedAttackIndex = -1;

    // Rush 전용 코루틴 & 상태
    private Coroutine rushPrepareCoroutine;
    private Coroutine rushCoroutine;
    private GameObject spawnedRushHitbox;
    private int runningRushIndex = -1;
    private Transform rushTarget;

    // ─── 향후 확장용 Scaffold (현재 미사용) ───
    private enum CooldownMicroState { None, Idle, Backstep, Move }
    private CooldownMicroState microState = CooldownMicroState.None;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        int n = AttackCount;
        lastUsedTimes = n > 0 ? new float[n] : System.Array.Empty<float>();
        for (int i = 0; i < lastUsedTimes.Length; i++)
            lastUsedTimes[i] = -Mathf.Infinity;
    }

    private void Update()
    {
        // AttackTime 종료 체크 (Melee / Rush 준비 아님)
        if (attackInProgress && !IsRushing)
        {
            if (Time.time >= attackEndTime)
            {
                FinishMeleeAttackAndStartCooldown();
            }
            else
            {
                // 애니 길이보다 AttackTime이 길 경우 프레임 Freeze (단순 구현: stateInfo 끝나면 speed=0)
                if (currentAttack is MeleeAttackData mData && mData.attackTime > 0f)
                {
                    var info = enemy.animator != null ? enemy.animator.GetCurrentAnimatorStateInfo(0) : default;
                    if (info.IsName("Attack"))
                    {
                        if (info.normalizedTime >= 0.99f && Time.time < attackEndTime)
                        {
                            enemy.animator.speed = 0f; // TODO: 나중에 전용 AfterFrame / TailClip 활용 개선
                        }
                    }
                }
            }
        }
    }

    // ───────────────── 전역 / 개별 쿨다운 공개 API ─────────────────
    public bool IsGlobalCooling() => Time.time < globalAvailableTime;
    public bool IsOffCooldown(int index)
    {
        if (index < 0 || index >= AttackCount) return false;
        float appliedCd = GetAppliedPerAttackCooldown(index);
        return Time.time >= lastUsedTimes[index] + appliedCd;
    }

    private void StartGlobalCooldown(float duration)
    {
        globalAvailableTime = Time.time + duration;
    }

    private float GetAppliedPerAttackCooldown(int index)
    {
        float baseCd = GetAttackCooldown(index);
        if (baseCd <= 0f) return 0f;
        if (baseCd < 1f) return 1f;
        return baseCd;
    }

    // ───────────────── 공격 선택/시작 ─────────────────
    public int SelectAttackIndex(float distance)
    {
        // 전역 GCD 중이면 어떤 공격도 시작 불가
        if (IsGlobalCooling()) return -1;

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
        if (available.Count == 0) return -1;

        int chosen = available[Random.Range(0, available.Count)];
        lockedAttackIndex = chosen;
        return chosen;
    }

    public bool TryStartAttack(int index, Transform target)
    {
        if (IsGlobalCooling()) return false;        // 전역 GCD
        if (index < 0 || index >= AttackCount) return false;
        if (!IsOffCooldown(index)) return false;
        if (enemy != null && enemy.CurrentState == Enemy.EnemyState.ShieldBreak) return false;

        var so = attackPatterns[index];
        if (so == null) return false;

        lockedAttackIndex = index;

        // Melee
        if (so is MeleeAttackData meleeData)
        {
            PrepareMeleeAttack(index, meleeData);
            return true;
        }

        // Rush
        if (so is RushAttackData rushData)
        {
            StartRushAttack(rushData, target, index);
            return true;
        }

        Debug.LogWarning($"[EnemyAttackController] 알 수 없는 SO 타입: {so.GetType().Name}");
        return false;
    }

    private void PrepareMeleeAttack(int index, MeleeAttackData meleeData)
    {
        currentAttackIndex = index;
        currentAttack = meleeData;

        // SuperArmor
        if (meleeData.grantSuperArmor)
            enemy.AddSuperArmor(SuperArmorSource.Attack);
        else
            enemy.RemoveSuperArmor(SuperArmorSource.Attack);

        // 상태 전환 (애니메이션 'Attack' 재생)
        enemy.SetState(Enemy.EnemyState.Attack);

        // AttackTime 계산
        float raw = meleeData.attackTime;
        float clipLen = EstimateAttackClipLength(); // 간단 추정 (정확도 높이려면 개선)
        if (raw <= 0f) raw = clipLen;               // 0 이하 → 클립 길이 사용
        effectiveAttackDuration = raw;

        attackStartTime = Time.time;
        attackEndTime = attackStartTime + effectiveAttackDuration;
        attackInProgress = true;

        // 혹시 이전에 animator.speed=0 되어 있었으면 복구
        if (enemy.animator) enemy.animator.speed = 1f;
    }

    private float EstimateAttackClipLength()
    {
        if (enemy == null || enemy.animator == null) return 0.5f;
        var clips = enemy.animator.GetCurrentAnimatorClipInfo(0);
        if (clips.Length > 0 && clips[0].clip != null)
            return clips[0].clip.length;
        return 0.5f;
    }

    // ───────────────── 공격 종료 처리 (Melee) ─────────────────
    private void FinishMeleeAttackAndStartCooldown()
    {
        // 애니 속도 복구
        if (enemy.animator) enemy.animator.speed = 1f;

        if (currentAttackIndex >= 0)
        {
            float baseCd = GetAttackCooldown(currentAttackIndex);
            ApplyPerAttackCooldown(currentAttackIndex, baseCd);

            if (baseCd > 0f)   // baseCooldown == 0 이면 전역 GCD 없음
                StartGlobalCooldown(1f);
        }

        // SuperArmor 제거
        enemy.RemoveSuperArmor(SuperArmorSource.Attack);

        // 상태 Chase로 복귀 (Rush 제외)
        if (enemy.CurrentState != Enemy.EnemyState.Dead &&
            enemy.CurrentState != Enemy.EnemyState.ShieldBreak)
        {
            enemy.SetState(Enemy.EnemyState.Chase);
        }

        attackInProgress = false;
        currentAttack = null;
        currentAttackIndex = -1;
        lockedAttackIndex = -1;
    }

    // ───────────────── 히트 처리 (애니 이벤트) ─────────────────
    public void AttackHit()
    {
        if (!attackInProgress) return;
        if (currentAttack is MeleeAttackData meleeData)
        {
            SpawnMeleeHitBox(meleeData);
            // 쿨다운은 AttackTime 종료 시점에 시작 (여기서 제거)
        }
    }

    private void SpawnMeleeHitBox(MeleeAttackData meleeData)
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

    // ───────────────── Rush 로직 (전역/개별 쿨 반영) ─────────────────
    private void StartRushAttack(RushAttackData data, Transform target, int index)
    {
        StopRushCoroutines();

        runningRushIndex = index;
        rushTarget = target;

        enemy.SetState(Enemy.EnemyState.Attack);

        if (data.grantSuperArmor)
            enemy.AddSuperArmor(SuperArmorSource.Attack);
        else
            enemy.RemoveSuperArmor(SuperArmorSource.Attack);

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
                Vector3 dir = rushTarget.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(dir.normalized);
                }
            }

            // 인터럽트 등으로 상태 벗어났는지
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
            // 방향 보정 옵션
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

            // 상태 이탈 체크
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

        if (runningRushIndex >= 0)
        {
            float baseCd = data.cooldown;
            ApplyPerAttackCooldown(runningRushIndex, baseCd);
            if (baseCd > 0f)
                StartGlobalCooldown(1f);
        }

        enemy.RemoveSuperArmor(SuperArmorSource.Attack);

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

        enemy.RemoveSuperArmor(SuperArmorSource.Attack);

        runningRushIndex = -1;
        rushTarget = null;
    }

    public void StopRushExternally(bool noCooldown)
    {
        if (IsRushing || rushPrepareCoroutine != null)
        {
            if (!noCooldown && runningRushIndex >= 0)
            {
                float baseCd = GetAttackCooldown(runningRushIndex);
                ApplyPerAttackCooldown(runningRushIndex, baseCd);
                if (baseCd > 0f)
                    StartGlobalCooldown(1f);
            }
            StopRushCoroutines();
        }
    }

    // ───────────────── 유틸 ─────────────────
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

    private void ApplyPerAttackCooldown(int index, float baseCooldown)
    {
        if (index < 0 || index >= AttackCount) return;

        float applied = 0f;
        if (baseCooldown > 0f)
        {
            applied = baseCooldown < 1f ? 1f : baseCooldown;
        }
        // baseCooldown == 0 → applied 0 (재사용 즉시 가능)
        lastUsedTimes[index] = Time.time; // 판단 시 applied 더해서 비교

        // NOTE: per-attack 재사용 판정은 IsOffCooldown에서 applied 재계산
    }

    // 기존 외부에서 쓰던 쿨다운 인터페이스 유지(호환용) - 이제 전역 GCD만 의미
    public bool IsCooldownActive() => IsGlobalCooling();

    // 인터럽트: 모든 공격 쿨다운 리셋 (현행 유지)
    public void InterruptCooldown()
    {
        attackInProgress = false;
        if (enemy != null && enemy.animator != null)
            enemy.animator.speed = 1f;

        for (int i = 0; i < lastUsedTimes.Length; i++)
            lastUsedTimes[i] = -Mathf.Infinity;

        // 전역 GCD 해제
        globalAvailableTime = -Mathf.Infinity;

        currentAttackIndex = -1;
        currentAttack = null;
        lockedAttackIndex = -1;

        if (enemy != null &&
            enemy.CurrentState != Enemy.EnemyState.Dead &&
            enemy.CurrentState != Enemy.EnemyState.ShieldBreak)
        {
            enemy.SetState(Enemy.EnemyState.Chase);
        }

        enemy.RemoveSuperArmor(SuperArmorSource.Attack);
    }
}