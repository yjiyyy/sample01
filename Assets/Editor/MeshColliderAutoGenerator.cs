using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 프리팹/폴더의 콜리더를 메쉬 형태에 맞는 BoxCollider 또는 CapsuleCollider로 자동 생성합니다.
/// · MeshCollider가 있으면 → 해당 메쉬를 분석해 Box/Capsule로 교체
/// · MeshCollider가 없고 MeshFilter만 있으면 → 기존 콜리더 제거 후 Box/Capsule 생성
/// 사용: Project 창에서 프리팹/폴더 선택 → 우클릭 → 콜리더 자동 생성 (Box/Capsule)
/// </summary>
public static class MeshColliderAutoGenerator
{
    private const string MenuPath = "Assets/콜리더 자동 생성 (Box/Capsule)";

    private const float MaxRadiusCoefficientOfVariation = 0.18f;
    private const float MinInnerOuterRadiusRatio = 0.88f;
    private const float CrossSectionSimilarity = 0.90f;
    private const float MinHeightToDiameterRatio = 0.12f;
    private const float MinTallAspectRatio = 1.35f;
    private const float MinSquatRoundRatio = 1.65f;
    private const float NearCubeMinRatio = 0.75f;
    private const float MaxProfileToBoundsRadiusRatio = 1.15f;
    private const float MinAngularCoverage = 0.70f;
    private const int AngleBinCount = 16;
    private const int MinVertexCount = 8;
    private const float MinRadius = 0.01f;
    private const float MinPerpRadiusRatio = 0.20f;

    private struct ConversionStats
    {
        public int boxCount;
        public int capsuleCount;
        public int skippedCount;
        public int removedCount;
        public int fromMeshColliderCount;
        public int fromMeshFilterCount;
    }

    private struct ColliderSettings
    {
        public bool isTrigger;
        public bool enabled;
        public PhysicsMaterial sharedMaterial;
        public LayerMask includeLayers;
        public LayerMask excludeLayers;
        public int layerOverridePriority;

        public static ColliderSettings FromCollider(Collider collider)
        {
            return new ColliderSettings
            {
                isTrigger = collider.isTrigger,
                enabled = collider.enabled,
                sharedMaterial = collider.sharedMaterial,
                includeLayers = collider.includeLayers,
                excludeLayers = collider.excludeLayers,
                layerOverridePriority = collider.layerOverridePriority
            };
        }

        public static ColliderSettings Default => new ColliderSettings { enabled = true };
    }

    private struct CapsuleFit
    {
        public Vector3 center;
        public float radius;
        public float height;
        public int direction;
    }

    private struct CapsuleCandidate
    {
        public int axis;
        public float radius;
        public float height;
        public int tier;
    }

    private struct SortedDimensions
    {
        public float dim0;
        public float dim1;
        public float dim2;
        public int axis0;
        public int axis1;
        public int axis2;
    }

    [MenuItem(MenuPath, false, 2001)]
    public static void Generate()
    {
        List<string> prefabPaths = CollectPrefabPaths();
        if (prefabPaths.Count == 0)
        {
            Debug.LogWarning("[ColliderAuto] 처리할 프리팹이 없습니다.");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "콜리더 자동 생성",
                $"{prefabPaths.Count}개 프리팹을 처리합니다.\n\n" +
                "· MeshCollider 있음 → 메쉬 분석 후 Box/Capsule로 교체\n" +
                "· MeshCollider 없음 → MeshFilter 메쉬 분석 후 Box/Capsule 생성\n" +
                "  (기존 콜리더는 제거 후 새로 만듭니다)\n" +
                "· 기본은 Box, 확실한 원기둥만 Capsule\n" +
                "· 납작한 원형은 이름에 Round/Cylinder 등이 있을 때만 Capsule\n\n" +
                "계속할까요?",
                "생성",
                "취소"))
        {
            return;
        }

        int processedPrefabs = 0;
        ConversionStats totalStats = default;

        try
        {
            for (int i = 0; i < prefabPaths.Count; i++)
            {
                string path = prefabPaths[i];
                EditorUtility.DisplayProgressBar("콜리더 자동 생성", path, (float)i / prefabPaths.Count);

                ConversionStats stats = ProcessPrefab(path);
                if (stats.boxCount + stats.capsuleCount > 0)
                    processedPrefabs++;

                totalStats.boxCount += stats.boxCount;
                totalStats.capsuleCount += stats.capsuleCount;
                totalStats.skippedCount += stats.skippedCount;
                totalStats.removedCount += stats.removedCount;
                totalStats.fromMeshColliderCount += stats.fromMeshColliderCount;
                totalStats.fromMeshFilterCount += stats.fromMeshFilterCount;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[ColliderAuto] 완료 — 프리팹 {processedPrefabs}개, " +
            $"MeshCollider {totalStats.fromMeshColliderCount}개, MeshFilter {totalStats.fromMeshFilterCount}개, " +
            $"제거 {totalStats.removedCount}개, Box {totalStats.boxCount}개, Capsule {totalStats.capsuleCount}개, " +
            $"스킵 {totalStats.skippedCount}개.");
    }

    [MenuItem(MenuPath, true)]
    public static bool ValidateGenerate()
    {
        foreach (Object obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (IsFolderOrPrefab(path))
                return true;
        }

        return false;
    }

    private static ConversionStats ProcessPrefab(string prefabPath)
    {
        ConversionStats stats = default;
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null)
            return stats;

        var hadMeshCollider = new HashSet<GameObject>();
        MeshCollider[] meshColliders = root.GetComponentsInChildren<MeshCollider>(true);
        foreach (MeshCollider meshCollider in meshColliders)
            hadMeshCollider.Add(meshCollider.gameObject);

        foreach (MeshCollider meshCollider in meshColliders)
        {
            if (TryReplaceMeshCollider(meshCollider, ref stats))
                stats.fromMeshColliderCount++;
            else
                stats.skippedCount++;
        }

        MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter meshFilter in meshFilters)
        {
            if (hadMeshCollider.Contains(meshFilter.gameObject))
                continue;

            Mesh mesh = meshFilter.sharedMesh;
            if (mesh == null)
            {
                stats.skippedCount++;
                continue;
            }

            GameObject go = meshFilter.gameObject;
            ColliderSettings settings = CaptureColliderSettings(go);
            stats.removedCount += RemoveAllColliders(go);
            AddColliderFromMesh(mesh, go, settings, ref stats);
            stats.fromMeshFilterCount++;
        }

        if (stats.boxCount + stats.capsuleCount > 0)
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

        PrefabUtility.UnloadPrefabContents(root);
        return stats;
    }

    private static bool TryReplaceMeshCollider(MeshCollider meshCollider, ref ConversionStats stats)
    {
        Mesh mesh = meshCollider.sharedMesh;
        if (mesh == null)
        {
            Debug.LogWarning(
                $"[ColliderAuto] MeshCollider 메쉬 없음, 스킵: {GetHierarchyPath(meshCollider.transform)}",
                meshCollider);
            return false;
        }

        ColliderSettings settings = ColliderSettings.FromCollider(meshCollider);
        AddColliderFromMesh(mesh, meshCollider.gameObject, settings, ref stats);
        Object.DestroyImmediate(meshCollider);
        return true;
    }

    private static ColliderSettings CaptureColliderSettings(GameObject go)
    {
        Collider[] colliders = go.GetComponents<Collider>();
        if (colliders.Length == 0)
            return ColliderSettings.Default;

        foreach (Collider collider in colliders)
        {
            if (collider is MeshCollider)
                return ColliderSettings.FromCollider(collider);
        }

        return ColliderSettings.FromCollider(colliders[0]);
    }

    private static int RemoveAllColliders(GameObject go)
    {
        Collider[] colliders = go.GetComponents<Collider>();
        foreach (Collider collider in colliders)
            Object.DestroyImmediate(collider);

        return colliders.Length;
    }

    private static void AddColliderFromMesh(
        Mesh mesh,
        GameObject go,
        ColliderSettings settings,
        ref ConversionStats stats)
    {
        if (TryAnalyzeCapsuleFit(mesh, go.name, out CapsuleFit capsuleFit))
        {
            CapsuleCollider capsule = go.AddComponent<CapsuleCollider>();
            capsule.center = capsuleFit.center;
            capsule.radius = capsuleFit.radius;
            capsule.height = capsuleFit.height;
            capsule.direction = capsuleFit.direction;
            ApplyColliderSettings(settings, capsule);
            stats.capsuleCount++;
            return;
        }

        Bounds bounds = mesh.bounds;
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.center = bounds.center;
        box.size = bounds.size;
        ApplyColliderSettings(settings, box);
        stats.boxCount++;
    }

    private static void ApplyColliderSettings(ColliderSettings settings, Collider target)
    {
        target.isTrigger = settings.isTrigger;
        target.enabled = settings.enabled;
        target.sharedMaterial = settings.sharedMaterial;
        target.includeLayers = settings.includeLayers;
        target.excludeLayers = settings.excludeLayers;
        target.layerOverridePriority = settings.layerOverridePriority;
    }

    private static bool TryAnalyzeCapsuleFit(Mesh mesh, string objectName, out CapsuleFit fit)
    {
        fit = default;
        if (mesh == null)
            return false;

        Bounds bounds = mesh.bounds;
        Vector3 size = bounds.size;
        if (Mathf.Max(size.x, Mathf.Max(size.y, size.z)) < MinRadius)
            return false;

        // 거의 정육면체/상자형이면 Capsule 후보에서 제외
        if (IsNearCube(size))
            return false;

        Vector3[] vertices = TryGetMeshVertices(mesh);
        bool hasVertices = vertices != null && vertices.Length >= MinVertexCount;

        // 꼭짓점을 못 읽으면 bounds만으로는 사각 상자와 원기둥을 구분할 수 없음 → Box
        // (이름에 Round/Cylinder 등이 있을 때만 예외적으로 bounds Capsule 허용)
        if (!hasVertices)
        {
            if (!NameSuggestsCylindrical(objectName))
                return false;

            return TryCapsuleFromBoundsOnly(size, bounds, out fit);
        }

        CapsuleCandidate? best = null;

        for (int axis = 0; axis < 3; axis++)
        {
            if (!PassesBoundsCylinderTest(size, axis, out float expectedRadius, out float height))
                continue;

            bool tall = IsTallCylinderBounds(size, axis);
            bool squat = IsSquatRoundBounds(size, axis);
            if (!tall && !squat)
                continue;

            if (!HasCircularAngularSpread(vertices, bounds, axis))
                continue;

            // 사각 단면은 외접원 꼭짓점 반지름 비율이 ~0.707이라 여기서 탈락해야 함
            if (!ValidateCylindricalProfile(vertices, bounds, axis, out float profileRadius))
                continue;

            if (profileRadius > expectedRadius * MaxProfileToBoundsRadiusRatio)
                continue;

            // 납작한 형태는 이름 힌트가 있을 때만 (에어콘 같은 납작 상자 오인 방지)
            if (squat && !tall && !NameSuggestsCylindrical(objectName))
                continue;

            CapsuleCandidate candidate = new CapsuleCandidate
            {
                axis = axis,
                radius = Mathf.Max(expectedRadius, profileRadius),
                height = height,
                tier = tall ? 0 : 1
            };

            if (!IsBetterCapsuleCandidate(candidate, best))
                continue;

            best = candidate;
        }

        if (!best.HasValue)
            return false;

        CapsuleCandidate chosen = best.Value;
        fit = BuildCapsuleFit(bounds, chosen.axis, chosen.radius, chosen.height);
        return true;
    }

    private static Vector3[] TryGetMeshVertices(Mesh mesh)
    {
        try
        {
            Vector3[] vertices = mesh.vertices;
            if (vertices != null && vertices.Length > 0)
                return vertices;
        }
        catch (System.Exception)
        {
            // Read/Write 비활성 등으로 실패
        }

        return null;
    }

    private static bool NameSuggestsCylindrical(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return false;

        string lower = objectName.ToLowerInvariant();
        return lower.Contains("round")
            || lower.Contains("cylinder")
            || lower.Contains("cyl_")
            || lower.Contains("pipe")
            || lower.Contains("pillar")
            || lower.Contains("column")
            || lower.Contains("barrel")
            || lower.Contains("tank")
            || lower.Contains("silo");
    }

    private static bool TryCapsuleFromBoundsOnly(Vector3 size, Bounds bounds, out CapsuleFit fit)
    {
        fit = default;
        CapsuleCandidate? best = null;

        for (int axis = 0; axis < 3; axis++)
        {
            if (!PassesBoundsCylinderTest(size, axis, out float expectedRadius, out float height))
                continue;

            if (!IsStrongCylinderBounds(size, axis))
                continue;

            CapsuleCandidate candidate = new CapsuleCandidate
            {
                axis = axis,
                radius = expectedRadius,
                height = height,
                tier = 3
            };

            if (!IsBetterCapsuleCandidate(candidate, best))
                continue;

            best = candidate;
        }

        if (!best.HasValue)
            return false;

        CapsuleCandidate chosen = best.Value;
        fit = BuildCapsuleFit(bounds, chosen.axis, chosen.radius, chosen.height);
        return true;
    }

    private static bool IsBetterCapsuleCandidate(CapsuleCandidate candidate, CapsuleCandidate? currentBest)
    {
        if (!currentBest.HasValue)
            return true;

        CapsuleCandidate best = currentBest.Value;
        if (candidate.tier != best.tier)
            return candidate.tier < best.tier;

        return candidate.height / candidate.radius > best.height / best.radius;
    }

    private static bool IsNearCube(Vector3 size)
    {
        SortedDimensions dims = SortDimensions(size);
        if (dims.dim2 < MinRadius)
            return false;

        return dims.dim0 / dims.dim2 >= NearCubeMinRatio;
    }

    private static bool PassesBoundsCylinderTest(
        Vector3 size,
        int axis,
        out float expectedRadius,
        out float height)
    {
        expectedRadius = 0f;
        height = 0f;

        int axis1 = (axis + 1) % 3;
        int axis2 = (axis + 2) % 3;
        float along = GetComponent(size, axis);
        float perp1 = GetComponent(size, axis1);
        float perp2 = GetComponent(size, axis2);
        float crossMin = Mathf.Min(perp1, perp2);
        float crossMax = Mathf.Max(perp1, perp2);

        if (along < MinRadius)
            return false;

        if (crossMin / crossMax < CrossSectionSimilarity)
            return false;

        if (along / crossMax < MinHeightToDiameterRatio)
            return false;

        expectedRadius = crossMax * 0.5f;
        height = along;
        return true;
    }

    /// <summary>
    /// 긴 기둥(높이 > 지름) 또는 납작한 원형 건물(지름 >> 높이)일 때만 true.
    /// </summary>
    private static bool IsStrongCylinderBounds(Vector3 size, int axis)
    {
        return IsTallCylinderBounds(size, axis) || IsSquatRoundBounds(size, axis);
    }

    private static bool IsTallCylinderBounds(Vector3 size, int axis)
    {
        GetAxisCross(size, axis, out float along, out float crossMax);
        return crossMax >= MinRadius && along / crossMax >= MinTallAspectRatio;
    }

    private static bool IsSquatRoundBounds(Vector3 size, int axis)
    {
        GetAxisCross(size, axis, out float along, out float crossMax);
        return along >= MinRadius && crossMax / along >= MinSquatRoundRatio;
    }

    private static void GetAxisCross(Vector3 size, int axis, out float along, out float crossMax)
    {
        int axis1 = (axis + 1) % 3;
        int axis2 = (axis + 2) % 3;
        along = GetComponent(size, axis);
        crossMax = Mathf.Max(GetComponent(size, axis1), GetComponent(size, axis2));
    }

    /// <summary>
    /// 축 주변 꼭짓점 각도가 고르게 퍼져 있으면 원형, 4모서리에만 몰리면 상자로 본다.
    /// (정사각 상자 꼭짓점은 외접원 위에 있어 반지름만으로는 원기둥과 구분이 안 됨)
    /// </summary>
    private static bool HasCircularAngularSpread(Vector3[] vertices, Bounds bounds, int axis)
    {
        Vector3 center = bounds.center;
        Vector3 axisDir = GetAxisVector(axis);
        Vector3 tangent = GetAxisVector((axis + 1) % 3);
        Vector3 bitangent = Vector3.Cross(axisDir, tangent).normalized;
        tangent = Vector3.Cross(bitangent, axisDir).normalized;

        int[] bins = new int[AngleBinCount];
        int used = 0;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 planar = Vector3.ProjectOnPlane(vertices[i] - center, axisDir);
            float radius = planar.magnitude;
            if (radius < MinRadius)
                continue;

            float x = Vector3.Dot(planar, tangent);
            float y = Vector3.Dot(planar, bitangent);
            float angle = Mathf.Atan2(y, x);
            if (angle < 0f)
                angle += Mathf.PI * 2f;

            int bin = Mathf.Clamp(Mathf.FloorToInt(angle / (Mathf.PI * 2f) * AngleBinCount), 0, AngleBinCount - 1);
            if (bins[bin] == 0)
                used++;
            bins[bin]++;
        }

        if (used == 0)
            return false;

        return (float)used / AngleBinCount >= MinAngularCoverage;
    }

    private static bool ValidateCylindricalProfile(
        Vector3[] vertices,
        Bounds bounds,
        int axis,
        out float profileRadius)
    {
        profileRadius = 0f;
        Vector3 axisDir = GetAxisVector(axis);
        Vector3 center = bounds.center;

        float maxPerp = 0f;
        var perpRadii = new List<float>(vertices.Length);

        for (int i = 0; i < vertices.Length; i++)
        {
            float perp = Vector3.ProjectOnPlane(vertices[i] - center, axisDir).magnitude;
            maxPerp = Mathf.Max(maxPerp, perp);
            perpRadii.Add(perp);
        }

        if (maxPerp < MinRadius)
            return false;

        float perpThreshold = maxPerp * MinPerpRadiusRatio;
        var validRadii = new List<float>();
        for (int i = 0; i < perpRadii.Count; i++)
        {
            if (perpRadii[i] >= perpThreshold)
                validRadii.Add(perpRadii[i]);
        }

        if (validRadii.Count < MinVertexCount)
            return false;

        float meanRadius = ComputeMean(validRadii);
        if (meanRadius < MinRadius)
            return false;

        if (ComputeStdDev(validRadii, meanRadius) / meanRadius > MaxRadiusCoefficientOfVariation)
            return false;

        if (ComputeMin(validRadii) / ComputeMax(validRadii) < MinInnerOuterRadiusRatio)
            return false;

        profileRadius = ComputeMax(validRadii);
        return true;
    }

    private static CapsuleFit BuildCapsuleFit(Bounds bounds, int axis, float radius, float longLength)
    {
        return new CapsuleFit
        {
            center = bounds.center,
            radius = radius,
            height = Mathf.Max(longLength, radius * 2f),
            direction = axis
        };
    }

    private static SortedDimensions SortDimensions(Vector3 size)
    {
        var entries = new (float length, int axis)[]
        {
            (size.x, 0),
            (size.y, 1),
            (size.z, 2)
        };

        if (entries[0].length > entries[1].length)
            (entries[0], entries[1]) = (entries[1], entries[0]);
        if (entries[1].length > entries[2].length)
            (entries[1], entries[2]) = (entries[2], entries[1]);
        if (entries[0].length > entries[1].length)
            (entries[0], entries[1]) = (entries[1], entries[0]);

        return new SortedDimensions
        {
            dim0 = entries[0].length,
            dim1 = entries[1].length,
            dim2 = entries[2].length,
            axis0 = entries[0].axis,
            axis1 = entries[1].axis,
            axis2 = entries[2].axis
        };
    }

    private static float GetComponent(Vector3 vector, int axis)
    {
        return axis switch
        {
            0 => vector.x,
            1 => vector.y,
            _ => vector.z
        };
    }

    private static Vector3 GetAxisVector(int axis)
    {
        return axis switch
        {
            0 => Vector3.right,
            1 => Vector3.up,
            _ => Vector3.forward
        };
    }

    private static float ComputeMean(List<float> values)
    {
        float sum = 0f;
        for (int i = 0; i < values.Count; i++)
            sum += values[i];
        return sum / values.Count;
    }

    private static float ComputeStdDev(List<float> values, float mean)
    {
        float sumSq = 0f;
        for (int i = 0; i < values.Count; i++)
        {
            float delta = values[i] - mean;
            sumSq += delta * delta;
        }

        return Mathf.Sqrt(sumSq / values.Count);
    }

    private static float ComputeMin(List<float> values)
    {
        float min = float.MaxValue;
        for (int i = 0; i < values.Count; i++)
            min = Mathf.Min(min, values[i]);
        return min;
    }

    private static float ComputeMax(List<float> values)
    {
        float max = 0f;
        for (int i = 0; i < values.Count; i++)
            max = Mathf.Max(max, values[i]);
        return max;
    }

    private static List<string> CollectPrefabPaths()
    {
        var paths = new HashSet<string>();

        foreach (Object obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path))
                continue;

            if (AssetDatabase.IsValidFolder(path))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { path }))
                    paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }
            else if (path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
            {
                paths.Add(path);
            }
        }

        return new List<string>(paths);
    }

    private static bool IsFolderOrPrefab(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        return AssetDatabase.IsValidFolder(path)
            || path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase);
    }

    private static string GetHierarchyPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }

        return path;
    }
}
