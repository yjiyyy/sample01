#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class WeaponRegistryBuilder
{
    private const string ScanFolder = "Assets/Arts/Weapon";
    private const string ResourcesAssetPath = "Assets/Resources/Dev/WeaponRegistry.asset";

    [MenuItem("Dev/Build Weapon Registry")]
    public static void BuildRegistry()
    {
        // 폴더 보장
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/Dev");

        var list = new List<WeaponRegistrySO.WeaponEntry>();

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { ScanFolder });
        int total = 0, used = 0, skipped = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            total++;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { skipped++; continue; }

            // WeaponBehavior가 포함된 프리팹만 등록
            if (prefab.GetComponentInChildren<WeaponBehavior>(true) == null)
            {
                skipped++;
                continue;
            }

            string name = Path.GetFileNameWithoutExtension(path);

            list.Add(new WeaponRegistrySO.WeaponEntry
            {
                id = name,
                displayName = name,
                prefab = prefab
            });
            used++;
        }

        // 정렬
        list.Sort((a, b) =>
        {
            string sa = a != null ? a.displayName : "";
            string sb = b != null ? b.displayName : "";
            return string.Compare(sa, sb, System.StringComparison.OrdinalIgnoreCase);
        });

        // 기존 자산 로드 or 생성
        var asset = AssetDatabase.LoadAssetAtPath<WeaponRegistrySO>(ResourcesAssetPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<WeaponRegistrySO>();
            AssetDatabase.CreateAsset(asset, ResourcesAssetPath);
        }

        asset.entries = list;
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[WeaponRegistryBuilder] 빌드 완료: 총 {total}개 스캔, 사용 {used}, 스킵 {skipped}\n→ {ResourcesAssetPath}");
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }

    [MenuItem("Dev/Open Weapon Registry")]
    public static void OpenRegistry()
    {
        var asset = AssetDatabase.LoadAssetAtPath<WeaponRegistrySO>(ResourcesAssetPath);
        if (asset == null)
        {
            Debug.LogWarning("[WeaponRegistryBuilder] 레지스트리가 없습니다. 먼저 Dev/Build Weapon Registry를 실행하세요.");
            return;
        }
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }

    private static void EnsureFolder(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            string parent = Path.GetDirectoryName(folder).Replace("\\", "/");
            string leaf = Path.GetFileName(folder);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif