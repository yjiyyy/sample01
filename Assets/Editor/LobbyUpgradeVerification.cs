using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>배치 실행용 회귀 검사. 씬을 저장하지 않고 임시 캐릭터의 기록만 사용합니다.</summary>
public static class LobbyUpgradeVerification
{
    private const string Running = "LobbyUpgradeVerification.Running";
    private const string ExitCode = "LobbyUpgradeVerification.ExitCode";
    private static double runAt;
    private static int assertions;

    public static void Run()
    {
        try
        {
            var catalog = SetupLobbyUpgradePanel.CreateOrUpdateCatalog();
            AssetDatabase.SaveAssets();
            Check(catalog.characters.Length == 5, "Five character profiles");
            Check(LobbyStatUpgradeCatalogSO.Load() == catalog, "Runtime settings reference");
            foreach (var profile in catalog.characters)
            {
                Check(profile.Config != null && !string.IsNullOrEmpty(profile.CharacterId), "Profile binding: " + profile.name);
                foreach (var entry in profile.stats)
                {
                    float end = LobbyStatUpgradeApplier.GetCurrentValue(entry, profile.Config, entry.maxLevel);
                    Near(end, entry.maxValue, "Full bar at limit: " + profile.name + "/" + entry.id);
                }
            }
            TestMath();
            Directory.CreateDirectory("Logs/LobbyUpgrade");
            File.WriteAllText("Logs/LobbyUpgrade/edit-checks.txt", assertions + " edit checks passed");
            EditorSceneManager.OpenScene("Assets/Scenes/03_Lobby.unity", OpenSceneMode.Single);
            SetupLobbyUpgradePanel.Setup();
            var editPanel = Object.FindFirstObjectByType<LobbyUpgradePanel>();
            Check(editPanel != null && editPanel.GetComponentsInChildren<LobbyUpgradeRowUI>().Length == 4, "Edit-mode preview");
            Check(!FindActive<Button>(editPanel, "UpgradeButton").interactable, "No purchases in edit mode");
            EditorSceneManager.OpenScene("Assets/Scenes/03_Lobby.unity", OpenSceneMode.Single);
            SessionState.SetBool(Running, true);
            SessionState.SetInt(ExitCode, -1);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }

    [InitializeOnLoadMethod]
    private static void Subscribe()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (!SessionState.GetBool(Running, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                runAt = EditorApplication.timeSinceStartup + 2;
                EditorApplication.update += WaitForLobby;
            }
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetInt(ExitCode, -1) >= 0)
            {
                int code = SessionState.GetInt(ExitCode, 1);
                SessionState.SetBool(Running, false);
                EditorApplication.Exit(code);
            }
        };
    }

    private static void WaitForLobby()
    {
        if (EditorApplication.timeSinceStartup < runAt) return;
        EditorApplication.update -= WaitForLobby;
        int code = 0;
        try { TestPlay(); }
        catch (Exception ex) { code = 1; Debug.LogException(ex); }
        SessionState.SetInt(ExitCode, code);
        EditorApplication.ExitPlaymode();
    }

    private static void TestMath()
    {
        var config = ScriptableObject.CreateInstance<PlayerConfig>();
        config.maxHealth = 100; config.baseMoveSpeed = 5.5f;
        var entry = new LobbyStatUpgradeEntry { id = LobbyStatId.HP, perLevel = 10, maxLevel = 10, maxValue = 150, baseCost = 50, costPerLevel = 50 };
        Check(LobbyStatUpgradeApplier.GetEffectiveMaxLevel(entry, config) == 5, "Stat cap restricts purchase count");
        Near(LobbyStatUpgradeApplier.GetCurrentValue(entry, config, 999), 150, "Old saves clamped without rewriting");
        Check(!LobbyStatUpgradeApplier.CanUpgrade(entry, config, 5), "No purchase beyond cap");
        entry.maxValue = 145;
        Near(LobbyStatUpgradeApplier.GetCurrentValue(entry, config, 5), 145, "Partial last increment");
        entry.maxValue = 90;
        Near(LobbyStatUpgradeApplier.GetCurrentValue(entry, config, 5), 100, "Invalid cap cannot lower base stat");
        entry.maxValue = 300; entry.maxLevel = 3;
        Near(LobbyStatUpgradeApplier.GetCurrentValue(entry, config, 10), 130, "Purchase-count cap");
        entry.id = LobbyStatId.SPD; entry.maxLevel = 10; entry.perLevel = 0.4f; entry.maxValue = 9.5f;
        Check(LobbyStatUpgradeApplier.GetEffectiveMaxLevel(entry, config) == 10, "Fractional speed has no phantom level");
        entry.perLevel = 0;
        Check(!LobbyStatUpgradeApplier.CanUpgrade(entry, config, 0), "No zero-effect purchase");
        entry.baseCost = int.MaxValue; entry.costPerLevel = int.MaxValue;
        Check(LobbyStatUpgradeApplier.GetNextCost(entry, int.MaxValue) == int.MaxValue, "Cost overflow cannot create free purchase");
        Object.DestroyImmediate(config);
    }

    private static void TestPlay()
    {
        var catalog = LobbyStatUpgradeCatalogSO.Load();
        var originalProfiles = catalog.characters;
        var originalCharacter = GameState.Instance.SelectedCharacter;
        var source = catalog.GetCharacter(originalCharacter);
        var first = MakeTestProfile(source, "a");
        var second = MakeTestProfile(originalProfiles[2] != source ? originalProfiles[2] : originalProfiles[0], "b");
        var originalWallet = PlayerResources.Instance;
        var walletGo = new GameObject("VerificationWallet");
        var testWallet = walletGo.AddComponent<PlayerResources>();
        var statsGo = new GameObject("VerificationPlayer");
        try
        {
            catalog.characters = new[] { first, second };
            int originalCount = LobbyStatUpgradeState.GetLevel(source, LobbyStatId.HP);
            testWallet.AddMoney(10000);
            Check(LobbyStatUpgradeState.TryPurchase(first, LobbyStatId.HP, testWallet), "Purchase succeeds");
            Check(testWallet.Money == 9950, "Gold deducted exactly once");
            Check(LobbyStatUpgradeState.GetLevel(first, LobbyStatId.HP) == 1, "Saved purchase count");
            Check(LobbyStatUpgradeState.GetLevel(second, LobbyStatId.HP) == 0, "Other character unchanged");
            Check(LobbyStatUpgradeState.GetLevel(source, LobbyStatId.HP) == originalCount, "Real save untouched");
            var clone = Object.Instantiate(first);
            Check(LobbyStatUpgradeState.GetLevel(clone, LobbyStatId.HP) == 1, "Record reload by stable ID");
            Object.DestroyImmediate(clone);
            bool nestedPurchase = true;
            Action<int, int> callback = (money, gem) => nestedPurchase = LobbyStatUpgradeState.TryPurchase(first, LobbyStatId.HP, testWallet);
            testWallet.OnResourcesChanged += callback;
            Check(LobbyStatUpgradeState.TryPurchase(first, LobbyStatId.HP, testWallet), "Second purchase succeeds");
            testWallet.OnResourcesChanged -= callback;
            Check(!nestedPurchase && testWallet.Money == 9850, "Reentrant purchase rejected");
            testWallet.SetAllToZero();
            Check(!LobbyStatUpgradeState.TryPurchase(first, LobbyStatId.STA, testWallet), "Insufficient gold rejected");
            Check(LobbyStatUpgradeState.GetLevel(first, LobbyStatId.STA) == 0, "Failed purchase has no level gain");
            testWallet.AddMoney(10000);
            var str = first.GetEntry(LobbyStatId.STR);
            while (LobbyStatUpgradeState.GetLevel(first, LobbyStatId.STR) <
                LobbyStatUpgradeApplier.GetEffectiveMaxLevel(str, first.Config))
                Check(LobbyStatUpgradeState.TryPurchase(first, LobbyStatId.STR, testWallet), "Fill STR to cap");
            int before = testWallet.Money;
            Check(!LobbyStatUpgradeState.TryPurchase(first, LobbyStatId.STR, testWallet) && before == testWallet.Money, "MAX does not spend gold");

            var stats = statsGo.AddComponent<PlayerStats>();
            var facade = statsGo.AddComponent<PlayerFacade>();
            facade.BindCharacter(first.character);
            Near(stats.maxHealth, first.Config.maxHealth + 20, "Battle matches displayed HP");
            facade.BindCharacter(first.character);
            Near(stats.maxHealth, first.Config.maxHealth + 20, "Repeated application does not stack");
            Near(first.Config.maxHealth, source.Config.maxHealth, "Base SO remains unchanged");

            GameState.Instance.SelectedCharacter = first.character;
            var panel = LobbyUpgradePanel.EnsureOnLobbyCanvas();
            panel.Show();
            foreach (var row in panel.GetComponentsInChildren<LobbyUpgradeRowUI>())
                if (row.StatId == LobbyStatId.SPD) row.GetComponent<Button>().onClick.Invoke();
            var action = FindActive<Button>(panel, "UpgradeButton");
            Check(action.GetComponentInChildren<LocalizedText>().GetComponent<TextMeshProUGUI>().text.Contains("SPD"), "SPD selection updates action");
            panel.SelectStat(LobbyStatId.STR);
            Check(!action.interactable, "MAX button disabled");
            panel.SelectStat(LobbyStatId.HP);
            Check(action.GetComponentInChildren<LocalizedText>().GetComponent<TextMeshProUGUI>().text.Contains("HP"), "HP selection updates action");
            // 임시 지갑만 UI에 연결하고 끝날 때 원래 지갑을 돌려놓습니다.
            typeof(PlayerResources).GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { testWallet });
            panel.Show();
            Check(action.interactable, "Affordable purchase button enabled");
            testWallet.SetAllToZero();
            Check(!action.interactable, "Balance-change notification disables button");
            testWallet.AddMoney(1250);
            Check(action.interactable, "Balance-change notification enables button");
            Object.FindFirstObjectByType<LobbyMenuUI>().BindResources();
            Capture(panel, 1920, 1080, "Logs/LobbyUpgrade/upgrade-implemented.png");
            Capture(panel, 1280, 720, "Logs/LobbyUpgrade/upgrade-720p.png");
            int oldLevel = LobbyStatUpgradeState.GetLevel(first, LobbyStatId.HP);
            int oldMoney = testWallet.Money;
            action.onClick.Invoke();
            Check(LobbyStatUpgradeState.GetLevel(first, LobbyStatId.HP) == oldLevel + 1 && testWallet.Money == oldMoney - 150,
                "Action-button click purchases the selected stat");
            GameState.Instance.SelectedCharacter = second.character; panel.Show();
            Check(FindActive<TextMeshProUGUI>(panel, "CharacterTitle").text.Contains(second.character.displayName), "Character switch rebuilds panel");
            // 목록 삭제와 순서 변경 후에도 올바른 행을 선택합니다.
            second.stats = new[] { second.GetEntry(LobbyStatId.SPD), second.GetEntry(LobbyStatId.STA) };
            panel.Show();
            Check(panel.GetComponentsInChildren<LobbyUpgradeRowUI>().Length == 2, "Catalog list removal supported");
            Check(FindActive<Button>(panel, "UpgradeButton").GetComponentInChildren<TextMeshProUGUI>().text.Contains("SPD"), "First remaining row selected without HP");
            File.WriteAllText("Logs/LobbyUpgrade/play-checks.txt", assertions + " play checks passed");
            Debug.Log("[LobbyUpgradeVerification] All checks passed.");
        }
        finally
        {
            catalog.characters = originalProfiles;
            GameState.Instance.SelectedCharacter = originalCharacter;
            typeof(PlayerResources).GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { originalWallet });
            Object.DestroyImmediate(statsGo); Object.DestroyImmediate(walletGo);
            foreach (var p in new[] { first, second })
            {
                Object.DestroyImmediate(p.character.playerConfig); Object.DestroyImmediate(p.character); Object.DestroyImmediate(p);
            }
        }
    }

    private static LobbyCharacterUpgradeSO MakeTestProfile(LobbyCharacterUpgradeSO source, string suffix)
    {
        var profile = Object.Instantiate(source);
        profile.character = Object.Instantiate(source.character);
        profile.character.playerConfig = Object.Instantiate(source.Config);
        typeof(LobbyCharacterUpgradeSO).GetField("characterId", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(profile, "verification_" + Guid.NewGuid().ToString("N") + suffix);
        return profile;
    }

    private static void Capture(LobbyUpgradePanel panel, int width, int height, string path)
    {
        var camera = Camera.main;
        LobbyUpgradeCameraBlend.Ensure().GoUpgrade(true);
        var canvas = panel.GetComponentInParent<Canvas>();
        var mode = canvas.renderMode; var oldCamera = canvas.worldCamera; float distance = canvas.planeDistance;
        var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
        var rt = new RenderTexture(width, height, 24);
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera;
            canvas.planeDistance = camera.nearClipPlane + 0.5f;
            camera.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            foreach (var text in panel.GetComponentsInChildren<TextMeshProUGUI>())
            {
                text.ForceMeshUpdate();
                Check(!text.isTextOverflowing, "Text fits: " + text.name + " @" + width);
            }
            camera.Render(); RenderTexture.active = rt;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = distance;
            Object.DestroyImmediate(image); Object.DestroyImmediate(rt);
        }
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("Upgrade check failed: " + message);
        assertions++;
    }
    private static void Near(float actual, float expected, string message) => Check(Mathf.Abs(actual - expected) < 0.0001f, message);
    private static T FindActive<T>(LobbyUpgradePanel panel, string objectName) where T : Component
    {
        foreach (var component in panel.GetComponentsInChildren<T>())
            if (component.name == objectName) return component;
        throw new Exception("Missing active UI: " + objectName);
    }
}
