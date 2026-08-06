using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// PC Weapon FBX를 선택해 우클릭하면 PC 무기 프리팹을 생성합니다.
/// 생성 루트에 WeaponBehavior, WeaponTrailController를 기본 부착합니다.
/// </summary>
public static class PCWeaponPrefabGenerator
{
    private const string MenuName = "Assets/Create PC Weapon Prefab from FBX";
    private const string TrailStartName = "TrailStart";
    private const string TrailEndName = "TrailEnd";
    private const string DefaultTrailMaterialPath = "Assets/Arts/FX/Mat_WeaponTrail_SoftRefraction.mat";

    [MenuItem(MenuName, true, 4)]
    private static bool ValidateCreate()
    {
        return HasSelectedFbx();
    }

    [MenuItem(MenuName, false, 4)]
    private static void Create()
    {
        Object[] selections = Selection.objects;
        if (selections == null || selections.Length == 0)
            return;

        int createdCount = 0;
        for (int i = 0; i < selections.Length; i++)
        {
            if (!TryGetFbxPath(selections[i], out string fbxPath))
                continue;

            GameObject fbxSource = selections[i] as GameObject;
            if (fbxSource == null)
                continue;

            if (GenerateAndSave(fbxSource, fbxPath))
                createdCount++;
        }

        if (createdCount > 0)
            AssetDatabase.Refresh();
    }

    private static bool HasSelectedFbx()
    {
        Object[] selections = Selection.objects;
        if (selections == null || selections.Length == 0)
            return false;

        for (int i = 0; i < selections.Length; i++)
        {
            if (TryGetFbxPath(selections[i], out _))
                return true;
        }

        return false;
    }

    private static bool TryGetFbxPath(Object obj, out string assetPath)
    {
        assetPath = obj != null ? AssetDatabase.GetAssetPath(obj) : null;
        return !string.IsNullOrEmpty(assetPath)
               && assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool GenerateAndSave(GameObject fbxSource, string fbxAssetPath)
    {
        string prefabName = Path.GetFileNameWithoutExtension(fbxAssetPath);
        string folderPath = Path.GetDirectoryName(fbxAssetPath)?.Replace("\\", "/");
        if (string.IsNullOrEmpty(folderPath))
        {
            Debug.LogError($"[PCWeaponPrefabGenerator] 폴더 경로를 확인할 수 없습니다: {fbxAssetPath}");
            return false;
        }

        string savePath = $"{folderPath}/{prefabName}.prefab";
        if (File.Exists(savePath))
            Debug.LogWarning($"[PCWeaponPrefabGenerator] 기존 프리팹을 덮어씁니다: {savePath}");

        GameObject fbxInstance = PrefabUtility.InstantiatePrefab(fbxSource) as GameObject;
        if (fbxInstance == null)
        {
            Debug.LogError($"[PCWeaponPrefabGenerator] FBX 인스턴스 생성 실패: {fbxAssetPath}");
            return false;
        }

        GameObject root = new GameObject(prefabName);
        try
        {
            fbxInstance.transform.SetParent(root.transform, false);
            fbxInstance.transform.localPosition = Vector3.zero;
            fbxInstance.transform.localRotation = Quaternion.identity;
            fbxInstance.transform.localScale = Vector3.one;

            Bounds localBounds = CalculateOrFallbackBounds(root.transform, fbxInstance.transform);

            AddComponentIfMissing<WeaponBehavior>(root);
            WeaponTrailController trail = AddComponentIfMissing<WeaponTrailController>(root);
            SetupTrailController(root.transform, trail, localBounds);
            CreateDieCollider(root.transform, localBounds);

            PrefabUtility.SaveAsPrefabAssetAndConnect(root, savePath, InteractionMode.AutomatedAction);
            Debug.Log($"[PCWeaponPrefabGenerator] 프리팹 생성 완료: {savePath}", AssetDatabase.LoadAssetAtPath<GameObject>(savePath));
            return true;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static T AddComponentIfMissing<T>(GameObject target) where T : Component
    {
        T existing = target.GetComponent<T>();
        if (existing != null)
            return existing;

        return target.AddComponent<T>();
    }

    private static void SetupTrailController(Transform root, WeaponTrailController trail, Bounds localBounds)
    {
        if (trail == null || root == null)
            return;

        Vector3 axis = ResolveLongestAxis(localBounds.size);
        Vector3 half = axis * GetAxisLength(localBounds.extents, axis);
        Vector3 center = localBounds.center;

        Transform trailStart = CreateOrGetChild(root, TrailStartName);
        Transform trailEnd = CreateOrGetChild(root, TrailEndName);
        trailStart.localPosition = center - half;
        trailEnd.localPosition = center + half;
        trailStart.localRotation = Quaternion.identity;
        trailEnd.localRotation = Quaternion.identity;
        trailStart.localScale = Vector3.one;
        trailEnd.localScale = Vector3.one;

        trail.trailStart = trailStart;
        trail.trailEnd = trailEnd;

        // 요청한 기본값 적용
        trail.maxPoints = 5;
        trail.minPointDistance = 0.001f;
        trail.trailLifetime = 0.1f;
        trail.smoothSegments = 7;

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(DefaultTrailMaterialPath);
        if (mat != null)
            trail.trailMaterial = mat;
    }

    private static Transform CreateOrGetChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
            return child;

        GameObject go = new GameObject(childName);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static void CreateDieCollider(Transform root, Bounds localBounds)
    {
        Transform existing = root.Find(DieColliderUtility.DieColliderObjectName);
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        var dieObject = new GameObject(DieColliderUtility.DieColliderObjectName);
        dieObject.transform.SetParent(root, false);
        dieObject.transform.localPosition = Vector3.zero;
        dieObject.transform.localRotation = Quaternion.identity;
        dieObject.transform.localScale = Vector3.one;
        dieObject.layer = DieColliderUtility.PartsLayer;

        var box = dieObject.AddComponent<BoxCollider>();
        box.isTrigger = false;
        box.center = localBounds.center;
        box.size = localBounds.size;
        box.enabled = true;

        // 평소에는 비활성, 필요 시 DieColliderUtility로 활성화
        dieObject.SetActive(false);
    }

    private static Bounds CalculateOrFallbackBounds(Transform referenceRoot, Transform meshRoot)
    {
        if (TryCalculateLocalBounds(referenceRoot, meshRoot, out Bounds localBounds))
            return localBounds;

        Debug.LogWarning("[PCWeaponPrefabGenerator] Renderer를 찾지 못해 기본 Bounds(0.2m)를 사용합니다.");
        return new Bounds(Vector3.zero, Vector3.one * 0.2f);
    }

    private static bool TryCalculateLocalBounds(Transform reference, Transform meshRoot, out Bounds localBounds)
    {
        localBounds = default;
        bool hasBounds = false;

        Renderer[] renderers = meshRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Bounds rendererLocal = TransformWorldBoundsToLocal(reference, renderer.bounds);
            if (!hasBounds)
            {
                localBounds = rendererLocal;
                hasBounds = true;
            }
            else
            {
                localBounds.Encapsulate(rendererLocal.min);
                localBounds.Encapsulate(rendererLocal.max);
            }
        }

        return hasBounds;
    }

    private static Bounds TransformWorldBoundsToLocal(Transform localSpace, Bounds worldBounds)
    {
        Vector3 center = worldBounds.center;
        Vector3 extents = worldBounds.extents;

        Bounds local = new Bounds(localSpace.InverseTransformPoint(center), Vector3.zero);
        for (int xi = -1; xi <= 1; xi += 2)
        {
            for (int yi = -1; yi <= 1; yi += 2)
            {
                for (int zi = -1; zi <= 1; zi += 2)
                {
                    Vector3 corner = center + Vector3.Scale(extents, new Vector3(xi, yi, zi));
                    local.Encapsulate(localSpace.InverseTransformPoint(corner));
                }
            }
        }

        return local;
    }

    private static Vector3 ResolveLongestAxis(Vector3 size)
    {
        if (size.x >= size.y && size.x >= size.z)
            return Vector3.right;
        if (size.y >= size.x && size.y >= size.z)
            return Vector3.up;
        return Vector3.forward;
    }

    private static float GetAxisLength(Vector3 vector, Vector3 axis)
    {
        if (axis == Vector3.right)
            return vector.x;
        if (axis == Vector3.up)
            return vector.y;
        return vector.z;
    }
}
