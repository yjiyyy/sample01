using UnityEngine;
using System.Collections.Generic;

public class HitBox_Enemy : MonoBehaviour
{
    private float damage;
    private float knockbackPower;
    private float knockbackDuration;
    private float stunDuration;

    // 중복 데미지 옵션
    private bool allowDuplicateHit = false;
    private float duplicateHitInterval = 0f;
    private Dictionary<GameObject, float> lastHitTimes = new();

    public void Initialize(
        float dmg, float rng, float kbPower, float kbDuration, float lifetime, float stun = 0f,
        bool allowDup = false, float dupInterval = 0f)
    {
        damage = dmg;
        knockbackPower = kbPower;
        knockbackDuration = kbDuration;
        stunDuration = stun;

        allowDuplicateHit = allowDup;
        duplicateHitInterval = dupInterval;

        Debug.Log($"[HitBox_Enemy] Init │ dmg:{damage}, kbPower:{knockbackPower}, kbDur:{knockbackDuration}, stun:{stunDuration}, allowDup:{allowDuplicateHit}, dupInterval:{duplicateHitInterval}");
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        GameObject playerGO = other.gameObject;
        float now = Time.time;

        if (!allowDuplicateHit)
        {
            if (lastHitTimes.ContainsKey(playerGO)) return;
        }
        else
        {
            if (lastHitTimes.TryGetValue(playerGO, out float lastTime))
            {
                if (now - lastTime < duplicateHitInterval)
                    return;
            }
        }
        lastHitTimes[playerGO] = now;

        // ✅ 무적 상태 체크 (회피 중 무적)
        if (other.TryGetComponent(out PlayerWeaponController weaponController))
        {
            if (weaponController.IsInvincible())
            {
                Debug.Log("[HitBox_Enemy] 플레이어 무적 상태 - 공격 무시됨");
                return;
            }
        }

        // 🔧 Health → PlayerHealth로 변경
        if (other.TryGetComponent(out PlayerHealth hp))
        {
            hp.ApplyDamage(damage);
            Debug.Log($"✅ [HitBox_Enemy] PlayerHealth에 {damage} 데미지 적용!");
        }
        else
        {
            Debug.LogWarning($"❌ [HitBox_Enemy] {other.name}에서 PlayerHealth를 찾을 수 없습니다!");
        }

        // 🔧 PlayerWeaponController에서 넉백+스턴 처리 (최우선)
        if (other.TryGetComponent(out PlayerWeaponController weaponController2))
        {
            Vector3 hitDir = (other.transform.position - transform.position).normalized;
            hitDir.y = 0f;

            Debug.Log($"[HitBox_Enemy] 플레이어 공격! 넉백: {knockbackPower}, 스턴: {stunDuration}");
            weaponController2.ForceApplyKnockback(hitDir, knockbackPower, knockbackDuration, stunDuration);
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