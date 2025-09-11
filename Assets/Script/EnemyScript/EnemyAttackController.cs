using UnityEngine;

public class EnemyAttackController : MonoBehaviour
{
    [Header("공격 패턴 데이터 (SO 배열)")]
    public ScriptableObject[] attackPatterns;

    private ScriptableObject currentAttack;

    public int AttackCount => attackPatterns != null ? attackPatterns.Length : 0;

    /// <summary> Enemy.cs에서 호출: 이번 공격에 어떤 데이터를 쓸지 캐싱 </summary>
    public void NotifyAttack(int index)
    {
        if (index < 0 || index >= AttackCount) return;
        currentAttack = attackPatterns[index];
    }

    /// <summary> 애니메이션 이벤트 AttackHit에서 호출됨 </summary>
    public void AttackHit()
    {
        if (currentAttack == null) return;

        // 각 공격 유형에 맞게 처리
        if (currentAttack is MeleeAttackData meleeData)
        {
            HandleMeleeAttack(meleeData);
        }
        else if (currentAttack is RushAttackData rushData)
        {
            // 돌진 공격은 별도 로직에서 처리됨
            Debug.Log($"[EnemyAttackController] RushAttack 실행 중: {rushData.name}");
        }
    }

    private void HandleMeleeAttack(MeleeAttackData meleeData)
    {
        if (meleeData.hitBoxPrefab == null) return;

        GameObject go = Instantiate(meleeData.hitBoxPrefab, transform);

        if (go.TryGetComponent<HitBox_PC>(out var pcHitBox))
        {
            // Player 무기 힛박스 (Enemy 타격용)
            pcHitBox.Initialize(
                meleeData.damage,
                meleeData.range,
                meleeData.knockbackPower,
                meleeData.hitBoxLifetime
            );
        }
        else if (go.TryGetComponent<HitBox_Enemy>(out var enemyHitBox))
        {
            // Enemy 공격 힛박스 (Player 타격용)
            enemyHitBox.Initialize(
                meleeData.damage,
                meleeData.range,
                meleeData.knockbackPower,     // 힘
                meleeData.knockbackDuration,  // 시간
                meleeData.hitBoxLifetime,
                meleeData.stunDuration        // 스턴
            );
        }
    }

    public float GetAttackCooldown(int index)
    {
        if (index < 0 || index >= AttackCount) return 1f;

        if (attackPatterns[index] is MeleeAttackData meleeData)
            return meleeData.cooldown;
        else if (attackPatterns[index] is RushAttackData rushData)
            return rushData.cooldown;

        return 1f;
    }

    public float GetAttackRange(int index)
    {
        if (index < 0 || index >= AttackCount) return 2f;

        if (attackPatterns[index] is MeleeAttackData meleeData)
            return meleeData.range;
        else if (attackPatterns[index] is RushAttackData rushData)
            return 5f; // 러시 공격의 기본 사거리 설정

        return 2f;
    }
}