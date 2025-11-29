// (수정본: 발사 성공 직후 탄약이 비어있으면 RequestSwitchToDefault 호출 추가)
using UnityEngine;
using System.Collections;

public class WeaponBehavior : MonoBehaviour
{
    [Header("무기 데이터")]
    public WeaponDataSO data;

    [Header("공격 지점 설정")]
    [SerializeField] private Transform meleeSpawnPoint;
    [SerializeField] private Transform projectileSpawnPoint;  // Fire_Point

    [Header("프리팹 연결")]
    public GameObject meleeHitboxPrefab;
    public GameObject projectilePrefab;
    [SerializeField] private GameObject shotgunSectorPrefab;

    /* ─ Ammo (기본 WeaponAmmoRuntime 재사용) ─ */
    [SerializeField] private WeaponAmmoRuntime ammoRuntime;
    public WeaponAmmoRuntime Ammo => ammoRuntime;

    /* ─ 시각화 ─ */
    private LineRenderer previewLR;
    private Material previewMat;
    private const int kPreviewSegments = 36;

    void Awake()
    {
        if (meleeSpawnPoint == null)
        {
            Transform root = transform.root;
            meleeSpawnPoint = System.Array.Find(
                root.GetComponentsInChildren<Transform>(),
                t => t.name == "Root_dummy"
            );
            Debug.Log(meleeSpawnPoint
                ? $"✅ Root_dummy 자동연결: {meleeSpawnPoint.name}"
                : "⚠ Root_dummy가 캐릭터 계층에 없습니다.");
        }

        EnsurePreviewLine();
        EnsureAmmoInitialized(); // Gun/Shotgun이면 1회 초기화
    }

    public void EnsureAmmoInitialized()
    {
        var gun = data as WeaponDataSO_Gun;
        if (gun != null)
        {
            if (ammoRuntime == null)
                ammoRuntime = GetComponent<WeaponAmmoRuntime>();
            if (ammoRuntime == null)
                ammoRuntime = gameObject.AddComponent<WeaponAmmoRuntime>();

            ammoRuntime.Initialize(gun, force: false);
            return;
        }

        var sg = data as WeaponDataSO_Shotgun;
        if (sg != null)
        {
            if (ammoRuntime == null)
                ammoRuntime = GetComponent<WeaponAmmoRuntime>();
            if (ammoRuntime == null)
                ammoRuntime = gameObject.AddComponent<WeaponAmmoRuntime>();

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
        StartCoroutine(DelayedHitbox());
    }

    private IEnumerator DelayedHitbox()
    {
        if (data.hitboxSpawnDelay > 0f)
            yield return new WaitForSeconds(data.hitboxSpawnDelay);

        if (data is WeaponDataSO_Melee)
        {
            SpawnMeleeHitbox(); yield break;
        }
        if (data is WeaponDataSO_Gun)
        {
            SpawnProjectile(); yield break;
        }
        if (data is WeaponDataSO_Shotgun)
        {
            SpawnShotgunSector(); yield break;
        }
        if (data is WeaponDataSO_Launcher)
        {
            SpawnProjectile(); yield break;
        }

        SpawnMeleeHitbox();
    }

    private void SpawnMeleeHitbox()
    {
        if (meleeHitboxPrefab == null || meleeSpawnPoint == null)
        {
            Debug.LogWarning("meleeHitboxPrefab 또는 meleeSpawnPoint 미연결");
            return;
        }

        GameObject hitboxGO = Instantiate(
            meleeHitboxPrefab,
            meleeSpawnPoint.position,
            meleeSpawnPoint.rotation
        );

        if (hitboxGO.TryGetComponent(out HitBox_PC hitbox))
        {
            hitbox.SetWeapon(data);

            hitbox.Initialize(
                data.damage,
                data.range,
                data.knockbackPower,
                data.hitBoxLifetime
            );
        }

        Debug.Log($"[WeaponBehavior] Melee Hitbox Spawn │ dmg:{data.damage}, range:{data.range}, kb:{data.knockbackPower}, life:{data.hitBoxLifetime}");
    }

    private void SpawnProjectile()
    {
        // Gun 탄약 게이트 (중복 초기화 제거)
        var gun = data as WeaponDataSO_Gun;
        if (gun != null)
        {
            if (ammoRuntime != null && gun.usesAmmo)
            {
                if (!ammoRuntime.TryConsumeForShot(gun.consumePerShot))
                {
                    if (!ammoRuntime.IsReloading && gun.autoReloadOnEmpty)
                        ammoRuntime.TryStartReload();

                    if (!ammoRuntime.HasAnyReserveOrInfinite())
                    {
                        var pwcFallback = Object.FindFirstObjectByType<PlayerWeaponController>();
                        if (pwcFallback != null)
                        {
                            Debug.Log("[WeaponBehavior] Gun 탄 완전 고갈 → 기본 무기로 전환 요청");
                            pwcFallback.RequestSwitchToDefault();
                        }
                    }

                    Debug.Log("[WeaponBehavior] 탄 부족/리로드 중 - 발사 취소");
                    return;
                }
                // <-- 성공적으로 소비했다면, 바로 빈 상태인지 검사해서 요청 남김
                if (ammoRuntime.IsMagazineEmpty() && !ammoRuntime.HasAnyReserveOrInfinite())
                {
                    var pwcFallback2 = Object.FindFirstObjectByType<PlayerWeaponController>();
                    if (pwcFallback2 != null)
                    {
                        if (Debug.isDebugBuild) Debug.Log("[WeaponBehavior] Gun: 발사 후 탄창 비어있음 → 전환 요청");
                        pwcFallback2.RequestSwitchToDefault();
                    }
                }
            }
        }

        if (projectilePrefab == null || projectileSpawnPoint == null)
        {
            Debug.LogWarning("projectilePrefab 또는 projectileSpawnPoint 미연결");
            return;
        }

        PlayerWeaponController playerCtrl = Object.FindFirstObjectByType<PlayerWeaponController>();
        Vector3 shootDir = playerCtrl ? playerCtrl.transform.forward : transform.forward;

        if (playerCtrl && playerCtrl.enemyDetector != null)
        {
            var list = playerCtrl.DetectEnemies();
            if (list != null && list.Count > 0)
            {
                shootDir = (list[0].transform.position - projectileSpawnPoint.position).normalized;
                Debug.Log($"[WeaponBehavior] 감지 성공 → {list[0].name} 방향");
            }
            else
            {
                Debug.Log("[WeaponBehavior] 감지 실패, 정면 발사");
            }
        }

        GameObject bulletGO = Instantiate(
            projectilePrefab,
            projectileSpawnPoint.position,
            Quaternion.LookRotation(shootDir)
        );

        if (bulletGO.TryGetComponent(out HitBox_PC_Projectile_Sector sectorProj))
        {
            sectorProj.Initialize(this.data, shootDir);
            return;
        }

        if (bulletGO.TryGetComponent(out HitBox_PC_Projectile proj))
        {
            proj.SetWeapon(this.data);

            float spd = 10f, life = 5f;
            int pierce = 0;
            if (data is WeaponDataSO_Gun g)
            {
                spd = g.projectileSpeed;
                life = g.projectileLifetime;
                pierce = g.pierceCount;
            }
            else if (data is WeaponDataSO_Launcher l)
            {
                spd = l.projectileSpeed;
                life = l.projectileLifetime;
            }
            else if (data is WeaponDataSO_AR ar)
            {
                spd = ar.projectileSpeed;
                life = ar.projectileLifetime;
                pierce = ar.pierceCount;
            }

            if (pierce > 0)
                proj.InitializeTowards(shootDir, data.damage, spd, life, pierce);
            else
                proj.InitializeTowards(shootDir, data.damage, spd, life);
            return;
        }

        Debug.LogWarning("[WeaponBehavior] 지원 컴포넌트를 찾지 못한 발사체");
    }

    private void SpawnShotgunSector()
    {
        var sg = data as WeaponDataSO_Shotgun;
        if (sg != null)
        {
            if (ammoRuntime != null && sg.usesAmmo)
            {
                if (!ammoRuntime.TryConsumeForShot(sg.consumePerShot))
                {
                    if (!ammoRuntime.IsReloading && sg.autoReloadOnEmpty)
                        ammoRuntime.TryStartReload();

                    if (!ammoRuntime.HasAnyReserveOrInfinite())
                    {
                        var pwc = Object.FindFirstObjectByType<PlayerWeaponController>();
                        if (pwc != null)
                        {
                            Debug.Log("[WeaponBehavior] Shotgun 탄 완전 고갈 → 기본 무기로 전환 요청");
                            pwc.RequestSwitchToDefault();
                        }
                    }

                    Debug.Log("[WeaponBehavior] 샷건 탄 부족/리로드 중 - 발사 취소");
                    return;
                }

                // 성공 소비 후 빈 상태 검사 및 요청
                if (ammoRuntime.IsMagazineEmpty() && !ammoRuntime.HasAnyReserveOrInfinite())
                {
                    var pwc2 = Object.FindFirstObjectByType<PlayerWeaponController>();
                    if (pwc2 != null)
                    {
                        if (Debug.isDebugBuild) Debug.Log("[WeaponBehavior] Shotgun: 발사 후 탄창 비어있음 → 전환 요청");
                        pwc2.RequestSwitchToDefault();
                    }
                }
            }
        }

        if (shotgunSectorPrefab == null)
        {
            Debug.LogWarning("shotgunSectorPrefab 미연결");
            return;
        }

        Transform spawnPoint = projectileSpawnPoint != null ? projectileSpawnPoint
                                : (meleeSpawnPoint != null ? meleeSpawnPoint : transform);

        if (projectileSpawnPoint == null)
            Debug.LogWarning("[WeaponBehavior] projectileSpawnPoint(Fire_Point) 비어 있음 → 대체 사용");

        var owner = GetComponentInParent<PlayerWeaponController>();
        Vector3 fwd = owner != null ? owner.transform.forward : transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        fwd.Normalize();

        Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);

        GameObject sectorGO = Instantiate(
            shotgunSectorPrefab,
            spawnPoint.position,
            rot
        );

        if (sectorGO.TryGetComponent(out HitBox_PC_Sector sector))
        {
            sector.SetWeapon(data);
            sector.SetForwardOverride(fwd);
            float radius = sg != null ? sg.shotgunRadius : 5f;
            sector.Initialize(
                data.damage,
                radius,
                data.knockbackPower,
                data.hitBoxLifetime
            );
        }

        if (sg != null)
            Debug.Log($"[WeaponBehavior] Shotgun Sector Spawn │ pos@{spawnPoint.name}, forward=Snap({fwd}), dmg:{data.damage}, radius:{sg.shotgunRadius}, angle:{sg.shotgunAngle}, life:{sg.hitBoxLifetime}");
        else
            Debug.Log($"[WeaponBehavior] Shotgun Sector Spawn │ pos@{spawnPoint.name}, forward=Snap({fwd}), dmg:{data.damage}, life:{data.hitBoxLifetime}");
    }

    public void FireProjectileForced(Vector3 shootDir, bool preserveVertical = false)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("projectilePrefab 미연결");
            return;
        }

        Transform spawn = projectileSpawnPoint != null ? projectileSpawnPoint
                               : (meleeSpawnPoint != null ? meleeSpawnPoint : transform);

        Vector3 dir = shootDir;
        if (!preserveVertical)
            dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();

        GameObject bulletGO = Instantiate(
            projectilePrefab,
            spawn.position,
            Quaternion.LookRotation(dir, Vector3.up)
        );

        if (bulletGO.TryGetComponent<HitBox_PC_Projectile_Sector>(out var sectorProj))
        {
            sectorProj.Initialize(this.data, dir);
            return;
        }

        if (bulletGO.TryGetComponent<HitBox_PC_Projectile>(out var proj))
        {
            proj.SetWeapon(this.data);

            float spd = 10f, life = 5f;
            int pierce = 0;

            if (data is WeaponDataSO_Gun g)
            {
                spd = g.projectileSpeed;
                life = g.projectileLifetime;
                pierce = g.pierceCount;
            }
            else if (data is WeaponDataSO_Launcher l)
            {
                spd = l.projectileSpeed;
                life = l.projectileLifetime;
            }
            else if (data is WeaponDataSO_AR ar)
            {
                spd = ar.projectileSpeed;
                life = ar.projectileLifetime;
                pierce = ar.pierceCount;
            }

            if (pierce > 0)
                proj.InitializeTowards(dir, data.damage, spd, life, pierce);
            else
                proj.InitializeTowards(dir, data.damage, spd, life);
            return;
        }

        Debug.LogWarning("[WeaponBehavior] 지원 컴포넌트를 찾지 못한 발사체(Forced)");
    }

    private void EnsurePreviewLine()
    {
        if (previewLR != null) return;

        var go = new GameObject("ShotgunPreview_Line");
        go.transform.SetParent(transform, false);
        previewLR = go.AddComponent<LineRenderer>();
        previewLR.useWorldSpace = true;
        previewLR.loop = false;
        previewLR.widthMultiplier = 0.03f;
        previewLR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        previewLR.receiveShadows = false;

        var shader = Shader.Find("Sprites/Default");
        previewMat = new Material(shader);
        previewLR.material = previewMat;

        previewLR.positionCount = kPreviewSegments + 3;
        previewLR.enabled = false;
    }

    private void UpdatePreviewSector(Vector3 center, Vector3 forward, float radius, float angle, Color color)
    {
        if (previewLR == null) return;

        previewLR.enabled = true;
        previewLR.startColor = color;
        previewLR.endColor = color;

        int idx = 0;
        float half = angle * 0.5f;

        Vector3 leftDir = Quaternion.AngleAxis(-half, Vector3.up) * forward;
        Vector3 leftEnd = center + leftDir.normalized * radius;
        previewLR.SetPosition(idx++, center);
        previewLR.SetPosition(idx++, leftEnd);

        for (int i = 1; i <= kPreviewSegments; i++)
        {
            float t = i / (float)kPreviewSegments;
            float yaw = Mathf.Lerp(-half, half, t);
            Vector3 dir = Quaternion.AngleAxis(yaw, Vector3.up) * forward;
            Vector3 cur = center + dir.normalized * radius;
            previewLR.SetPosition(idx++, cur);
        }

        Vector3 rightDir = Quaternion.AngleAxis(half, Vector3.up) * forward;
        Vector3 rightEnd = center + rightDir.normalized * radius;
        previewLR.SetPosition(idx++, rightEnd);
    }
}