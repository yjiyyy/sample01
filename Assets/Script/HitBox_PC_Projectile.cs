using UnityEngine;

/// <summary>
/// 투사체 힛박스
/// WeaponBehavior → SetWeapon() 으로 WeaponDataSO 주입
/// InitializeTowards() 로 탄속·수명 설정
/// </summary>
public class HitBox_PC_Projectile : MonoBehaviour
{
    private float speed;
    private float lifetime;
    private float damage;
    private float knockbackPower;
    private Vector3 moveDir;

    private WeaponDataSO weapon;
    public void SetWeapon(WeaponDataSO w) => weapon = w;

    /// <summary>
    /// 발사 방향을 직접 받아서 초기화
    /// </summary>
    public void InitializeTowards(Vector3 direction, float dmg, float spd, float life)
    {
        damage = dmg;
        speed = spd;
        lifetime = life;

        moveDir = direction.normalized;

        Destroy(gameObject, lifetime);
        Debug.Log($"🚀 Projectile Init │ dmg:{damage}, spd:{speed}, life:{lifetime}, moveDir:{moveDir}, impulse:{weapon?.ragdollImpulse}");
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

        if (other.GetComponentInParent<Health>() is Health hp)
        {
            Vector3 damageDir = moveDir;
            damageDir.y = 0f;
            damageDir = damageDir == Vector3.zero ? Vector3.back : damageDir.normalized;

            hp.ApplyDamage(damage, damageDir, weapon);
        }

        Destroy(gameObject);
    }
}
