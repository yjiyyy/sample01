using UnityEngine.AI;
using UnityEngine;
using UnityEngine.UI;

public class EnemySpawner : MonoBehaviour
{
    public GameObject[] enemyPrefabsByLevel;
    public GameObject hpuiPrefab; // ✅ 드래그해서 연결할 HP UI 프리팹
    public float spawnInterval = 2f;
    public float spawnRadius = 5f;

    private float spawnTimer;
    private int currentLevel = 0;

    public void SetSpawnLevel(int level)
    {
        currentLevel = Mathf.Clamp(level, 0, enemyPrefabsByLevel.Length - 1);
    }

    void Start()
    {
        // ✅ 게임 시작 시 1마리 즉시 스폰
        SpawnEnemy();
        spawnTimer = 0f; // 이후 spawnInterval 기준으로 재시작
    }

    void Update()
    {
        if (GameManager.Instance.playerTransform == null || enemyPrefabsByLevel.Length == 0)
            return;

        spawnTimer += Time.deltaTime;

        if (spawnTimer >= spawnInterval)
        {
            spawnTimer = 0f;
            SpawnEnemy();
        }
    }

    void SpawnEnemy()
    {
        Vector3 basePos = GameManager.Instance.playerTransform.position;
        Vector3 randomOffset = Random.insideUnitSphere * spawnRadius;
        randomOffset.y = 0;

        Vector3 targetPos = basePos + randomOffset;

        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 10f, NavMesh.AllAreas))
        {
            GameObject enemyPrefab = enemyPrefabsByLevel[currentLevel];
            GameObject enemy = Instantiate(enemyPrefab, hit.position, Quaternion.identity);

            // ✅ HP UI 자동 생성 및 연결
            if (hpuiPrefab != null)
            {
                GameObject hpui = Instantiate(hpuiPrefab);
                HPUIController controller = hpui.GetComponent<HPUIController>();
                controller.target = enemy.transform;
                controller.health = enemy.GetComponent<Health>();
                controller.hpSlider = hpui.GetComponentInChildren<Slider>();
            }
        }
    }
}
