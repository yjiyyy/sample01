using UnityEngine;

public class EnemyAttackController : MonoBehaviour
{
    [Header("공격 패턴 데이터 (SO 배열)")]
    public ScriptableObject[] attackPatterns;

    private ScriptableObject currentAttack;

    // 쿨다운 타이머(공격 인덱스별)
    private float[] lastUsedTimes;

    public int AttackCount => attackPatterns != null ? attackPatterns.Length : 0;

    private void Awake()
    {
        int n = AttackCount;
        lastUsedTimes = n > 0 ? new float[n] : System.Array.Empty<float>();
        for (int i = 0; i < lastUsedTimes.Length; i++)
            lastUsedTimes[i] = -Mathf.Infinity;
    }

    // 이번 공격에 어떤 패턴을 쓸지 캐싱
    public void NotifyAttack(int index)
    {
        if (index < 0 || index >= AttackCount) return;
        currentAttack = attackPatterns[index];
    }

    // 애니메이션 이벤트에서 호출
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

        if (go.TryGetComponent<HitBox_PC>(out var pcHitBox))
        {
            // 플레이어용 히트박스와 호환
            pcHitBox.SetWeapon(null);
            pcHitBox.Initialize(meleeData.damage, meleeData.range, meleeData.knockbackPower, meleeData.hitBoxLifetime);
        }
        else if (go.TryGetComponent<HitBox_Enemy>(out var enemyHitBox))
        {
            // 플레이어를 때리는 적 히트박스
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

    /* ───────── 쿨다운/사거리 ───────── */

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
        if (attackPatterns[index] is RushAttackData) return 5f; // 정책값
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
    }

    public float CooldownRemaining(int index)
    {
        if (index < 0 || index >= AttackCount) return 0f;
        float nextReady = lastUsedTimes[index] + GetAttackCooldown(index);
        return Mathf.Max(0f, nextReady - Time.time);
    }

    // 거리/쿨다운 조건을 모두 만족하는 공격 선택
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