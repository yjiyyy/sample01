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
    // ������ ���� ����(������ ���ӿ� ���)
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
        if (enemy.animator)
        {
            // �Ķ���Ͱ� ��� Play������ �����ϵ���
            if (data.prepareClip != null)
            {
                enemy.animator.speed = 1f;
                enemy.animator.Play(data.prepareClip.name, 0, 0f);
            }
            else
            {
                // Ŭ�� ������ �� ����(������): "RushPrepare"
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

        if (enemy.animator)
        {
            // ���� Ŭ�� �켱, ������ attackName, �׵� ������ "Rush"
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
        // �ʱ� ���� ����
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

        // FixedUpdate ��� �̵�(�÷���/������ ����)
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
                    // ���� ������ ��� ����ġ
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

        // ���� ���� ���� �� ������ �������� �Ѿ (��Ʈ�ڽ��� ���� ����������)
        DespawnRushHitbox();

        // ������ ��ƾ ����(��� IsRushing ����)
        rushCoroutine = StartCoroutine(RushFinishRoutine(data, lastRushDir));
    }

    private IEnumerator RushFinishRoutine(RushAttackData data, Vector3 dir)
    {
        // ������ Ŭ��(����) ���
        if (enemy.animator && data.finishClip != null)
        {
            enemy.animator.speed = 1f;
            enemy.animator.Play(data.finishClip.name, 0, 0f);
        }

        float dur = Mathf.Max(0f, data.finishDuration);
        float elapsed = 0f;

        // ���� ����: rushSpeed �� 0
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

            // ������ �߿��� ���� ���� ���� ���Ӹ�
            enemy.MoveFilteredDisplacement(disp);

            // �ü��� ������ ���� ����
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
                null
            );
        }
    }

    private void DespawnRushHitbox()
    {
        if (spawnedRushHitbox != null)
            Destroy(spawnedRushHitbox);
        spawnedRushHitbox = null;
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