using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// PC 총기 FBX → 프리팹 생성.
/// WeaponBehavior + Fire_Point(X=90) + DieCollider. 트레일 관련은 넣지 않습니다.
/// </summary>
public static class PCWeaponGunPrefabGenerator
{
    private const string MenuName = "Assets/Create PC Gun Prefab from FBX";
    private const string FirePointName = "Fire_Point";

    [MenuItem(MenuName, true, 5)]
    private static bool ValidateCreate()
    {
        return HasSelectedFbx();
    }

    [MenuItem(MenuName, false, 5)]
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
            Debug.LogError($"[PCWeaponGunPrefabGenerator] 폴더 경로를 확인할 수 없습니다: {fbxAssetPath}");
            return false;
        }

        string savePath = $"{folderPath}/{prefabName}.prefab";
        if (File.Exists(savePath))
            Debug.LogWarning($"[PCWeaponGunPrefabGenerator] 기존 프리팹을 덮어씁니다: {savePath}");

        GameObject fbxInstance = PrefabUtility.InstantiatePrefab(fbxSource) as GameObject;
        if (fbxInstance == null)
        {
            Debug.LogError($"[PCWeaponGunPrefabGenerator] FBX 인스턴스 생성 실패: {fbxAssetPath}");
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
            CreateFirePoint(root.transform, localBounds);
            CreateDieCollider(root.transform, localBounds);

            PrefabUtility.SaveAsPrefabAssetAndConnect(root, savePath, InteractionMode.AutomatedAction);
            Debug.Log(
                $"[PCWeaponGunPrefabGenerator] 총기 프리팹 생성 완료: {savePath}",
                AssetDatabase.LoadAssetAtPath<GameObject>(savePath));
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

    /// <summary>
    /// Fire_Point 생성. 위치는 바운즈 앞쪽 임시값, Rotation X=90 고정.
    /// </summary>
    private static void CreateFirePoint(Transform root, Bounds localBounds)
    {
        Transform firePoint = root.Find(FirePointName);
        if (firePoint == null)
        {
            var go = new GameObject(FirePointName);
            go.transform.SetParent(root, false);
            firePoint = go.transform;
        }

        // 총구 근처로 보이게 바운즈 앞쪽(+Z)에 임시 배치. 이후 수동 조정.
        Vector3 pos = localBounds.center;
        pos.z = localBounds.max.z;
        firePoint.localPosition = pos;
        firePoint.localEulerAngles = new Vector3(90f, 0f, 0f);
        firePoint.localScale = Vector3.one;
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
        dieObject.SetActive(false);
    }

    private static Bounds CalculateOrFallbackBounds(Transform referenceRoot, Transform meshRoot)
    {
        if (TryCalculateLocalBounds(referenceRoot, meshRoot, out Bounds localBounds))
            return localBounds;

        Debug.LogWarning("[PCWeaponGunPrefabGenerator] Renderer를 찾지 못해 기본 Bounds(0.2m)를 사용합니다.");
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
}
