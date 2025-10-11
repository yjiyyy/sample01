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
        // ── 타입 기반: 샷건 미리보기 ──
        var sg = data as WeaponDataSO_Shotgun;
        if (sg != null && sg.shotgunDebugVisualize && projectileSpawnPoint != null)
        {
            if (previewLR == null) EnsurePreviewLine();
            UpdatePreviewSector(projectileSpawnPoint.position,
                                projectileSpawnPoint.forward,
                                sg.shotgunRadius,
                                sg.shotgunAngle,
                                sg.shotgunDebugColor);
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

        // ── 타입 기반 ──
        if (data is WeaponDataSO_Melee)
        {
            SpawnMeleeHitbox();
            yield break;
        }
        if (data is WeaponDataSO_Gun)
        {
            SpawnProjectile();
            yield break;
        }
        if (data is WeaponDataSO_Shotgun)
        {
            SpawnShotgunSector();
            yield break;
        }
        if (data is WeaponDataSO_Launcher)
        {
            SpawnProjectile();
            yield break;
        }

        // 기본(안전) 처리: 근접으로 간주
        SpawnMeleeHitbox();
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
            // Launcher 폭발 투사체 전용
            sectorProj.Initialize(this.data, shootDir);
            return;
        }

        if (bulletGO.TryGetComponent(out HitBox_PC_Projectile proj))
        {
            // 일반 총알(직선)
            proj.SetWeapon(this.data);

            float spd = 10f, life = 5f;
            if (data is WeaponDataSO_Gun g)
            {
                spd = g.projectileSpeed;
                life = g.projectileLifetime;
            }
            else if (data is WeaponDataSO_Launcher l)
            {
                // 런처가 직선 탄환 프리팹을 사용할 수도 있으니 가드
                spd = l.projectileSpeed;
                life = l.projectileLifetime;
            }

            proj.InitializeTowards(
                shootDir,
                data.damage,
                spd,
                life
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

        var sg = data as WeaponDataSO_Shotgun;

        GameObject sectorGO = Instantiate(
            shotgunSectorPrefab,
            spawnPoint.position,
            spawnPoint.rotation
        );

        if (sectorGO.TryGetComponent(out HitBox_PC_Sector sector))
        {
            sector.SetWeapon(data);
            float radius = sg != null ? sg.shotgunRadius : 5f;
            sector.Initialize(
                data.damage,
                radius,
                data.knockbackPower,
                data.hitBoxLifetime
            );
        }

        if (sg != null)
            Debug.Log($"[WeaponBehavior] Shotgun Sector Spawn │ dmg:{data.damage}, radius:{sg.shotgunRadius}, angle:{sg.shotgunAngle}, life:{data.hitBoxLifetime}");
        else
            Debug.Log($"[WeaponBehavior] Shotgun Sector Spawn │ dmg:{data.damage}, life:{data.hitBoxLifetime}");
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