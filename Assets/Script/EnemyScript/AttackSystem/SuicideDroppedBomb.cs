using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 자폭 도중 적이 플레이어에게 죽었을 때 떨어지는 폭탄.
/// - Initialize로 SuicideAttackData + 남은 시간(explodeAtTime)을 저장
/// - 시간이 되면 이 오브젝트의 transform.position(루트)에서 폭발(OverlapSphereNonAlloc)
/// - Unity 6 / 모바일: 프레임 독립(Time.time 기반), NonAlloc
/// </summary>
public class SuicideDroppedBomb : MonoBehaviour
{
    private SuicideAttackData data;
    private float explodeAtTime;

    private static readonly Collider[] s_overlap = new Collider[256];
    private bool exploded;

    // ✅ 스폰 시 살짝 위로 던지는 속도(m/s). (VelocityChange라 질량 영향 없음)
    private const float SPAWN_UP_VELOCITY = 1.5f;

    public void Initialize(SuicideAttackData data, float explodeAtTime)
    {
        this.data = data;
        this.explodeAtTime = explodeAtTime;
        exploded = false;

        // ✅ 리지드바디가 있으면 스폰 직후 살짝 위로 튀게
        var rb = GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.AddForce(Vector3.up * SPAWN_UP_VELOCITY, ForceMode.VelocityChange);
        }
    }

    private void Update()
    {
        if (exploded) return;
        if (data == null) { Destroy(gameObject); return; }

        if (Time.time >= explodeAtTime)
        {
            Explode();
        }
    }

    private void Explode()
    {
        if (exploded) return;
        exploded = true;

        Vector3 center = transform.position; // ✅ 드랍 폭탄은 루트에서 폭발
        float radius = data.explosionRadius;

        if (data.spawnDebugSphereOnExplode)
            SpawnDebugSphere(center, radius, data.debugSphereLifetime);

        int count = Physics.OverlapSphereNonAlloc(center, radius, s_overlap, ~0, QueryTriggerInteraction.Collide);

        var hitSeen = new HashSet<object>();

        for (int i = 0; i < count; i++)
        {
            var col = s_overlap[i];
            if (col == null) continue;

            Vector3 targetPos = col.bounds.center;
            float dist = Vector3.Distance(center, targetPos);
            if (dist > radius) continue;

            float t = radius > 0f ? dist / radius : 1f;
            t = Mathf.Clamp01(t);
            float mul = Mathf.Lerp(1f, data.edgeDamageMultiplier, t);

            float actualDamage = data.damage * mul;

            Vector3 hitDir = (targetPos - center);
            hitDir.y = 0f;
            if (hitDir.sqrMagnitude < 0.0001f) hitDir = Vector3.forward;
            hitDir.Normalize();

            Vector3? hitPoint = col.ClosestPoint(center);

            var ph = col.GetComponentInParent<PlayerHealth>() ?? col.GetComponent<PlayerHealth>();
            if (ph != null)
            {
                if (!hitSeen.Contains(ph))
                {
                    hitSeen.Add(ph);
                    ApplyExplosionToPlayer(ph, actualDamage, hitDir, mul, hitPoint);
                }
                continue;
            }

            if (data.explosionTargets == SuicideAttackData.SuicideExplosionTargetType.PlayerAndEnemies)
            {
                var eh = col.GetComponentInParent<EnemyHealth>() ?? col.GetComponent<EnemyHealth>();
                if (eh != null && !hitSeen.Contains(eh))
                {
                    hitSeen.Add(eh);
                    ApplyExplosionToEnemy(eh, actualDamage, hitDir, mul, hitPoint);
                }
            }
        }

        Destroy(gameObject);
    }

    private void ApplyExplosionToPlayer(PlayerHealth ph, float dmg, Vector3 hitDir, float mul, System.Nullable<Vector3> hitPoint = null)
    {
        if (ph == null) return;

        var pwc = ph.GetComponentInParent<PlayerWeaponController>() ?? ph.GetComponent<PlayerWeaponController>();
        if (pwc != null && pwc.IsInvincible())
            return;

        float kbPower = data.knockbackPower * mul;
        float kbDur = data.knockbackDuration * mul;
        float stun = data.stunDuration * mul;

        var deathProxy = WeaponDataSO.CreatePlayerDeathProxy(data.deathMode, data.ragdollImpulse, data.ragdollUpImpulse, data.ragdollSpinTorque, data.sliceTargets, data.sliceImpulse);
        ph.ApplyDamage(dmg, hitDir, deathProxy, 1f, hitPoint);

        if (ph.GetCurrentHP() <= 0f)
            return;

        if (pwc != null)
        {
            pwc.ForceApplyKnockback(hitDir, kbPower, kbDur, stun);
            return;
        }

        var pm = ph.GetComponentInParent<PlayerMovement>() ?? ph.GetComponent<PlayerMovement>();
        if (pm != null)
            pm.ApplyKnockback(hitDir, kbPower, kbDur, null);
    }

    private void ApplyExplosionToEnemy(EnemyHealth eh, float dmg, Vector3 hitDir, float mul, System.Nullable<Vector3> hitPoint = null)
    {
        if (eh == null) return;

        WeaponDataSO proxy = null;
        try
        {
            proxy = ScriptableObject.CreateInstance<WeaponDataSO>();
            proxy.hideFlags = HideFlags.HideAndDontSave;

            proxy.deathMode = data.deathMode;
            proxy.ragdollImpulse = data.ragdollImpulse;
            proxy.ragdollUpImpulse = data.ragdollUpImpulse;
            proxy.ragdollSpinTorque = data.ragdollSpinTorque;

            proxy.sliceTargets = data.sliceTargets != null ? new List<SliceTarget>(data.sliceTargets) : new List<SliceTarget>();
            proxy.sliceImpulse = data.sliceImpulse;

            proxy.targetHoldDuration = data.targetHoldDuration;
            proxy.usePushInsteadOfKnockback = data.usePushInsteadOfKnockback;

            proxy.knockbackPower = data.knockbackPower * mul;
            proxy.knockbackDuration = data.knockbackDuration * mul;
            proxy.stunDuration = data.stunDuration * mul;

            proxy.jerkIntensity = data.jerkIntensity;
            proxy.jerkDuration = data.jerkDuration;

            eh.ApplyDamage(dmg, hitDir, proxy, 1f, hitPoint);

            var e = eh.GetComponentInParent<Enemy>();
            if (e != null && e.CurrentState != Enemy.EnemyState.Dead)
            {
                if (proxy.usePushInsteadOfKnockback) e.ApplyPush(hitDir, proxy, 1f);
                else e.ApplyKnockback(hitDir, proxy, 1f);
            }
        }
        finally
        {
            if (proxy != null) Object.Destroy(proxy);
        }
    }

    private void SpawnDebugSphere(Vector3 pos, float radius, float lifeTime)
    {
        GameObject dbg = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dbg.transform.position = pos;
        dbg.transform.localScale = Vector3.one * radius * 2f;

        var col = dbg.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Destroy(dbg, lifeTime);
    }
}