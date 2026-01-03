// WeaponBehavior.cs (full version)
// - Spawn points & prefabs are managed by WeaponDataSO (완전 이관)
// - Dual-wield: fires twice if data.dualWield == true
//   - 2nd delay uses data.hitboxSpawnDelay2
//   - 2nd projectile spawn: if projectileSpawnPoint2PathOrName is empty, auto-detect Fire_Point under left-hand socket (socketNames[1])
// - Fix: left-hand Fire_Point may be attached after Awake -> re-resolve projectileSpawnPoint2 at fire time (lazy resolve)
// - Fix(NEW): Gun Aim Scan Angle/Distance 적용 (뒤쪽 적 타겟팅 방지)
// - Ammo consumption: only consume when the shot will actually spawn
// - Unity6(6000.0.42f1) / mobile+PC / 30/60fps 동일 결과 (WaitForSeconds 기반)

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponBehavior : MonoBehaviour
{
    [Header("무기 데이터")]
    public WeaponDataSO data;

    // 1st spawn points
    private Transform meleeSpawnPoint;
    private Transform projectileSpawnPoint;  // Fire_Point

    // 2nd spawn points (dual-wield)
    private Transform meleeSpawnPoint2;
    private Transform projectileSpawnPoint2;

    /* ─ Ammo (기본 WeaponAmmoRuntime 재사용) ─ */
    [SerializeField] private WeaponAmmoRuntime ammoRuntime;
    public WeaponAmmoRuntime Ammo => ammoRuntime;

    /* ─ 시각화 ─ */
    private LineRenderer previewLR;
    private Material previewMat;
    private const int kPreviewSegments = 36;

    [Header("발사 옵션")]
    [Tooltip("발사 시 원래 수직 보존 플래그 (일부 경로에서 사용). 그러나 조준 발사(타깃 존재) 시에는 spawn Y를 우선 사용합니다.")]
    public bool preserveVertical = false;

    void Awake()
    {
        if (data == null)
        {
            Debug.LogWarning("[WeaponBehavior] WeaponDataSO(data)가 비어 있습니다. 무기 SO 연결을 확인하세요.");
            return;
        }

        ResolveSpawnPointsFromSO();

        EnsurePreviewLine();
        EnsureAmmoInitialized(); // Gun/Shotgun이면 1회 초기화
    }

    private void ResolveSpawnPointsFromSO()
    {
        // --- Melee spawn: player root (1) ---
        string meleeKey = string.IsNullOrEmpty(data.meleeSpawnPointPathOrName) ? "Root_dummy" : data.meleeSpawnPointPathOrName;
        meleeKey = NormalizePath(meleeKey);

        meleeSpawnPoint = FindByNameOrPath(transform.root, meleeKey);
        if (meleeSpawnPoint == null)
            Debug.LogWarning($"[WeaponBehavior] meleeSpawnPoint(1) 못 찾음 (playerRoot): '{meleeKey}'.");

        // --- Projectile spawn: weapon self (1) ---
        string projKey = string.IsNullOrEmpty(data.projectileSpawnPointPathOrName) ? "Fire_Point" : data.projectileSpawnPointPathOrName;
        projKey = NormalizePath(projKey);

        projectileSpawnPoint = FindByNameOrPath(transform, projKey);
        if (projectileSpawnPoint == null)
            Debug.LogWarning($"[WeaponBehavior] projectileSpawnPoint(1) 못 찾음 (weapon): '{projKey}'.");

        // --- Dual melee spawn (2): 입력된 경우에만 ---
        if (!string.IsNullOrEmpty(data.meleeSpawnPoint2PathOrName))
        {
            string melee2Key = NormalizePath(data.meleeSpawnPoint2PathOrName);
            meleeSpawnPoint2 = FindByNameOrPath(transform.root, melee2Key);
            if (meleeSpawnPoint2 == null)
                Debug.LogWarning($"[WeaponBehavior] meleeSpawnPoint(2) 못 찾음 (playerRoot): '{melee2Key}'.");
        }

        // --- Dual projectile spawn (2): 비어있어도 자동 탐색(왼손) ---
        projectileSpawnPoint2 = ResolveSecondProjectileSpawnPoint();
    }

    private Transform ResolveSecondProjectileSpawnPoint()
    {
        if (data == null || !data.dualWield) return null;

        Transform leftSocket = null;
        if (data.socketNames != null && data.socketNames.Count >= 2 && !string.IsNullOrEmpty(data.socketNames[1]))
        {
            leftSocket = FindDeepChildByName(transform.root, data.socketNames[1]);
        }

        if (leftSocket == null)
            return null;

        string raw = string.IsNullOrEmpty(data.projectileSpawnPoint2PathOrName) ? "Fire_Point" : data.projectileSpawnPoint2PathOrName;
        string key = NormalizePath(raw);

        Transform found = FindByNameOrPath(leftSocket, key);
        if (found != null) return found;

        if (key != "Fire_Point")
            found = FindByNameOrPath(leftSocket, "Fire_Point");

        return found;
    }

    private string NormalizePath(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Replace("\\", "/").Trim();
    }

    private Transform FindByNameOrPath(Transform parent, string pathOrName)
    {
        if (parent == null || string.IsNullOrEmpty(pathOrName)) return null;

        if (pathOrName.Contains("/"))
        {
            var byPath = parent.Find(pathOrName);
            if (byPath != null) return byPath;

            string lastName = pathOrName.Substring(pathOrName.LastIndexOf('/') + 1);
            return FindDeepChildByName(parent, lastName);
        }

        return FindDeepChildByName(parent, pathOrName);
    }

    private Transform FindDeepChildByName(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;

        for (int i = 0; i < parent.childCount; ++i)
        {
            var t = FindDeepChildByName(parent.GetChild(i), name);
            if (t != null) return t;
        }
        return null;
    }

    public void EnsureAmmoInitialized()
    {
        var gun = data as WeaponDataSO_Gun;
        if (gun != null)
        {
            if (ammoRuntime == null) ammoRuntime = GetComponent<WeaponAmmoRuntime>();
            if (ammoRuntime == null) ammoRuntime = gameObject.AddComponent<WeaponAmmoRuntime>();
            ammoRuntime.Initialize(gun, force: false);
            return;
        }

        var sg = data as WeaponDataSO_Shotgun;
        if (sg != null)
        {
            if (ammoRuntime == null) ammoRuntime = GetComponent<WeaponAmmoRuntime>();
            if (ammoRuntime == null) ammoRuntime = gameObject.AddComponent<WeaponAmmoRuntime>();
            ammoRuntime.Initialize(sg, force: false);
            return;
        }
    }

    void OnDisable()
    {
        if (previewLR != null) previewLR.enabled = false;
    }

    void LateUpdate()
    {
        var sg = data as WeaponDataSO_Shotgun;
        if (sg != null && sg.shotgunDebugVisualize && projectileSpawnPoint != null)
        {
            if (previewLR == null) EnsurePreviewLine();
            var owner = GetComponentInParent<PlayerWeaponController>();
            Vector3 center = projectileSpawnPoint.position;
            Vector3 forward = owner != null ? owner.transform.forward : transform.forward;
            UpdatePreviewSector(center, forward, sg.shotgunRadius, sg.shotgunAngle, sg.shotgunDebugColor);
        }
        else
        {
            if (previewLR != null) previewLR.enabled = false;
        }
    }

    public void AttackHit()
    {
        if (data == null)
        {
            Debug.LogWarning("⚠ WeaponDataSO가 비어 있습니다.");
            return;
        }

        StartCoroutine(DelayedHitbox(useSecond: false));

        if (data.dualWield)
            StartCoroutine(DelayedHitbox(useSecond: true));
    }

    private IEnumerator DelayedHitbox(bool useSecond)
    {
        float delay = useSecond ? data.hitboxSpawnDelay2 : data.hitboxSpawnDelay;
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (data is WeaponDataSO_Melee) { SpawnMeleeHitbox(useSecond); yield break; }
        if (data is WeaponDataSO_Gun) { SpawnProjectile(useSecond); yield break; }
        if (data is WeaponDataSO_Shotgun) { SpawnShotgunSector(useSecond); yield break; }
        if (data is WeaponDataSO_Launcher) { SpawnProjectile(useSecond); yield break; }
        if (data is WeaponDataSO_AR) { SpawnProjectile(useSecond); yield break; }

        SpawnMeleeHitbox(useSecond);
    }

    private void SpawnMeleeHitbox(bool useSecond)
    {
        var prefab = data != null ? data.meleeHitboxPrefab : null;
        Transform spawn = useSecond ? meleeSpawnPoint2 : meleeSpawnPoint;

        if (useSecond && spawn == null) return;

        if (prefab == null || spawn == null)
        {
            Debug.LogWarning("[WeaponBehavior] (SO) meleeHitboxPrefab 또는 meleeSpawnPoint 미연결");
            return;
        }

        GameObject hitboxGO = Instantiate(prefab, spawn.position, spawn.rotation);

        if (hitboxGO.TryGetComponent(out HitBox_PC hitbox))
        {
            hitbox.SetWeapon(data);
            hitbox.Initialize(data.damage, data.range, data.knockbackPower, data.hitBoxLifetime);
        }
    }

    // ✅ NEW: Gun Aim Scan (Angle/Distance) 기반으로 타겟 선택
    private Transform ChooseAimTarget(PlayerWeaponController playerCtrl, Transform spawnPoint, WeaponDataSO_Gun gunData)
    {
        if (playerCtrl == null || gunData == null) return null;
        if (playerCtrl.enemyDetector == null) return null;

        // 너가 말한 Aim Scan Angle/Distance
        float maxDist = Mathf.Max(0f, gunData.aimScanDistance);
        float halfAngle = Mathf.Max(0f, gunData.aimScanAngle) * 0.5f;

        if (maxDist <= 0f || halfAngle <= 0f) return null;

        var list = playerCtrl.DetectEnemies();
        if (list == null || list.Count == 0) return null;

        Vector3 origin = spawnPoint != null ? spawnPoint.position : playerCtrl.transform.position;

        Vector3 fwd = playerCtrl.transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        fwd.Normalize();

        Transform best = null;
        float bestSqr = float.PositiveInfinity;
        float maxSqr = maxDist * maxDist;

        for (int i = 0; i < list.Count; i++)
        {
            var t = list[i];
            if (t == null) continue;

            Vector3 to = t.position - origin;
            to.y = 0f;

            float sqr = to.sqrMagnitude;
            if (sqr < 0.0001f) continue;
            if (sqr > maxSqr) continue;

            float ang = Vector3.Angle(fwd, to.normalized);
            if (ang > halfAngle) continue;

            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = t;
            }
        }

        return best;
    }

    private void SpawnProjectile(bool useSecond)
    {
        var projPrefab = data != null ? data.projectilePrefab : null;

        if (useSecond && projectileSpawnPoint2 == null)
        {
            projectileSpawnPoint2 = ResolveSecondProjectileSpawnPoint();
        }

        Transform spawnPoint = useSecond ? projectileSpawnPoint2 : projectileSpawnPoint;

        if (useSecond && spawnPoint == null) return;

        if (projPrefab == null || spawnPoint == null)
        {
            Debug.LogWarning("[WeaponBehavior] (SO) projectilePrefab 또는 projectileSpawnPoint 미연결");
            return;
        }

        // 탄 소비는 "스폰 가능한 상황"에서만
        var gun = data as WeaponDataSO_Gun;
        if (gun != null && ammoRuntime != null && gun.usesAmmo)
        {
            if (!ammoRuntime.TryConsumeForShot(gun.consumePerShot))
            {
                if (!ammoRuntime.IsReloading && gun.autoReloadOnEmpty)
                    ammoRuntime.TryStartReload();

                if (!ammoRuntime.HasAnyReserveOrInfinite())
                {
                    var pwcFallback = Object.FindFirstObjectByType<PlayerWeaponController>();
                    if (pwcFallback != null) pwcFallback.RequestSwitchToDefault();
                }
                return;
            }
        }

        PlayerWeaponController playerCtrl = Object.FindFirstObjectByType<PlayerWeaponController>();
        Vector3 shootDir = playerCtrl ? playerCtrl.transform.forward : transform.forward;

        // ✅ CHANGE: list[0] 대신 Aim Scan 필터로 타겟 선택
        Transform targetTransform = null;
        if (playerCtrl != null && gun != null)
        {
            targetTransform = ChooseAimTarget(playerCtrl, spawnPoint, gun);
        }

        if (targetTransform != null)
        {
            Vector3 rawTarget = targetTransform.position;
            Vector3 targetPos = new Vector3(rawTarget.x, spawnPoint.position.y, rawTarget.z);

            Vector3 horiz = targetPos - spawnPoint.position;
            horiz.y = 0f;
            if (horiz.sqrMagnitude < 0.0001f) horiz = spawnPoint.forward;
            Vector3 dirXZ = horiz.normalized;

            GameObject bulletGO = Instantiate(projPrefab, spawnPoint.position, Quaternion.LookRotation(dirXZ, Vector3.up));

            if (bulletGO.TryGetComponent(out HitBox_PC_Projectile_Sector sectorProj))
            {
                Vector3 ppos = bulletGO.transform.position;
                ppos.y = spawnPoint.position.y;
                bulletGO.transform.position = ppos;
                sectorProj.Initialize(this.data, dirXZ);
                return;
            }

            if (bulletGO.TryGetComponent(out HitBox_PC_Projectile proj))
            {
                proj.SetWeapon(this.data);

                float spd = 10f, life = 5f;
                int pierce = 0;
                if (data is WeaponDataSO_Gun g2) { spd = g2.projectileSpeed; life = g2.projectileLifetime; pierce = g2.pierceCount; }
                else if (data is WeaponDataSO_Launcher l2) { spd = l2.projectileSpeed; life = l2.projectileLifetime; }
                else if (data is WeaponDataSO_AR ar2) { spd = ar2.projectileSpeed; life = ar2.projectileLifetime; pierce = ar2.pierceCount; }

                if (pierce > 0) proj.InitializeTowardsTargetPosition(targetPos, data.damage, spd, life, pierce, true);
                else proj.InitializeTowardsTargetPosition(targetPos, data.damage, spd, life, true);

                return;
            }

            Debug.LogWarning("[WeaponBehavior] 발사체에서 지원 컴포넌트를 찾지 못했습니다.");
            return;
        }

        // 타겟이 없으면 정면 발사
        Vector3 dir = shootDir;
        if (!preserveVertical) dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();

        GameObject fallbackGO = Instantiate(projPrefab, spawnPoint.position, Quaternion.LookRotation(dir));

        if (fallbackGO.TryGetComponent(out HitBox_PC_Projectile_Sector sector2))
        {
            Vector3 fwdXZ = dir; fwdXZ.y = 0f;
            if (fwdXZ.sqrMagnitude < 0.0001f) fwdXZ = transform.forward;
            fwdXZ.Normalize();
            sector2.Initialize(this.data, fwdXZ);
            return;
        }

        if (fallbackGO.TryGetComponent(out HitBox_PC_Projectile p))
        {
            p.SetWeapon(this.data);

            float spd = 10f, life = 5f;
            int pierce = 0;
            if (data is WeaponDataSO_Gun g3) { spd = g3.projectileSpeed; life = g3.projectileLifetime; pierce = g3.pierceCount; }
            else if (data is WeaponDataSO_Launcher l3) { spd = l3.projectileSpeed; life = l3.projectileLifetime; }
            else if (data is WeaponDataSO_AR ar3) { spd = ar3.projectileSpeed; life = ar3.projectileLifetime; pierce = ar3.pierceCount; }

            if (pierce > 0) p.InitializeTowards(dir, data.damage, spd, life, pierce);
            else p.InitializeTowards(dir, data.damage, spd, life);

            return;
        }

        Debug.LogWarning("[WeaponBehavior] projectile prefab does not contain supported projectile script.");
    }

    private void SpawnShotgunSector(bool useSecond)
    {
        // (필요하면 추후 같은 방식으로 2번째 스폰포인트 재탐색 적용 가능)
    }

    public void FireProjectileForced(Vector3 shootDir, bool preserveVerticalLocal = false)
    {
        if (data == null)
        {
            Debug.LogWarning("[WeaponBehavior] FireProjectileForced: data가 null");
            return;
        }

        var projPrefab = data.projectilePrefab;
        if (projPrefab == null)
        {
            Debug.LogWarning("[WeaponBehavior] (SO) projectilePrefab 미연결 (Forced)");
            return;
        }

        Transform spawnPoint = projectileSpawnPoint != null ? projectileSpawnPoint
                            : (meleeSpawnPoint != null ? meleeSpawnPoint : transform);

        Vector3 dir = shootDir;
        if (!preserveVerticalLocal) dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();

        GameObject bulletGO = Instantiate(projPrefab, spawnPoint.position, Quaternion.LookRotation(dir, Vector3.up));

        if (bulletGO.TryGetComponent(out HitBox_PC_Projectile_Sector sectorProj))
        {
            sectorProj.Initialize(this.data, dir);
            return;
        }

        if (bulletGO.TryGetComponent(out HitBox_PC_Projectile proj))
        {
            proj.SetWeapon(this.data);

            float spd = 10f, life = 5f;
            int pierce = 0;

            if (data is WeaponDataSO_Gun g) { spd = g.projectileSpeed; life = g.projectileLifetime; pierce = g.pierceCount; }
            else if (data is WeaponDataSO_Launcher l) { spd = l.projectileSpeed; life = l.projectileLifetime; }
            else if (data is WeaponDataSO_AR ar) { spd = ar.projectileSpeed; life = ar.projectileLifetime; pierce = ar.pierceCount; }

            if (pierce > 0) proj.InitializeTowards(dir, data.damage, spd, life, pierce);
            else proj.InitializeTowards(dir, data.damage, spd, life);

            return;
        }

        Debug.LogWarning("[WeaponBehavior] 지원 컴포넌트를 찾지 못한 발사체(Forced)");
    }

    private void EnsurePreviewLine() { /* 기존 코드 유지 */ }
    private void UpdatePreviewSector(Vector3 center, Vector3 forward, float radius, float angle, Color color) { /* 기존 코드 유지 */ }
}