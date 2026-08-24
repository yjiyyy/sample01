using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 업그레이드 폴더의 SO를 상점 목록에 등록합니다. 이미 있는 항목의 가격은 건드리지 않습니다.
/// 메뉴: Tools → UI → Refresh InGame Shop Catalog
/// </summary>
public static class SetupInGameShopCatalog
{
    private const string MenuPath = "Tools/UI/Refresh InGame Shop Catalog";
    private const string AutoRunFlagPath = "Assets/Editor/SetupInGameShopCatalog.run";
    private const string CatalogPath = "Assets/Data/Shop/InGameShopCatalog.asset";
    private const string UpgradeFolder = "Assets/Data/WeaponSO/Player/Upgrade";
    private const int DefaultPrice = 10;

    [InitializeOnLoadMethod]
    private static void AutoRunIfFlagExists()
    {
        EditorApplication.delayCall += TryAutoRun;
    }

    private static void TryAutoRun()
    {
        if (!File.Exists(AutoRunFlagPath))
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryAutoRun;
            return;
        }

        try
        {
            File.Delete(AutoRunFlagPath);
        }
        catch
        {
            /* ignore */
        }

        RefreshCatalog();
    }

    [MenuItem(MenuPath, false, 21)]
    public static void RefreshCatalog()
    {
        EnsureFolder("Assets/Data", "Shop");

        var catalog = AssetDatabase.LoadAssetAtPath<InGameShopCatalogSO>(CatalogPath);
        bool created = false;
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<InGameShopCatalogSO>();
            catalog.rerollCurrency = ShopCurrency.Money;
            catalog.rerollCost = 5;
            catalog.rerollCostIncrease = 0;
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            created = true;
        }

        var so = new SerializedObject(catalog);
        var offers = so.FindProperty("offers");
        if (offers == null)
        {
            Debug.LogError("[SetupInGameShopCatalog] offers 필드를 찾지 못했습니다.");
            return;
        }

        var existing = new System.Collections.Generic.HashSet<UpgradeEffectSO>();
        for (int i = 0; i < offers.arraySize; i++)
        {
            var upgrade = offers.GetArrayElementAtIndex(i).FindPropertyRelative("upgrade");
            var value = upgrade != null ? upgrade.objectReferenceValue as UpgradeEffectSO : null;
            if (value != null)
                existing.Add(value);
        }

        string[] guids = AssetDatabase.FindAssets("t:UpgradeEffectSO", new[] { UpgradeFolder });
        System.Array.Sort(guids, (a, b) =>
            string.CompareOrdinal(AssetDatabase.GUIDToAssetPath(a), AssetDatabase.GUIDToAssetPath(b)));

        int added = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var upgrade = AssetDatabase.LoadAssetAtPath<UpgradeEffectSO>(path);
            if (upgrade == null || existing.Contains(upgrade))
                continue;

            int index = offers.arraySize;
            offers.arraySize++;
            var entry = offers.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("upgrade").objectReferenceValue = upgrade;
            entry.FindPropertyRelative("currency").enumValueIndex = (int)ShopCurrency.Money;
            entry.FindPropertyRelative("price").intValue = DefaultPrice;
            existing.Add(upgrade);
            added++;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = catalog;
        EditorGUIUtility.PingObject(catalog);

        string action = created ? "생성" : "갱신";
        Debug.Log($"[SetupInGameShopCatalog] {action} 완료: {CatalogPath} (추가 {added}개, 전체 {offers.arraySize}개)");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
