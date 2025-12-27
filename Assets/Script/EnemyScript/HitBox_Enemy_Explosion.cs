using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Explosion hitbox: 폭발 판정 + 데미지 적용 + 디버그 로깅 강화
/// - OverlapSphereNonAlloc(Trigger 포함)로 판정
/// - 대상에서 PlayerHealth / EnemyHealth를 robust하게 찾음
/// - owner(발사자) 별도 제외 로직 제거: explosionTargets에 따라 Player/Enemy/Both로만 판정합니다.
/// </summary>
public class HitBox_Enemy_Explosion : MonoBehaviour
{
    private static readonly Collider[] Overlap = new Collider[256];

    private TimeProjectileAttackData data;
    private Enemy enemyOwner; // 보관은 해두되 별도 제외 처리하지 않음

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
            float mul = Mathf.Lerp(1f, data.edgeDamageMultiplier, t);
            float actualDamage = data.damage * mul;
            Vector3 hitDir = (targetPos - center);
            hitDir.y = 0f;
            if (hitDir.sqrMagnitude < 0.0001f) hitDir = Vector3.forward;
            hitDir.Normalize();

            // Player 처리
            if (ph != null && (data.explosionTargets == TimeProjectileAttackData.ExplosionTargetType.PlayerOnly || data.explosionTargets == TimeProjectileAttackData.ExplosionTargetType.Both))
            {
                if (!hitSeen.Contains(ph))
                {
                    hitSeen.Add(ph);
                    Debug.Log($"[Explosion] Applying {actualDamage:F2} dmg to Player '{ph.gameObject.name}' (dist={dist:F3})");
                    TryApplyDamageToPlayer(ph, actualDamage, hitDir);
                }
                else
                {
                    Debug.Log($"[Explosion] Player '{ph.gameObject.name}' already hit by this explosion (skip).");
                }
            }

            // Enemy 처리 (owner 포함 여부에 대한 별도 처리 없음)
            if (eh != null && (data.explosionTargets == TimeProjectileAttackData.ExplosionTargetType.EnemyOnly || data.explosionTargets == TimeProjectileAttackData.ExplosionTargetType.Both))
            {
                if (!hitSeen.Contains(eh))
                {
                    hitSeen.Add(eh);
                    Debug.Log($"[Explosion] Applying {actualDamage:F2} dmg to Enemy '{eh.gameObject.name}' (dist={dist:F3})");
                    TryApplyDamageToEnemy(eh, actualDamage, hitDir);
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

    private void TryApplyDamageToPlayer(PlayerHealth ph, float dmg, Vector3 hitDir)
    {
        if (ph == null) return;
        try
        {
            ph.ApplyDamage(dmg, hitDir, null, 1f);
            Debug.Log($"[Explosion] Player '{ph.gameObject.name}' ApplyDamage called successfully.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Explosion] Exception while applying damage to Player '{ph.gameObject.name}': {ex}");
        }
    }

    private void TryApplyDamageToEnemy(EnemyHealth eh, float dmg, Vector3 hitDir)
    {
        if (eh == null) return;
        try
        {
            eh.ApplyDamage(dmg, hitDir, null, 1f);
            Debug.Log($"[Explosion] Enemy '{eh.gameObject.name}' ApplyDamage called successfully.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Explosion] Exception while applying damage to Enemy '{eh.gameObject.name}': {ex}");
        }

        var enemyT = eh.GetComponentInParent<Enemy>();
        if (enemyT != null && enemyT.CurrentState != Enemy.EnemyState.Dead)
        {
            try { enemyT.ApplyPush(hitDir, null, 1f); }
            catch { /* defensive */ }
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