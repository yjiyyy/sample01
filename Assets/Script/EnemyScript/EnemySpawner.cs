using UnityEngine.AI;
using UnityEngine;
using UnityEngine.UI;

public class EnemySpawner : MonoBehaviour
{
    public GameObject[] enemyPrefabsByLevel;
    public GameObject hpuiPrefab;
    public float spawnInterval = 2f;
    public float spawnRadius = 5f;

    private float spawnTimer;
    private int currentLevel = 0;
    private bool hasSpawnedInitial = false; // ✅ 초기 스폰 여부 체크

    public void SetSpawnLevel(int level)
    {
        currentLevel = Mathf.Clamp(level, 0, enemyPrefabsByLevel.Length - 1);
    }

    void Start()
    {
        // ✅ 초기 스폰을 Update에서 처리하도록 변경
        spawnTimer = 0f;
        hasSpawnedInitial = false;
    }

    void Update()
    {
        // ✅ playerTransform null 체크 강화
        if (GameManager.Instance == null || GameManager.Instance.playerTransform == null)
        {
            Debug.LogWarning("[EnemySpawner] GameManager 또는 playerTransform이 아직 준비되지 않았습니다.");
            return;
        }

        if (enemyPrefabsByLevel.Length == 0)
        {
            Debug.LogWarning("[EnemySpawner] 적 프리팹이 설정되지 않았습니다.");
            return;
        }

        // ✅ 게임 시작 시 1마리 즉시 스폰 (한 번만)
        if (!hasSpawnedInitial)
        {
            SpawnEnemy();
            hasSpawnedInitial = true;
            spawnTimer = 0f;
            Debug.Log("[EnemySpawner] 초기 적 스폰 완료");
            return;
        }

        // ✅ 이후 주기적 스폰
        spawnTimer += Time.deltaTime;

        if (spawnTimer >= spawnInterval)
        {
            spawnTimer = 0f;
            SpawnEnemy();
        }
    }

    void SpawnEnemy()
    {
        // ✅ 추가 안전 검사
        if (GameManager.Instance?.playerTransform == null)
        {
            Debug.LogError("[EnemySpawner] playerTransform이 null입니다. 스폰을 건너뜁니다.");
            return;
        }

        Vector3 basePos = GameManager.Instance.playerTransform.position;
        Vector3 randomOffset = Random.insideUnitSphere * spawnRadius;
        randomOffset.y = 0;

        Vector3 targetPos = basePos + randomOffset;

        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 10f, NavMesh.AllAreas))
        {
            GameObject enemyPrefab = enemyPrefabsByLevel[currentLevel];
            GameObject enemy = Instantiate(enemyPrefab, hit.position, Quaternion.identity);

            // ✅ HP UI 자동 생성 및 연결 (EnemyHealth로 수정)
            if (hpuiPrefab != null)
            {
                GameObject hpui = Instantiate(hpuiPrefab);
                HPUIController controller = hpui.GetComponent<HPUIController>();
                controller.target = enemy.transform;
                controller.health = enemy.GetComponent<EnemyHealth>(); // ✅ EnemyHealth로 수정
                controller.hpSlider = hpui.GetComponentInChildren<Slider>();

                Debug.Log($"[EnemySpawner] 적 + HP UI 생성 완료: {enemy.name}");
            }
            else
            {
                Debug.LogWarning("[EnemySpawner] hpuiPrefab이 연결되지 않았습니다.");
            }
        }
        else
        {
            Debug.LogWarning("[EnemySpawner] NavMesh 위치를 찾을 수 없습니다.");
        }
    }
}