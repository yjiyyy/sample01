using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 적 스폰. 플레이어 중심 반경에서 Ground 레이어 위에만 생성.
/// 동시 생존 수·총 스폰 수 제한 가능 (각각 0이면 무제한).
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("플레이어 중심 스폰")]
    [Tooltip("플레이어로부터 최소 거리")]
    public float minSpawnRadius = 8f;

    [Tooltip("플레이어로부터 최대 거리")]
    public float maxSpawnRadius = 15f;

    [Header("스폰 설정")]
    public GameObject[] enemyPrefabsByLevel;
    public GameObject hpuiPrefab;
    public float spawnInterval = 2f;

    [Header("스폰 개수 제한 (이 스포너 기준)")]
    [Tooltip("0이면 무제한. 동시에 살아 있을 수 있는 유닛 수(이 스포너가 스폰한 것만).")]
    public int maxConcurrentAlive = 0;

    [Tooltip("0이면 무제한. 이 스포너가 생성할 수 있는 총 스폰 횟수(누적).")]
    public int maxTotalSpawns = 0;

    [Header("지면 (Ground 레이어)")]
    public float raycastHeight = 20f;
    public LayerMask groundLayers;
    [Tooltip("플레이어 Y 기준 허용 바닥 높이 하한 (m). 예: -0.5")]
    public float spawnFloorMinOffset = -0.5f;
    [Tooltip("플레이어 Y 기준 허용 바닥 높이 상한 (m). 예: +1")]
    public float spawnFloorMaxOffset = 1f;
    [Tooltip("유효한 Ground 위치를 찾지 못하면 재시도 횟수")]
    public int maxSpawnAttempts = 6;

    [Header("스폰 장애물 검사")]
    [Tooltip("Wall·Prop 등 장애물 레이어. 비어 있으면 Movement Settings 또는 기본 Wall|Prop|Player 사용.")]
    public MovementSettings movementSettings;
    [Tooltip("스폰 위치에 몬스터 프리팹 캡슐이 겹치면 실패. Enemy 레이어(다른 몬스터)는 검사하지 않음.")]
    public bool checkSpawnClearance = true;

    [Header("디버그")]
    public bool debugSpawnLog = false;
    public bool drawGizmos = true;

    [Header("디스폰 (플레이어 거리 기준)")]
    [Tooltip("켜면 이 스포너가 생성한 적을 플레이어 거리 기준으로 디스폰합니다.")]
    public bool enableDistanceDespawn = true;
    [Tooltip("이 거리 밖으로 벗어나면 유예시간 카운트를 시작합니다.")]
    public float despawnDistance = 55f;
    [Tooltip("거리 초과 상태가 이 시간 이상 유지되면 디스폰합니다.")]
    public float despawnDelay = 3f;
    [Tooltip("거리 디스폰 체크 주기(초). 너무 낮추면 불필요한 연산이 늘어납니다.")]
    public float despawnCheckInterval = 0.25f;
    [Tooltip("여기에 등록된 프리팹은 거리 디스폰 예외로 처리합니다.")]
    public GameObject[] despawnExceptionPrefabs;
    [Tooltip("여기에 등록된 EnemyConfig를 사용하는 몬스터는 거리 디스폰 예외로 처리합니다.")]
    public EnemyConfig[] despawnExceptionConfigs;

    private float spawnTimer;
    private int currentLevel = 0;
    private bool hasSpawnedInitial = false;
    private bool playerMissingLogged = false;

    private Transform _playerTransform;
    private int _aliveFromThisSpawner;
    private int _totalSpawnedByThisSpawner;
    private LayerMask _spawnClearanceMask;
    private Collider[] _overlapBuffer;
    private readonly RaycastHit[] _groundHitBuffer = new RaycastHit[8];
    private readonly List<TrackedEnemy> _trackedEnemies = new List<TrackedEnemy>(32);
    private float _despawnCheckTimer;

    /// <summary>이 스포너가 스폰해 아직 사망 처리 전으로 잡고 있는 수(EnemyHealth.OnDeath 기준).</summary>
    public int AliveFromThisSpawner => _aliveFromThisSpawner;

    /// <summary>이 스포너가 실제로 Instantiate한 누적 횟수.</summary>
    public int TotalSpawnedByThisSpawner => _totalSpawnedByThisSpawner;

    void Awake()
    {
        if (groundLayers == 0)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0)
                groundLayers = 1 << groundLayer;
        }

        ResolveSpawnClearanceMask();
        int bufferSize = movementSettings != null ? movementSettings.overlapBufferSize : 16;
        _overlapBuffer = new Collider[Mathf.Max(4, bufferSize)];
    }

    private void ResolveSpawnClearanceMask()
    {
        _spawnClearanceMask = movementSettings != null ? movementSettings.blockMask : 0;
        if (_spawnClearanceMask == 0)
        {
            int wall = LayerMask.NameToLayer("Wall");
            int prop = LayerMask.NameToLayer("Prop");
            if (wall >= 0) _spawnClearanceMask |= 1 << wall;
            if (prop >= 0) _spawnClearanceMask |= 1 << prop;
        }

        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0)
            _spawnClearanceMask |= 1 << playerLayer;
    }

    public void SetSpawnLevel(int level)
    {
        if (enemyPrefabsByLevel == null || enemyPrefabsByLevel.Length == 0)
        {
            currentLevel = 0;
            return;
        }
        currentLevel = Mathf.Clamp(level, 0, enemyPrefabsByLevel.Length - 1);
    }

    /// <summary>레벨 재시작 등에서 카운터 초기화.</summary>
    public void ResetSpawnCounters()
    {
        _aliveFromThisSpawner = 0;
        _totalSpawnedByThisSpawner = 0;
        hasSpawnedInitial = false;
        spawnTimer = 0f;
        _despawnCheckTimer = 0f;
        _playerTransform = null;
        _trackedEnemies.Clear();
    }

    void Start()
    {
        spawnTimer = 0f;
        hasSpawnedInitial = false;
        _despawnCheckTimer = 0f;
    }

    void Update()
    {
        if (!TryResolvePlayer()) return;
        if (enemyPrefabsByLevel == null || enemyPrefabsByLevel.Length == 0) return;

        if (!hasSpawnedInitial)
        {
            if (TrySpawnEnemy())
            {
                hasSpawnedInitial = true;
                spawnTimer = 0f;
            }
            return;
        }

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnInterval)
        {
            spawnTimer = 0f;
            TrySpawnEnemy();
        }

        TickDistanceDespawn();
    }

    private bool TryResolvePlayer()
    {
        if (_playerTransform != null && _playerTransform.gameObject.activeInHierarchy)
            return true;

        _playerTransform = GameManager.Instance != null ? GameManager.Instance.playerTransform : null;
        if (_playerTransform == null)
        {
            if (!playerMissingLogged)
            {
                Debug.LogWarning("[EnemySpawner] 플레이어를 찾을 수 없어 스폰을 대기합니다.");
                playerMissingLogged = true;
            }
            return false;
        }

        playerMissingLogged = false;
        return true;
    }

    /// <returns>스폰 성공 여부</returns>
    private bool TrySpawnEnemy()
    {
        if (!TryResolvePlayer()) return false;
        if (enemyPrefabsByLevel == null || enemyPrefabsByLevel.Length == 0) return false;

        if (maxTotalSpawns > 0 && _totalSpawnedByThisSpawner >= maxTotalSpawns)
        {
            if (debugSpawnLog)
                Debug.Log("[EnemySpawner] 총 스폰 상한 도달 — 스폰 생략.");
            return false;
        }

        if (maxConcurrentAlive > 0 && _aliveFromThisSpawner >= maxConcurrentAlive)
        {
            if (debugSpawnLog)
                Debug.Log("[EnemySpawner] 동시 생존 상한 도달 — 스폰 생략.");
            return false;
        }

        GameObject prefab = enemyPrefabsByLevel[currentLevel];
        if (prefab == null)
        {
            if (debugSpawnLog) Debug.LogWarning("[EnemySpawner] 선택된 레벨 프리팹이 null입니다.");
            return false;
        }

        if (!TryFindSpawnPosition(prefab, out Vector3 spawnPos))
        {
            if (debugSpawnLog)
                Debug.Log("[EnemySpawner] Ground·장애물 검사를 통과한 스폰 위치를 찾지 못해 스폰 생략.");
            return false;
        }

        Quaternion spawnRot = GetSpawnFacingRotation(spawnPos);
        GameObject enemy = Instantiate(prefab, spawnPos, spawnRot);

        var spawnedEnemy = enemy.GetComponent<Enemy>();
        spawnedEnemy?.BeginSpawnIntro();

        _totalSpawnedByThisSpawner++;
        _aliveFromThisSpawner++;
        RegisterAliveTracking(enemy, prefab);

        if (debugSpawnLog)
        {
            Debug.Log($"[EnemySpawner] Spawned enemy level={currentLevel} at {spawnPos} (alive={_aliveFromThisSpawner}, total={_totalSpawnedByThisSpawner})");
        }

        if (hpuiPrefab != null && enemy != null)
        {
            GameObject hpui = Instantiate(hpuiPrefab);
            var baseCtrl = hpui.GetComponent<HPUIControllerBase>();
            if (baseCtrl != null)
            {
                baseCtrl.health = enemy.GetComponent<EnemyHealth>();

                var world = hpui.GetComponent<WorldHPUIController>();
                if (world != null) world.target = enemy.transform;

                Slider[] sliders = hpui.GetComponentsInChildren<Slider>(true);
                foreach (var s in sliders)
                {
                    string n = s.name.ToLower();
                    if (n.Contains("shield")) baseCtrl.shieldSlider = s;
                    else if (n.Contains("hp")) baseCtrl.hpSlider = s;
                    else if (n.Contains("evade")) baseCtrl.evadeSlider = s;
                }

                baseCtrl.Initialize(baseCtrl.health);
            }
        }

        return true;
    }

    private bool TryFindSpawnPosition(GameObject prefab, out Vector3 spawnPos)
    {
        spawnPos = default;

        Vector3 center = _playerTransform.position;
        float playerY = center.y;
        float minGroundY = playerY + spawnFloorMinOffset;
        float maxGroundY = playerY + spawnFloorMaxOffset;
        float minRadius = Mathf.Max(0f, minSpawnRadius);
        float maxRadius = Mathf.Max(minRadius, maxSpawnRadius);
        int attempts = Mathf.Max(1, maxSpawnAttempts);
        float castDistance = raycastHeight + Mathf.Max(0f, -spawnFloorMinOffset) + 2f;

        CapsuleCollider prefabCapsule = prefab != null ? prefab.GetComponent<CapsuleCollider>() : null;
        bool useClearance = checkSpawnClearance && prefabCapsule != null && _spawnClearanceMask != 0;

        for (int i = 0; i < attempts; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float distance = Random.Range(minRadius, maxRadius);
            Vector3 candidate = center + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);

            Vector3 castOrigin = new Vector3(candidate.x, playerY + raycastHeight, candidate.z);
            int hitCount = Physics.RaycastNonAlloc(
                castOrigin,
                Vector3.down,
                _groundHitBuffer,
                castDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore);

            if (hitCount <= 0)
                continue;

            if (!TryPickGroundHit(_groundHitBuffer, hitCount, playerY, minGroundY, maxGroundY, out RaycastHit groundHit))
                continue;

            Vector3 candidateSpawn = groundHit.point;
            if (useClearance && HasSpawnClearanceBlocked(prefabCapsule, candidateSpawn))
                continue;

            spawnPos = candidateSpawn;
            return true;
        }

        return false;
    }

    /// <summary>스폰 순간 플레이어 방향(수평)을 바라보도록 회전. 추적은 하지 않음.</summary>
    private Quaternion GetSpawnFacingRotation(Vector3 spawnPos)
    {
        if (_playerTransform == null) return Quaternion.identity;

        Vector3 lookDir = _playerTransform.position - spawnPos;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude < 0.0001f) return Quaternion.identity;

        return Quaternion.LookRotation(lookDir.normalized, Vector3.up);
    }

    /// <summary>플레이어와 같은 층에 가장 가까운 Ground 히트를 선택.</summary>
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

    /// <summary>프리팹 루트 캡슐 기준 장애물 겹침 여부 (Enemy 레이어는 마스크에 없음).</summary>
    private bool HasSpawnClearanceBlocked(CapsuleCollider prefabCapsule, Vector3 spawnRootPosition)
    {
        return StepChecker.WouldCapsuleOverlap(
            prefabCapsule,
            spawnRootPosition,
            _spawnClearanceMask,
            _overlapBuffer,
            null);
    }

    private void RegisterAliveTracking(GameObject enemy, GameObject sourcePrefab)
    {
        if (enemy == null) return;

        bool isDespawnException = IsDespawnException(sourcePrefab, enemy);
        var runtime = enemy.GetComponent<SpawnedEnemyRuntime>();
        if (runtime == null)
            runtime = enemy.AddComponent<SpawnedEnemyRuntime>();
        runtime.Init(this);

        _trackedEnemies.Add(new TrackedEnemy
        {
            root = enemy.transform,
            runtime = runtime,
            isDespawnException = isDespawnException,
            outOfRangeSince = -1f
        });

        var h = enemy.GetComponent<EnemyHealth>();
        if (h != null)
        {
            StageManager.Active?.RegisterEnemyKillTracking(h, sourcePrefab);

            void Handler()
            {
                h.OnDeath -= Handler;
                runtime.ReleaseFromOwner();
            }
            h.OnDeath += Handler;
        }
    }

    internal void ReleaseOneAlive()
    {
        _aliveFromThisSpawner = Mathf.Max(0, _aliveFromThisSpawner - 1);
    }

    private bool IsDespawnException(GameObject sourcePrefab, GameObject spawnedInstance)
    {
        if (sourcePrefab != null && despawnExceptionPrefabs != null)
        {
            for (int i = 0; i < despawnExceptionPrefabs.Length; i++)
            {
                if (despawnExceptionPrefabs[i] == sourcePrefab)
                    return true;
            }
        }

        if (despawnExceptionConfigs != null && despawnExceptionConfigs.Length > 0)
        {
            EnemyFacade facade = spawnedInstance != null ? spawnedInstance.GetComponent<EnemyFacade>() : null;
            EnemyConfig cfg = facade != null ? facade.config : null;
            if (cfg != null)
            {
                for (int i = 0; i < despawnExceptionConfigs.Length; i++)
                {
                    if (despawnExceptionConfigs[i] == cfg)
                        return true;
                }
            }
        }

        return false;
    }

    private void TickDistanceDespawn()
    {
        if (!enableDistanceDespawn) return;
        if (_trackedEnemies.Count == 0) return;
        if (_playerTransform == null) return;

        _despawnCheckTimer += Time.deltaTime;
        if (_despawnCheckTimer < Mathf.Max(0.05f, despawnCheckInterval))
            return;
        _despawnCheckTimer = 0f;

        float sqrDistance = Mathf.Max(0f, despawnDistance);
        sqrDistance *= sqrDistance;
        float delay = Mathf.Max(0f, despawnDelay);
        float now = Time.time;
        Vector3 playerPos = _playerTransform.position;

        for (int i = _trackedEnemies.Count - 1; i >= 0; i--)
        {
            TrackedEnemy tracked = _trackedEnemies[i];
            if (tracked.root == null)
            {
                _trackedEnemies.RemoveAt(i);
                continue;
            }

            if (tracked.isDespawnException)
                continue;

            Vector3 delta = tracked.root.position - playerPos;
            delta.y = 0f;
            bool outOfRange = delta.sqrMagnitude > sqrDistance;

            if (!outOfRange)
            {
                tracked.outOfRangeSince = -1f;
                _trackedEnemies[i] = tracked;
                continue;
            }

            if (tracked.outOfRangeSince < 0f)
            {
                tracked.outOfRangeSince = now;
                _trackedEnemies[i] = tracked;
                continue;
            }

            if (now - tracked.outOfRangeSince < delay)
                continue;

            if (debugSpawnLog)
                Debug.Log($"[EnemySpawner] Distance despawn: {tracked.root.name}");

            if (tracked.runtime != null)
                tracked.runtime.ReleaseFromOwner();

            Destroy(tracked.root.gameObject);
            _trackedEnemies.RemoveAt(i);
        }
    }

    private struct TrackedEnemy
    {
        public Transform root;
        public SpawnedEnemyRuntime runtime;
        public bool isDespawnException;
        public float outOfRangeSince;
    }

    private sealed class SpawnedEnemyRuntime : MonoBehaviour
    {
        private EnemySpawner _owner;
        private bool _released;

        public void Init(EnemySpawner owner)
        {
            _owner = owner;
            _released = false;
        }

        public void ReleaseFromOwner()
        {
            if (_released) return;
            _released = true;
            _owner?.ReleaseOneAlive();
        }

        private void OnDestroy()
        {
            ReleaseFromOwner();
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Transform centerTransform = Application.isPlaying ? _playerTransform : null;
        if (centerTransform == null && GameManager.Instance != null)
            centerTransform = GameManager.Instance.playerTransform;

        if (centerTransform == null) return;

        Vector3 center = centerTransform.position;

        Gizmos.color = new Color(1f, 0.6f, 0f, 0.6f);
        DrawCircle(center, minSpawnRadius);

        Gizmos.color = new Color(1f, 0.2f, 0f, 0.8f);
        DrawCircle(center, maxSpawnRadius);
    }

    private static void DrawCircle(Vector3 center, float radius)
    {
        if (radius <= 0f) return;

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
}
