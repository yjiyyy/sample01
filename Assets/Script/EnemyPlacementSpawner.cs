using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 맵 영역에 적을 한 번 배치하는 스포너.
/// 배치된 적은 스폰 연출 없이 Peace 상태에서 시작하고, 플레이어를 감지하면 발견 연출 후 추적한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class EnemyPlacementSpawner : MonoBehaviour
{
    [Serializable]
    public class PlacementEntry
    {
        [Tooltip("배치할 적 프리팹")]
        public GameObject enemyPrefab;

        [Min(1), Tooltip("이 프리팹을 배치할 수")]
        public int count = 1;
    }

    [Header("배치할 몬스터")]
    [SerializeField] private PlacementEntry[] placements = Array.Empty<PlacementEntry>();
    [SerializeField] private GameObject hpuiPrefab;

    [Header("맵 배치 영역")]
    [Tooltip("배치 영역 BoxCollider 목록. 비어 있으면 이 오브젝트의 BoxCollider를 사용합니다.")]
    [SerializeField] private List<BoxCollider> spawnAreas = new List<BoxCollider>();
    [Tooltip("배치된 몬스터끼리의 최소 수평 거리")]
    [SerializeField] private float minEnemySpacing = 2f;
    [Tooltip("몬스터 한 마리당 위치 탐색 최대 횟수")]
    [SerializeField] private int maxSpawnAttempts = 12;
    [Tooltip("배치 시 바라볼 수평 방향을 무작위로 정합니다.")]
    [SerializeField] private bool randomizeFacing = true;

    [Header("시작")]
    [Tooltip("씬 시작 시 자동으로 한 번 배치합니다.")]
    [SerializeField] private bool placeOnStart = true;
    [Tooltip("플레이어와 스테이지 초기화가 끝날 때까지 기다리는 시간")]
    [SerializeField] private float initialPlacementDelay = 0.1f;
    [Tooltip("스테이지가 아직 시작되지 않았을 때 대기하는 최대 시간. 이 시간이 지나도 배치합니다.")]
    [SerializeField] private float stageReadyTimeout = 3f;

    [Header("지면")]
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private float raycastHeight = 20f;
    [Tooltip("배치 영역 박스 Y 밖으로 벗어난 Ground 히트를 허용하는 여유(m)")]
    [SerializeField] private float groundYTolerance = 0.25f;

    [Header("장애물 검사")]
    [SerializeField] private MovementSettings movementSettings;
    [SerializeField] private bool checkSpawnClearance = true;

    [Header("디버그")]
    [SerializeField] private bool debugPlacementLog;
    [SerializeField] private bool drawGizmos = true;

    private readonly List<BoxCollider> resolvedSpawnAreas = new List<BoxCollider>(4);
    private readonly List<GameObject> spawnedEnemies = new List<GameObject>(16);
    private readonly List<Vector3> occupiedPositions = new List<Vector3>(16);
    private readonly List<PendingKillTracking> pendingKillTracking = new List<PendingKillTracking>(16);
    private readonly RaycastHit[] groundHitBuffer = new RaycastHit[8];

    private LayerMask spawnClearanceMask;
    private Collider[] overlapBuffer;
    private Coroutine placementRoutine;
    private bool hasPlaced;

    private struct PendingKillTracking
    {
        public EnemyHealth health;
        public GameObject sourcePrefab;
    }

    private void Reset()
    {
        BoxCollider area = GetComponent<BoxCollider>();
        if (area != null)
            area.isTrigger = true;
    }

    private void Awake()
    {
        ResolveGroundLayers();
        ResolveSpawnClearanceMask();

        if (groundLayers == 0)
        {
            Debug.LogWarning(
                "[EnemyPlacementSpawner] Ground Layers가 비어 있습니다. Ground 레이어나 Movement Settings를 지정하세요.",
                this);
        }

        int bufferSize = movementSettings != null ? movementSettings.overlapBufferSize : 16;
        overlapBuffer = new Collider[Mathf.Max(4, bufferSize)];
    }

    private void Start()
    {
        if (placeOnStart)
            BeginPlacement();
    }

    private void Update()
    {
        TryFlushPendingKillTracking();
    }

    private void OnValidate()
    {
        minEnemySpacing = Mathf.Max(0f, minEnemySpacing);
        maxSpawnAttempts = Mathf.Max(1, maxSpawnAttempts);
        initialPlacementDelay = Mathf.Max(0f, initialPlacementDelay);
        stageReadyTimeout = Mathf.Max(0f, stageReadyTimeout);
        raycastHeight = Mathf.Max(0.1f, raycastHeight);
        groundYTolerance = Mathf.Max(0f, groundYTolerance);

        if (placements == null)
            return;

        for (int i = 0; i < placements.Length; i++)
        {
            if (placements[i] != null)
                placements[i].count = Mathf.Max(1, placements[i].count);
        }
    }

    /// <summary>등록된 몬스터를 맵 영역에 한 번 배치합니다.</summary>
    public void BeginPlacement()
    {
        if (hasPlaced || placementRoutine != null)
            return;

        placementRoutine = StartCoroutine(PlacementRoutine());
    }

    private IEnumerator PlacementRoutine()
    {
        if (initialPlacementDelay > 0f)
            yield return new WaitForSeconds(initialPlacementDelay);

        float waitDeadline = Time.time + stageReadyTimeout;
        while (!IsStageReadyForKillTracking() && Time.time < waitDeadline)
            yield return null;

        if (!IsStageReadyForKillTracking() && debugPlacementLog)
        {
            Debug.LogWarning(
                "[EnemyPlacementSpawner] 스테이지 시작을 기다렸지만 아직 비활성입니다. 배치는 진행하고 킬 등록은 재시도합니다.",
                this);
        }

        ResolveSpawnAreas();
        if (resolvedSpawnAreas.Count == 0)
        {
            Debug.LogError(
                "[EnemyPlacementSpawner] 배치 영역이 없습니다. 이 오브젝트의 BoxCollider 또는 Spawn Areas를 설정하세요.",
                this);
            placementRoutine = null;
            yield break;
        }

        hasPlaced = true;
        occupiedPositions.Clear();

        if (placements != null)
        {
            for (int i = 0; i < placements.Length; i++)
            {
                PlacementEntry entry = placements[i];
                if (entry == null || entry.enemyPrefab == null)
                    continue;

                int count = Mathf.Max(1, entry.count);
                for (int j = 0; j < count; j++)
                {
                    if (!TryPlaceEnemy(entry.enemyPrefab) && debugPlacementLog)
                    {
                        Debug.LogWarning(
                            $"[EnemyPlacementSpawner] '{entry.enemyPrefab.name}' 배치 위치를 찾지 못했습니다. ({j + 1}/{count})",
                            this);
                    }

                    yield return null;
                }
            }
        }

        placementRoutine = null;
    }

    private bool TryPlaceEnemy(GameObject prefab)
    {
        if (!TryFindPlacementPosition(prefab, out Vector3 spawnPosition))
            return false;

        Quaternion spawnRotation = randomizeFacing
            ? Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f)
            : transform.rotation;

        GameObject enemy = Instantiate(prefab, spawnPosition, spawnRotation);
        spawnedEnemies.Add(enemy);
        occupiedPositions.Add(spawnPosition);

        RegisterKillTracking(enemy, prefab);
        CreateHpUi(enemy);

        if (debugPlacementLog)
            Debug.Log($"[EnemyPlacementSpawner] Peace 상태로 배치: {enemy.name} at {spawnPosition}", enemy);

        return true;
    }

    private bool TryFindPlacementPosition(GameObject prefab, out Vector3 spawnPosition)
    {
        spawnPosition = default;

        CapsuleCollider prefabCapsule = prefab.GetComponent<CapsuleCollider>();
        bool useClearance =
            checkSpawnClearance &&
            prefabCapsule != null &&
            spawnClearanceMask != 0;
        float spacingSqr = minEnemySpacing * minEnemySpacing;

        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            BoxCollider area = resolvedSpawnAreas[UnityEngine.Random.Range(0, resolvedSpawnAreas.Count)];
            if (area == null)
                continue;

            Vector3 localPoint = area.center + new Vector3(
                UnityEngine.Random.Range(-area.size.x * 0.5f, area.size.x * 0.5f),
                0f,
                UnityEngine.Random.Range(-area.size.z * 0.5f, area.size.z * 0.5f));
            Vector3 worldPoint = area.transform.TransformPoint(localPoint);
            Bounds bounds = area.bounds;
            Vector3 castOrigin = new Vector3(worldPoint.x, bounds.max.y + raycastHeight, worldPoint.z);
            float castDistance = raycastHeight + Mathf.Max(bounds.size.y, 2f) + 2f;

            int hitCount = Physics.RaycastNonAlloc(
                castOrigin,
                Vector3.down,
                groundHitBuffer,
                castDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore);

            if (!TryPickGroundHitInArea(
                    groundHitBuffer,
                    hitCount,
                    bounds.min.y - groundYTolerance,
                    bounds.max.y + groundYTolerance,
                    bounds.center.y,
                    out RaycastHit groundHit))
                continue;

            Vector3 candidate = groundHit.point;
            if (IsTooCloseToPlacedEnemy(candidate, spacingSqr))
                continue;

            if (useClearance &&
                StepChecker.WouldCapsuleOverlap(
                    prefabCapsule,
                    candidate,
                    spawnClearanceMask,
                    overlapBuffer,
                    null))
            {
                continue;
            }

            spawnPosition = candidate;
            return true;
        }

        return false;
    }

    private bool IsTooCloseToPlacedEnemy(Vector3 candidate, float spacingSqr)
    {
        if (spacingSqr <= 0f)
            return false;

        for (int i = 0; i < occupiedPositions.Count; i++)
        {
            Vector3 delta = candidate - occupiedPositions[i];
            delta.y = 0f;
            if (delta.sqrMagnitude < spacingSqr)
                return true;
        }

        return false;
    }

    private static bool TryPickGroundHitInArea(
        RaycastHit[] hits,
        int hitCount,
        float minGroundY,
        float maxGroundY,
        float referenceY,
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

            float delta = Mathf.Abs(y - referenceY);
            if (delta >= bestDelta)
                continue;

            bestDelta = delta;
            bestHit = hits[i];
            found = true;
        }

        return found;
    }

    private static bool IsStageReadyForKillTracking()
    {
        StageManager stage = StageManager.Active;
        return stage != null && stage.IsStageActive;
    }

    private void ResolveSpawnAreas()
    {
        resolvedSpawnAreas.Clear();

        if (spawnAreas != null)
        {
            for (int i = 0; i < spawnAreas.Count; i++)
            {
                BoxCollider area = spawnAreas[i];
                if (area != null && !resolvedSpawnAreas.Contains(area))
                    resolvedSpawnAreas.Add(area);
            }
        }

        if (resolvedSpawnAreas.Count == 0)
        {
            BoxCollider ownArea = GetComponent<BoxCollider>();
            if (ownArea != null)
                resolvedSpawnAreas.Add(ownArea);
        }
    }

    private void ResolveGroundLayers()
    {
        if (groundLayers != 0)
            return;

        if (movementSettings != null && movementSettings.groundMask != 0)
        {
            groundLayers = movementSettings.groundMask;
            return;
        }

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0)
            groundLayers = 1 << groundLayer;
    }

    private void ResolveSpawnClearanceMask()
    {
        spawnClearanceMask = movementSettings != null ? movementSettings.blockMask : 0;
        if (spawnClearanceMask == 0)
        {
            int wallLayer = LayerMask.NameToLayer("Wall");
            int propLayer = LayerMask.NameToLayer("Prop");
            if (wallLayer >= 0) spawnClearanceMask |= 1 << wallLayer;
            if (propLayer >= 0) spawnClearanceMask |= 1 << propLayer;
        }

        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0)
            spawnClearanceMask |= 1 << playerLayer;
    }

    private void RegisterKillTracking(GameObject enemy, GameObject sourcePrefab)
    {
        if (enemy == null)
            return;

        EnemyHealth health = enemy.GetComponent<EnemyHealth>();
        if (health == null)
            return;

        StageManager stage = StageManager.Active;
        if (stage != null && stage.RegisterEnemyKillTracking(health, sourcePrefab))
            return;

        pendingKillTracking.Add(new PendingKillTracking
        {
            health = health,
            sourcePrefab = sourcePrefab
        });
    }

    private void TryFlushPendingKillTracking()
    {
        if (pendingKillTracking.Count == 0)
            return;

        StageManager stage = StageManager.Active;
        if (stage == null || !stage.IsStageActive)
            return;

        for (int i = pendingKillTracking.Count - 1; i >= 0; i--)
        {
            PendingKillTracking pending = pendingKillTracking[i];
            if (pending.health == null)
            {
                pendingKillTracking.RemoveAt(i);
                continue;
            }

            if (stage.RegisterEnemyKillTracking(pending.health, pending.sourcePrefab))
                pendingKillTracking.RemoveAt(i);
        }
    }

    private void CreateHpUi(GameObject enemy)
    {
        if (hpuiPrefab == null || enemy == null)
            return;

        GameObject hpui = Instantiate(hpuiPrefab);
        HPUIControllerBase baseController = hpui.GetComponent<HPUIControllerBase>();
        if (baseController == null)
            return;

        baseController.health = enemy.GetComponent<EnemyHealth>();

        WorldHPUIController worldController = hpui.GetComponent<WorldHPUIController>();
        if (worldController != null)
            worldController.target = enemy.transform;

        Slider[] sliders = hpui.GetComponentsInChildren<Slider>(true);
        for (int i = 0; i < sliders.Length; i++)
        {
            string sliderName = sliders[i].name.ToLowerInvariant();
            if (sliderName.Contains("shield")) baseController.shieldSlider = sliders[i];
            else if (sliderName.Contains("hp")) baseController.hpSlider = sliders[i];
            else if (sliderName.Contains("evade")) baseController.evadeSlider = sliders[i];
        }

        baseController.Initialize(baseController.health);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        Gizmos.color = new Color(0.3f, 1f, 0.45f, 0.85f);

        bool drewAssignedArea = false;
        if (spawnAreas != null)
        {
            for (int i = 0; i < spawnAreas.Count; i++)
            {
                BoxCollider area = spawnAreas[i];
                if (area == null)
                    continue;

                Gizmos.DrawWireCube(area.bounds.center, area.bounds.size);
                drewAssignedArea = true;
            }
        }

        if (!drewAssignedArea)
        {
            BoxCollider ownArea = GetComponent<BoxCollider>();
            if (ownArea != null)
                Gizmos.DrawWireCube(ownArea.bounds.center, ownArea.bounds.size);
        }
    }
}
