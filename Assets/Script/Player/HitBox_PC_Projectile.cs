using System.Collections.Generic;
using UnityEngine;

public class HitBox_PC_Projectile : MonoBehaviour
{
    private float speed;
    private float lifetime;
    private float damage;
    private Vector3 moveDir;

    private WeaponDataSO weapon;

    // 관통 관련
    private int remainingPierce = 0;
    private readonly HashSet<EnemyHealth> hitSet = new HashSet<EnemyHealth>();

    public void SetWeapon(WeaponDataSO w) => weapon = w;

    public void InitializeTowards(Vector3 direction, float dmg, float spd, float life)
    {
        damage = dmg;
        speed = spd;
        lifetime = life;
        moveDir = direction.normalized;

        Destroy(gameObject, lifetime);
        //Debug.Log($"🚀 Projectile Init │ dmg:{damage}, spd:{speed}, life:{lifetime}, moveDir:{moveDir}");
    }

    // 오버로드: 피어스 카운트까지 설정
    public void InitializeTowards(Vector3 direction, float dmg, float spd, float life, int pierceCount)
    {
        InitializeTowards(direction, dmg, spd, life);
        remainingPierce = Mathf.Max(0, pierceCount);
        if (remainingPierce > 0)
            Debug.Log($"🛡️ Pierce Enabled │ count:{remainingPierce}");
    }

    void Update()
    {
        transform.position += moveDir * speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Enemy")) return;

        // 대상 Health 찾기(중복 타격 방지용 키)
        var hp = other.GetComponentInParent<EnemyHealth>();
        if (hp == null)
        {
            Debug.LogWarning($"❌ [Projectile] {other.name}에서 EnemyHealth를 찾지 못했습니다.");
            return;
        }
        if (hitSet.Contains(hp))
            return; // 같은 적의 다른 콜라이더 중복 방지

        // 넉백 / Push 분기 처리
        if (other.GetComponentInParent<Enemy>() is Enemy enemy)
        {
            Vector3 knockbackDir = moveDir;
            knockbackDir.y = 0f;
            if (knockbackDir == Vector3.zero) knockbackDir = Vector3.back;
            knockbackDir = knockbackDir.normalized;

            if (weapon != null && weapon.usePushInsteadOfKnockback)
            {
                enemy.ApplyPush(knockbackDir, weapon);
                Debug.Log($"💥 Projectile 충돌 │ Push 방향: {knockbackDir}");
            }
            else
            {
                enemy.ApplyKnockback(knockbackDir, weapon);
                Debug.Log($"💥 Projectile 충돌 │ 넉백 방향: {knockbackDir}");
            }
        }

        // 데미지 1회 적용(Projectile 기준)
        {
            Vector3 damageDir = moveDir;
            damageDir.y = 0f;
            if (damageDir == Vector3.zero) damageDir = Vector3.back;
            damageDir = damageDir.normalized;

            hp.ApplyDamage(damage, damageDir, weapon);
            Debug.Log($"✅ [Projectile] EnemyHealth에 {damage} 데미지 적용!");
        }

        // 관통 처리
        hitSet.Add(hp);
        if (remainingPierce <= 0)
        {
            Destroy(gameObject);
            return;
        }

        remainingPierce--;
        if (remainingPierce <= 0)
        {
            Destroy(gameObject);
        }
    }
}