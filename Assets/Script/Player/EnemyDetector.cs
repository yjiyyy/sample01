using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class EnemyDetector : MonoBehaviour
{
    [Header("시야 설정")]
    [Tooltip("반각(deg). 실제 시야각은 이 값의 두 배입니다.")]
    public float viewAngle = 45f; // 반각
    public float viewDistance = 10f;
    public int segmentCount = 30;

    [Header("시각화 y 오프셋")]
    public float height = 0.8f;

    [Header("무기 상태(시각화 연동)")]
    public WeaponBehavior weaponBehavior; // 자동 주입 권장

    [Header("감지(물리 스캔)")]
    [Tooltip("물리 스캔으로 적 감지 수행")]
    public bool usePhysicsScan = true;
    [Tooltip("감지 대상 레이어 (기본: 전부). Enemy 태그로도 최종 필터링합니다.")]
    public LayerMask enemyLayers = ~0;
    [Tooltip("정렬: 거리 우선, 다음으로 시야 중심과의 각도 우선")]
    public bool sortByCenterBias = true;

    private Mesh viewMesh;
    private MeshFilter meshFilter;

    // 현재 감지된 적 리스트(최적 타겟이 0번)
    private readonly List<Transform> detectedEnemies = new List<Transform>();

    // NonAlloc 버퍼
    private static readonly Collider[] OverlapBuffer = new Collider[256];

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        viewMesh = new Mesh();
        meshFilter.mesh = viewMesh;
    }

    void LateUpdate()
    {
        var d = weaponBehavior != null ? weaponBehavior.data : null;
        bool showFOV = d is WeaponDataSO_Gun || d is WeaponDataSO_Launcher;

        // Gun SO에 시야 파라미터가 있으면 동기화(없으면 인스펙터 값 사용)
        if (d is WeaponDataSO_Gun g)
        {
            // aimScanAngle: 전체각이라고 가정 → 반각으로 변환
            float half = Mathf.Clamp(g.aimScanAngle * 0.5f, 0f, 180f);
            if (half > 0f) viewAngle = half;
            if (g.aimScanDistance > 0f) viewDistance = g.aimScanDistance;
        }

        // 감지 갱신
        if (usePhysicsScan)
            RefreshDetection();

        // FOV 시각화
        if (showFOV)
        {
            DrawFOV();
            meshFilter.mesh = viewMesh;
        }
        else
        {
            viewMesh.Clear();
        }
    }

    private void RefreshDetection()
    {
        detectedEnemies.Clear();

        int count = Physics.OverlapSphereNonAlloc(transform.position, viewDistance, OverlapBuffer, enemyLayers, QueryTriggerInteraction.Ignore);
        if (count <= 0) return;

        Vector3 origin = transform.position;

        // 수평 forward
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        float halfAngle = Mathf.Clamp(viewAngle, 0f, 180f);

        // 임시 후보와 메트릭 저장
        var candidates = new List<(Transform t, float dist, float ang)>(count);
        var seen = new HashSet<Transform>();

        for (int i = 0; i < count; i++)
        {
            Collider col = OverlapBuffer[i];
            if (col == null) continue;

            Transform root = col.transform;

            // Enemy 태그 부모까지 탐색
            if (!root.CompareTag("Enemy"))
            {
                var p = root.GetComponentInParent<Transform>();
                if (p == null || !p.CompareTag("Enemy")) continue;
                root = p;
            }

            if (!seen.Add(root)) continue;

            Vector3 to = root.position - origin;
            float dist = to.magnitude;
            if (dist <= 0.0001f || dist > viewDistance) continue;

            // 수평 각도
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) continue;
            to.Normalize();

            float ang = Vector3.Angle(forward, to);
            if (ang > halfAngle) continue;

            candidates.Add((root, dist, ang));
        }

        if (candidates.Count == 0) return;

        if (sortByCenterBias)
        {
            candidates.Sort((a, b) =>
            {
                int c = a.dist.CompareTo(b.dist);
                if (c != 0) return c;
                return a.ang.CompareTo(b.ang);
            });
        }
        else
        {
            candidates.Sort((a, b) => a.dist.CompareTo(b.dist));
        }

        foreach (var c in candidates)
            detectedEnemies.Add(c.t);
    }

    void DrawFOV()
    {
        Vector3[] vertices = new Vector3[segmentCount + 2];
        int[] triangles = new int[segmentCount * 3];

        // 중심(로컬) + y오프셋
        vertices[0] = new Vector3(0, height, 0);

        float angleStep = viewAngle * 2f / Mathf.Max(1, segmentCount);

        for (int i = 0; i <= segmentCount; i++)
        {
            float angle = -viewAngle + i * angleStep;
            float rad = Mathf.Deg2Rad * angle;
            Vector3 dir = new Vector3(Mathf.Sin(rad), 0, Mathf.Cos(rad));
            vertices[i + 1] = new Vector3(dir.x * viewDistance, height, dir.z * viewDistance);
        }

        for (int i = 0; i < segmentCount; i++)
        {
            triangles[i * 3 + 0] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        viewMesh.Clear();
        viewMesh.vertices = vertices;
        viewMesh.triangles = triangles;
    }

    // ───────── 감지 결과 제공 ─────────
    /// <summary>
    /// range 이내의 적 목록(최적 타겟이 0번). 내부에서 최신 스캔을 보장합니다.
    /// </summary>
    public List<Transform> GetEnemiesInRange(float range)
    {
        if (usePhysicsScan)
            RefreshDetection();

        List<Transform> result = new List<Transform>();
        for (int i = 0; i < detectedEnemies.Count; i++)
        {
            var t = detectedEnemies[i];
            if (t == null) continue;
            if (Vector3.Distance(transform.position, t.position) <= range)
                result.Add(t);
        }
        return result;
    }

    // 기존 트리거 방식은 더 이상 필요 없지만, 혹시 사용할 수 있도록 남겨둠
    private void OnTriggerEnter(Collider other)
    {
        // 미사용
    }
    private void OnTriggerExit(Collider other)
    {
        // 미사용
    }
}