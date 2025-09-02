using UnityEngine;
using System.Collections;

public class WeaponBehavior : MonoBehaviour
{
    [Header("무기 데이터")]
    public WeaponDataSO data;

    [Header("공격 지점 설정")]
    [SerializeField] private Transform meleeSpawnPoint;
    [SerializeField] private Transform projectileSpawnPoint;  // ← Fire_Point 연결

    [Header("프리팹 연결")]
    public GameObject meleeHitboxPrefab;
    public GameObject projectilePrefab;

    // ✅ 샷건 섹터용 히트박스 프리팹
    [SerializeField] private GameObject shotgunSectorPrefab;

    /* ─────────── 런타임 전용(게임뷰 시각화) ─────────── */
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
                ? $"✅ Root_dummy 자동 연결: {meleeSpawnPoint.name}"
                : "⚠ Root_dummy가 캐릭터 계층에 없습니다.");
        }

        EnsurePreviewLine();
    }

    void OnDisable()
    {
        if (previewLR != null) previewLR.enabled = false;
    }

    void LateUpdate()
    {
        if (data != null
            && data.weaponCategory == WeaponCategory.Shotgun
            && data.shotgunDebugVisualize
            && projectileSpawnPoint != null)
        {
            if (previewLR == null) EnsurePreviewLine();
            UpdatePreviewSector(projectileSpawnPoint.position,
                                projectileSpawnPoint.forward,
                                data.shotgunRadius,
                                data.shotgunAngle,
                                data.shotgunDebugColor);
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

        switch (data.weaponCategory)
        {
            case WeaponCategory.None:
            case WeaponCategory.Bat:
                SpawnMeleeHitbox();
                break;

            case WeaponCategory.Gun:
                SpawnProjectile();
                break;

            case WeaponCategory.Shotgun:
                SpawnShotgunSector();
                break;

            case WeaponCategory.Launcher:
                SpawnProjectile();
                break;
        }
    }

    private void SpawnMeleeHitbox()
    {
        if (meleeHitboxPrefab == null || meleeSpawnPoint == null)
        {
            Debug.LogWarning("meleeHitboxPrefab 또는 meleeSpawnPoint가 연결되지 않았습니다!");
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
        if (projectilePrefab == null || projectileSpawnPoint == null)
        {
            Debug.LogWarning("projectilePrefab 또는 projectileSpawnPoint가 연결되지 않았습니다!");
            return;
        }

        PlayerWeaponController pwc = Object.FindFirstObjectByType<PlayerWeaponController>();
        Vector3 shootDir = pwc ? pwc.transform.forward : transform.forward;

        if (pwc && pwc.enemyDetector != null)
        {
            var list = pwc.DetectEnemies();
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
            proj.InitializeTowards(
                shootDir,
                data.damage,
                data.projectileSpeed,
                data.projectileLifetime
            );
            return;
        }

        Debug.LogWarning("[WeaponBehavior] 발사체에서 지원하는 컴포넌트를 찾지 못했습니다.");
    }

    private void SpawnShotgunSector()
    {
        if (shotgunSectorPrefab == null)
        {
            Debug.LogWarning("shotgunSectorPrefab 또는 projectileSpawnPoint가 연결되지 않았습니다!");
            return;
        }

        Transform spawnPoint = projectileSpawnPoint != null ? projectileSpawnPoint
                                : (meleeSpawnPoint != null ? meleeSpawnPoint : transform);

        if (projectileSpawnPoint == null)
            Debug.LogWarning("[WeaponBehavior] projectileSpawnPoint(Fire_Point)가 비어 있어 다른 위치로 대체합니다.");

        GameObject sectorGO = Instantiate(
            shotgunSectorPrefab,
            spawnPoint.position,
            spawnPoint.rotation
        );

        if (sectorGO.TryGetComponent(out HitBox_PC_Sector sector))
        {
            sector.SetWeapon(data);
            sector.Initialize(
                data.damage,
                data.shotgunRadius,
                data.knockbackPower,
                data.hitBoxLifetime
            );
        }

        Debug.Log($"[WeaponBehavior] Shotgun Sector Spawn │ dmg:{data.damage}, radius:{data.shotgunRadius}, angle:{data.shotgunAngle}, life:{data.hitBoxLifetime}");
    }

    /* ─────────── LineRenderer 유틸 ─────────── */
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
