using UnityEngine;

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

            // 🔧 Health 컴포넌트 사용 (PlayerHealth는 Health를 상속하므로 호환됨)
            if (other.TryGetComponent(out Health hp))
            {
                hp.ApplyDamage(damage);
                Debug.Log($"✅ [HitBox_Enemy] Health에 {damage} 데미지 적용!");
            }
            else
            {
                Debug.LogWarning($"❌ [HitBox_Enemy] {other.name}에서 Health를 찾을 수 없습니다!");
            }

            // 🔧 PlayerWeaponController에서 넉백+스턴 처리 (최우선)
            if (weaponController != null)
            {
                Vector3 hitDir = (other.transform.position - transform.position).normalized;
                hitDir.y = 0f;

                Debug.Log($"[HitBox_Enemy] 플레이어 공격! 넉백: {knockbackPower}, 스턴: {stunDuration}");
                weaponController.ForceApplyKnockback(hitDir, knockbackPower, knockbackDuration, stunDuration);
            }
            else if (other.TryGetComponent(out PlayerMovement playerMove))
            {
                // ✅ PlayerMovement 넉백 (백업용)
                Vector3 hitDir = (other.transform.position - transform.position).normalized;
                playerMove.ApplyKnockback(hitDir, knockbackPower, knockbackDuration, this.transform);
                Debug.Log("[HitBox_Enemy] PlayerMovement 백업 넉백 실행");
            }
        }
    }
}