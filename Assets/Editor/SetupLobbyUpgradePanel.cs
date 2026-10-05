using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 로비 업그레이드 그림·목록·NPC 액자를 씬에 넣습니다.
/// 메뉴: Tools → Setup Lobby Upgrade Panel
/// </summary>
public static class SetupLobbyUpgradePanel
{
    private const string MenuPath = "Tools/Setup Lobby Upgrade Panel";
    private const string ScenePath = "Assets/Scenes/03_Lobby.unity";
    public const string CatalogPath = "Assets/Data/Lobby/Upgrade/LobbyStatUpgradeCatalog.asset";
    private const string SpriteFolder = "Assets/Resources/UI/LobbyUpgrade";

    [MenuItem(MenuPath)]
    public static void Setup()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[SetupLobbyUpgradePanel] Play를 종료한 뒤 편집 미리보기를 적용하세요.");
            return;
        }
        CreateOrUpdateCatalog();
        AssetDatabase.SaveAssets();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            Debug.LogWarning("[SetupLobbyUpgradePanel] 03_Lobby 씬을 연 뒤 다시 실행하세요. 현재 씬은 바꾸지 않았습니다.");
            return;
        }
        var canvasGo = GameObject.Find("LobbyCanvas");
        if (canvasGo == null)
        {
            Debug.LogError("[SetupLobbyUpgradePanel] LobbyCanvas를 찾지 못했습니다.");
            return;
        }

        var panel = LobbyUpgradePanelBuilder.Build(canvasGo.GetComponent<RectTransform>());
        if (panel == null)
        {
            Debug.LogError("[SetupLobbyUpgradePanel] 업그레이드 패널을 만들지 못했습니다.");
            return;
        }

        panel.Show();
        var catalog = AssetDatabase.LoadAssetAtPath<LobbyStatUpgradeCatalogSO>(CatalogPath);
        WireMenu(canvasGo, panel, catalog);

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = panel.gameObject;
        Debug.Log("[SetupLobbyUpgradePanel] 미리보기를 적용했습니다. 확인 후 씬을 저장하세요. 플레이 시에도 자동으로 최신 화면을 만듭니다.");
    }

    public static void ImportSprites()
    {
        LobbyUpgradeSpriteMaker.GenerateUiPieces();

        if (!Directory.Exists(SpriteFolder))
            return;

        foreach (var file in Directory.GetFiles(SpriteFolder, "*.png"))
        {
            string assetPath = file.Replace("\\", "/");
            int assetsIndex = assetPath.IndexOf("Assets/");
            if (assetsIndex >= 0)
                assetPath = assetPath.Substring(assetsIndex);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }
    }

    public static LobbyStatUpgradeCatalogSO CreateOrUpdateCatalog()
    {
        const string oldPath = "Assets/Resources/UI/LobbyStatUpgradeCatalog.asset";
        if (!AssetDatabase.IsValidFolder("Assets/Data/Lobby")) AssetDatabase.CreateFolder("Assets/Data", "Lobby");
        if (AssetDatabase.LoadAssetAtPath<LobbyStatUpgradeCatalogSO>(CatalogPath) == null &&
            AssetDatabase.LoadAssetAtPath<LobbyStatUpgradeCatalogSO>(oldPath) != null)
        {
            string error = AssetDatabase.MoveAsset(oldPath, CatalogPath);
            if (!string.IsNullOrEmpty(error)) throw new System.InvalidOperationException(error);
        }
        var catalog = AssetDatabase.LoadAssetAtPath<LobbyStatUpgradeCatalogSO>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<LobbyStatUpgradeCatalogSO>();
            catalog.stats = LobbyStatUpgradeCatalogSO.CreateDefaultStats();
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath) ?? "Assets/Resources/UI");
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        catalog.EnsureLabels();
        if (catalog.npcPortrait == null) catalog.npcPortrait = LoadSprite("Npc_BearCoach");
        AssignIcon(catalog, LobbyStatId.HP, "Icon_HP");
        AssignIcon(catalog, LobbyStatId.STA, "Icon_STA");
        AssignIcon(catalog, LobbyStatId.SPD, "Icon_SPD");
        AssignIcon(catalog, LobbyStatId.STR, "Icon_STR");

        const string settingsPath = "Assets/Resources/UI/LobbyUpgradeSettings.asset";
        var settings = AssetDatabase.LoadAssetAtPath<LobbyUpgradeSettingsSO>(settingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<LobbyUpgradeSettingsSO>();
            AssetDatabase.CreateAsset(settings, settingsPath);
        }
        settings.catalog = catalog;
        EditorUtility.SetDirty(settings);
        // 최초 한 번만 캐릭터별 설정을 만들고, 이후의 편집값은 덮어쓰지 않습니다.
        if (catalog.characters == null || catalog.characters.Length == 0)
        {
            var profiles = new System.Collections.Generic.List<LobbyCharacterUpgradeSO>();
            var guids = AssetDatabase.FindAssets("t:CharacterDataSO", new[] { "Assets/Data/PlayerSelect" });
            System.Array.Sort(guids, (a, b) => string.CompareOrdinal(AssetDatabase.GUIDToAssetPath(a), AssetDatabase.GUIDToAssetPath(b)));
            foreach (string guid in guids)
            {
                var character = AssetDatabase.LoadAssetAtPath<CharacterDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (character.playerConfig == null) continue;
                string path = "Assets/Data/Lobby/Upgrade/Upgrade_" + character.name + ".asset";
                var profile = AssetDatabase.LoadAssetAtPath<LobbyCharacterUpgradeSO>(path);
                if (profile == null)
                {
                    profile = ScriptableObject.CreateInstance<LobbyCharacterUpgradeSO>();
                    profile.character = character;
                    profile.stats = LobbyStatUpgradeCatalogSO.CreateDefaultStats();
                    foreach (var entry in profile.stats)
                    {
                        var source = catalog.GetEntry(entry.id);
                        if (source != null)
                        {
                            entry.icon = source.icon; entry.displayName = source.displayName; entry.selectLine = source.selectLine;
                            entry.baseCost = source.baseCost; entry.costPerLevel = source.costPerLevel;
                            entry.maxLevel = source.maxLevel;
                            entry.perLevel = entry.id == LobbyStatId.STA ? 1f : source.perLevel;
                        }
                        entry.fallbackBaseValue = LobbyStatUpgradeApplier.GetConfigBase(entry, character.playerConfig);
                        entry.maxValue = entry.fallbackBaseValue + entry.perLevel * entry.maxLevel;
                    }
                    AssetDatabase.CreateAsset(profile, path);
                    var serialized = new SerializedObject(profile);
                    serialized.FindProperty("characterId").stringValue = guid;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(profile);
                }
                profiles.Add(profile);
            }
            catalog.characters = profiles.ToArray();
        }
        EditorUtility.SetDirty(catalog);
        return catalog;
    }

    private static void AssignIcon(LobbyStatUpgradeCatalogSO catalog, LobbyStatId id, string file)
    {
        var entry = catalog.GetEntry(id);
        if (entry != null && entry.icon == null)
            entry.icon = LoadSprite(file);
    }

    private static Sprite LoadSprite(string fileName)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "/" + fileName + ".png");
    }

    private static void WireMenu(GameObject canvasGo, LobbyUpgradePanel panel, LobbyStatUpgradeCatalogSO catalog)
    {
        var menuUi = canvasGo.GetComponentInChildren<LobbyMenuUI>(true);
        if (menuUi == null)
            return;

        var so = new SerializedObject(menuUi);
        var prop = so.FindProperty("upgradePanel");
        if (prop != null)
            prop.objectReferenceValue = panel;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(menuUi);

        var panelSo = new SerializedObject(panel);
        var catalogProp = panelSo.FindProperty("catalog");
        if (catalogProp != null)
            catalogProp.objectReferenceValue = catalog;
        panelSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(panel);
    }
}
