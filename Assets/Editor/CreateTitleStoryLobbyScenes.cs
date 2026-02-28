using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Title, Story, Lobby 씬을 생성하고 Build Settings에 등록합니다.
/// 메뉴: Tools > Create Title/Story/Lobby Scenes
/// </summary>
public static class CreateTitleStoryLobbyScenes
{
    private const string MenuPath = "Tools/Create Title/Story/Lobby Scenes";
    private const string ScenesRoot = "Assets/Scenes";

    [MenuItem(MenuPath)]
    public static void CreateScenes()
    {
        EnsureFolders();
        CreateTitleScene();
        CreateStoryScene();
        CreateLobbyScene();
        UpdateBuildSettings();
        AssetDatabase.Refresh();
        Debug.Log("[CreateTitleStoryLobbyScenes] Title, Story, Lobby 씬 생성 완료.");
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");
    }

    private static void CreateTitleScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Canvas
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // EventSystem (Input System 전용 프로젝트에서는 InputSystemUIInputModule 사용)
        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            esGO.AddComponent<InputSystemUIInputModule>();
#else
            esGO.AddComponent<StandaloneInputModule>();
#endif
        }

        // Menu root
        var menuGO = new GameObject("Menu");
        menuGO.transform.SetParent(canvasGO.transform, false);
        var menuRect = menuGO.AddComponent<RectTransform>();
        menuRect.anchorMin = new Vector2(0.5f, 0.5f);
        menuRect.anchorMax = new Vector2(0.5f, 0.5f);
        menuRect.sizeDelta = new Vector2(400, 300);
        menuRect.anchoredPosition = Vector2.zero;

        var ctrl = menuGO.AddComponent<TitleMenuController>();

        // New Game (활성화)
        var btnNew = CreateMenuButton("New Game", menuGO.transform, 0);
        var btnNewC = btnNew.GetComponent<Button>();
        btnNewC.onClick.AddListener(() => ctrl.OnNewGame());

        // Load Game (비활성화)
        var btnLoad = CreateMenuButton("Load Game", menuGO.transform, -60);
        var btnLoadC = btnLoad.GetComponent<Button>();
        btnLoadC.interactable = false;
        btnLoadC.onClick.AddListener(() => ctrl.OnLoadGame());

        // Option (비활성화)
        var btnOpt = CreateMenuButton("Option", menuGO.transform, -120);
        var btnOptC = btnOpt.GetComponent<Button>();
        btnOptC.interactable = false;
        btnOptC.onClick.AddListener(() => ctrl.OnOption());

        // Exit (비활성화)
        var btnExit = CreateMenuButton("Exit", menuGO.transform, -180);
        var btnExitC = btnExit.GetComponent<Button>();
        btnExitC.interactable = false;
        btnExitC.onClick.AddListener(() => ctrl.OnExit());

        EditorSceneManager.SaveScene(scene, $"{ScenesRoot}/Title.unity");
    }

    private static GameObject CreateMenuButton(string label, Transform parent, float offsetY)
    {
        var go = new GameObject(label);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(300, 50);
        rect.anchoredPosition = new Vector2(0, offsetY);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        go.AddComponent<Button>();

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textGO.AddComponent<Text>();
        text.text = label;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 24;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;

        return go;
    }

    private static void CreateStoryScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Canvas - full screen
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            esGO.AddComponent<InputSystemUIInputModule>();
#else
            esGO.AddComponent<StandaloneInputModule>();
#endif
        }

        // Full-screen Image (스토리 이미지 표시)
        var imageGO = new GameObject("StoryImage");
        imageGO.transform.SetParent(canvasGO.transform, false);
        var imageRect = imageGO.AddComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;

        var image = imageGO.AddComponent<Image>();
        image.color = Color.black; // 이미지 없을 때 기본 색
        image.enabled = false; // 스프라이트 없으면 비활성

        imageGO.AddComponent<StorySequenceController>();

        // 터치로 넘기기 위한 투명 버튼 (전체 화면)
        var tapGO = new GameObject("TapToAdvance");
        tapGO.transform.SetParent(canvasGO.transform, false);
        var tapRect = tapGO.AddComponent<RectTransform>();
        tapRect.anchorMin = Vector2.zero;
        tapRect.anchorMax = Vector2.one;
        tapRect.offsetMin = Vector2.zero;
        tapRect.offsetMax = Vector2.zero;
        tapRect.SetAsLastSibling(); // 맨 앞에 표시

        var tapImg = tapGO.AddComponent<Image>();
        tapImg.color = new Color(0, 0, 0, 0.01f); // 거의 투명
        var tapBtn = tapGO.AddComponent<Button>();
        tapBtn.transition = Selectable.Transition.None;
        var storyCtrl = imageGO.GetComponent<StorySequenceController>();
        tapBtn.onClick.AddListener(() => storyCtrl.OnAdvance());

        EditorSceneManager.SaveScene(scene, $"{ScenesRoot}/Story.unity");
    }

    private static void CreateLobbyScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, $"{ScenesRoot}/Lobby.unity");
    }

    private static void UpdateBuildSettings()
    {
        var scenes = new[]
        {
            new EditorBuildSettingsScene($"{ScenesRoot}/Title.unity", true),
            new EditorBuildSettingsScene($"{ScenesRoot}/Story.unity", true),
            new EditorBuildSettingsScene($"{ScenesRoot}/Lobby.unity", true),
        };

        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes);

        // 기존 DemoScene 등 다른 씬이 있으면 뒤에 추가 (옵션)
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (s.path.Contains("DemoScene") || s.path.Contains("Stage_"))
                list.Add(s);
        }

        EditorBuildSettings.scenes = list.ToArray();
    }
}
