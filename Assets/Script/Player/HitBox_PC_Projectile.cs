using UnityEngine;

public class HitBox_PC_Projectile : MonoBehaviour
{
    private float speed;
    private float lifetime;
    private float damage;
    private float knockbackPower;
    private Vector3 moveDir;

    private WeaponDataSO weapon;
    public void SetWeapon(WeaponDataSO w) => weapon = w;

    public void InitializeTowards(Vector3 direction, float dmg, float spd, float life)
    {
        damage = dmg;
        speed = spd;
        lifetime = life;
        moveDir = direction.normalized;

        Destroy(gameObject, lifetime);
        Debug.Log($"🚀 Projectile Init │ dmg:{damage}, spd:{speed}, life:{lifetime}, moveDir:{moveDir}");
    }

    void Update()
    {
        transform.position += moveDir * speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Enemy")) return;

        if (other.GetComponentInParent<Enemy>() is Enemy enemy)
        {
            Vector3 knockbackDir = moveDir;
            knockbackDir.y = 0f;

            if (knockbackDir == Vector3.zero)
            {
                Debug.LogWarning("❗ moveDir이 0벡터입니다. fallback 적용");
                knockbackDir = Vector3.back;
            }

            knockbackDir = knockbackDir.normalized;

            Debug.Log($"💥 Projectile 충돌 │ 넉백 방향: {knockbackDir}");
            enemy.ApplyKnockback(knockbackDir * weapon.knockbackPower, weapon);
        }

        // 🔧 Health → EnemyHealth로 변경
        if (other.GetComponentInParent<EnemyHealth>() is EnemyHealth hp)
        {
            Vector3 damageDir = moveDir;
            damageDir.y = 0f;
            damageDir = damageDir == Vector3.zero ? Vector3.back : damageDir.normalized;

            hp.ApplyDamage(damage, damageDir, weapon);
            Debug.Log($"✅ [Projectile] EnemyHealth에 {damage} 데미지 적용!");
        }
        else
        {
            Debug.LogWarning($"❌ [Projectile] {other.name}에서 EnemyHealth를 찾을 수 없습니다!");
        }

        Destroy(gameObject);
    }
}