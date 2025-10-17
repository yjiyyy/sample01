using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class HitBox_PC : MonoBehaviour
{
    private float damage;
    private float knockbackPower; // 주입만 받고 사용은 SO(weapon) 측 값 적용
    private float lifetime;
    private float range;
    private WeaponDataSO weapon;

    // 드릴형 중복 히트 옵션
    private bool duplicateEnabled = false;
    private float duplicateInterval = 0.2f;

    // 겹침/히트 관리
    private readonly HashSet<EnemyHealth> overlapping = new();
    private readonly HashSet<EnemyHealth> alreadyHit = new();
    private Coroutine dupRoutine;

    // ───────── 오버로드 1: 즉발 1회 ─────────
    public void Initialize(float dmg, float rng, float kbPower, float life)
    {
        damage = dmg;
        range = rng;
        knockbackPower = kbPower;
        lifetime = life;

        duplicateEnabled = false;
        duplicateInterval = 0.2f;

        Debug.Log($"[HitBox_PC] Init │ dmg:{damage}, kb:{knockbackPower}, life:{lifetime}, Dup:false");
        Destroy(gameObject, lifetime);
    }

    // ───────── 오버로드 2: 드릴형 중복 히트 ─────────
    // allowDup=true면 겹쳐 있는 동안 dupInterval마다 재타격(매번 데미지+넉백+스턴 적용)
    public void Initialize(float dmg, float rng, float kbPower, float life, bool allowDup, float dupInterval)
    {
        damage = dmg;
        range = rng;
        knockbackPower = kbPower;
        lifetime = life;

        duplicateEnabled = allowDup;
        duplicateInterval = Mathf.Max(0.01f, dupInterval);

        Debug.Log($"[HitBox_PC] Init │ dmg:{damage}, kb:{knockbackPower}, life:{lifetime}, Dup:{duplicateEnabled}, interval:{duplicateInterval}");
        Destroy(gameObject, lifetime);

        if (duplicateEnabled)
        {
            if (dupRoutine != null) StopCoroutine(dupRoutine);
            dupRoutine = StartCoroutine(DuplicateTickRoutine());
        }
    }

    public void SetWeapon(WeaponDataSO w) => weapon = w;

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
        if (!other.CompareTag("Enemy")) return;

        var hp = other.GetComponentInParent<EnemyHealth>();
        if (hp == null) return;

        if (!duplicateEnabled)
        {
            // 즉발 1회: 동일 대상 중복방지(멀티 콜라이더 보호)
            if (alreadyHit.Contains(hp)) return;
            alreadyHit.Add(hp);
            ApplyHit(hp);
            return;
        }

        // 중복 히트: 진입 즉시 1회 + 겹침 등록
        ApplyHit(hp);
        overlapping.Add(hp);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!duplicateEnabled) return;
        if (!other.CompareTag("Enemy")) return;

        var hp = other.GetComponentInParent<EnemyHealth>();
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
            var snapshot = new List<EnemyHealth>(overlapping);
            foreach (var hp in snapshot)
            {
                if (hp == null) continue;
                ApplyHit(hp);
            }
        }
    }

    private void ApplyHit(EnemyHealth hp)
    {
        if (hp == null) return;

        // 방향 계산(수평)
        Vector3 dir = (hp.transform.position - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        dir.Normalize();

        // 데미지
        hp.ApplyDamage(damage, dir, weapon);

        // 넉백/스턴(무기 SO 값 사용)
        var enemy = hp.GetComponent<Enemy>() ?? hp.GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            // power/duration/stun은 weapon에서 읽음 → 방향만 전달
            enemy.ApplyKnockback(dir, weapon);
        }

        Debug.Log($"✅ [HitBox_PC] {hp.name} hit │ dmg:{damage}, dup:{duplicateEnabled}");
    }
}