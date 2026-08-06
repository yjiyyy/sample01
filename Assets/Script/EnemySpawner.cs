using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 전투 적 스폰. 플레이어 중심 반경에서 Ground 레이어 위에 생성하고,
/// StageManager 레벨(StageLevelIconBar 표시와 동일)에 맞춰 난이도를 적용합니다.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("플레이어 중심 스폰")]
    [Tooltip("플레이어로부터 최소 거리")]
    public float minSpawnRadius = 8f;

    [Tooltip("플레이어로부터 최대 거리")]
    public float maxSpawnRadius = 15f;

    [Header("스폰 설정")]
    public GameObject hpuiPrefab;

    [Tooltip("index 0 = 표시 레벨 1. StageManager.SetSpawnLevel과 동일 인덱스.")]
    public EnemySpawnLevelSettings[] levelSettings;

    [Tooltip("스테이지 진입 후 첫 스폰까지 대기 시간(초). 이후 첫 스폰은 즉시, 다음부터 spawnInterval 적용.")]
    [Min(0f)]
    public float initialSpawnDelay = 3f;

    [Header("스폰 개수 제한 (이 스포너 기준)")]
    [Tooltip("0이면 무제한. 이 스포너가 생성할 수 있는 총 스폰 횟수(누적, 레벨 무관).")]
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
    [Tooltip("여기에 등록된 바디 프리팹은 거리 디스폰 예외로 처리합니다.")]
    public GameObject[] despawnExceptionPrefabs;
    [Tooltip("여기에 등록된 EnemyConfig를 사용하는 몬스터는 거리 디스폰 예외로 처리합니다.")]
    public EnemyConfig[] despawnExceptionConfigs;

    private float spawnTimer;
    private int currentLevel;
    private bool playerMissingLogged;
    private bool _stageSpawnActive;
    private bool _waitingInitialSpawnDelay;
    private float _initialSpawnDelayTimer;

    private Transform _playerTransform;
    private int _totalSpawnedByThisSpawner;

    // 레벨별 생존 카운터. OnDeath 즉시 감소하여 정확한 동시 생존 수를 보장.
    private int[] _alivePerLevel = new int[0];

    private LayerMask _spawnClearanceMask;
    private Collider[] _overlapBuffer;
    private readonly RaycastHit[] _groundHitBuffer = new RaycastHit[8];
    private readonly List<TrackedEnemy> _trackedEnemies = new List<TrackedEnemy>(32);
    private float _despawnCheckTimer;

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

        int levelCount = levelSettings != null ? levelSettings.Length : 0;
        _alivePerLevel = new int[Mathf.Max(1, levelCount)];
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

    /// <summary>StageManager currentLevel(0=표시 Lv.1)과 동기화.</summary>
    public void SetSpawnLevel(int level, bool isStageBegin = false)
    {
        currentLevel = ResolveLevelIndex(level);

        if (isStageBegin)
        {
            _stageSpawnActive = true;
            _waitingInitialSpawnDelay = true;
            _initialSpawnDelayTimer = 0f;
            spawnTimer = 0f;
        }
        else
        {
            // 레벨업: 즉시 스폰 (타이머를 인터벌 이상으로 세팅)
            EnemySpawnLevelSettings settings = GetCurrentLevelSettings();
            spawnTimer = settings != null ? settings.spawnInterval : 0f;
        }

        if (debugSpawnLog)
            Debug.Log($"[EnemySpawner] SetSpawnLevel displayLevel={currentLevel + 1}, isStageBegin={isStageBegin}");
    }

    /// <summary>레벨 재시작 등에서 카운터 초기화.</summary>
    public void ResetSpawnCounters()
    {
        _totalSpawnedByThisSpawner = 0;
        for (int i = 0; i < _alivePerLevel.Length; i++) _alivePerLevel[i] = 0;
        spawnTimer = 0f;
        _despawnCheckTimer = 0f;
        _playerTransform = null;
        _trackedEnemies.Clear();
        _stageSpawnActive = false;
        _waitingInitialSpawnDelay = false;
        _initialSpawnDelayTimer = 0f;
    }

    /// <summary>신규 스폰만 중지합니다. 이미 나온 적은 그대로 둡니다.</summary>
    public void StopSpawning()
    {
        _stageSpawnActive = false;
        _waitingInitialSpawnDelay = false;
        _initialSpawnDelayTimer = 0f;
    }

    void Start()
    {
        spawnTimer = 0f;
        _despawnCheckTimer = 0f;
    }

    void Update()
    {
        if (!TryResolvePlayer()) return;
        if (!_stageSpawnActive) return;

        EnemySpawnLevelSettings settings = GetCurrentLevelSettings();
        if (settings == null) return;

        if (_waitingInitialSpawnDelay)
        {
            _initialSpawnDelayTimer += Time.deltaTime;
            if (_initialSpawnDelayTimer >= initialSpawnDelay)
            {
                _waitingInitialSpawnDelay = false;
                TrySpawnEnemy(settings);
                spawnTimer = 0f;
            }

            TickDistanceDespawn();
            return;
        }

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= settings.spawnInterval)
        {
            spawnTimer = 0f;
            TrySpawnEnemy(settings);
        }

        TickDistanceDespawn();
    }

    private int ResolveLevelIndex(int level)
    {
        if (levelSettings == null || levelSettings.Length == 0)
            return 0;

        if (level < 0)
            return 0;

        if (level >= levelSettings.Length)
            return levelSettings.Length - 1;

        return level;
    }

    private EnemySpawnLevelSettings GetCurrentLevelSettings()
    {
        if (levelSettings == null || levelSettings.Length == 0)
            return null;

        return levelSettings[ResolveLevelIndex(currentLevel)];
    }

    private int GetAliveAtLevel(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= _alivePerLevel.Length)
            return 0;
        return _alivePerLevel[levelIndex];
    }

    internal void ReleaseOneAliveAtLevel(int levelIndex)
    {
        if (levelIndex >= 0 && levelIndex < _alivePerLevel.Length)
            _alivePerLevel[levelIndex] = Mathf.Max(0, _alivePerLevel[levelIndex] - 1);
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
    private bool TrySpawnEnemy(EnemySpawnLevelSettings settings)
    {
        if (!TryResolvePlayer()) return false;
        if (settings == null) return false;

        if (maxTotalSpawns > 0 && _totalSpawnedByThisSpawner >= maxTotalSpawns)
        {
            if (debugSpawnLog)
                Debug.Log("[EnemySpawner] 총 스폰 상한 도달 — 스폰 생략.");
            return false;
        }

        int levelIndex = ResolveLevelIndex(currentLevel);
        if (settings.maxConcurrentAlive > 0 && GetAliveAtLevel(levelIndex) >= settings.maxConcurrentAlive)
        {
            if (debugSpawnLog)
                Debug.Log("[EnemySpawner] 현재 레벨 동시 생존 상한 도달 — 스폰 생략.");
            return false;
        }

        if (!settings.TryPickConfig(out EnemyConfig config))
        {
            if (debugSpawnLog)
                Debug.LogWarning($"[EnemySpawner] 레벨 {levelIndex + 1} 몬스터 풀이 비어 있거나 가중치가 0입니다.");
            return false;
        }

        if (!config.TryPickBodyPrefab(out GameObject bodyPrefab))
        {
            if (debugSpawnLog)
                Debug.LogWarning(
                    $"[EnemySpawner] '{config.name}' Appearance Pool에 Body Prefabs가 없습니다.");
            return false;
        }

        if (!TryFindSpawnPosition(bodyPrefab, out Vector3 spawnPos))
        {
            if (debugSpawnLog)
                Debug.Log("[EnemySpawner] Ground·장애물 검사를 통과한 스폰 위치를 찾지 못해 스폰 생략.");
            return false;
        }

        Quaternion spawnRot = GetSpawnFacingRotation(spawnPos);
        GameObject enemy = EnemyConfigSpawner.Spawn(config, bodyPrefab, spawnPos, spawnRot);
        if (enemy == null)
            return false;

        var spawnedEnemy = enemy.GetComponent<Enemy>();
        spawnedEnemy?.BeginCombatSpawnIntro();

        _totalSpawnedByThisSpawner++;
        if (levelIndex < _alivePerLevel.Length)
            _alivePerLevel[levelIndex]++;
        RegisterAliveTracking(enemy, bodyPrefab, config, levelIndex);

        if (debugSpawnLog)
        {
            Debug.Log(
                $"[EnemySpawner] Spawned '{config.name}' body={bodyPrefab.name} displayLevel={levelIndex + 1} at {spawnPos} " +
                $"(aliveAtLevel={GetAliveAtLevel(levelIndex)}, total={_totalSpawnedByThisSpawner})");
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

    private Quaternion GetSpawnFacingRotation(Vector3 spawnPos)
    {
        if (_playerTransform == null) return Quaternion.identity;

        Vector3 lookDir = _playerTransform.position - spawnPos;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude < 0.0001f) return Quaternion.identity;

        return Quaternion.LookRotation(lookDir.normalized, Vector3.up);
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

    private bool HasSpawnClearanceBlocked(CapsuleCollider prefabCapsule, Vector3 spawnRootPosition)
    {
        return StepChecker.WouldCapsuleOverlap(
            prefabCapsule,
            spawnRootPosition,
            _spawnClearanceMask,
            _overlapBuffer,
            null);
    }

    private void RegisterAliveTracking(
        GameObject enemy,
        GameObject sourceBodyPrefab,
        EnemyConfig sourceConfig,
        int spawnLevelIndex)
    {
        if (enemy == null) return;

        bool isDespawnException = IsDespawnException(sourceBodyPrefab, sourceConfig);
        var runtime = enemy.GetComponent<SpawnedEnemyRuntime>();
        if (runtime == null)
            runtime = enemy.AddComponent<SpawnedEnemyRuntime>();
        runtime.Init(this, spawnLevelIndex);

        _trackedEnemies.Add(new TrackedEnemy
        {
            root = enemy.transform,
            runtime = runtime,
            spawnLevelIndex = spawnLevelIndex,
            isDespawnException = isDespawnException,
            outOfRangeSince = -1f
        });

        var h = enemy.GetComponent<EnemyHealth>();
        if (h != null)
        {
            StageManager.Active?.RegisterEnemyKillTracking(h, sourceConfig);

            // OnDeath 즉시 레벨별 카운터 감소 (오브젝트 Destroy 전에 바로 반영)
            void Handler()
            {
                h.OnDeath -= Handler;
                runtime.ReleaseFromOwner();
            }
            h.OnDeath += Handler;
        }
    }

    private bool IsDespawnException(GameObject sourceBodyPrefab, EnemyConfig sourceConfig)
    {
        if (sourceBodyPrefab != null && despawnExceptionPrefabs != null)
        {
            for (int i = 0; i < despawnExceptionPrefabs.Length; i++)
            {
                if (despawnExceptionPrefabs[i] == sourceBodyPrefab)
                    return true;
            }
        }

        if (sourceConfig != null && despawnExceptionConfigs != null)
        {
            for (int i = 0; i < despawnExceptionConfigs.Length; i++)
            {
                if (despawnExceptionConfigs[i] == sourceConfig)
                    return true;
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

            tracked.runtime?.ReleaseFromOwner();

            Destroy(tracked.root.gameObject);
            _trackedEnemies.RemoveAt(i);
        }
    }

    private struct TrackedEnemy
    {
        public Transform root;
        public SpawnedEnemyRuntime runtime;
        public int spawnLevelIndex;
        public bool isDespawnException;
        public float outOfRangeSince;
    }

    private sealed class SpawnedEnemyRuntime : MonoBehaviour
    {
        private EnemySpawner _owner;
        private int _spawnLevelIndex;
        private bool _released;

        public void Init(EnemySpawner owner, int spawnLevelIndex)
        {
            _owner = owner;
            _spawnLevelIndex = spawnLevelIndex;
            _released = false;
        }

        public void ReleaseFromOwner()
        {
            if (_released) return;
            _released = true;
            _owner?.ReleaseOneAliveAtLevel(_spawnLevelIndex);
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
