using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class EnemyDetector : MonoBehaviour
{
    [Header("시야 설정")]
    public float viewAngle = 45f;
    public float viewDistance = 10f;
    public int segmentCount = 30;

    [Header("시각화 y 오프셋")]
    public float height = 0.8f; // Inspector에서 바로 조정 가능

    [Header("무기 상태")]
    public WeaponBehavior weaponBehavior; // 연결 필수

    private Mesh viewMesh;
    private MeshFilter meshFilter;

    // ✅ 현재 감지된 적 리스트
    private List<Transform> detectedEnemies = new List<Transform>();

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        viewMesh = new Mesh();
        meshFilter.mesh = viewMesh;
    }

    void LateUpdate()
    {
        if (weaponBehavior != null && weaponBehavior.data != null && !weaponBehavior.data.isMelee)
        {
            DrawFOV();
            meshFilter.mesh = viewMesh;
        }
        else
        {
            viewMesh.Clear(); // 총 아니면 안보이게
        }
    }

    void DrawFOV()
    {
        Vector3[] vertices = new Vector3[segmentCount + 2];
        int[] triangles = new int[segmentCount * 3];

        // y축 오프셋 적용
        vertices[0] = new Vector3(0, height, 0);

        float angleStep = viewAngle * 2 / segmentCount;

        for (int i = 0; i <= segmentCount; i++)
        {
            float angle = -viewAngle + i * angleStep;
            float rad = Mathf.Deg2Rad * angle;
            Vector3 dir = new Vector3(Mathf.Sin(rad), 0, Mathf.Cos(rad));
            vertices[i + 1] = new Vector3(dir.x * viewDistance, height, dir.z * viewDistance);
        }

        for (int i = 0; i < segmentCount; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        viewMesh.Clear();
        viewMesh.vertices = vertices;
        viewMesh.triangles = triangles;
    }

    /* ───────── 감지 기능 ───────── */
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            Transform enemy = other.transform;
            if (!detectedEnemies.Contains(enemy))
                detectedEnemies.Add(enemy);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            Transform enemy = other.transform;
            if (detectedEnemies.Contains(enemy))
                detectedEnemies.Remove(enemy);
        }
    }

    /// <summary>
    /// 지정한 범위 내의 적들 반환
    /// </summary>
    public List<Transform> GetEnemiesInRange(float range)
    {
        List<Transform> result = new List<Transform>();

        foreach (var enemy in detectedEnemies)
        {
            if (enemy == null) continue;
            float dist = Vector3.Distance(transform.position, enemy.position);
            if (dist <= range)
                result.Add(enemy);
        }

        return result;
    }
}
