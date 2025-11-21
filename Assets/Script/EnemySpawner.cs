using UnityEngine;
using UnityEngine.UI;

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

    public void SetSpawnLevel(int level)
    {
        if (enemyPrefabsByLevel == null || enemyPrefabsByLevel.Length == 0)
        {
            currentLevel = 0;
            return;
        }
        currentLevel = Mathf.Clamp(level, 0, enemyPrefabsByLevel.Length - 1);
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

        // 초기 1회 스폰
        if (!hasSpawnedInitial)
        {
            SpawnEnemy();
            hasSpawnedInitial = true;
            spawnTimer = 0f;
            return;
        }

        // 주기적 스폰
        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnInterval)
        {
            spawnTimer = 0f;
            SpawnEnemy();
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

    private void SpawnEnemy()
    {
        if (!ValidateSpawnPointExists()) return;
        if (enemyPrefabsByLevel == null || enemyPrefabsByLevel.Length == 0) return;

        GameObject prefab = enemyPrefabsByLevel[currentLevel];
        if (prefab == null)
        {
            if (debugSpawnLog) Debug.LogWarning("[EnemySpawner] 선택된 레벨 프리팹이 null입니다.");
            return;
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
                // 실패 시 y=0 강제 (씬 평면 가정)
                spawnPos.y = 0f;
            }
        }

        GameObject enemy = Instantiate(prefab, spawnPos, Quaternion.identity);

        if (debugSpawnLog)
        {
            Debug.Log($"[EnemySpawner] Spawned enemy level={currentLevel} at {spawnPos}");
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
            }
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