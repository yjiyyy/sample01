using System;
using System.Reflection;
using UnityEngine.AI;
using UnityEngine;
using UnityEngine.UI;

// 변경 요약:
// - HPUIController 타입을 직접 참조하지 않음(컴파일 오류 방지).
// - HPUIControllerBase 우선 사용, 없으면 런타임에 GetComponent("HPUIController")로 reflection 폴백.
// - reflection 폴백은 필드명이 기존과 같을 것을 기대하고 안전하게 시도함.

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
        if (GameManager.Instance?.playerTransform == null)
        {
            Debug.LogError("[EnemySpawner] playerTransform이 null입니다. 스폰을 건너뜁니다.");
            return;
        }

        Vector3 basePos = GameManager.Instance.playerTransform.position;
        // 명시적으로 UnityEngine.Random을 사용해서 System.Random과의 충돌을 피합니다.
        Vector3 randomOffset = UnityEngine.Random.insideUnitSphere * spawnRadius;
        randomOffset.y = 0;

        Vector3 targetPos = basePos + randomOffset;

        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 10f, NavMesh.AllAreas))
        {
            GameObject enemyPrefab = enemyPrefabsByLevel[currentLevel];
            GameObject enemy = Instantiate(enemyPrefab, hit.position, Quaternion.identity);

            if (hpuiPrefab != null)
            {
                // 기존과 동일하게 프리팹의 설정을 따르며 인스턴스화
                GameObject hpui = Instantiate(hpuiPrefab);

                // 1) 새 구조 우선
                var baseCtrl = hpui.GetComponent<HPUIControllerBase>();
                if (baseCtrl != null)
                {
                    baseCtrl.health = enemy.GetComponent<EnemyHealth>();

                    // 월드 타입 컨트롤러가 있다면 target 연결
                    var world = hpui.GetComponent<WorldHPUIController>();
                    if (world != null)
                    {
                        world.target = enemy.transform;
                    }

                    // 슬라이더 자동 매핑
                    Slider[] sliders = hpui.GetComponentsInChildren<Slider>(true);
                    foreach (Slider s in sliders)
                    {
                        string n = s.name.ToLower();
                        if (n.Contains("shield")) baseCtrl.shieldSlider = s;
                        else if (n.Contains("hp")) baseCtrl.hpSlider = s;
                        else if (n.Contains("evade")) baseCtrl.evadeSlider = s;
                    }
                }
                else
                {
                    // 2) 폴백: 레거시 HPUIController가 남아 있으면 reflection으로 설정
                    Component legacy = hpui.GetComponent("HPUIController") as Component;
                    if (legacy != null)
                    {
                        // 가능한 필드들을 안전하게 설정
                        TrySetFieldOrProperty(legacy, "target", enemy.transform);
                        TrySetFieldOrProperty(legacy, "health", enemy.GetComponent<EnemyHealth>());

                        Slider[] sliders = hpui.GetComponentsInChildren<Slider>(true);
                        foreach (Slider s in sliders)
                        {
                            string n = s.name.ToLower();
                            if (n.Contains("shield")) TrySetFieldOrProperty(legacy, "shieldSlider", s);
                            else if (n.Contains("hp")) TrySetFieldOrProperty(legacy, "hpSlider", s);
                            else if (n.Contains("evade")) TrySetFieldOrProperty(legacy, "evadeSlider", s);
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[EnemySpawner] hpuiPrefab에 HPUIControllerBase 또는 레거시 HPUIController가 없습니다.");
                    }
                }
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

    // reflection helper: 필드 또는 프로퍼티를 찾아 값 설정 시도
    private static void TrySetFieldOrProperty(Component comp, string name, object value)
    {
        if (comp == null) return;
        var t = comp.GetType();
        try
        {
            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null)
            {
                // null 허용 시에도 SetValue 시도 (필드타입에 맞지 않으면 예외 발생 가능하므로 try-catch)
                f.SetValue(comp, value);
                return;
            }

            var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.CanWrite)
            {
                p.SetValue(comp, value);
                return;
            }

            // 없으면 시도할 다른 이름(간혹 필드명이 다를 수 있음) 등 확장 가능
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[EnemySpawner] 레거시 HPUIController에 필드/프로퍼티 '{name}' 설정 실패: {ex.Message}");
        }
    }
}