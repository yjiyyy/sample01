using UnityEngine;
using UnityEditor;
using System.IO;

public class PrefabGenerator
{
    [MenuItem("Assets/Generate Prefab from FBX", true)]
    private static bool ValidateGeneratePrefab()
    {
        foreach (var obj in Selection.objects)
        {
            if (!(obj is GameObject)) return false;
            string path = AssetDatabase.GetAssetPath(obj);
            if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    [MenuItem("Assets/Generate Prefab from FBX")]
    private static void GeneratePrefabs()
    {
        foreach (var obj in Selection.objects)
        {
            GameObject fbx = obj as GameObject;
            if (fbx == null) continue;

            string assetPath = AssetDatabase.GetAssetPath(fbx);
            string folderPath = Path.GetDirectoryName(assetPath);
            string prefabName = Path.GetFileNameWithoutExtension(assetPath);
            string savePath = Path.Combine(folderPath, prefabName + ".prefab").Replace("\\", "/");

            GameObject instance = PrefabUtility.InstantiatePrefab(fbx) as GameObject;

            GameObject root = new GameObject(prefabName);
            instance.transform.SetParent(root.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            PrefabUtility.SaveAsPrefabAssetAndConnect(root, savePath, InteractionMode.AutomatedAction);
            GameObject.DestroyImmediate(root);

            Debug.Log($"✅ 프리팹 생성 완료: {savePath}");
        }
    }
}
