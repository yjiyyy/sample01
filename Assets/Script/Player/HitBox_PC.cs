using UnityEngine;

public class HitBox_PC : MonoBehaviour
{
    private float damage;
    private float knockbackPower;
    private float lifetime;
    private float range;
    private WeaponDataSO weapon;

    public void Initialize(float dmg, float rng, float kbPower, float life)
    {
        damage = dmg;
        range = rng;
        knockbackPower = kbPower;
        lifetime = life;

        Debug.Log($"[HitBox_PC] Init │ dmg:{damage}, kb:{knockbackPower}, stun:{weapon?.stunDuration}");
        Destroy(gameObject, lifetime);
    }

    public void SetWeapon(WeaponDataSO w) => weapon = w;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Enemy")) return;

        Debug.Log($"[HitBox_PC] collide:{other.name} | weapon:{weapon?.name}");

        // 🔧 넉백 → Enemy.cs 내부에서 stunDuration 처리됨
        if (other.GetComponentInParent<Enemy>() is Enemy enemy)
        {
            Vector3 dir = (enemy.transform.position - transform.position).normalized;
            dir.y = 0f;
            enemy.ApplyKnockback(dir * knockbackPower, weapon);
        }

        // 🔧 Health → EnemyHealth로 변경
        if (other.GetComponentInParent<EnemyHealth>() is EnemyHealth hp)
        {
            Vector3 dir = (other.transform.position - transform.position).normalized;
            hp.ApplyDamage(damage, dir, weapon);
            Debug.Log($"✅ [HitBox_PC] EnemyHealth에 {damage} 데미지 적용!");
        }
        else
        {
            Debug.LogWarning($"❌ [HitBox_PC] {other.name}에서 EnemyHealth를 찾을 수 없습니다!");
        }
    }
}