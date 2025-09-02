using UnityEngine;

public class EnemyAttackController : MonoBehaviour
{
    [Header("공격 패턴 데이터 (SO 배열)")]
    public EnemyAttackData[] attackPatterns;

    private EnemyAttackData currentAttack;

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
        if (currentAttack == null || currentAttack.hitBoxPrefab == null) return;

        GameObject go = Instantiate(currentAttack.hitBoxPrefab, transform);

        if (go.TryGetComponent<HitBox_PC>(out var pcHitBox))
        {
            // Player 무기 힛박스 (Enemy 타격용)
            pcHitBox.Initialize(
                currentAttack.damage,
                currentAttack.range,
                currentAttack.knockbackPower,
                currentAttack.hitBoxLifetime
            // stunDuration은 WeaponDataSO에서 관리됨
            );
        }
        else if (go.TryGetComponent<HitBox_Enemy>(out var enemyHitBox))
        {
            // Enemy 공격 힛박스 (Player 타격용)
            enemyHitBox.Initialize(
                currentAttack.damage,
                currentAttack.range,
                currentAttack.knockbackPower,     // 힘
                currentAttack.knockbackDuration,  // 시간
                currentAttack.hitBoxLifetime,
                currentAttack.stunDuration        // 스턴
            );
        }
    }

    public float GetAttackCooldown(int index)
    {
        if (index < 0 || index >= AttackCount) return 1f;
        return attackPatterns[index].cooldown;
    }

    public float GetAttackRange(int index)
    {
        if (index < 0 || index >= AttackCount) return 2f;
        return attackPatterns[index].range;
    }
}
