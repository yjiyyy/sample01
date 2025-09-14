using UnityEngine;
using System.Collections;

public class EnemyAttackController : MonoBehaviour
{
    [Header("공격 패턴 데이터 (SO 배열)")]
    public ScriptableObject[] attackPatterns;

    private ScriptableObject currentAttack;
    private int currentAttackIndex = -1;
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
        if (index < 0 || index >= AttackCount) { currentAttack = null; currentAttackIndex = -1; return; }
        currentAttack = attackPatterns[index];
        currentAttackIndex = index;
    }

    public void AttackHit()
    {
        if (currentAttack == null || currentAttackIndex < 0) return;

        if (currentAttack is MeleeAttackData meleeData)
        {
            HandleMeleeAttack(meleeData);
            BeginCooldown(currentAttackIndex); // 밀리어택 쿨다운
        }
        // RushAttack 등은 별도 관리
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
        return 1f;
    }

    public float GetAttackRange(int index)
    {
        if (index < 0 || index >= AttackCount) return 2f;
        if (attackPatterns[index] is MeleeAttackData melee) return melee.range;
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