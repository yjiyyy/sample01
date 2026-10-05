using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>씬을 저장하지 않는 개발자 치트 메뉴 배치 검증.</summary>
public static class DevCheatVerification
{
    private const string Running = "DevCheatVerification.Running";
    private const string Result = "DevCheatVerification.Result";
    private static double runAt;
    private static int checks;

    public static void Run()
    {
        Directory.CreateDirectory("Logs/DevCheats");
        EditorSceneManager.OpenScene("Assets/Scenes/03_Lobby.unity", OpenSceneMode.Single);
        SessionState.SetBool(Running, true);
        SessionState.SetInt(Result, -1);
        EditorApplication.EnterPlaymode();
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
                EditorApplication.update += Wait;
            }
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetInt(Result, -1) >= 0)
            {
                SessionState.SetBool(Running, false);
                EditorApplication.Exit(SessionState.GetInt(Result, 1));
            }
        };
    }
    private static void Wait()
    {
        if (EditorApplication.timeSinceStartup < runAt) return;
        EditorApplication.update -= Wait;
        int result = 0;
        try { Verify(); }
        catch (Exception ex) { result = 1; Debug.LogException(ex); }
        SessionState.SetInt(Result, result);
        EditorApplication.ExitPlaymode();
    }

    private static void Verify()
    {
        var options = OptionsUI.EnsureExists();
        var wallet = PlayerResources.Instance;
        Check(wallet != null, "Lobby wallet exists");
        int money = wallet.Money, gems = wallet.Gem;
        var originalStage = StageManager.Active;
        var stageObject = new GameObject("CheatTestStage"); stageObject.SetActive(false);
        var stage = stageObject.AddComponent<StageManager>();
        try
        {
            options.Show();
            var open = Find<Button>(options, "Button_DeveloperCheats");
            Check(open != null && open.gameObject.activeInHierarchy, "Options entry visible");
            options.PrepareDeveloperButton(); options.PrepareDeveloperButton();
            int count = 0;
            foreach (var b in options.GetComponentsInChildren<Button>(true)) if (b.name == "Button_DeveloperCheats") count++;
            Check(count == 1, "No duplicate options buttons");
            Capture(1920, 1080, "options-entry.png");
            open.onClick.Invoke();
            var console = Object.FindFirstObjectByType<DevCheatConsole>();
            var view = console.GetComponentInChildren<DevCheatMenuView>();
            Check(console.IsOverlayOpen, "Options opens shared console");
            Check(!open.gameObject.activeInHierarchy, "Options suspended behind cheats");
            Check(!Find<Button>(view, "Weapons").gameObject.activeSelf, "Battle actions hidden in lobby");
            var upgrade = LobbyUpgradePanel.EnsureOnLobbyCanvas();
            upgrade.Show();
            Click(view, "Money1000"); Click(view, "Money100"); Click(view, "Gem1000"); Click(view, "Gem100");
            Check(wallet.Money == money + 1100 && wallet.Gem == gems + 1100, "All four currency buttons update shared wallet");
            Check(Find<TextMeshProUGUI>(view, "Balance").text.Contains(wallet.Money.ToString("N0")), "Balance label updates");
            Check(Find<Button>(upgrade, "UpgradeButton").interactable, "Added gold enables lobby upgrade");
            Capture(1920, 1080, "cheats-lobby.png");
            Click(view, "Close");
            Check(open.gameObject.activeInHierarchy && !console.IsOverlayOpen, "Back returns to options");
            Check(!GameplayTime.IsGameplayPaused, "Lobby pause owned by cheats is released");
            options.Hide();

            // 전투 표시 조건만 임시로 제공하며 원래 씬과 스테이지는 수정하지 않습니다.
            SetStage(stage);
            options.ShowAndPauseGameplay(); open.onClick.Invoke();
            Check(GameplayTime.IsGameplayPaused && Time.timeScale == 0f, "Battle options-to-cheats stays paused");
            Check(Find<Button>(view, "Weapons").gameObject.activeSelf, "Battle actions visible");
            Canvas.ForceUpdateCanvases();
            var scroll = view.GetComponentInChildren<ScrollRect>();
            Check(scroll.content.rect.height > scroll.viewport.rect.height, "Overflow becomes scrollable content");
            var data = new PointerEventData(EventSystem.current) { position = new Vector2(Screen.width / 2f, Screen.height / 2f), button = PointerEventData.InputButton.Left };
            scroll.verticalNormalizedPosition = 1f;
            data.scrollDelta = new Vector2(0f, -4f); scroll.OnScroll(data);
            Check(scroll.verticalNormalizedPosition < 0.99f, "Mouse wheel scrolls");
            scroll.verticalNormalizedPosition = 1f;
            scroll.OnInitializePotentialDrag(data); scroll.OnBeginDrag(data);
            data.position += Vector2.up * 150f; scroll.OnDrag(data); scroll.OnEndDrag(data);
            Check(scroll.verticalNormalizedPosition < 0.99f, "Pointer drag scrolls while paused");
            scroll.StopMovement(); scroll.verticalNormalizedPosition = 0f; Canvas.ForceUpdateCanvases();
            Check(Inside(Find<Button>(view, "ResetResources").GetComponent<RectTransform>(), scroll.viewport), "Last row reachable inside viewport");
            Check(!Find<Button>(view, "Close").transform.IsChildOf(scroll.content), "Close button stays outside scroll content");
            Capture(1920, 1080, "cheats-battle-bottom.png");
            Capture(1280, 720, "cheats-battle-720p.png");
            Capture(2340, 1080, "cheats-battle-wide.png");
            Click(view, "ResetResources");
            Check(wallet.Money == 0 && wallet.Gem == 0, "Bottom reset button works");
            Check(!Find<Button>(upgrade, "UpgradeButton").interactable, "Reset disables unaffordable lobby upgrade");
            Click(view, "Close");
            Check(GameplayTime.IsGameplayPaused && Time.timeScale == 0f, "Back to battle options keeps pause");
            options.Hide(); Check(!GameplayTime.IsGameplayPaused, "Closing battle options resumes");
            options.ShowAndPauseGameplay(); options.OpenDeveloperCheats();
            var childObject = new GameObject("CheatTestChildMenus");
            try
            {
                var weapons = childObject.AddComponent<DevWeaponSwitcher>();
                typeof(DevCheatConsole).GetField("weaponSwitcher", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(console, weapons);
                console.OpenWeaponMenu();
                Check(weapons.IsOverlayOpen && !console.IsOverlayOpen && GameplayTime.IsGameplayPaused, "Weapon submenu keeps pause");
                weapons.CloseOverlay();
                typeof(DevCheatConsole).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(console, null);
                Check(console.IsOverlayOpen && view.gameObject.activeSelf && GameplayTime.IsGameplayPaused, "Weapon submenu returns to scroll menu");
                var upgrades = childObject.AddComponent<DevUpgradeSwitcher>();
                typeof(DevCheatConsole).GetField("upgradeSwitcher", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(console, upgrades);
                console.OpenUpgradeMenu();
                Check(upgrades.IsOverlayOpen && !console.IsOverlayOpen && GameplayTime.IsGameplayPaused, "Upgrade submenu keeps pause");
                upgrades.CloseOverlay();
                Object.DestroyImmediate(upgrades);
                typeof(DevCheatConsole).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(console, null);
                Check(console.IsOverlayOpen && GameplayTime.IsGameplayPaused, "Upgrade submenu returns to scroll menu");
                Check(EventSystem.current != null && EventSystem.current.enabled, "UI events restored after upgrade submenu");
            }
            finally { Object.DestroyImmediate(childObject); }
            console.CloseOverlay(); options.Hide();
            console.OpenOverlay(); Check(console.IsOverlayOpen && GameplayTime.IsGameplayPaused, "Portrait entry still opens and pauses");
            console.CloseOverlay(); Check(!GameplayTime.IsGameplayPaused, "Portrait exit resumes");
            options.ShowAndPauseGameplay(); options.OpenDeveloperCheats();
            options.Hide(); Check(!console.IsOverlayOpen && !GameplayTime.IsGameplayPaused, "Scene/option dismissal closes both without reopening options");
            File.WriteAllText("Logs/DevCheats/checks.txt", checks + " checks passed");
            Debug.Log("[DevCheatVerification] " + checks + " checks passed.");
        }
        finally
        {
            options.Hide(); SetStage(originalStage); Object.DestroyImmediate(stageObject);
            wallet.SetAllToZero(); wallet.AddMoney(money); wallet.AddGem(gems);
        }
    }

    private static void SetStage(StageManager stage) => typeof(StageManager).GetProperty("Active").GetSetMethod(true).Invoke(null, new object[] { stage });
    private static void Click(DevCheatMenuView view, string name) => Find<Button>(view, name).onClick.Invoke();
    private static T Find<T>(Component root, string name) where T : Component
    {
        // 교체된 UI는 프레임 끝에 제거되므로 활성 인스턴스를 우선합니다.
        foreach (var component in root.GetComponentsInChildren<T>()) if (component.name == name) return component;
        foreach (var component in root.GetComponentsInChildren<T>(true)) if (component.name == name) return component;
        throw new Exception("Missing UI: " + name);
    }
    private static bool Inside(RectTransform item, RectTransform viewport)
    {
        var corners = new Vector3[4]; item.GetWorldCorners(corners);
        foreach (var corner in corners)
        {
            var local = viewport.InverseTransformPoint(corner);
            if (local.y < viewport.rect.yMin - 1f || local.y > viewport.rect.yMax + 1f) return false;
        }
        return true;
    }

    private static void Capture(int width, int height, string name)
    {
        var camera = Camera.main;
        var originals = new List<(Canvas canvas, RenderMode mode, Camera camera, float distance)>();
        var rt = new RenderTexture(width, height, 24);
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
        try
        {
            camera.targetTexture = rt;
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                originals.Add((canvas, canvas.renderMode, canvas.worldCamera, canvas.planeDistance));
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + 0.5f;
            }
            Canvas.ForceUpdateCanvases();
            foreach (var list in Object.FindObjectsByType<ScrollRect>(FindObjectsSortMode.None))
                typeof(ScrollRect).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(list, null);
            var view = Object.FindFirstObjectByType<DevCheatMenuView>();
            if (view != null)
                foreach (var text in view.GetComponentsInChildren<TextMeshProUGUI>())
                { text.ForceMeshUpdate(); Check(!text.isTextOverflowing, "Text fits " + text.name + " @ " + width); }
            camera.Render(); RenderTexture.active = rt;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            File.WriteAllBytes("Logs/DevCheats/" + name, image.EncodeToPNG());
        }
        finally
        {
            foreach (var old in originals)
            { old.canvas.renderMode = old.mode; old.canvas.worldCamera = old.camera; old.canvas.planeDistance = old.distance; }
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            Object.DestroyImmediate(image); Object.DestroyImmediate(rt);
        }
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("Cheat check failed: " + message);
        checks++;
    }
}
