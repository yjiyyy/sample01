using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    /// <summary> true면 넉백/랙돌 방향을 플레이어 중심(transform.root) 기준으로 계산. 무기 콜리더 전용. </summary>
    private bool usePlayerCenterForDirection = false;

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

    /// <summary>
    /// 무기 콜리더에 붙어 있을 때 사용. Destroy 없이 lifetime 후 콜리더 비활성화는 호출자가 처리.
    /// 넉백/랙돌 방향은 플레이어 중심 기준으로 계산.
    /// </summary>
    public void InitializeAttached(float dmg, float rng, float kbPower, float life)
    {
        damage = dmg;
        range = rng;
        knockbackPower = kbPower;
        lifetime = life;
        duplicateEnabled = false;
        duplicateInterval = 0.2f;
        usePlayerCenterForDirection = true;
        overlapping.Clear();
        alreadyHit.Clear();
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

        // 방향 계산(수평). 무기 콜리더는 플레이어 중심, 스폰 히트박스는 히트박스 위치
        Vector3 origin = usePlayerCenterForDirection && transform.root != null ? transform.root.position : transform.position;
        Vector3 dir = (hp.transform.position - origin);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        dir.Normalize();

        // 1) 데미지 먼저 적용
        hp.ApplyDamage(damage, dir, weapon);
        Debug.Log($"✅ [HitBox_PC] {hp.name} hit │ dmg:{damage}, dup:{duplicateEnabled}");

        // 2) 사망 여부 확인 후 넉백/푸시 분기
        var enemy = hp.GetComponent<Enemy>() ?? hp.GetComponentInParent<Enemy>();
        if (enemy == null) return;

        if (enemy.CurrentState == Enemy.EnemyState.Dead)
        {
            // 치명타(사망)면 방향 전환/넉백/푸시 적용하지 않음
            return;
        }

        if (weapon != null && weapon.usePushInsteadOfKnockback)
        {
            // Push: 상태 변화 없음, 대상만 잠깐 밀기
            enemy.ApplyPush(dir, weapon);
        }
        else
        {
            // 기존 넉백 동작(상태 변화, 스턴 등)
            enemy.ApplyKnockback(dir, weapon);
        }
    }
}