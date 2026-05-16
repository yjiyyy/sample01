using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Explosion hitbox: 주변 피해 + 플레이어/적 대상 선택 + 넉백 호출
/// - OverlapSphereNonAlloc(트리거 포함) 사용
/// - 주변의 PlayerHealth / EnemyHealth를 robust하게 탐색
/// - owner(발사한 적) 정보를 이용해 explosionTargets를 Player/Enemy/Both 처리.
/// </summary>
public class HitBox_Enemy_Explosion : MonoBehaviour
{
    private static readonly Collider[] Overlap = new Collider[256];

    private TimeProjectileAttackData data;
    private Enemy enemyOwner; // 발사한 적 (owner) 정보

    // shared material for debug sphere to avoid per-instance material allocations
    private static Material s_debugSphereMaterial;

    // simple set to avoid double-hitting same target in one explosion
    private readonly HashSet<object> hitSeen = new HashSet<object>();

    // Verbose logs for diagnostics
    private const bool VERBOSE = true;

    public void Initialize(TimeProjectileAttackData data, Enemy owner)
    {
        this.data = data;
        this.enemyOwner = owner;

        DoExplosion();
    }

    private void DoExplosion()
    {
        if (data == null)
        {
            Debug.LogWarning("[HitBox_Enemy_Explosion] data is null.");
            Destroy(gameObject);
            return;
        }

        Vector3 center = transform.position;

        Debug.Log($"[Explosion] Triggered at {center} radius={data.explosionRadius} damage={data.damage} targets={data.explosionTargets}");

        // Scene-wide EnemyHealth scan for diagnostics (use new API)
        var allEnemyHealth = Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
        if (allEnemyHealth == null || allEnemyHealth.Length == 0)
        {
            Debug.LogWarning("[Explosion][DIAG] No EnemyHealth components found in scene (FindObjectsByType returned 0).");
        }
        else if (VERBOSE)
        {
            Debug.Log($"[Explosion][DIAG] Found {allEnemyHealth.Length} EnemyHealth in scene. Distances to explosion center:");
            for (int ei = 0; ei < allEnemyHealth.Length; ei++)
            {
                var eh = allEnemyHealth[ei];
                if (eh == null) continue;
                float d = Vector3.Distance(center, eh.transform.position);
                Debug.Log($"  - EnemyHealth[{ei}] '{eh.gameObject.name}' at dist={d:F3} (withinRadius={(d <= data.explosionRadius)})");
            }
        }

        // OverlapSphereNonAlloc (Trigger 포함)
        int count = Physics.OverlapSphereNonAlloc(center, data.explosionRadius, Overlap, ~0, QueryTriggerInteraction.Collide);
        Debug.Log($"[Explosion] Overlap found {count} colliders (including triggers).");

        for (int i = 0; i < count; i++)
        {
            Collider col = Overlap[i];
            if (col == null) continue;

            string colName = col.gameObject.name;
            string layerName = LayerMask.LayerToName(col.gameObject.layer);
            Debug.Log($"[Explosion] Collider[{i}] = {colName} (layer={layerName})");

            // Skip projectile/hitbox objects themselves to avoid self-detection.
            if (IsLikelyProjectileOrHitbox(col))
            {
                Debug.Log($"[Explosion] Collider '{colName}' appears to be a projectile/hitbox -> SKIP");
                continue;
            }

            // Try to robustly find health components
            PlayerHealth ph = TryFindPlayerHealth(col);
            EnemyHealth eh = TryFindEnemyHealth(col);

            Vector3 targetPos = col.bounds.center;
            float dist = Vector3.Distance(center, targetPos);
            if (dist > data.explosionRadius)
            {
                Debug.Log("[Explosion] Collider outside radius after precise check.");
                continue;
            }

            float t = data.explosionRadius > 0f ? dist / data.explosionRadius : 1f;
            t = Mathf.Clamp01(t);
            float mul = Mathf.Lerp(1f, data.edgeDamageMultiplier, t); // distance-based multiplier (1@center -> edgeDamageMultiplier@edge)
            float actualDamage = data.damage * mul;
            Vector3 hitDir = (targetPos - center);
            hitDir.y = 0f;
            if (hitDir.sqrMagnitude < 0.0001f) hitDir = Vector3.forward;
            hitDir.Normalize();

            Vector3? hitPoint = col.ClosestPoint(center);

            // Player 처리
            if (ph != null && (data.explosionTargets == TimeProjectileAttackData.ExplosionTargetType.PlayerOnly || data.explosionTargets == TimeProjectileAttackData.ExplosionTargetType.Both))
            {
                if (!hitSeen.Contains(ph))
                {
                    hitSeen.Add(ph);
                    Debug.Log($"[Explosion] Applying {actualDamage:F2} dmg to Player '{ph.gameObject.name}' (dist={dist:F3})");
                    TryApplyDamageToPlayer(ph, actualDamage, hitDir, mul, hitPoint);
                }
                else
                {
                    Debug.Log($"[Explosion] Player '{ph.gameObject.name}' already hit by this explosion (skip).");
                }
            }

            // Enemy 처리
            if (eh != null && (data.explosionTargets == TimeProjectileAttackData.ExplosionTargetType.EnemyOnly || data.explosionTargets == TimeProjectileAttackData.ExplosionTargetType.Both))
            {
                if (!hitSeen.Contains(eh))
                {
                    hitSeen.Add(eh);
                    Debug.Log($"[Explosion] Applying {actualDamage:F2} dmg to Enemy '{eh.gameObject.name}' (dist={dist:F3})");
                    TryApplyDamageToEnemy(eh, actualDamage, hitDir, mul, hitPoint);
                }
                else
                {
                    Debug.Log($"[Explosion] Enemy '{eh.gameObject.name}' already hit by this explosion (skip).");
                }
            }

            // If neither was found, provide diagnostic info
            if (ph == null && eh == null)
            {
                string path = BuildHierarchyPath(col.transform);
                Debug.LogWarning($"[Explosion] No Health component found on collider '{colName}'. Hierarchy: {path}");
                if (VERBOSE) ReportNearbyEnemyHealths(center, data.explosionRadius + 0.5f);
            }
        }

        // Debug sphere visual
        if (data.spawnDebugSphereOnExplode)
        {
            CreateDebugSphere(center, data.explosionRadius, 0.5f);
        }

        Destroy(gameObject);
    }

    private bool IsLikelyProjectileOrHitbox(Collider col)
    {
        string n = col.gameObject.name.ToLowerInvariant();
        if (n.Contains("projectile") || n.Contains("grenade") || n.Contains("hitbox") || n.Contains("explosion") || n.Contains("bullet"))
            return true;

        if (col.GetComponentInParent<TimeProjectile>() != null) return true;
        if (col.GetComponentInParent<HitBox_Enemy_Projectile>() != null) return true;
        if (col.GetComponentInParent<HitBox_PC_Projectile>() != null) return true;
        if (col.GetComponentInParent<HitBox_PC_Projectile_Sector>() != null) return true;
        if (col.GetComponentInParent<HitBox_PC>() != null) return true;

        return false;
    }

    private PlayerHealth TryFindPlayerHealth(Collider col)
    {
        PlayerHealth ph = col.GetComponentInParent<PlayerHealth>();
        if (ph != null) return ph;
        ph = col.GetComponent<PlayerHealth>();
        return ph;
    }

    private EnemyHealth TryFindEnemyHealth(Collider col)
    {
        EnemyHealth eh = col.GetComponentInParent<EnemyHealth>();
        if (eh != null) return eh;

        eh = col.GetComponent<EnemyHealth>();
        if (eh != null) return eh;

        eh = col.GetComponentInChildren<EnemyHealth>();
        if (eh != null) return eh;

        var parentEnemy = col.GetComponentInParent<Enemy>();
        if (parentEnemy != null)
        {
            eh = parentEnemy.GetComponent<EnemyHealth>() ?? parentEnemy.GetComponentInChildren<EnemyHealth>();
            if (eh != null) return eh;
        }

        var root = col.transform.root;
        if (root != null)
        {
            eh = root.GetComponentInChildren<EnemyHealth>();
            if (eh != null) return eh;
        }

        return null;
    }

    private void TryApplyDamageToPlayer(PlayerHealth ph, float dmg, Vector3 hitDir, float mul, System.Nullable<Vector3> hitPoint = null)
    {
        if (ph == null) return;

        PlayerWeaponController pwc = ph.GetComponentInParent<PlayerWeaponController>() ?? ph.GetComponent<PlayerWeaponController>();
        if (pwc != null && pwc.IsInvincible())
        {
            Debug.Log($"[Explosion] Player '{ph.gameObject.name}' is invincible/evading - skipping damage/knockback.");
            return;
        }

        float kbPower = data.knockbackPower * mul;
        float kbDuration = data.knockbackDuration * mul;
        float stun = data.stunDuration * mul;

        try
        {
            var deathProxy = WeaponDataSO.CreatePlayerDeathProxy(data.deathMode, data.ragdollImpulse, data.ragdollUpImpulse, data.ragdollSpinTorque, data.sliceTargets, data.sliceImpulse, data.isPoisonAttack, data.poisonOnHitStatus);
            float finalDamage = EnemyPlayerHitEffectApplier.ApplyIronBodyExtraDamageIfNeeded(pwc, dmg);
            ph.ApplyDamage(finalDamage, hitDir, deathProxy, 1f, hitPoint);
            Debug.Log($"[Explosion] Player '{ph.gameObject.name}' ApplyDamage called successfully. dmg={finalDamage}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Explosion] Exception while applying damage to Player '{ph.gameObject.name}': {ex}");
        }

        if (ph.GetCurrentHP() <= 0f)
        {
            if (VERBOSE) Debug.Log($"[Explosion] Player '{ph.gameObject.name}' is dead after damage -> skip knockback/stun.");
            return;
        }

        float targetHold = data.targetHoldDuration * mul;
        float attackerHold = data.attackerHoldDuration * mul;
        var pm = ph.GetComponentInParent<PlayerMovement>() ?? ph.GetComponent<PlayerMovement>();

        try
        {
            EnemyPlayerHitEffectApplier.ApplyCrowdControlAndTargetHitstop(
                pwc,
                pm,
                hitDir,
                kbPower,
                kbDuration,
                stun,
                data.usePushInsteadOfKnockback,
                targetHold,
                enemyOwner != null ? enemyOwner.transform : null,
                enemyOwner,
                attackerHold);
            if (VERBOSE && data.usePushInsteadOfKnockback)
                Debug.Log($"[Explosion] Player '{ph.gameObject.name}' Push applied");
            else if (VERBOSE && pwc != null && !data.usePushInsteadOfKnockback)
                Debug.Log($"[Explosion] Player '{ph.gameObject.name}' ForceApplyKnockback called: power={kbPower}, dur={kbDuration}, stun={stun}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Explosion] Exception applying CC/hitstop to Player '{ph.gameObject.name}': {ex}");
        }
    }

    private void TryApplyDamageToEnemy(EnemyHealth eh, float dmg, Vector3 hitDir, float mul, System.Nullable<Vector3> hitPoint = null)
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

            var enemyT = eh.GetComponentInParent<Enemy>();
            if (enemyT != null && enemyT.CurrentState != Enemy.EnemyState.Dead)
            {
                if (proxy.usePushInsteadOfKnockback) enemyT.ApplyPush(hitDir, proxy, 1f);
                else enemyT.ApplyKnockback(hitDir, proxy, 1f);
            }
        }
        finally
        {
            if (proxy != null)
                Object.Destroy(proxy);
        }
    }

    private string BuildHierarchyPath(Transform t)
    {
        if (t == null) return "<null>";
        string path = t.gameObject.name;
        Transform p = t.parent;
        while (p != null)
        {
            path = p.gameObject.name + "/" + path;
            p = p.parent;
        }
        return path;
    }

    private void ReportNearbyEnemyHealths(Vector3 center, float radius)
    {
        var all = Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
        if (all == null || all.Length == 0)
        {
            Debug.Log("[Explosion][DIAG] No EnemyHealth found in scene at all.");
            return;
        }

        Debug.Log($"[Explosion][DIAG] Reporting {all.Length} EnemyHealth distances (radius check {radius:F2}):");
        for (int i = 0; i < all.Length; i++)
        {
            var eh = all[i];
            if (eh == null) continue;
            float d = Vector3.Distance(center, eh.transform.position);
            Debug.Log($"  - EnemyHealth[{i}] '{eh.gameObject.name}' dist={d:F3} (withinRequestedRadius={(d <= radius)}) path={BuildHierarchyPath(eh.transform)}");
        }
    }

    private void CreateDebugSphere(Vector3 pos, float radius, float lifeTime)
    {
        GameObject dbg = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dbg.transform.position = pos;
        dbg.transform.localScale = Vector3.one * radius * 2f;

        var col = dbg.GetComponent<Collider>();
        if (col != null) Destroy(col);

        var mr = dbg.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            if (s_debugSphereMaterial == null)
            {
                Shader shader = Shader.Find("Legacy Shaders/Transparent/Diffuse") ?? Shader.Find("Standard");
                s_debugSphereMaterial = new Material(shader);
                s_debugSphereMaterial.color = new Color(1f, 0.5f, 0f, 0.25f);
                s_debugSphereMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
            mr.sharedMaterial = s_debugSphereMaterial;
        }

        Destroy(dbg, lifeTime);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (data == null) return;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawSphere(transform.position, data.explosionRadius);
    }
#endif
}