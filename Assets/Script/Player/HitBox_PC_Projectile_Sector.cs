using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class HitBox_PC_Projectile_Sector : MonoBehaviour
{
    [Header("무기 데이터 (WeaponBehavior에서 Initialize로 주입 권장)")]
    [SerializeField] private WeaponDataSO weaponData;

    [Header("디버그")]
    [SerializeField] private bool debugDrawGizmos = false;

    [Header("피해 대상 레이어 (Enemy 전용 권장)")]
    [SerializeField] private LayerMask damageLayers = ~0;

    private Rigidbody rb;
    private Collider col;

    private bool initialized;
    private bool hasExploded;
    private Vector3 launchForward;

    private float cachedLifetime, cachedSpeed, cachedDamage, cachedRadius, cachedEdgeMul;
    private float lifeTimer;

    private WeaponDataSO_Launcher launcherData;

    private static readonly Collider[] Overlap = new Collider[256];

    private const float DEF_LIFETIME = 5f;
    private const float DEF_SPEED = 30f;
    private const float DEF_DAMAGE = 20f;
    private const float DEF_RADIUS = 3f;
    private const float DEF_EDGE = 0.2f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    public void Initialize(WeaponDataSO data, Vector3 forward)
    {
        weaponData = data;
        launcherData = data as WeaponDataSO_Launcher;
        launchForward = forward;

        cachedLifetime = launcherData != null ? launcherData.projectileLifetime : DEF_LIFETIME;
        cachedSpeed = launcherData != null ? launcherData.projectileSpeed : DEF_SPEED;
        cachedDamage = data != null ? data.damage : DEF_DAMAGE;
        cachedRadius = launcherData != null ? launcherData.explosiveRadius : DEF_RADIUS;
        cachedEdgeMul = launcherData != null ? launcherData.explosiveEdgeMul : DEF_EDGE;

        initialized = true;
        hasExploded = false;
        lifeTimer = 0f;

        // Lifetime이 지나면 Destroy
        Destroy(gameObject, cachedLifetime);
    }

    private void Update()
    {
        if (!initialized) return;
        lifeTimer += Time.deltaTime;

        if (lifeTimer >= cachedLifetime)
        {
            Explode();
        }
        else
        {
            // 직선 이동
            transform.position += launchForward * cachedSpeed * Time.deltaTime;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!initialized || hasExploded) return;

        // LayerMask 필터
        if ((damageLayers.value & (1 << other.gameObject.layer)) == 0)
            return;

        Explode();
    }

    private void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        int count = Physics.OverlapSphereNonAlloc(transform.position, cachedRadius, Overlap, damageLayers);
        for (int i = 0; i < count; i++)
        {
            Collider target = Overlap[i];

            // --- DamageTargetType 필터링 ---
            bool isEnemy = target.CompareTag("Enemy");
            bool isPlayer = target.CompareTag("Player");

            DamageTargetType tgt = launcherData != null ? launcherData.damageTargetType : DamageTargetType.EnemyOnly;
            switch (tgt)
            {
                case DamageTargetType.EnemyOnly:
                    if (!isEnemy) continue;
                    break;
                case DamageTargetType.PlayerOnly:
                    if (!isPlayer) continue;
                    break;
                case DamageTargetType.Both:
                    break;
            }

            // --- 데미지 계산 ---
            float distance = Vector3.Distance(transform.position, target.transform.position);
            float t = Mathf.Clamp01(distance / cachedRadius);
            float finalDamage = Mathf.Lerp(cachedDamage * cachedEdgeMul, cachedDamage, 1f - t);

            if (target.TryGetComponent(out PlayerHealth playerHP))
            {
                Vector3 hitDir = (target.transform.position - transform.position).normalized;
                float impactScale = 1f;
                playerHP.ApplyDamage(finalDamage, hitDir, weaponData, impactScale);
                Debug.Log($"✅ [Explosion] PlayerHealth에 {finalDamage} 데미지!");
            }
            else if (target.TryGetComponent(out EnemyHealth enemyHP))
            {
                Vector3 hitDir = (target.transform.position - transform.position).normalized;
                float impactScale = 1f;
                enemyHP.ApplyDamage(finalDamage, hitDir, weaponData, impactScale);
                Debug.Log($"✅ [Explosion] EnemyHealth에 {finalDamage} 데미지!");

                // 넉백/푸시 분기 (weaponData 기준)
                var enemy = target.GetComponentInParent<Enemy>();
                if (enemy != null)
                {
                    if (weaponData != null && weaponData.usePushInsteadOfKnockback)
                    {
                        enemy.ApplyPush(hitDir, weaponData, impactScale);
                    }
                    else
                    {
                        enemy.ApplyKnockback(hitDir, weaponData, impactScale);
                    }
                }
            }
        }

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        if (debugDrawGizmos)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, cachedRadius > 0 ? cachedRadius : DEF_RADIUS);
        }
    }
}