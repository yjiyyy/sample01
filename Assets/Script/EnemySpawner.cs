using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 적 스폰. 동시 생존 수·총 스폰 수 제한 가능 (각각 0이면 무제한).
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("스폰 포인트 (첫 번째 하나만 사용)")]
    public Transform[] spawnPoints;

    [Header("스폰 설정")]
    public GameObject[] enemyPrefabsByLevel;
    public GameObject hpuiPrefab;
    public float spawnInterval = 2f;

    [Tooltip("지터(수평 원) 반경. 0이면 고정 위치")]
    public float jitterRadius = 0.5f;

    [Header("스폰 개수 제한 (이 스포너 기준)")]
    [Tooltip("0이면 무제한. 동시에 살아 있을 수 있는 유닛 수(이 스포너가 스폰한 것만).")]
    public int maxConcurrentAlive = 0;

    [Tooltip("0이면 무제한. 이 스포너가 생성할 수 있는 총 스폰 횟수(누적).")]
    public int maxTotalSpawns = 0;

    [Header("지면 보정")]
    public bool applyGroundRaycast = true;
    public float raycastHeight = 20f;
    public LayerMask groundLayers = ~0;

    [Header("디버그")]
    public bool debugSpawnLog = false;
    public bool drawGizmos = true;

    private float spawnTimer;
    private int currentLevel = 0;
    private bool hasSpawnedInitial = false;
    private bool spawnPointErrorLogged = false;

    private int _aliveFromThisSpawner;
    private int _totalSpawnedByThisSpawner;

    /// <summary>이 스포너가 스폰해 아직 사망 처리 전으로 잡고 있는 수(EnemyHealth.OnDeath 기준).</summary>
    public int AliveFromThisSpawner => _aliveFromThisSpawner;

    /// <summary>이 스포너가 실제로 Instantiate한 누적 횟수.</summary>
    public int TotalSpawnedByThisSpawner => _totalSpawnedByThisSpawner;

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
    }

    void Start()
    {
        spawnTimer = 0f;
        hasSpawnedInitial = false;
    }

    void Update()
    {
        if (!ValidateSpawnPointExists()) return;
        if (enemyPrefabsByLevel == null || enemyPrefabsByLevel.Length == 0) return;

        // 초기 1회 스폰 (성공할 때까지 플래그 미설정은 아님 — 실패 시 매 프레임 재시도)
        if (!hasSpawnedInitial)
        {
            if (TrySpawnEnemy())
            {
                hasSpawnedInitial = true;
                spawnTimer = 0f;
            }
            return;
        }

        // 주기적 스폰
        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnInterval)
        {
            spawnTimer = 0f;
            TrySpawnEnemy();
        }
    }

    private bool ValidateSpawnPointExists()
    {
        if (spawnPoints == null || spawnPoints.Length == 0 || spawnPoints[0] == null)
        {
            if (!spawnPointErrorLogged)
            {
                Debug.LogWarning("[EnemySpawner] 유효한 spawnPoints[0]가 없습니다. 스폰 중단.");
                spawnPointErrorLogged = true;
            }
            return false;
        }
        return true;
    }

    /// <returns>스폰 성공 여부</returns>
    private bool TrySpawnEnemy()
    {
        if (!ValidateSpawnPointExists()) return false;
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

        // 기본 위치
        Vector3 basePos = spawnPoints[0].position;

        // 지터 적용
        Vector3 spawnPos = basePos;
        if (jitterRadius > 0f)
        {
            Vector2 jitter2D = Random.insideUnitCircle * jitterRadius;
            spawnPos += new Vector3(jitter2D.x, 0f, jitter2D.y);
        }

        // 지면 보정 (Raycast)
        if (applyGroundRaycast)
        {
            Vector3 castOrigin = spawnPos + Vector3.up * raycastHeight;
            if (Physics.Raycast(castOrigin, Vector3.down, out RaycastHit hit, raycastHeight * 2f, groundLayers, QueryTriggerInteraction.Ignore))
            {
                spawnPos = hit.point;
            }
            else
            {
                spawnPos.y = 0f;
            }
        }

        GameObject enemy = Instantiate(prefab, spawnPos, Quaternion.identity);

        _totalSpawnedByThisSpawner++;
        _aliveFromThisSpawner++;
        RegisterAliveTracking(enemy);

        if (debugSpawnLog)
        {
            Debug.Log($"[EnemySpawner] Spawned enemy level={currentLevel} at {spawnPos} (alive={_aliveFromThisSpawner}, total={_totalSpawnedByThisSpawner})");
        }

        // HP UI 연결
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

    private void RegisterAliveTracking(GameObject enemy)
    {
        if (enemy == null) return;

        var h = enemy.GetComponent<EnemyHealth>();
        if (h != null)
        {
            void Handler()
            {
                h.OnDeath -= Handler;
                ReleaseOneAlive();
            }
            h.OnDeath += Handler;
            return;
        }

        var rel = enemy.AddComponent<SpawnerAliveRelease>();
        rel.Init(this);
    }

    internal void ReleaseOneAlive()
    {
        _aliveFromThisSpawner = Mathf.Max(0, _aliveFromThisSpawner - 1);
    }

    /// <summary>EnemyHealth 없을 때 오브젝트 파괴 시 동시 수 보정.</summary>
    private sealed class SpawnerAliveRelease : MonoBehaviour
    {
        private EnemySpawner _owner;

        public void Init(EnemySpawner owner)
        {
            _owner = owner;
        }

        private void OnDestroy()
        {
            _owner?.ReleaseOneAlive();
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        if (spawnPoints == null || spawnPoints.Length == 0 || spawnPoints[0] == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(spawnPoints[0].position, 0.15f);

        if (jitterRadius > 0f)
        {
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.4f);
            Gizmos.DrawWireSphere(spawnPoints[0].position, jitterRadius);
        }
    }
}
