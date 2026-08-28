using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CharacterDataSO에 PC_Pre_* 프리뷰 프리팹을 자동 연결합니다.
/// 메뉴: Tools → Install Character Preview Prefabs
/// </summary>
public static class InstallCharacterPreviewPrefabs
{
    private const string MenuPath = "Tools/Install Character Preview Prefabs";
    private const string CharacterDataRoot = "Assets/Data/PlayerSelect";
    private const string PreviewPrefabRoot = "Assets/Arts/Characters/PC";

    [MenuItem(MenuPath)]
    public static void Install()
    {
        int linked = 0;
        var guids = AssetDatabase.FindAssets("t:CharacterDataSO", new[] { CharacterDataRoot });
        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var data = AssetDatabase.LoadAssetAtPath<CharacterDataSO>(path);
            if (data == null)
                continue;

            var preview = ResolvePreviewPrefab(data);
            if (preview == null)
            {
                Debug.LogWarning($"[InstallCharacterPreviewPrefabs] 프리뷰 프리팹을 찾지 못했습니다: {path}");
                continue;
            }

            var so = new SerializedObject(data);
            so.FindProperty("previewPrefab").objectReferenceValue = preview;

            // 기존에 modelPrefab만 PC_Pre_* 로 들어가 있었다면, preview로 옮기고 model은 비워 둡니다.
            var modelProp = so.FindProperty("modelPrefab");
            var currentModel = modelProp.objectReferenceValue as GameObject;
            if (currentModel != null && IsPreviewPrefab(currentModel) && currentModel == preview)
                modelProp.objectReferenceValue = null;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            linked++;
            Debug.Log($"[InstallCharacterPreviewPrefabs] {data.name} ← {preview.name}");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[InstallCharacterPreviewPrefabs] 완료: {linked}개 연결");
    }

    private static GameObject ResolvePreviewPrefab(CharacterDataSO data)
    {
        if (data.previewPrefab != null)
            return data.previewPrefab;

        string key = ExtractCharacterKey(data);
        if (string.IsNullOrEmpty(key))
            return null;

        string prefabPath = $"{PreviewPrefabRoot}/PC_Pre_{key}.prefab";
        if (!File.Exists(prefabPath))
            return null;

        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }

    private static string ExtractCharacterKey(CharacterDataSO data)
    {
        string assetPath = AssetDatabase.GetAssetPath(data);
        string fileName = Path.GetFileNameWithoutExtension(assetPath);
        // 001_Cool_SO → Cool
        if (fileName.EndsWith("_SO", System.StringComparison.OrdinalIgnoreCase))
            fileName = fileName.Substring(0, fileName.Length - 3);

        int underscore = fileName.IndexOf('_');
        if (underscore >= 0 && underscore < fileName.Length - 1)
            return fileName.Substring(underscore + 1);

        if (!string.IsNullOrWhiteSpace(data.displayName))
        {
            string name = data.displayName.Trim();
            if (name.StartsWith("MR.", System.StringComparison.OrdinalIgnoreCase))
                name = name.Substring(3).Trim();
            if (name.StartsWith("Mr.", System.StringComparison.OrdinalIgnoreCase))
                name = name.Substring(3).Trim();
            return name.Replace(" ", "");
        }

        return fileName;
    }

    private static bool IsPreviewPrefab(GameObject prefab)
    {
        return prefab != null && prefab.name.StartsWith("PC_Pre_", System.StringComparison.OrdinalIgnoreCase);
    }
}
