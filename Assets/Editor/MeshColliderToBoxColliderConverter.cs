using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 선택한 프리팹 또는 폴더 안의 프리팹에서 MeshCollider를 제거하고,
/// 메쉬 bounds에 맞춘 BoxCollider로 교체합니다.
/// 사용: Project 창에서 프리팹/폴더 선택 → 우클릭 → MeshCollider → BoxCollider 변환
/// </summary>
public static class MeshColliderToBoxColliderConverter
{
    private const string MenuPath = "Assets/MeshCollider → BoxCollider 변환";

    [MenuItem(MenuPath, false, 2001)]
    public static void Convert()
    {
        List<string> prefabPaths = CollectPrefabPaths();
        if (prefabPaths.Count == 0)
        {
            Debug.LogWarning("[Mesh→Box] 변환할 프리팹이 없습니다.");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "MeshCollider → BoxCollider",
                $"{prefabPaths.Count}개 프리팹을 처리합니다.\n" +
                "각 MeshCollider는 메쉬 bounds에 맞는 BoxCollider로 교체됩니다.\n\n계속할까요?",
                "변환",
                "취소"))
        {
            return;
        }

        int processedPrefabs = 0;
        int convertedColliders = 0;
        int skippedColliders = 0;

        try
        {
            for (int i = 0; i < prefabPaths.Count; i++)
            {
                string path = prefabPaths[i];
                EditorUtility.DisplayProgressBar(
                    "MeshCollider → BoxCollider",
                    path,
                    (float)i / prefabPaths.Count);

                int changedInPrefab = ConvertPrefab(path, ref skippedColliders);
                if (changedInPrefab > 0)
                {
                    processedPrefabs++;
                    convertedColliders += changedInPrefab;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[Mesh→Box] 완료 — 프리팹 {processedPrefabs}개 수정, " +
            $"BoxCollider {convertedColliders}개 생성, " +
            $"스킵 {skippedColliders}개 (메쉬 없음).");
    }

    [MenuItem(MenuPath, true)]
    public static bool ValidateConvert()
    {
        foreach (Object obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (IsFolderOrPrefab(path))
                return true;
        }

        return false;
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

    private static int ConvertPrefab(string prefabPath, ref int skippedColliders)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null)
            return 0;

        int converted = 0;
        MeshCollider[] meshColliders = root.GetComponentsInChildren<MeshCollider>(true);

        foreach (MeshCollider meshCollider in meshColliders)
        {
            if (TryReplaceWithBoxCollider(meshCollider))
                converted++;
            else
                skippedColliders++;
        }

        if (converted > 0)
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

        PrefabUtility.UnloadPrefabContents(root);
        return converted;
    }

    private static bool TryReplaceWithBoxCollider(MeshCollider meshCollider)
    {
        Mesh mesh = meshCollider.sharedMesh;
        if (mesh == null)
        {
            Debug.LogWarning(
                $"[Mesh→Box] 메쉬가 없어 스킵: {GetHierarchyPath(meshCollider.transform)}",
                meshCollider);
            return false;
        }

        GameObject go = meshCollider.gameObject;
        Bounds bounds = mesh.bounds;

        BoxCollider box = go.AddComponent<BoxCollider>();
        box.center = bounds.center;
        box.size = bounds.size;
        box.isTrigger = meshCollider.isTrigger;
        box.enabled = meshCollider.enabled;
        box.sharedMaterial = meshCollider.sharedMaterial;
        box.includeLayers = meshCollider.includeLayers;
        box.excludeLayers = meshCollider.excludeLayers;
        box.layerOverridePriority = meshCollider.layerOverridePriority;

        Object.DestroyImmediate(meshCollider);
        return true;
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
