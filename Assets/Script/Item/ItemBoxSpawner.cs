using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 아이템 박스 동적 스폰. 아트 씬에 배치하며, 층별 Map Spawn Area·플레이어 주변·Ground·clearance를 검사합니다.
/// </summary>
public class ItemBoxSpawner : MonoBehaviour
{
    public enum SpawnMode
    {
        [Tooltip("항상 플레이어 주변 링(최소~최대 거리) 안에서만 스폰합니다.")]
        AlwaysNearPlayer = 0,

        [Tooltip("레벨 시작 첫 스폰만 맵 영역(층별 Box) 전체. 이후 재스폰은 플레이어 주변(및 시야 밖 조건)입니다.")]
        InitialMapOnly = 1,

        [Tooltip("처음·재스폰 모두 맵 영역(층별 Box) 안에서만 스폰합니다. 층은 영역을 균등 랜덤 선택합니다.")]
        AlwaysMap = 2,
    }

    private const string LegacyMapSpawnAreaObjectName = "ItemBoxSpawnArea";

    private static readonly Color[] DefaultFloorGizmoColors =
    {
        new Color(0.3f, 1f, 0.45f, 0.85f),
        new Color(0.35f, 0.75f, 1f, 0.85f),
        new Color(1f, 0.85f, 0.3f, 0.85f),
        new Color(1f, 0.5f, 0.85f, 0.85f),
    };

    [Header("프리팹")]
    [Tooltip("스폰할 ItemBox 프리팹. 루트에 ItemBox·BoxCollider가 있어야 합니다.")]
    [SerializeField] private GameObject itemBoxPrefab;

    [Header("스폰 방식")]
    [Tooltip("박스 위치를 정하는 방식. AlwaysNearPlayer=플레이어 주변, InitialMapOnly=첫 스폰만 맵(층) 전체, AlwaysMap=항상 맵(층) 전체.")]
    [SerializeField] private SpawnMode spawnMode = SpawnMode.InitialMapOnly;

    [Header("맵 스폰 영역 (층마다 Box 1개)")]
    [Tooltip("맵/층 스폰에 쓸 BoxCollider. 비우면 같은 아트 씬의 ItemBoxSpawnFloorArea를 자동 수집합니다.")]
    [SerializeField] private List<BoxCollider> mapSpawnAreas = new List<BoxCollider>();

    [Tooltip("Inspector 목록이 비어 있으면, 이 스포너와 같은 아트 씬에서 ItemBoxSpawnFloorArea·ItemBoxSpawnArea 이름을 자동 검색합니다.")]
    [SerializeField] private bool autoFindMapSpawnAreasInScene = true;

    [Header("개수")]
    [Tooltip("레벨에 동시에 존재할 박스 수.")]
    [SerializeField] private int targetActiveCount = 5;

    [Header("플레이어 주변 스폰 (AlwaysNearPlayer / InitialMapOnly 재스폰)")]
    [Tooltip("플레이어로부터 최소 수평 거리(m). 이보다 가깝지 않게 스폰합니다.")]
    [SerializeField] private float minDistanceFromPlayer = 10f;

    [Tooltip("플레이어로부터 최대 수평 거리(m). 이보다 멀지 않게 스폰합니다.")]
    [SerializeField] private float maxDistanceFromPlayer = 25f;

    [Tooltip("활성 박스끼리 최소 수평(XZ) 거리.")]
    [SerializeField] private float minBoxSpacing = 8f;

    [Header("카메라 시야 밖 (플레이어 주변 재스폰 시)")]
    [Tooltip("플레이어 주변으로 스폰할 때, 메인 카메라 Viewport 밖일 때만 허용합니다. 맵 스폰에는 적용되지 않습니다.")]
    [SerializeField] private bool requireOutsideCameraView = true;

    [Tooltip("Viewport 0~1 밖으로 벗어나야 하는 여유. 예: 0.05")]
    [SerializeField] private float cameraViewportMargin = 0.05f;

    [Tooltip("레벨 시작 첫 스폰(플레이어 주변 모드일 때)에도 시야 밖 조건을 적용할지 여부.")]
    [SerializeField] private bool requireOutsideCameraForInitialSpawn = false;

    [Header("타이밍")]
    [Tooltip("레벨 시작 후 첫 스폰까지 대기 시간(초).")]
    [SerializeField] private float initialSpawnDelay = 0.5f;

    [Tooltip("재스폰 위치를 찾지 못했을 때 다시 시도하기까지 대기 시간(초).")]
    [SerializeField] private float respawnRetryDelay = 0.75f;

    [Header("지면 (Ground 레이어)")]
    [Tooltip("스폰 위치를 찾을 때 아래로 쏘는 레이캐스트에 사용할 레이어. 비어 있으면 EnemySpawner·MovementSettings·Ground+Default를 참고합니다.")]
    [SerializeField] private LayerMask groundLayers;

    [Tooltip("바닥을 찾을 때 위에서 아래로 쏘는 레이 시작 높이(m).")]
    [SerializeField] private float raycastHeight = 20f;

    [Tooltip("플레이어 주변 스폰 시, 플레이어 Y 기준 허용 바닥 높이 하한(m). 계단·다층 맵에서 내 층에 묶는 데 사용합니다.")]
    [SerializeField] private float spawnFloorMinOffset = -0.5f;

    [Tooltip("플레이어 주변 스폰 시, 플레이어 Y 기준 허용 바닥 높이 상한(m).")]
    [SerializeField] private float spawnFloorMaxOffset = 1f;

    [Tooltip("유효한 위치를 찾지 못하면 후보를 뽑는 최대 시도 횟수(1회 스폰당).")]
    [SerializeField] private int maxSpawnAttempts = 12;

    [Header("장애물 검사")]
    [Tooltip("Wall·Prop·Player와 겹치면 스폰 실패. Movement Settings 사용을 권장합니다.")]
    [SerializeField] private MovementSettings movementSettings;

    [Tooltip("Wall·Prop·Player와 BoxCollider 겹침 검사를 켭니다.")]
    [SerializeField] private bool checkSpawnClearance = true;

    [Header("디버그")]
    [Tooltip("스폰 성공·실패를 Console에 출력합니다.")]
    [SerializeField] private bool debugSpawnLog = false;

    [Tooltip("Scene 뷰에서 층별 맵 영역(초록 등)과 플레이어 주변 링(파랑)을 그립니다.")]
    [SerializeField] private bool drawGizmos = true;

    private readonly List<TrackedBox> trackedBoxes = new List<TrackedBox>(16);
    private readonly List<BoxCollider> resolvedMapSpawnAreas = new List<BoxCollider>(8);
    private readonly RaycastHit[] groundHitBuffer = new RaycastHit[8];

    private Transform playerTransform;
    private LayerMask spawnClearanceMask;
    private Collider[] overlapBuffer;
    private BoxCollider prefabBoxCollider;
    private bool spawningActive;
    private bool playerMissingLogged;
    private bool mapAreaMissingLogged;
    private float respawnRetryTimer;
    private bool respawnPending;
    private Coroutine initialSpawnRoutine;

    private struct TrackedBox
    {
        public ItemBox box;
        public Vector3 spawnPosition;
    }

    private void Awake()
    {
        ResolveReferencesFromScene();
        ResolveGroundLayers();
        ResolveSpawnClearanceMask();

        int bufferSize = movementSettings != null ? movementSettings.overlapBufferSize : 16;
        overlapBuffer = new Collider[Mathf.Max(4, bufferSize)];

        if (itemBoxPrefab != null)
            prefabBoxCollider = itemBoxPrefab.GetComponent<BoxCollider>();
    }

    private void ResolveReferencesFromScene()
    {
        EnemySpawner enemySpawner = FindFirstObjectByType<EnemySpawner>();
        if (enemySpawner == null)
            return;

        if (movementSettings == null)
            movementSettings = enemySpawner.movementSettings;
    }

    private void ResolveGroundLayers()
    {
        if (groundLayers != 0)
            return;

        EnemySpawner enemySpawner = FindFirstObjectByType<EnemySpawner>();
        if (enemySpawner != null && enemySpawner.groundLayers != 0)
        {
            groundLayers = enemySpawner.groundLayers;
            return;
        }

        if (movementSettings != null && movementSettings.groundMask != 0)
        {
            groundLayers = movementSettings.groundMask;
            return;
        }

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0)
            groundLayers |= 1 << groundLayer;

        groundLayers |= 1 << 0;
    }

    private void ResolveMapSpawnAreas()
    {
        resolvedMapSpawnAreas.Clear();
        var seen = new HashSet<BoxCollider>();

        AddMapSpawnAreaCandidates(mapSpawnAreas, seen);

        ItemBoxSpawnFloorArea[] childMarkers = GetComponentsInChildren<ItemBoxSpawnFloorArea>(true);
        for (int i = 0; i < childMarkers.Length; i++)
        {
            if (childMarkers[i] == null)
                continue;

            BoxCollider col = childMarkers[i].Box;
            if (col != null)
                TryAddMapSpawnArea(col, seen);
        }

        if (!autoFindMapSpawnAreasInScene)
            return;

        Scene ownerScene = gameObject.scene;

        ItemBoxSpawnFloorArea[] sceneMarkers = FindObjectsByType<ItemBoxSpawnFloorArea>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneMarkers.Length; i++)
        {
            ItemBoxSpawnFloorArea marker = sceneMarkers[i];
            if (marker == null || marker.gameObject.scene != ownerScene)
                continue;

            BoxCollider col = marker.Box;
            if (col != null)
                TryAddMapSpawnArea(col, seen);
        }

        BoxCollider[] colliders = FindObjectsByType<BoxCollider>(FindObjectsSortMode.None);
        for (int i = 0; i < colliders.Length; i++)
        {
            BoxCollider col = colliders[i];
            if (col == null || col.gameObject.scene != ownerScene)
                continue;

            if (col.gameObject.name != LegacyMapSpawnAreaObjectName)
                continue;

            TryAddMapSpawnArea(col, seen);
        }
    }

    private void AddMapSpawnAreaCandidates(List<BoxCollider> candidates, HashSet<BoxCollider> seen)
    {
        if (candidates == null)
            return;

        for (int i = 0; i < candidates.Count; i++)
            TryAddMapSpawnArea(candidates[i], seen);
    }

    private void TryAddMapSpawnArea(BoxCollider col, HashSet<BoxCollider> seen)
    {
        if (col == null || !seen.Add(col))
            return;

        resolvedMapSpawnAreas.Add(col);
    }

    private void OnValidate()
    {
        targetActiveCount = Mathf.Max(0, targetActiveCount);
        minDistanceFromPlayer = Mathf.Max(0f, minDistanceFromPlayer);
        maxDistanceFromPlayer = Mathf.Max(minDistanceFromPlayer, maxDistanceFromPlayer);
        minBoxSpacing = Mathf.Max(0f, minBoxSpacing);
        cameraViewportMargin = Mathf.Max(0f, cameraViewportMargin);
        initialSpawnDelay = Mathf.Max(0f, initialSpawnDelay);
        respawnRetryDelay = Mathf.Max(0.1f, respawnRetryDelay);
        maxSpawnAttempts = Mathf.Max(1, maxSpawnAttempts);
    }

    private void Update()
    {
        if (!spawningActive)
            return;

        if (!TryResolvePlayer())
            return;

        PruneDestroyedBoxes();

        if (respawnPending)
        {
            respawnRetryTimer -= Time.deltaTime;
            if (respawnRetryTimer > 0f)
                return;

            respawnRetryTimer = respawnRetryDelay;
            if (TrySpawnOne(isInitialSpawn: false))
            {
                respawnPending = false;
                if (trackedBoxes.Count < targetActiveCount)
                    respawnPending = true;
            }
        }
    }

    public void BeginSpawning()
    {
        if (spawningActive)
            return;

        if (itemBoxPrefab == null)
        {
            Debug.LogError("[ItemBoxSpawner] Item Box Prefab이 비어 있습니다. Inspector에 ItemBox_Prefab을 연결하세요.", this);
            return;
        }

        if (targetActiveCount <= 0)
        {
            Debug.LogWarning("[ItemBoxSpawner] Target Active Count가 0입니다.", this);
            return;
        }

        ResolveGroundLayers();
        ResolveMapSpawnAreas();

        if (UsesMapRegion(isInitialSpawn: true) && resolvedMapSpawnAreas.Count == 0)
        {
            Debug.LogError(
                $"[ItemBoxSpawner] Spawn Mode가 {spawnMode}인데 맵 스폰 영역이 없습니다. " +
                "아트 씬에 ItemBoxSpawnFloorArea + BoxCollider(층마다 1개)를 배치하거나 Map Spawn Areas에 연결하세요.",
                this);
            return;
        }

        spawningActive = true;
        enabled = true;
        respawnPending = false;
        mapAreaMissingLogged = false;

        if (initialSpawnRoutine != null)
            StopCoroutine(initialSpawnRoutine);
        initialSpawnRoutine = StartCoroutine(InitialSpawnRoutine());
    }

    /// <summary>신규 스폰만 중지합니다. 이미 나온 박스는 그대로 둡니다.</summary>
    public void StopSpawning()
    {
        spawningActive = false;
        respawnPending = false;

        if (initialSpawnRoutine != null)
        {
            StopCoroutine(initialSpawnRoutine);
            initialSpawnRoutine = null;
        }
    }

    public void StopAndClear()
    {
        StopSpawning();
        resolvedMapSpawnAreas.Clear();

        for (int i = trackedBoxes.Count - 1; i >= 0; i--)
        {
            var tracked = trackedBoxes[i];
            if (tracked.box != null)
            {
                tracked.box.Removed -= HandleBoxRemoved;
                Destroy(tracked.box.gameObject);
            }
        }

        trackedBoxes.Clear();
    }

    private IEnumerator InitialSpawnRoutine()
    {
        if (initialSpawnDelay > 0f)
            yield return new WaitForSeconds(initialSpawnDelay);

        while (!TryResolvePlayer())
            yield return null;

        int safety = targetActiveCount * maxSpawnAttempts * 2;
        while (trackedBoxes.Count < targetActiveCount && safety-- > 0)
        {
            if (!TrySpawnOne(isInitialSpawn: true))
                yield return null;
        }

        if (trackedBoxes.Count < targetActiveCount)
        {
            respawnPending = true;
            respawnRetryTimer = respawnRetryDelay;
            LogInitialSpawnFailure();
        }

        initialSpawnRoutine = null;
    }

    private void LogInitialSpawnFailure()
    {
        Debug.LogWarning(
            $"[ItemBoxSpawner] 초기 스폰 실패 — active={trackedBoxes.Count}/{targetActiveCount}, mode={spawnMode}, " +
            $"areas={resolvedMapSpawnAreas.Count}, groundLayers={groundLayers.value}. " +
            "Debug Spawn Log·Ground·Map Spawn Area·거리 설정을 확인하세요.",
            this);
    }

    private bool UsesMapRegion(bool isInitialSpawn)
    {
        switch (spawnMode)
        {
            case SpawnMode.AlwaysMap:
                return true;
            case SpawnMode.InitialMapOnly:
                return isInitialSpawn;
            default:
                return false;
        }
    }

    private bool ShouldRequireOutsideCamera(bool isInitialSpawn)
    {
        if (UsesMapRegion(isInitialSpawn))
            return false;

        if (!requireOutsideCameraView)
            return false;

        if (isInitialSpawn)
            return requireOutsideCameraForInitialSpawn;

        return true;
    }

    private bool TrySpawnOne(bool isInitialSpawn)
    {
        if (itemBoxPrefab == null)
            return false;

        if (!TryResolvePlayer())
            return false;

        if (trackedBoxes.Count >= targetActiveCount)
            return false;

        bool useMap = UsesMapRegion(isInitialSpawn);
        if (useMap && resolvedMapSpawnAreas.Count == 0)
        {
            if (!mapAreaMissingLogged)
            {
                Debug.LogWarning(
                    "[ItemBoxSpawner] 맵 스폰이 필요하지만 Map Spawn Area가 없습니다. ItemBoxSpawnFloorArea를 배치하세요.",
                    this);
                mapAreaMissingLogged = true;
            }
            return false;
        }

        bool outsideCamera = ShouldRequireOutsideCamera(isInitialSpawn);

        if (!TryFindSpawnPosition(useMap, outsideCamera, out Vector3 spawnPos))
        {
            if (debugSpawnLog)
            {
                Debug.Log(
                    $"[ItemBoxSpawner] 스폰 위치 실패 — map={useMap}, cameraOutside={outsideCamera}, initial={isInitialSpawn}",
                    this);
            }
            return false;
        }

        GameObject go = Instantiate(itemBoxPrefab, spawnPos, Quaternion.identity);
        ItemBox box = go.GetComponent<ItemBox>();
        if (box == null)
        {
            Debug.LogWarning("[ItemBoxSpawner] itemBoxPrefab에 ItemBox 컴포넌트가 없습니다.", go);
            Destroy(go);
            return false;
        }

        RegisterBox(box, spawnPos);

        if (debugSpawnLog)
        {
            Debug.Log(
                $"[ItemBoxSpawner] Spawned at {spawnPos} (active={trackedBoxes.Count}/{targetActiveCount}, " +
                $"mode={spawnMode}, initial={isInitialSpawn})",
                this);
        }

        return true;
    }

    private void RegisterBox(ItemBox box, Vector3 spawnPos)
    {
        trackedBoxes.Add(new TrackedBox { box = box, spawnPosition = spawnPos });
        box.Removed += HandleBoxRemoved;
    }

    private void HandleBoxRemoved(ItemBox box)
    {
        if (box == null)
            return;

        box.Removed -= HandleBoxRemoved;

        for (int i = trackedBoxes.Count - 1; i >= 0; i--)
        {
            if (trackedBoxes[i].box != box)
                continue;
            trackedBoxes.RemoveAt(i);
            break;
        }

        if (!spawningActive)
            return;

        if (trackedBoxes.Count < targetActiveCount)
        {
            respawnPending = true;
            respawnRetryTimer = respawnRetryDelay;
        }
    }

    private void PruneDestroyedBoxes()
    {
        for (int i = trackedBoxes.Count - 1; i >= 0; i--)
        {
            if (trackedBoxes[i].box != null)
                continue;

            trackedBoxes.RemoveAt(i);
        }
    }

    private bool TryFindSpawnPosition(bool useMapRegion, bool outsideCameraRequired, out Vector3 spawnPos)
    {
        return useMapRegion
            ? TryFindSpawnPositionInMap(outsideCameraRequired, out spawnPos)
            : TryFindSpawnPositionNearPlayer(outsideCameraRequired, out spawnPos);
    }

    private bool TryFindSpawnPositionNearPlayer(bool outsideCameraRequired, out Vector3 spawnPos)
    {
        spawnPos = default;

        Vector3 center = playerTransform.position;
        float playerY = center.y;
        float minGroundY = playerY + spawnFloorMinOffset;
        float maxGroundY = playerY + spawnFloorMaxOffset;
        float minRadius = Mathf.Max(0f, minDistanceFromPlayer);
        float maxRadius = Mathf.Max(minRadius, maxDistanceFromPlayer);
        int attempts = Mathf.Max(1, maxSpawnAttempts);
        float castDistance = raycastHeight + Mathf.Max(0f, -spawnFloorMinOffset) + 2f;
        float spacingSqr = minBoxSpacing * minBoxSpacing;
        bool useClearance = checkSpawnClearance && prefabBoxCollider != null && spawnClearanceMask != 0;

        for (int i = 0; i < attempts; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float distance = Random.Range(minRadius, maxRadius);
            Vector3 candidate = center + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);

            if (!TryRaycastGround(
                    candidate,
                    playerY + raycastHeight,
                    castDistance,
                    playerY,
                    minGroundY,
                    maxGroundY,
                    filterByPlayerHeight: true,
                    out Vector3 candidateSpawn))
                continue;

            if (!IsSpawnCandidateValid(candidateSpawn, outsideCameraRequired, spacingSqr, useClearance))
                continue;

            spawnPos = candidateSpawn;
            return true;
        }

        return false;
    }

    private bool TryFindSpawnPositionInMap(bool outsideCameraRequired, out Vector3 spawnPos)
    {
        spawnPos = default;

        if (resolvedMapSpawnAreas.Count == 0)
            return false;

        int attempts = Mathf.Max(1, maxSpawnAttempts);
        float spacingSqr = minBoxSpacing * minBoxSpacing;
        bool useClearance = checkSpawnClearance && prefabBoxCollider != null && spawnClearanceMask != 0;

        for (int i = 0; i < attempts; i++)
        {
            BoxCollider area = resolvedMapSpawnAreas[Random.Range(0, resolvedMapSpawnAreas.Count)];
            if (area == null)
                continue;

            Bounds bounds = area.bounds;
            float castDistance = raycastHeight + Mathf.Max(bounds.size.y, 2f) + 2f;
            float castOriginY = bounds.max.y + raycastHeight;

            float x = Random.Range(bounds.min.x, bounds.max.x);
            float z = Random.Range(bounds.min.z, bounds.max.z);
            Vector3 candidate = new Vector3(x, 0f, z);

            if (!TryRaycastGround(
                    candidate,
                    castOriginY,
                    castDistance,
                    referenceY: 0f,
                    minGroundY: float.NegativeInfinity,
                    maxGroundY: float.PositiveInfinity,
                    filterByPlayerHeight: false,
                    out Vector3 candidateSpawn))
                continue;

            if (!IsSpawnCandidateValid(candidateSpawn, outsideCameraRequired, spacingSqr, useClearance))
                continue;

            spawnPos = candidateSpawn;
            return true;
        }

        return false;
    }

    private bool TryRaycastGround(
        Vector3 candidateXZ,
        float castOriginY,
        float castDistance,
        float referenceY,
        float minGroundY,
        float maxGroundY,
        bool filterByPlayerHeight,
        out Vector3 spawnPoint)
    {
        spawnPoint = default;

        Vector3 castOrigin = new Vector3(candidateXZ.x, castOriginY, candidateXZ.z);
        int hitCount = Physics.RaycastNonAlloc(
            castOrigin,
            Vector3.down,
            groundHitBuffer,
            castDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore);

        if (hitCount <= 0)
            return false;

        if (filterByPlayerHeight)
        {
            if (!TryPickGroundHit(groundHitBuffer, hitCount, referenceY, minGroundY, maxGroundY, out RaycastHit groundHit))
                return false;
            spawnPoint = groundHit.point;
            return true;
        }

        spawnPoint = groundHitBuffer[0].point;
        return true;
    }

    private bool IsSpawnCandidateValid(
        Vector3 candidateSpawn,
        bool outsideCameraRequired,
        float spacingSqr,
        bool useClearance)
    {
        if (outsideCameraRequired && !IsOutsideCameraView(candidateSpawn))
            return false;

        if (IsTooCloseToOtherBoxes(candidateSpawn, spacingSqr))
            return false;

        if (useClearance && HasBoxClearanceBlocked(prefabBoxCollider, candidateSpawn))
            return false;

        return true;
    }

    private bool IsTooCloseToOtherBoxes(Vector3 pos, float spacingSqr)
    {
        if (spacingSqr <= 0f)
            return false;

        for (int i = 0; i < trackedBoxes.Count; i++)
        {
            if (trackedBoxes[i].box == null)
                continue;

            Vector3 other = trackedBoxes[i].box.transform.position;
            float dx = pos.x - other.x;
            float dz = pos.z - other.z;
            if (dx * dx + dz * dz < spacingSqr)
                return true;
        }

        return false;
    }

    private bool IsOutsideCameraView(Vector3 worldPos)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return true;

        Vector3 vp = cam.WorldToViewportPoint(worldPos);
        if (vp.z <= 0f)
            return true;

        float m = cameraViewportMargin;
        return vp.x < -m || vp.x > 1f + m || vp.y < -m || vp.y > 1f + m;
    }

    private bool HasBoxClearanceBlocked(BoxCollider box, Vector3 spawnRootPosition)
    {
        Vector3 worldCenter = spawnRootPosition + box.center;
        Vector3 halfExtents = box.size * 0.5f;

        int count = Physics.OverlapBoxNonAlloc(
            worldCenter,
            halfExtents,
            overlapBuffer,
            Quaternion.identity,
            spawnClearanceMask,
            QueryTriggerInteraction.Ignore);

        return count > 0;
    }

    private static bool TryPickGroundHit(
        RaycastHit[] hits,
        int hitCount,
        float playerY,
        float minGroundY,
        float maxGroundY,
        out RaycastHit bestHit)
    {
        bestHit = default;
        float bestDelta = float.MaxValue;
        bool found = false;

        for (int i = 0; i < hitCount; i++)
        {
            float y = hits[i].point.y;
            if (y < minGroundY || y > maxGroundY)
                continue;

            float delta = Mathf.Abs(y - playerY);
            if (delta >= bestDelta)
                continue;

            bestDelta = delta;
            bestHit = hits[i];
            found = true;
        }

        return found;
    }

    private void ResolveSpawnClearanceMask()
    {
        spawnClearanceMask = movementSettings != null ? movementSettings.blockMask : 0;
        if (spawnClearanceMask == 0)
        {
            int wall = LayerMask.NameToLayer("Wall");
            int prop = LayerMask.NameToLayer("Prop");
            if (wall >= 0) spawnClearanceMask |= 1 << wall;
            if (prop >= 0) spawnClearanceMask |= 1 << prop;
        }

        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0)
            spawnClearanceMask |= 1 << playerLayer;
    }

    private bool TryResolvePlayer()
    {
        if (playerTransform != null && playerTransform.gameObject.activeInHierarchy)
            return true;

        if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
            playerTransform = GameManager.Instance.playerTransform;

        if (playerTransform == null && PlayerResources.Instance != null)
            playerTransform = PlayerResources.Instance.transform;

        if (playerTransform == null)
        {
            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
                playerTransform = playerGo.transform;
        }

        if (playerTransform == null)
        {
            if (!playerMissingLogged)
            {
                Debug.LogWarning("[ItemBoxSpawner] 플레이어를 찾을 수 없어 스폰을 대기합니다.");
                playerMissingLogged = true;
            }
            return false;
        }

        playerMissingLogged = false;
        return true;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        DrawResolvedMapAreaGizmos();

        if (spawnMode == SpawnMode.AlwaysMap)
            return;

        Transform centerTransform = Application.isPlaying ? playerTransform : null;
        if (centerTransform == null && GameManager.Instance != null)
            centerTransform = GameManager.Instance.playerTransform;

        if (centerTransform == null)
            return;

        Vector3 center = centerTransform.position;

        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.6f);
        DrawCircle(center, minDistanceFromPlayer);

        Gizmos.color = new Color(0.1f, 0.55f, 1f, 0.85f);
        DrawCircle(center, maxDistanceFromPlayer);
    }

    private void DrawResolvedMapAreaGizmos()
    {
        if (Application.isPlaying && resolvedMapSpawnAreas.Count > 0)
        {
            for (int i = 0; i < resolvedMapSpawnAreas.Count; i++)
                DrawMapAreaGizmo(resolvedMapSpawnAreas[i], i);
            return;
        }

        var drawn = new HashSet<BoxCollider>();
        for (int i = 0; i < mapSpawnAreas.Count; i++)
        {
            if (mapSpawnAreas[i] == null || !drawn.Add(mapSpawnAreas[i]))
                continue;
            DrawMapAreaGizmo(mapSpawnAreas[i], drawn.Count - 1);
        }

        ItemBoxSpawnFloorArea[] markers = GetComponentsInChildren<ItemBoxSpawnFloorArea>(true);
        for (int i = 0; i < markers.Length; i++)
        {
            if (markers[i] == null)
                continue;

            BoxCollider col = markers[i].GetComponent<BoxCollider>();
            if (col == null || !drawn.Add(col))
                continue;

            Gizmos.color = markers[i].GizmoColor;
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }

    private void DrawMapAreaGizmo(BoxCollider area, int index)
    {
        if (area == null)
            return;

        Gizmos.color = DefaultFloorGizmoColors[index % DefaultFloorGizmoColors.Length];
        Gizmos.DrawWireCube(area.bounds.center, area.bounds.size);
    }

    private static void DrawCircle(Vector3 center, float radius)
    {
        if (radius <= 0f)
            return;

        const int segments = 32;
        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}
