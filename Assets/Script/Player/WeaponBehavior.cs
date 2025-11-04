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

    /// <summary>
    /// 무기 데이터에 따라 WeaponAmmoRuntime을 초기화.
    /// 기존 Gun 전용 초기화는 유지하고, Shotgun SO 같은 다른 SO도 범용 Initialize 오버로드로 초기화합니다.
    /// </summary>
    public void EnsureAmmoInitialized()
    {
        // Gun 전용(기존)
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

        // Shotgun 및 기타 WeaponDataSO 계열 (범용 초기화)
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

        // 다른 타입은 초기화 없음
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

            // 근접은 ‘즉발 1회’만 사용(중복 히트는 차지/별도에서 운용)
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
        // ───── Gun 탄약 게이트 (중복 초기화 제거) ─────
        if (data is WeaponDataSO_Gun gun)
        {
            // 초기화는 Awake/EquipWeapon 에서 1회
            if (ammoRuntime != null && gun.usesAmmo)
            {
                if (!ammoRuntime.TryConsumeForShot(gun.consumePerShot))
                {
                    // 탄 부족 → 자동 리로드 (로그는 내부 처리)
                    if (!ammoRuntime.IsReloading && gun.autoReloadOnEmpty)
                        ammoRuntime.TryStartReload();

                    // 탄창/예비 모두 없는 경우 → 기본 무기로 전환
                    if (!ammoRuntime.HasAnyReserveOrInfinite())
                    {
                        var pwcFallback = Object.FindFirstObjectByType<PlayerWeaponController>();
                        if (pwcFallback != null)
                        {
                            Debug.Log("[WeaponBehavior] Gun 탄 완전 고갈 → 기본 무기로 전환");
                            pwcFallback.EquipWeapon(null);
                        }
                    }

                    Debug.Log("[WeaponBehavior] 탄 부족/리로드 중 - 발사 취소");
                    return;
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
        // 탄약 게이트 (샷건도 WeaponAmmoRuntime 공유)
        var sg = data as WeaponDataSO_Shotgun;
        if (sg != null)
        {
            if (ammoRuntime != null && sg.usesAmmo)
            {
                if (!ammoRuntime.TryConsumeForShot(sg.consumePerShot))
                {
                    if (!ammoRuntime.IsReloading && sg.autoReloadOnEmpty)
                        ammoRuntime.TryStartReload();

                    // 탄창/예비 모두 없는 경우 → 기본 무기로 전환
                    if (!ammoRuntime.HasAnyReserveOrInfinite())
                    {
                        var pwc = Object.FindFirstObjectByType<PlayerWeaponController>();
                        if (pwc != null)
                        {
                            Debug.Log("[WeaponBehavior] Shotgun 탄 완전 고갈 → 기본 무기로 전환");
                            pwc.EquipWeapon(null);
                        }
                    }

                    Debug.Log("[WeaponBehavior] 샷건 탄 부족/리로드 중 - 발사 취소");
                    return;
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
            Debug.Log($"[WeaponBehavior] Shotgun Sector Spawn │ pos@{spawnPoint.name}, forward=Snap({fwd}), dmg:{data.damage}, radius:{sg.shotgunRadius}, angle:{sg.shotgunAngle}, life:{data.hitBoxLifetime}");
        else
            Debug.Log($"[WeaponBehavior] Shotgun Sector Spawn │ pos@{spawnPoint.name}, forward=Snap({fwd}), dmg:{data.damage}, life:{data.hitBoxLifetime}");
    }

    /// <summary>
    /// Assault Rifle 등에서 EnemyDetector 없이, 지정 방향으로 즉시 발사
    /// - 탄약 체크/소모는 호출측에서 수행
    /// - projectileSpawnPoint가 없으면 meleeSpawnPoint나 transform를 폴백으로 사용
    /// </summary>
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
        // preserveVertical 플래그가 false면 기존처럼 수직 성분을 제거(평면 발사)
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

    /* ─ 시각화 유틸 ─ */
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