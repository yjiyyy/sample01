using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class HitBox_PC : MonoBehaviour
{
    private float damage;
    private float knockbackPower;
    private float lifetime;
    private float range;
    private WeaponDataSO weapon;

    // AoE-DoT(틱 모드) 옵션
    private bool areaDotEnabled = false;
    private float dotDamagePerTick = 0f;
    private float dotTickInterval = 0.2f;
    private readonly HashSet<EnemyHealth> overlapping = new();
    private Coroutine dotRoutine;

    // ───────── 오버로드 1: 기존 4-인자 (기본 동작) ─────────
    public void Initialize(float dmg, float rng, float kbPower, float life)
    {
        damage = dmg;
        range = rng;
        knockbackPower = kbPower;
        lifetime = life;

        areaDotEnabled = false; // 기본 OFF
        dotDamagePerTick = 0f;
        dotTickInterval = 0.2f;

        Debug.Log($"[HitBox_PC] Init │ dmg:{damage}, kb:{knockbackPower}, life:{lifetime}, DoT:false");
        Destroy(gameObject, lifetime);
    }

    // ───────── 오버로드 2: AoE-DoT(틱 모드) 포함 ─────────
    // enableAreaDot=true이면 라이프타임 동안 dotTickInterval 주기로 dotDamagePerTick만큼 피해(즉발 1회 타격은 생략)
    public void Initialize(float dmg, float rng, float kbPower, float life, bool enableAreaDot, float tickDamage, float tickInterval)
    {
        damage = dmg;
        range = rng;
        knockbackPower = kbPower;
        lifetime = life;

        areaDotEnabled = enableAreaDot;
        dotDamagePerTick = Mathf.Max(0f, tickDamage);
        dotTickInterval = Mathf.Max(0.01f, tickInterval);

        Debug.Log($"[HitBox_PC] Init │ dmg:{damage}, kb:{knockbackPower}, life:{lifetime}, DoT:{areaDotEnabled}, tick:{dotDamagePerTick}@{dotTickInterval}s");
        Destroy(gameObject, lifetime);

        if (areaDotEnabled)
        {
            if (dotRoutine != null) StopCoroutine(dotRoutine);
            dotRoutine = StartCoroutine(DotTickRoutine());
        }
    }

    public void SetWeapon(WeaponDataSO w) => weapon = w;

    private void OnDisable()
    {
        if (dotRoutine != null)
        {
            StopCoroutine(dotRoutine);
            dotRoutine = null;
        }
        overlapping.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Enemy")) return;

        // DoT 모드: 겹침 등록만, 즉발 타격은 생략
        if (areaDotEnabled)
        {
            var hp = other.GetComponentInParent<EnemyHealth>();
            if (hp != null)
                overlapping.Add(hp);
            return;
        }

        Debug.Log($"[HitBox_PC] collide:{other.name} | weapon:{weapon?.name}");

        // 넉백 → Enemy.cs 내부에서 stunDuration 처리됨
        if (other.GetComponentInParent<Enemy>() is Enemy enemy)
        {
            Vector3 dir = (enemy.transform.position - transform.position).normalized;
            dir.y = 0f;
            enemy.ApplyKnockback(dir * knockbackPower, weapon);
        }

        // 데미지 1회 적용
        if (other.GetComponentInParent<EnemyHealth>() is EnemyHealth hp2)
        {
            Vector3 dir = (other.transform.position - transform.position).normalized;
            hp2.ApplyDamage(damage, dir, weapon);
            Debug.Log($"✅ [HitBox_PC] EnemyHealth에 {damage} 데미지 적용!");
        }
        else
        {
            Debug.LogWarning($"❌ [HitBox_PC] {other.name}에서 EnemyHealth를 찾을 수 없습니다!");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!areaDotEnabled) return;
        if (!other.CompareTag("Enemy")) return;

        var hp = other.GetComponentInParent<EnemyHealth>();
        if (hp != null)
            overlapping.Remove(hp);
    }

    private IEnumerator DotTickRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(dotTickInterval);

            if (overlapping.Count == 0) continue;

            // 스냅샷 순회(집합 변경 안전)
            var snapshot = new List<EnemyHealth>(overlapping);
            foreach (var hp in snapshot)
            {
                if (hp == null) continue;

                Vector3 dir = (hp.transform.position - transform.position).normalized;
                dir.y = 0f;
                hp.ApplyDamage(dotDamagePerTick, dir, weapon);
                Debug.Log($"🟢 [HitBox_PC.DoT] {hp.name}에 {dotDamagePerTick} 틱 데미지");
            }
        }
    }
}