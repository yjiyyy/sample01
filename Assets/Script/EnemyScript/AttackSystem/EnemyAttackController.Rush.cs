using System.Collections;
using UnityEngine;

public partial class EnemyAttackController
{
    /* Rush */
    public bool IsRushing { get; private set; } = false;
    private Coroutine rushPrepareCoroutine;
    private Coroutine rushCoroutine;
    private GameObject spawnedRushHitbox;
    private int runningRushIndex = -1;
    private Transform rushTarget;
    // 마지막 돌진 방향(마무리 감속에 사용)
    private Vector3 lastRushDir = Vector3.forward;

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
        ScheduleRushAttackFX(data, AttackFXPhase.Prepare);

        if (enemy.animator)
        {
            // 파라미터가 없어도 Play만으로 재생되도록
            if (data.prepareClip != null)
            {
                enemy.animator.speed = 1f;
                enemy.animator.Play(data.prepareClip.name, 0, 0f);
            }
            else
            {
                // 클립 없으면 이 상태(폴백): "RushPrepare"
                SafeSetBool("IsRushPrepare", true);
                SafeSetBool("IsRush", false);
                enemy.animator.Play("RushPrepare");
            }
        }

        float elapsed = 0f;
        while (elapsed < data.prepareDuration)
        {
            if (enemy != null && enemy.IsStateHoldActive)
            {
                yield return null;
                continue;
            }

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
        ScheduleRushAttackFX(data, AttackFXPhase.Attack);

        if (enemy.animator)
        {
            // 공격 클립 우선, 없으면 attackName, 그것도 없으면 "Rush"
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
        // 초기 진행 방향
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

        // FixedUpdate 기반 이동(프레임/플랫폼 독립)
        while (elapsed < data.attackDuration)
        {
            if (enemy != null && enemy.IsStateHoldActive)
            {
                yield return new WaitForFixedUpdate();
                continue;
            }

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
                    // 목표 방향으로 보간 가중치
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

        // 돌진 구간 종료 후 마무리 단계로 넘어감 (히트박스는 돌진 종료 시점에 제거)
        DespawnRushHitbox();

        // 마무리 코루틴 시작(아직 IsRushing 유지)
        rushCoroutine = StartCoroutine(RushFinishRoutine(data, lastRushDir));
    }

    private IEnumerator RushFinishRoutine(RushAttackData data, Vector3 dir)
    {
        ScheduleRushAttackFX(data, AttackFXPhase.Finish);

        // 마무리 클립(선택) 재생
        if (enemy.animator && data.finishClip != null)
        {
            enemy.animator.speed = 1f;
            enemy.animator.Play(data.finishClip.name, 0, 0f);
        }

        float dur = Mathf.Max(0f, data.finishDuration);
        float elapsed = 0f;

        // 감속 구간: rushSpeed → 0
        float initialSpeed = Mathf.Max(0f, data.rushSpeed);

        Vector3 finishDir = dir;
        finishDir.y = 0f;
        if (finishDir.sqrMagnitude < 0.0001f) finishDir = transform.forward;
        finishDir.Normalize();

        while (elapsed < dur)
        {
            if (enemy != null && enemy.IsStateHoldActive)
            {
                yield return new WaitForFixedUpdate();
                continue;
            }

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

            // 마무리 중에도 돌진 방향 이동만 유지
            enemy.MoveFilteredDisplacement(disp);

            // 시선은 진행 방향 유지
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
                data.duplicateHitInterval,
                WeaponDataSO.CreatePoisonPlayerHitProxyOrNull(data.isPoisonAttack, data.poisonOnHitStatus),
                data.targetHoldDuration,
                data.usePushInsteadOfKnockback,
                data.attackerHoldDuration,
                GetComponentInParent<Enemy>()
            );
        }
    }

    private void DespawnRushHitbox()
    {
        if (spawnedRushHitbox != null)
            Destroy(spawnedRushHitbox);
        spawnedRushHitbox = null;
    }

    private void ScheduleRushAttackFX(RushAttackData data, AttackFXPhase phase)
    {
        if (data == null) return;
        var fxList = AttackFXPhaseResolver.Resolve(data.attackFXPhases, phase);
        if (fxList == null || fxList.Count == 0) return;
        bool IsHold() => enemy != null && enemy.IsStateHoldActive;
        AttackFXEntry.ScheduleAttackFX(this, fxList, ResolveEnemyAttackFXEntry, IsHold);
    }

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

    private void InterruptRushIfNeeded()
    {
        if (rushPrepareCoroutine != null || IsRushing)
        {
            Log("INTERRUPT rush -> cancel");
            StopRushCoroutines();
            IsRushing = false;
            CancelRushNoCooldown();
        }
    }
}
