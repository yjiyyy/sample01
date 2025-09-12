using UnityEngine;
using System.Collections;

public class EnemyAttackController : MonoBehaviour
{
    [Header("공격 패턴 데이터 (SO 배열)")]
    public ScriptableObject[] attackPatterns;

    private ScriptableObject currentAttack;
    private float[] lastUsedTimes;
    public int AttackCount => attackPatterns != null ? attackPatterns.Length : 0;

    private bool isCooldown = false;
    private Coroutine cooldownRoutine;
    private Enemy enemy;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        int n = AttackCount;
        lastUsedTimes = n > 0 ? new float[n] : System.Array.Empty<float>();
        for (int i = 0; i < lastUsedTimes.Length; i++)
            lastUsedTimes[i] = -Mathf.Infinity;
    }

    public void NotifyAttack(int index)
    {
        if (index < 0 || index >= AttackCount) return;
        currentAttack = attackPatterns[index];
    }

    public void AttackHit()
    {
        if (currentAttack == null) return;

        if (currentAttack is MeleeAttackData meleeData)
        {
            HandleMeleeAttack(meleeData);
        }
        else if (currentAttack is RushAttackData rushData)
        {
            Debug.Log($"[EnemyAttackController] RushAttack 실행: {rushData.name}");
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
                meleeData.stunDuration
            );
        }
    }

    public float GetAttackCooldown(int index)
    {
        if (index < 0 || index >= AttackCount) return 1f;
        if (attackPatterns[index] is MeleeAttackData melee) return melee.cooldown;
        if (attackPatterns[index] is RushAttackData rush) return rush.cooldown;
        return 1f;
    }

    public float GetAttackRange(int index)
    {
        if (index < 0 || index >= AttackCount) return 2f;
        if (attackPatterns[index] is MeleeAttackData melee) return melee.range;
        if (attackPatterns[index] is RushAttackData) return 5f;
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
    }

    public float CooldownRemaining(int index)
    {
        if (index < 0 || index >= AttackCount) return 0f;
        float nextReady = lastUsedTimes[index] + GetAttackCooldown(index);
        return Mathf.Max(0f, nextReady - Time.time);
    }

    public bool IsCooldownActive() => isCooldown;

    public void InterruptCooldown()
    {
        if (isCooldown)
        {
            isCooldown = false;
            if (cooldownRoutine != null)
            {
                StopCoroutine(cooldownRoutine);
                cooldownRoutine = null;
            }
            if (enemy != null)
                enemy.SetState(Enemy.EnemyState.Chase);
        }
    }

    private IEnumerator CooldownRoutine(float duration)
    {
        isCooldown = true;

        float timer = 0f;
        while (timer < duration)
        {
            // 이동은 NavMeshAgent에 맡기고, Speed를 0으로 고정 (애니메이션만 Idle)
            if (enemy != null && enemy.animCtrl != null)
                enemy.animCtrl.UpdateMovement(0f);

            // 플레이어 바라보기 (쿨다운 중에도)
            if (enemy != null && enemy.agent != null && enemy.agent.isOnNavMesh)
            {
                enemy.agent.isStopped = false;
                Transform player = GameObject.FindWithTag("Player")?.transform;
                if (player != null)
                {
                    Vector3 dir = player.position - enemy.transform.position;
                    dir.y = 0f;
                    if (dir != Vector3.zero)
                        enemy.transform.rotation = Quaternion.LookRotation(dir);
                }
            }

            timer += Time.deltaTime;
            yield return null;

            // 만약 쿨다운이 중간에 강제 종료되면 코루틴 즉시 종료
            if (!isCooldown) yield break;
        }

        isCooldown = false;
        if (enemy != null)
            enemy.SetState(Enemy.EnemyState.Chase);
    }

    public int SelectAttackIndex(float distance)
    {
        int best = -1;
        float bestDelta = float.PositiveInfinity;
        for (int i = 0; i < AttackCount; i++)
        {
            if (!IsOffCooldown(i)) continue;
            float range = GetAttackRange(i);
            float delta = Mathf.Abs(distance - range);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                best = i;
            }
        }
        return best;
    }
}