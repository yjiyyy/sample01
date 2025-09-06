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
            // ✅ 무적 상태 체크 (회피 중 무적)
            if (other.TryGetComponent(out PlayerWeaponController weaponController))
            {
                if (weaponController.IsInvincible())
                {
                    Debug.Log("[HitBox_Enemy] 플레이어 무적 상태 - 공격 무시됨");
                    return;
                }
            }

            // ✅ 데미지 적용
            if (other.TryGetComponent(out Health hp))
                hp.ApplyDamage(damage);

            // 🔧 PlayerWeaponController에서 넉백+스턴 처리 (최우선)
            if (weaponController != null)
            {
                // 몬스터 → 플레이어 방향
                Vector3 hitDir = (other.transform.position - transform.position).normalized;
                hitDir.y = 0f; // Y축 제거

                Debug.Log($"[HitBox_Enemy] 플레이어 공격! 넉백: {knockbackPower}, 스턴: {stunDuration}");

                // 🔧 기존 넉백/스턴을 강제 중단하고 새로운 넉백 적용
                weaponController.ForceApplyKnockback(hitDir, knockbackPower, knockbackDuration, stunDuration);
            }
            else
            {
                // ✅ PlayerMovement 넉백 (백업용 - PlayerWeaponController가 없을 때만)
                if (other.TryGetComponent(out PlayerMovement playerMove))
                {
                    Vector3 hitDir = (other.transform.position - transform.position).normalized;
                    playerMove.ApplyKnockback(hitDir, knockbackPower, knockbackDuration, this.transform);
                    Debug.Log("[HitBox_Enemy] PlayerMovement 백업 넉백 실행");
                }
            }
        }
    }
}