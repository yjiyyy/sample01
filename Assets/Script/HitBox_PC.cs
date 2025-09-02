using UnityEngine;

/// <summary>
/// 플레이어 무기의 힛박스(근접·투사체 공용)
/// 스폰 시 Initialize로 기본 파라미터를, SetWeapon으로 WeaponDataSO를 주입받는다.
/// </summary>
public class HitBox_PC : MonoBehaviour
{
    /* ─────────── 런타임 파라미터(스폰 시 주입) ─────────── */
    private float damage;
    private float knockbackPower;
    private float lifetime;
    private float range;

    /// <summary>무기 SO 주입용</summary>
    private WeaponDataSO weapon;

    /* ─────────── 초기화 메서드 ─────────── */
    public void Initialize(float dmg, float rng, float kbPower, float life)
    {
        damage = dmg;
        range = rng;
        knockbackPower = kbPower;
        lifetime = life;

        Debug.Log($"[HitBox_PC] Init │ dmg:{damage}, kb:{knockbackPower}, stun:{weapon?.stunDuration}");
        Destroy(gameObject, lifetime);
    }

    /// <summary>스폰 코드에서 무기 SO를 전달</summary>
    public void SetWeapon(WeaponDataSO w) => weapon = w;

    /* ─────────── 충돌 처리 ─────────── */
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Enemy")) return;

        Debug.Log($"[HitBox_PC] collide:{other.name} | weapon:{weapon?.name}");

        // 넉백 → Enemy.cs 내부 KnockbackThenStunRoutine에서 stunDuration 처리됨
        if (other.GetComponentInParent<Enemy>() is Enemy enemy)
        {
            Vector3 dir = (enemy.transform.position - transform.position).normalized;
            dir.y = 0f;

            // ✅ stunDuration은 WeaponDataSO에서 직접 사용
            enemy.ApplyKnockback(dir * knockbackPower, weapon);
        }

        // 데미지
        if (other.GetComponentInParent<Health>() is Health hp)
        {
            Vector3 dir = (other.transform.position - transform.position).normalized;
            hp.ApplyDamage(damage, dir, weapon);
        }
    }
}
