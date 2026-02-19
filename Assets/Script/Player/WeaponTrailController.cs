// WeaponTrailController.cs
// 무기에 부착. TrailStart/TrailEnd로 길이·형태를 뷰포트에서 수동 지정.
// Vertex Color Alpha로 부드러운 페이드 지원.

using System.Collections.Generic;
using UnityEngine;

public class WeaponTrailController : MonoBehaviour
{
    [Header("트레일 지점 (뷰포트에서 위치 조절)")]
    [Tooltip("트레일 시작 지점. 예: 손잡이 쪽")]
    public Transform trailStart;

    [Tooltip("트레일 끝 지점. 예: 무기 선단. 두 지점 간격 = 트레일 리본 폭")]
    public Transform trailEnd;

    [Header("시각 설정")]
    [Tooltip("트레일 색상. 재질의 _Color에 적용")]
    public Color trailColor = new Color(1f, 1f, 1f, 0.9f);

    [Tooltip("트레일 재질. 비어있으면 기본 Unlit 생성")]
    public Material trailMaterial;

    [Header("트레일 길이/밀도")]
    [Tooltip("기록할 최대 세그먼트 수")]
    public int maxPoints = 32;

    [Tooltip("새 포인트를 추가하기 위한 최소 이동 거리")]
    public float minPointDistance = 0.015f;

    [Tooltip("트레일이 남아있는 시간(초)")]
    public float trailLifetime = 0.25f;

    [Tooltip("트레일 기록 시간(초). 0이면 애니메이션 전체. >0이면 이 시간 이후 기록 중단")]
    public float trailDrawDuration = 0f;

    [Header("부드러운 곡선")]
    [Tooltip("세그먼트당 보간 점 수. 높을수록 곡선이 부드러워지고 빠른 움직임에도 각지지 않음")]
    [Range(1, 8)]
    public int smoothSegments = 4;

    // 내부
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Material instanceMaterial;
    private readonly List<Vector3> pointsStart = new List<Vector3>();
    private readonly List<Vector3> pointsEnd = new List<Vector3>();
    private readonly List<float> pointsTime = new List<float>();
    private bool isEmitting;
    private float enableTrailTime;
    private Mesh trailMesh;
    private int materialColorId;

    private void Awake()
    {
        EnsureComponents();
        materialColorId = Shader.PropertyToID("_Color");
    }

    private void EnsureComponents()
    {
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();

        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();

        if (trailMesh == null) trailMesh = new Mesh();
        trailMesh.name = "WeaponTrailMesh";

        if (instanceMaterial == null)
        {
            if (trailMaterial != null)
                instanceMaterial = new Material(trailMaterial);
            else
            {
                var shader = Shader.Find("Unlit/Transparent");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                instanceMaterial = new Material(shader ?? Shader.Find("Legacy Shaders/Transparent/Diffuse"));
                instanceMaterial.color = trailColor;
            }
            meshRenderer.sharedMaterial = instanceMaterial;
        }

        meshRenderer.enabled = false;
    }

    private void LateUpdate()
    {
        if (!isEmitting || trailStart == null || trailEnd == null)
        {
            UpdateTrailFade();
            return;
        }

        // trailDrawDuration > 0이면 지정 시간 이후 기록 중단
        if (trailDrawDuration > 0f && (Time.time - enableTrailTime) >= trailDrawDuration)
            isEmitting = false;

        PruneExpiredPoints();
        TryAddPoint();
        RebuildMesh();
    }

    private void TryAddPoint()
    {
        Vector3 s = trailStart.position;
        Vector3 e = trailEnd.position;

        if (pointsStart.Count == 0)
        {
            pointsStart.Add(s);
            pointsEnd.Add(e);
            pointsTime.Add(Time.time);
            return;
        }

        Vector3 lastMid = (pointsStart[pointsStart.Count - 1] + pointsEnd[pointsEnd.Count - 1]) * 0.5f;
        Vector3 currMid = (s + e) * 0.5f;
        float dist = Vector3.Distance(lastMid, currMid);

        if (dist >= minPointDistance)
        {
            pointsStart.Add(s);
            pointsEnd.Add(e);
            pointsTime.Add(Time.time);

            while (pointsStart.Count > maxPoints)
            {
                pointsStart.RemoveAt(0);
                pointsEnd.RemoveAt(0);
                pointsTime.RemoveAt(0);
            }
        }
    }

    private void PruneExpiredPoints()
    {
        float now = Time.time;
        while (pointsStart.Count > 0 && (now - pointsTime[0]) > trailLifetime)
        {
            pointsStart.RemoveAt(0);
            pointsEnd.RemoveAt(0);
            pointsTime.RemoveAt(0);
        }
    }

    /// <summary>Catmull-Rom 스플라인. seg번째 구간에서 t∈[0,1] 위치의 점 반환.</summary>
    private static Vector3 CatmullRom(List<Vector3> points, int seg, float t)
    {
        int n = points.Count;
        Vector3 p0 = points[Mathf.Max(0, seg - 1)];
        Vector3 p1 = points[seg];
        Vector3 p2 = points[Mathf.Min(seg + 1, n - 1)];
        Vector3 p3 = points[Mathf.Min(seg + 2, n - 1)];

        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private void UpdateTrailFade()
    {
        if (!isEmitting)
            PruneExpiredPoints();

        RebuildMesh();
    }

    private void RebuildMesh()
    {
        if (trailMesh == null || meshFilter == null) return;

        int n = pointsStart.Count;
        if (n < 2)
        {
            trailMesh.Clear();
            meshRenderer.enabled = false;
            return;
        }

        meshRenderer.enabled = true;

        int segs = Mathf.Max(1, smoothSegments);
        int totalPoints = (n - 1) * segs + 1;

        var verts = new List<Vector3>(totalPoints * 2);
        var uvs = new List<Vector2>(totalPoints * 2);
        var colors = new List<Color32>(totalPoints * 2);
        var indices = new List<int>((totalPoints - 1) * 6);

        Transform anchor = transform;

        // Catmull-Rom 스플라인으로 보간된 점 생성
        for (int seg = 0; seg < n - 1; seg++)
        {
            int steps = (seg == n - 2) ? segs + 1 : segs; // 마지막 세그먼트만 끝점 포함
            for (int step = 0; step < steps; step++)
            {
                float u = (float)step / segs;
                float globalT = ((float)seg + u) / Mathf.Max(1, n - 1);
                // 방망이(현재 무기) 쪽이 진하게, 꼬리로 갈수록 페이드
                float alpha = globalT;
                byte a = (byte)Mathf.Clamp(alpha * 255f, 0, 255);
                Color32 c = new Color32(255, 255, 255, a);

                Vector3 ps = CatmullRom(pointsStart, seg, u);
                Vector3 pe = CatmullRom(pointsEnd, seg, u);

                verts.Add(anchor.InverseTransformPoint(ps));
                verts.Add(anchor.InverseTransformPoint(pe));
                uvs.Add(new Vector2(globalT, 0f));
                uvs.Add(new Vector2(globalT, 1f));
                colors.Add(c);
                colors.Add(c);
            }
        }

        int m = verts.Count / 2;
        for (int i = 0; i < m - 1; i++)
        {
            int a = i * 2;
            int b = a + 1;
            int c = a + 2;
            int d = a + 3;
            indices.Add(a);
            indices.Add(c);
            indices.Add(b);
            indices.Add(b);
            indices.Add(c);
            indices.Add(d);
        }

        trailMesh.Clear();
        trailMesh.SetVertices(verts);
        trailMesh.SetUVs(0, uvs);
        trailMesh.SetColors(colors);
        trailMesh.SetIndices(indices, MeshTopology.Triangles, 0);
        trailMesh.RecalculateBounds();
        meshFilter.sharedMesh = trailMesh;

        if (instanceMaterial != null)
            instanceMaterial.SetColor(materialColorId, trailColor);
    }

    /// <summary>공격 시작 시 호출. 트레일 기록을 시작합니다.</summary>
    public void EnableTrail()
    {
        if (trailStart == null || trailEnd == null)
        {
            Debug.LogWarning("[WeaponTrailController] trailStart 또는 trailEnd가 비어 있습니다.");
            return;
        }
        EnsureComponents();
        isEmitting = true;
        enableTrailTime = Time.time;
    }

    /// <summary>공격 종료 시 호출. 트레일 기록을 중단합니다. 기존 트레일은 lifetime 동안 페이드됩니다.</summary>
    public void DisableTrail()
    {
        isEmitting = false;
    }

    /// <summary>회피/넉백/스턴 등으로 공격이 끊길 때 호출. 트레일을 즉시 비웁니다.</summary>
    public void CancelTrailImmediate()
    {
        isEmitting = false;
        pointsStart.Clear();
        pointsEnd.Clear();
        pointsTime.Clear();
        if (trailMesh != null) trailMesh.Clear();
        if (meshRenderer != null) meshRenderer.enabled = false;
    }

    private void OnDisable()
    {
        pointsStart.Clear();
        pointsEnd.Clear();
        pointsTime.Clear();
        if (trailMesh != null) trailMesh.Clear();
        if (meshRenderer != null) meshRenderer.enabled = false;
    }

    private void OnDestroy()
    {
        if (instanceMaterial != null && instanceMaterial != trailMaterial)
            Destroy(instanceMaterial);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (trailStart != null && trailEnd != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(trailStart.position, 0.02f);
            Gizmos.DrawWireSphere(trailEnd.position, 0.02f);
            Gizmos.DrawLine(trailStart.position, trailEnd.position);
        }
    }
#endif
}
