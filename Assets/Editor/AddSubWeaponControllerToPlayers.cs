using UnityEngine;
using UnityEditor;

/// <summary>
/// 플레이어 프리팹에 SubWeaponController 컴포넌트를 추가합니다.
/// 메뉴: Tools > Add SubWeaponController to Player Prefabs
/// </summary>
public static class AddSubWeaponControllerToPlayers
{
    private const string MenuPath = "Tools/Add SubWeaponController to Player Prefabs";

    [MenuItem(MenuPath)]
    public static void Execute()
    {
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Arts/Player" });
        int added = 0;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            var go = PrefabUtility.LoadPrefabContents(path);
            var root = go.transform;

            if (root.GetComponent<SubWeaponController>() == null)
            {
                root.gameObject.AddComponent<SubWeaponController>();
                PrefabUtility.SaveAsPrefabAsset(go, path);
                added++;
            }

            PrefabUtility.UnloadPrefabContents(go);
        }

        AssetDatabase.Refresh();
        Debug.Log($"[AddSubWeaponController] 플레이어 프리팹 {added}개에 SubWeaponController 추가 완료.");
    }
}
