using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class HitBox_Enemy : MonoBehaviour
{
    private float damage;
    private float knockbackPower;
    private float knockbackDuration;
    private float stunDuration;
    private WeaponDataSO playerDeathWeapon;

    // 드릴형 중복 히트 옵션
    private bool duplicateEnabled = false;
    private float duplicateInterval = 0.2f;

    // 겹침/히트 관리 (멀티 콜라이더 보호를 위해 PlayerHealth 기준)
    private readonly HashSet<PlayerHealth> overlapping = new();
    private readonly HashSet<PlayerHealth> alreadyHit = new();
    private Coroutine dupRoutine;

    public void Initialize(
        float dmg, float rng, float kbPower, float kbDuration, float lifetime, float stun = 0f,
        bool allowDup = false, float dupInterval = 0f, WeaponDataSO deathWeapon = null)
    {
        damage = dmg;
        knockbackPower = kbPower;
        knockbackDuration = kbDuration;
        stunDuration = stun;
        playerDeathWeapon = deathWeapon;

        duplicateEnabled = allowDup;
        duplicateInterval = Mathf.Max(0.01f, dupInterval);

        Debug.Log($"[HitBox_Enemy] Init │ dmg:{damage}, kbPower:{knockbackPower}, kbDur:{knockbackDuration}, stun:{stunDuration}, allowDup:{duplicateEnabled}, dupInterval:{duplicateInterval}");
        Destroy(gameObject, lifetime);

        // 드릴형(중복 히트)이면 주기 타이머 시작
        if (duplicateEnabled)
        {
            if (dupRoutine != null) StopCoroutine(dupRoutine);
            dupRoutine = StartCoroutine(DuplicateTickRoutine());
        }
    }

    private void OnDisable()
    {
        if (dupRoutine != null)
        {
            StopCoroutine(dupRoutine);
            dupRoutine = null;
        }
        overlapping.Clear();
        alreadyHit.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // PlayerHealth 기준으로 동일 대상 중복 방지
        var hp = other.GetComponentInParent<PlayerHealth>() ?? other.GetComponent<PlayerHealth>();
        if (hp == null) return;

        if (!duplicateEnabled)
        {
            // 즉발 1회: 동일 대상 중복 방지(멀티 콜라이더 보호)
            if (alreadyHit.Contains(hp)) return;
            alreadyHit.Add(hp);
            ApplyHit(hp, other);
            return;
        }

        // 드릴형: 진입 즉시 1회 + 겹침 등록
        ApplyHit(hp, other);
        overlapping.Add(hp);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!duplicateEnabled) return;
        if (!other.CompareTag("Player")) return;

        var hp = other.GetComponentInParent<PlayerHealth>() ?? other.GetComponent<PlayerHealth>();
        if (hp != null)
            overlapping.Remove(hp);
    }

    private IEnumerator DuplicateTickRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(duplicateInterval);

            if (overlapping.Count == 0) continue;

            // 스냅샷 순회(집합 변경 안전)
            var snapshot = new List<PlayerHealth>(overlapping);
            foreach (var hp in snapshot)
            {
                if (hp == null) continue;
                var col = hp.GetComponentInChildren<Collider>();
                ApplyHit(hp, col);
            }
        }
    }

    private void ApplyHit(PlayerHealth hp, Collider hitCollider)
    {
        if (hp == null) return;

        // 무적 상태(회피 무적) 체크
        var weaponController = hp.GetComponent<PlayerWeaponController>() ?? hp.GetComponentInParent<PlayerWeaponController>();
        if (weaponController != null && weaponController.IsInvincible())
        {
            Debug.Log("[HitBox_Enemy] 플레이어 무적 상태 - 공격 무시됨");
            return;
        }

        // 1) 데미지 적용 (플레이어 사망 시 랙돌 여부는 playerDeathWeapon.deathMode로 결정)
        Vector3 hitDirForDamage = (hp.transform.position - transform.position);
        hitDirForDamage.y = 0f;
        if (hitDirForDamage.sqrMagnitude < 0.0001f) hitDirForDamage = Vector3.forward;
        hitDirForDamage.Normalize();

        Vector3? hitPoint = hitCollider != null ? hitCollider.ClosestPoint(transform.position) : (Vector3?)null;
        hp.ApplyDamage(damage, hitDirForDamage, playerDeathWeapon, 1f, hitPoint);
        Debug.Log($"✅ [HitBox_Enemy] PlayerHealth에 {damage} 데미지 적용! (dup:{duplicateEnabled})");

        // ✅ 핵심: HP가 0 이하로 떨어졌으면 넉백/스턴을 절대 실행하지 않음 (즉시 Death 우선)
        if (hp.GetCurrentHP() <= 0f)
        {
            if (Debug.isDebugBuild)
                Debug.Log("[HitBox_Enemy] Player is dead after damage → skip knockback/stun.");
            return;
        }

        // 2) 넉백/스턴 처리
        Vector3 hitDir = (hp.transform.position - transform.position);
        hitDir.y = 0f;
        if (hitDir.sqrMagnitude < 0.0001f) hitDir = Vector3.forward;
        hitDir.Normalize();

        if (weaponController != null)
        {
            Debug.Log($"[HitBox_Enemy] 플레이어 공격! 넉백: {knockbackPower}, 스턴: {stunDuration}");
            weaponController.ForceApplyKnockback(hitDir, knockbackPower, knockbackDuration, stunDuration);
        }
        else
        {
            // 백업: PlayerMovement가 있을 경우 밀기
            var playerMove = hp.GetComponent<PlayerMovement>() ?? hp.GetComponentInChildren<PlayerMovement>();
            if (playerMove != null)
            {
                playerMove.ApplyKnockback(hitDir, knockbackPower, knockbackDuration, this.transform);
                Debug.Log("[HitBox_Enemy] PlayerMovement 백업 넉백 실행");
            }
        }
    }
}