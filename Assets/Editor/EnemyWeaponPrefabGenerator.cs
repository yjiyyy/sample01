using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Enemy Weapon FBX → 프리팹 생성.
/// FBX를 자식으로 두고, 메쉬 Bounds에 맞춘 DieCollider(비활성) + Parts 레이어 + Rigidbody를 설정합니다.
/// </summary>
public static class EnemyWeaponPrefabGenerator
{
    private const string MenuName = "Assets/Create Enemy Weapon Prefab from FBX";

    [MenuItem(MenuName, true, 5)]
    private static bool ValidateSingle()
    {
        return TryGetFbxPath(Selection.activeObject, out _);
    }

    [MenuItem(MenuName, false, 5)]
    private static void CreateSingle()
    {
        if (!TryGetFbxPath(Selection.activeObject, out string path))
            return;

        GenerateAndSave(Selection.activeObject as GameObject, path);
    }

    private static bool TryGetFbxPath(Object obj, out string assetPath)
    {
        assetPath = obj != null ? AssetDatabase.GetAssetPath(obj) : null;
        return !string.IsNullOrEmpty(assetPath)
               && assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);
    }

    private static void GenerateAndSave(GameObject fbxSource, string fbxAssetPath)
    {
        if (fbxSource == null)
        {
            Debug.LogError("[EnemyWeaponPrefabGenerator] FBX 에셋이 없습니다.");
            return;
        }

        string prefabName = Path.GetFileNameWithoutExtension(fbxAssetPath);
        string savePath = ResolvePrefabSavePath(fbxAssetPath, prefabName);

        if (File.Exists(savePath))
            Debug.LogWarning($"[EnemyWeaponPrefabGenerator] 기존 프리팹을 덮어씁니다: {savePath}");

        GameObject fbxInstance = PrefabUtility.InstantiatePrefab(fbxSource) as GameObject;
        if (fbxInstance == null)
        {
            Debug.LogError($"[EnemyWeaponPrefabGenerator] FBX 인스턴스 생성 실패: {fbxAssetPath}");
            return;
        }

        GameObject root = new GameObject(prefabName);
        try
        {
            fbxInstance.transform.SetParent(root.transform, false);
            fbxInstance.transform.localPosition = Vector3.zero;
            fbxInstance.transform.localRotation = Quaternion.identity;
            fbxInstance.transform.localScale = Vector3.one;

            ApplyPartsLayer(root.transform);
            SetupRigidbody(root);
            CreateDieCollider(root.transform, fbxInstance.transform);

            PrefabUtility.SaveAsPrefabAssetAndConnect(root, savePath, InteractionMode.AutomatedAction);
            Debug.Log($"[EnemyWeaponPrefabGenerator] 프리팹 생성 완료: {savePath}", AssetDatabase.LoadAssetAtPath<GameObject>(savePath));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static string ResolvePrefabSavePath(string fbxAssetPath, string prefabName)
    {
        string folder = Path.GetDirectoryName(fbxAssetPath).Replace("\\", "/");
        if (folder.EndsWith("/FBX", System.StringComparison.OrdinalIgnoreCase))
            folder = Path.GetDirectoryName(folder).Replace("\\", "/");

        return $"{folder}/{prefabName}.prefab";
    }

    private static void ApplyPartsLayer(Transform root)
    {
        int partsLayer = DieColliderUtility.PartsLayer;
        DieColliderUtility.SetLayerRecursively(root, partsLayer);
    }

    private static void SetupRigidbody(GameObject root)
    {
        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.05f;
        rb.useGravity = false;
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.None;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
    }

    private static void CreateDieCollider(Transform root, Transform meshRoot)
    {
        if (!TryCalculateLocalBounds(root, meshRoot, out Bounds localBounds))
        {
            Debug.LogWarning("[EnemyWeaponPrefabGenerator] Renderer를 찾지 못해 DieCollider 기본 크기(0.1)를 사용합니다.");
            localBounds = new Bounds(Vector3.zero, Vector3.one * 0.1f);
        }

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

        // 죽음 연출 전까지 DieCollider 오브젝트 비활성 (기존 E_Weapon_1HD_Board와 동일)
        dieObject.SetActive(false);
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
