using UnityEngine;

/// <summary>
/// 적의 공격 히트박스 (플레이어 전용)
/// EnemyAttackData에서 데미지/넉백/스턴 데이터를 받아 Initialize()로 세팅된다.
/// </summary>
public class HitBox_Enemy : MonoBehaviour
{
    private float damage;
    private float knockbackPower;
    private float knockbackDuration;
    private float stunDuration;

    public void Initialize(float dmg, float rng, float kbPower, float kbDuration, float lifetime, float stun = 0f)
    {
        damage = dmg;
        knockbackPower = kbPower;
        knockbackDuration = kbDuration;
        stunDuration = stun;

        Debug.Log($"[HitBox_Enemy] Init │ dmg:{damage}, kbPower:{knockbackPower}, kbDur:{knockbackDuration}, stun:{stunDuration}");
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (other.TryGetComponent(out Health hp))
                hp.ApplyDamage(damage);

            // ✅ PlayerMovement에서 넉백 처리
            if (other.TryGetComponent(out PlayerMovement playerMove))
            {
                // 몬스터 → 플레이어 방향
                Vector3 hitDir = (other.transform.position - transform.position).normalized;

                // 넉백 적용 (PlayerMovement 안에서 weight, 회전 처리)
                playerMove.ApplyKnockback(hitDir, knockbackPower, knockbackDuration, this.transform);
            }
        }
    }
}
