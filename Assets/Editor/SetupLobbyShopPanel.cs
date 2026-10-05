using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SetupLobbyShopPanel
{
    public const string CatalogPath="Assets/Data/Lobby/Shop/LobbyShopCatalog.asset";
    [MenuItem("Tools/Setup Lobby Shop Panel")]
    public static void Setup()
    {
        if (Application.isPlaying) return;
        CreateOrUpdateCatalog(); AssetDatabase.SaveAssets();
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/03_Lobby.unity") { Debug.LogWarning("03_Lobby 씬에서 실행하세요."); return; }
        var canvas=GameObject.Find("LobbyCanvas"); if(canvas==null)return;
        var panel=LobbyShopPanelBuilder.Build(canvas.GetComponent<RectTransform>());
        panel.Show(); EditorSceneManager.MarkSceneDirty(scene); Selection.activeGameObject=panel.gameObject;
        Debug.Log("상점 미리보기를 적용했습니다. 씬은 자동 저장하지 않습니다.");
    }
    public static void RemoveOldPopupShop(Transform canvas) { }
    public static LobbyShopCatalogSO CreateOrUpdateCatalog()
    {
        Directory.CreateDirectory("Assets/Data/Lobby/Shop"); AssetDatabase.Refresh();
        var catalog=AssetDatabase.LoadAssetAtPath<LobbyShopCatalogSO>(CatalogPath);
        const string old="Assets/Resources/UI/LobbyShopCatalog.asset";
        if(catalog==null && AssetDatabase.LoadAssetAtPath<LobbyShopCatalogSO>(old)!=null)
        {
            var error=AssetDatabase.MoveAsset(old,CatalogPath); if(!string.IsNullOrEmpty(error))throw new Exception(error);
            catalog=AssetDatabase.LoadAssetAtPath<LobbyShopCatalogSO>(CatalogPath);
        }
        if(catalog==null) { catalog=ScriptableObject.CreateInstance<LobbyShopCatalogSO>(); AssetDatabase.CreateAsset(catalog,CatalogPath); }
        if(catalog.categoryAssets==null || catalog.categoryAssets.Length==0)
        {
            var weapon=Category("Weapons", "무기", "Weapons");
            var other=Category("Other", "기타", "Other");
            var paths=AssetDatabase.FindAssets("t:WeaponDataSO",new[]{"Assets/Data/WeaponSO/Player"}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(x=>x).ToArray();
            var items=paths.Select(AssetDatabase.LoadAssetAtPath<WeaponDataSO>).Where(w=>w!=null && !PlayerConfig.IsUnarmedAsset(w) && w.weaponPrefab!=null).ToArray();
            string[] names={"야구방망이","권총","샷건","소총","나이프","쌍나이프","전투도끼","카타나","복싱 글러브","쌍권총","저격총"};
            weapon.category.items=items.Select((w,i)=>new LobbyShopItem { weapon=w, price=500+i*100,
                displayName=new LocalizedString { korean=i<names.Length?names[i]:w.weaponName, english=w.weaponName } }).ToArray();
            catalog.categoryAssets=new[]{weapon,other};
            catalog.categories=Array.Empty<LobbyShopCategory>();
            EditorUtility.SetDirty(weapon); EditorUtility.SetDirty(other);
        }
        if(catalog.npcPortrait==null)catalog.npcPortrait=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/LobbyUpgrade/Npc_BearCoach.png");
        if(catalog.coinIcon==null)catalog.coinIcon=LobbyPanelChrome.ResolveCoinSprite();
        if(catalog.gemIcon==null)catalog.gemIcon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Arts/UI/Icon/UI/Icon_Gem.png");
        const string settingsPath="Assets/Resources/UI/LobbyShopSettings.asset";
        var settings=AssetDatabase.LoadAssetAtPath<LobbyShopSettingsSO>(settingsPath);
        if(settings==null) { settings=ScriptableObject.CreateInstance<LobbyShopSettingsSO>(); AssetDatabase.CreateAsset(settings,settingsPath); }
        settings.catalog=catalog; EditorUtility.SetDirty(settings); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); return catalog;
    }
    private static LobbyShopCategorySO Category(string name,string ko,string en)
    {
        string path="Assets/Data/Lobby/Shop/Category_"+name+".asset";
        var asset=AssetDatabase.LoadAssetAtPath<LobbyShopCategorySO>(path);
        if(asset==null) { asset=ScriptableObject.CreateInstance<LobbyShopCategorySO>(); asset.category.displayName=new LocalizedString{korean=ko,english=en}; AssetDatabase.CreateAsset(asset,path); }
        return asset;
    }
}
