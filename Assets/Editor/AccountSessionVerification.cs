using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>씬 전환, 카메라 이동과 Play 재시작을 검사합니다. 씬을 저장하지 않는 배치 전용 도구.</summary>
public static class AccountSessionVerification
{
    private const string Prefix = "AccountSessionVerification.";
    private static IEnumerator routine;
    private static double next;
    private static int checks;

    public static void Run()
    {
        checks = 0;
        Directory.CreateDirectory("Logs/AccountSession");
        Directory.CreateDirectory("Logs/DevCheats");
        SessionState.SetBool(Prefix + "OptionsEnabled", EditorSettings.enterPlayModeOptionsEnabled);
        SessionState.SetInt(Prefix + "Options", (int)EditorSettings.enterPlayModeOptions);
        SessionState.SetBool(Prefix + "Running", true);
        SessionState.SetInt(Prefix + "Round", 0);
        SessionState.SetInt(Prefix + "Result", 0);
        // 첫 실행과 재실행 모두 도메인 리로드 없이 검사하여 정적 데이터 초기화도 검증합니다.
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        EditorSceneManager.OpenScene("Assets/Scenes/03_Lobby.unity");
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Subscribe()
    {
        EditorApplication.playModeStateChanged -= StateChanged;
        EditorApplication.playModeStateChanged += StateChanged;
    }

    private static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Prefix + "Running", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            routine = Verify();
            next = EditorApplication.timeSinceStartup + 2;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            if (SessionState.GetInt(Prefix + "Round", 0) == 0 && SessionState.GetInt(Prefix + "Result", 0) == 0)
            {
                SessionState.SetInt(Prefix + "Round", 1);
                EditorApplication.delayCall += () => EditorApplication.EnterPlaymode();
                return;
            }
            EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Prefix + "OptionsEnabled", false);
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(Prefix + "Options", 0);
            SessionState.SetBool(Prefix + "Running", false);
            File.WriteAllText("Logs/AccountSession/result.txt", checks + " checks; result=" + SessionState.GetInt(Prefix + "Result", 1));
            EditorApplication.Exit(SessionState.GetInt(Prefix + "Result", 1));
        }
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        try
        {
            if (routine.MoveNext())
            {
                next = EditorApplication.timeSinceStartup + Convert.ToDouble(routine.Current);
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            SessionState.SetInt(Prefix + "Result", 1);
        }
        EditorApplication.update -= Tick;
        EditorApplication.ExitPlaymode();
    }

    private static IEnumerator Verify()
    {
        var catalog = LobbyStatUpgradeCatalogSO.Load();
        Check(PlayerResources.Instance != null, "Lobby wallet exists");
        Check(AccountSession.Money == 0 && AccountSession.Gem == 0, "New Play resets both currencies");
        foreach (var p in catalog.characters)
            foreach (var stat in p.stats)
                Check(LobbyStatUpgradeState.GetLevel(p, stat.id) == 0, "New Play resets " + p.name + "/" + stat.id);
        Check(GameState.Instance.SelectedCharacter == Object.FindFirstObjectByType<LobbyController>().PreviewCharacter, "Selection bound to account");
        var panel = Object.FindFirstObjectByType<LobbyUpgradePanel>(FindObjectsInactive.Include);
        Check(panel == null || !panel.gameObject.activeSelf, "Saved panel preview closes at startup");
        Vector3 basis = Camera.main.transform.position;
        var spawn = GameObject.Find("CharacterSpawnPoint").transform;
        Check(Mathf.Abs(Camera.main.WorldToViewportPoint(spawn.position + Vector3.up * 1.25f).x - 0.5f) < 0.001f,
            "Character centered on entering lobby");
        if (SessionState.GetInt(Prefix + "Round", 0) == 1) yield break;

        var first = catalog.GetCharacter(GameState.Instance.SelectedCharacter);
        var second = Array.Find(catalog.characters, p => p != first);
        float originalHP = first.Config.maxHealth;
        PlayerResources.Instance.AddMoney(10000);
        PlayerResources.Instance.AddGem(300);
        int price = LobbyStatUpgradeApplier.GetNextCost(first.GetEntry(LobbyStatId.HP), 0);
        Check(LobbyStatUpgradeState.TryPurchase(first, LobbyStatId.HP, PlayerResources.Instance), "Buy first character HP");
        int expectedMoney = 10000 - price;
        Check(AccountSession.Money == expectedMoney && AccountSession.Gem == 300, "Purchase uses account gold only");
        Check(LobbyStatUpgradeState.GetLevel(second, LobbyStatId.HP) == 0, "Second character has own upgrades");
        Check(first.Config.maxHealth == originalHP, "Purchase does not modify base SO");

        var blend = LobbyUpgradeCameraBlend.Ensure();
        typeof(LobbyUpgradeCameraBlend).GetField("blendSeconds", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(blend, 2f);
        panel = LobbyUpgradePanel.EnsureOnLobbyCanvas();
        panel.Show();
        LobbyUpgradeCameraBlend.TryGetUpgradePose(out var destination, out var rotation);
        yield return 0.25;
        Check(Vector3.Distance(Camera.main.transform.position, basis) > 0.001f &&
            Vector3.Distance(Camera.main.transform.position, destination) > 0.001f, "Panel camera interpolates between endpoints");
        yield return 2.2;
        Check(Vector3.Distance(Camera.main.transform.position, destination) < 0.001f, "Panel camera arrives");
        Capture("panel-camera.png");
        panel.Hide();
        yield return 2.2;
        Check(Vector3.Distance(Camera.main.transform.position, basis) < 0.001f, "Back returns to base camera");
        Capture("lobby-camera.png");
        var shop = LobbyShopPanel.EnsureOnLobbyCanvas();
        shop.Show();
        yield return 2.2;
        Check(Vector3.Distance(Camera.main.transform.position, basis) > 0.1f, "Shop camera moves away from lobby pose");
        panel.Show();
        panel.Hide();
        yield return 2.2;
        Check(Vector3.Distance(Camera.main.transform.position, basis) < 0.001f, "Panel switching preserves base pose");

        SceneManager.LoadScene(SceneNames.CharacterSelection);
        yield return 3;
        Check(AccountSession.Money == expectedMoney && AccountSession.Gem == 300, "Character-select scene preserves account wallet");
        Check(GameState.Instance.SelectedCharacter == first.character, "Selected character survives scene change");
        GameState.Instance.SelectedCharacter = second.character;
        SceneManager.LoadScene(SceneNames.Lobby);
        yield return 3;
        Check(PlayerResources.Instance.Money == expectedMoney && PlayerResources.Instance.Gem == 300, "New character reads existing wallet");
        Check(LobbyStatUpgradeState.GetLevel(first, LobbyStatId.HP) == 1 && LobbyStatUpgradeState.GetLevel(second, LobbyStatId.HP) == 0,
            "Character switch preserves separate upgrade records");
        Check(Object.FindFirstObjectByType<LobbyController>().PreviewCharacter == second.character, "Lobby displays changed character");
        GameState.Instance.SelectedCharacter = first.character;
        SceneManager.LoadScene("Stage00");
        yield return 4;
        Check(SceneManager.GetActiveScene().name == "Stage00", "Battle scene loaded");
        Check(PlayerResources.Instance != null && PlayerResources.Instance.Money == expectedMoney, "Battle uses same account wallet");
        PlayerResources.Instance.AddMoney(75);
        PlayerResources.Instance.AddGem(12);
        expectedMoney += 75;
        var stats = PlayerResources.Instance.GetComponentInChildren<PlayerStats>();
        if (stats == null) stats = PlayerResources.Instance.GetComponentInParent<PlayerStats>();
        Check(stats != null && Mathf.Abs(stats.maxHealth - LobbyStatUpgradeApplier.GetCurrentValue(first.GetEntry(LobbyStatId.HP), first.Config, 1)) < 0.001f,
            "Battle applies character HP upgrade");
        SceneManager.LoadScene(SceneNames.Lobby);
        yield return 3;
        Check(PlayerResources.Instance.Money == expectedMoney && PlayerResources.Instance.Gem == 312, "Battle earnings survive lobby return");
        Check(LobbyStatUpgradeState.GetLevel(first, LobbyStatId.HP) == 1, "Upgrade survives battle return");
        PlayerResources.Instance.AddMoney(int.MaxValue);
        PlayerResources.Instance.AddGem(int.MaxValue);
        Check(AccountSession.Money == int.MaxValue && AccountSession.Gem == int.MaxValue, "Currency overflow clamps safely");
        PlayerResources.Instance.TrySpend(ShopCurrency.Money, -100);
        Check(AccountSession.Money == int.MaxValue, "Negative spend cannot grant currency");
    }

    private static void Capture(string name)
    {
        typeof(DevCheatVerification).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { 1920, 1080, name });
        File.Copy("Logs/DevCheats/" + name, "Logs/AccountSession/" + name, true);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("Account check failed: " + message);
        checks++;
        Debug.Log("[AccountSessionVerification] PASS " + message);
    }
}
