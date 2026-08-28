using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEditor.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Build Settings에 씬을 등록하고, Character Selection 씬에 기본 UI를 설정합니다.
/// 메뉴: Tools > Setup Scene Transitions
/// </summary>
public static class SetupSceneTransitions
{
    private const string MenuPath = "Tools/Setup Scene Transitions";
    private const string ScenesRoot = "Assets/Scenes";

    [MenuItem("Tools/Setup Scene Transitions")]
    public static void Setup()
    {
        UpdateBuildSettings();
        SetupCharacterSelectionScene();
        SetupLobbyScene();
        AssetDatabase.Refresh();
        Debug.Log("[SetupSceneTransitions] Build Settings 및 씬 전환 설정 완료.");
    }

    [MenuItem("Tools/Setup Character Selection Layout")]
    public static void SetupCharacterSelectionLayoutMenu()
    {
        SetupCharacterSelectionLayout.Setup();
    }

    /// <summary>
    /// Build Settings에 Title, Loading, CharacterSelection, Lobby 등 등록
    /// </summary>
    private static void UpdateBuildSettings()
    {
        var basePaths = new[]
        {
            $"{ScenesRoot}/00_Title.unity",
            $"{ScenesRoot}/Loading/Loading_00.unity",
            $"{ScenesRoot}/02_CharacterSelectionLevel.unity",
            $"{ScenesRoot}/03_Lobby.unity",
            $"{ScenesRoot}/DemoScene.unity"
        };

        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>();
        foreach (var p in basePaths)
        {
            if (System.IO.File.Exists(p))
                list.Add(new EditorBuildSettingsScene(p, true));
        }

        // Assets/Scenes/Stage 폴더 아래의 모든 스테이지 씬을 자동 등록
        var stageDir = $"{ScenesRoot}/Stage";
        if (System.IO.Directory.Exists(stageDir))
        {
            var stageScenePaths = System.IO.Directory.GetFiles(stageDir, "*.unity", System.IO.SearchOption.AllDirectories);
            foreach (var p in stageScenePaths)
            {
                var normalized = p.Replace("\\", "/");
                list.Add(new EditorBuildSettingsScene(normalized, true));
            }
        }

        EditorBuildSettings.scenes = list.ToArray();
        Debug.Log($"[SetupSceneTransitions] Build Settings에 기본 씬 + Stage 폴더 씬 등록됨. 총 {list.Count}개.");
    }

    /// <summary>
    /// Character Selection 씬 UI 레이아웃 구성
    /// </summary>
    private static void SetupCharacterSelectionScene()
    {
        var path = $"{ScenesRoot}/02_CharacterSelectionLevel.unity";
        if (!System.IO.File.Exists(path))
        {
            Debug.LogWarning($"[SetupSceneTransitions] {path}를 찾을 수 없습니다.");
            return;
        }

        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        SetupCharacterSelectionLayout.ApplyToOpenScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }

    /// <summary>
    /// 로비 씬에 바닥, 캐릭터 스폰 포인트, LobbyController 추가
    /// </summary>
    private static void SetupLobbyScene()
    {
        var path = $"{ScenesRoot}/03_Lobby.unity";
        if (!System.IO.File.Exists(path))
        {
            Debug.LogWarning($"[SetupSceneTransitions] {path}를 찾을 수 없습니다.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        if (!scene.IsValid())
            return;

        var cam = Camera.main;
        if (cam == null)
        {
            var camGO = GameObject.Find("Main Camera");
            if (camGO == null) camGO = new GameObject("Main Camera");
            cam = camGO.GetComponent<Camera>();
            if (cam == null) cam = camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
            camGO.tag = "MainCamera";
        }

        // 바닥 (Plane)
        var floorGO = GameObject.Find("LobbyFloor");
        if (floorGO == null)
        {
            floorGO = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floorGO.name = "LobbyFloor";
            floorGO.transform.position = new Vector3(0, 0, 0);
            floorGO.transform.rotation = Quaternion.identity;
            floorGO.transform.localScale = new Vector3(2, 1, 2); // 20x20 유닛
        }

        // 캐릭터 스폰 포인트 (카메라 앞, 바닥 위)
        var spawnGO = GameObject.Find("CharacterSpawnPoint");
        if (spawnGO == null)
        {
            spawnGO = new GameObject("CharacterSpawnPoint");
            var pos = cam != null
                ? cam.transform.position + cam.transform.forward * 6f + Vector3.down * cam.transform.position.y
                : new Vector3(0, 0, 0);
            pos.y = 0;
            spawnGO.transform.position = pos;
            spawnGO.transform.rotation = Quaternion.identity;
        }

        // LobbyController
        var lobbyGO = GameObject.Find("LobbyController");
        LobbyController lobby;
        if (lobbyGO == null)
        {
            lobbyGO = new GameObject("LobbyController");
            lobby = lobbyGO.AddComponent<LobbyController>();
            var so = new SerializedObject(lobby);
            so.FindProperty("characterSpawnPoint").objectReferenceValue = spawnGO.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            lobby = lobbyGO.GetComponent<LobbyController>();
            if (lobby != null)
            {
                var so = new SerializedObject(lobby);
                if (so.FindProperty("characterSpawnPoint").objectReferenceValue == null && spawnGO != null)
                    so.FindProperty("characterSpawnPoint").objectReferenceValue = spawnGO.transform;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // 로비 UI: Canvas, EventSystem, 버튼 5개
        SetupLobbyUI();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SetupSceneTransitions] 로비 씬 설정 완료.");
    }

    /// <summary>
    /// 로비 씬에 Canvas, EventSystem, 상점/스테이지 패널을 만들고 새 로비 레이아웃을 붙입니다.
    /// </summary>
    private static void SetupLobbyUI()
    {
        // Canvas
        var canvasGO = GameObject.Find("LobbyCanvas");
        if (canvasGO == null)
        {
            canvasGO = new GameObject("LobbyCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        // EventSystem
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            esGO.AddComponent<InputSystemUIInputModule>();
#else
            esGO.AddComponent<StandaloneInputModule>();
#endif
        }

        var canvasTransform = canvasGO.transform;

        // StageSelectPanel은 ShopPanel처럼 항상 삭제 후 새로 생성
        var previousStagePanel = canvasTransform.Find("StageSelectPanel");
        if (previousStagePanel != null)
            Object.DestroyImmediate(previousStagePanel.gameObject);

        var stagePanelGO = new GameObject("StageSelectPanel");
        stagePanelGO.transform.SetParent(canvasTransform, false);
        var stageRect = stagePanelGO.AddComponent<RectTransform>();
        stageRect.anchorMin = Vector2.zero;
        stageRect.anchorMax = Vector2.one;
        stageRect.offsetMin = Vector2.zero;
        stageRect.offsetMax = Vector2.zero;

        var stageBgImg = stagePanelGO.AddComponent<Image>();
        stageBgImg.color = new Color(0f, 0f, 0f, 0.6f);

        var stagePanelComp = stagePanelGO.AddComponent<StageSelectPanel>();

        // Content 패널 (실제 보이는 영역)
        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(stagePanelGO.transform, false);
        var contentRect = contentGO.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(640f, 420f);
        var contentImg = contentGO.AddComponent<Image>();
        contentImg.color = new Color(0.1f, 0.12f, 0.18f, 0.96f);

        var viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(contentGO.transform, false);
        var viewportRect = viewportGO.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(12f, 12f);
        viewportRect.offsetMax = new Vector2(-28f, -12f);
        var viewportImg = viewportGO.AddComponent<Image>();
        viewportImg.color = new Color(0f, 0f, 0f, 0f);
        viewportGO.AddComponent<RectMask2D>();

        // 실제 그리드 콘텐츠
        var gridGO = new GameObject("ContentGrid");
        gridGO.transform.SetParent(viewportGO.transform, false);
        var gridRect = gridGO.AddComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0f, 1f);
        gridRect.anchorMax = new Vector2(0f, 1f);
        gridRect.pivot = new Vector2(0f, 1f);
        gridRect.anchoredPosition = Vector2.zero;

        // 2 x 5 고정 그리드 (정사각형 슬롯)
        var grid = gridGO.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(160f, 160f);
        grid.spacing = new Vector2(20f, 20f);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Vertical;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;

        const int stageColumns = 2;
        const int stageTotalButtons = 10;
        int stageRows = Mathf.CeilToInt(stageTotalButtons / (float)stageColumns);
        float gridContentW = stageColumns * grid.cellSize.x + (stageColumns - 1) * grid.spacing.x;
        float gridContentH = stageRows * grid.cellSize.y + (stageRows - 1) * grid.spacing.y;
        gridRect.sizeDelta = new Vector2(gridContentW, gridContentH);

        var scrollbarGO = new GameObject("Scrollbar Vertical");
        scrollbarGO.transform.SetParent(contentGO.transform, false);
        var scrollbarRect = scrollbarGO.AddComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 0.5f);
        scrollbarRect.sizeDelta = new Vector2(16f, -24f);
        scrollbarRect.anchoredPosition = new Vector2(-6f, 0f);
        var scrollbarBgImg = scrollbarGO.AddComponent<Image>();
        scrollbarBgImg.color = new Color(0f, 0f, 0f, 0.35f);
        var scrollbar = scrollbarGO.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        var scrollRect = contentGO.AddComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = gridRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 25f;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

        var handleGO = new GameObject("Handle");
        handleGO.transform.SetParent(scrollbarGO.transform, false);
        var handleRect = handleGO.AddComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = Vector2.zero;
        handleRect.offsetMax = Vector2.zero;
        var handleImg = handleGO.AddComponent<Image>();
        handleImg.color = new Color(0.8f, 0.8f, 0.9f, 0.9f);
        scrollbar.targetGraphic = handleImg;
        scrollbar.handleRect = handleRect;

        // 스테이지 버튼 10개 생성 (1번: Stage01, 2~10: 비활성화)
        string[] stageSceneNames = { "Stage01", "", "", "", "", "", "", "", "", "" };
        for (int i = 0; i < 10; i++)
        {
            var btnGO = new GameObject($"StageButton_{i + 1}");
            btnGO.transform.SetParent(gridGO.transform, false);
            var btnRect = btnGO.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.5f);
            btnRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnRect.sizeDelta = grid.cellSize;

            var slotBgImg = btnGO.AddComponent<Image>();
            slotBgImg.type = Image.Type.Simple;
            slotBgImg.color = (i == 0) ? new Color(0.22f, 0.25f, 0.32f, 1f) : new Color(0.15f, 0.15f, 0.18f, 1f);

            var stageBtn = btnGO.AddComponent<Button>();
            stageBtn.targetGraphic = slotBgImg;
            stageBtn.interactable = (i == 0);
            var btnColors = stageBtn.colors;
            btnColors.normalColor = Color.white;
            btnColors.highlightedColor = new Color(1.05f, 1.05f, 1.05f, 1f);
            btnColors.pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            btnColors.disabledColor = new Color(0.5f, 0.5f, 0.52f, 0.8f);
            stageBtn.colors = btnColors;

            var outline = btnGO.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(btnGO.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var labelText = labelGO.AddComponent<Text>();
            labelText.text = (i + 1).ToString();
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 26;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = (i == 0) ? Color.white : new Color(0.6f, 0.6f, 0.62f, 1f);

            if (!string.IsNullOrEmpty(stageSceneNames[i]))
            {
                var loadScene = btnGO.AddComponent<LoadSceneOnClick>();
                var soLoad = new SerializedObject(loadScene);
                soLoad.FindProperty("targetSceneName").stringValue = stageSceneNames[i];
                soLoad.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        var closeTopGO = new GameObject("CloseButtonTopRight");
        closeTopGO.transform.SetParent(contentGO.transform, false);
        var closeTopRect = closeTopGO.AddComponent<RectTransform>();
        closeTopRect.anchorMin = new Vector2(1f, 1f);
        closeTopRect.anchorMax = new Vector2(1f, 1f);
        closeTopRect.pivot = new Vector2(1f, 1f);
        closeTopRect.sizeDelta = new Vector2(52f, 52f);
        closeTopRect.anchoredPosition = new Vector2(-8f, -8f);
        var closeTopImg = closeTopGO.AddComponent<Image>();
        closeTopImg.color = new Color(0.8f, 0.2f, 0.25f, 1f);
        var closeTopBtn = closeTopGO.AddComponent<Button>();
        var closeColors = closeTopBtn.colors;
        closeColors.normalColor = closeTopImg.color;
        closeColors.highlightedColor = new Color(0.95f, 0.35f, 0.4f, 1f);
        closeColors.pressedColor = new Color(0.6f, 0.15f, 0.18f, 1f);
        closeColors.selectedColor = closeColors.normalColor;
        closeColors.disabledColor = new Color(0.3f, 0.1f, 0.1f, 0.7f);
        closeTopBtn.colors = closeColors;

        var closeTopTextGO = new GameObject("Text");
        closeTopTextGO.transform.SetParent(closeTopGO.transform, false);
        var closeTopTextRect = closeTopTextGO.AddComponent<RectTransform>();
        closeTopTextRect.anchorMin = Vector2.zero;
        closeTopTextRect.anchorMax = Vector2.one;
        closeTopTextRect.offsetMin = Vector2.zero;
        closeTopTextRect.offsetMax = Vector2.zero;
        var closeTopText = closeTopTextGO.AddComponent<Text>();
        closeTopText.text = "X";
        closeTopText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        closeTopText.fontSize = 30;
        closeTopText.alignment = TextAnchor.MiddleCenter;
        closeTopText.color = Color.white;
        var closeOutline = closeTopGO.AddComponent<Outline>();
        closeOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        closeOutline.effectDistance = new Vector2(2f, -2f);

        // StageSelectPanel closeButton 연결
        var soPanel = new SerializedObject(stagePanelComp);
        soPanel.FindProperty("closeButton").objectReferenceValue = closeTopBtn;
        soPanel.ApplyModifiedPropertiesWithoutUndo();

        UnityAction stageCloseAction = stagePanelComp.Hide;
        UnityEventTools.AddPersistentListener(closeTopBtn.onClick, stageCloseAction);
        EditorUtility.SetDirty(closeTopBtn);

        stagePanelGO.SetActive(false);

        // 상점 패널(ShopPanel)은 항상 최신 구조로 다시 생성 (기존 것이 있으면 제거)
        var previousShopPanel = canvasTransform.Find("ShopPanel");
        if (previousShopPanel != null)
        {
            Object.DestroyImmediate(previousShopPanel.gameObject);
        }

        // 새 상점 패널(ShopPanel) 생성
        var shopPanelGO = new GameObject("ShopPanel");
        shopPanelGO.transform.SetParent(canvasTransform, false);
        var shopRect = shopPanelGO.AddComponent<RectTransform>();
        shopRect.anchorMin = Vector2.zero;
        shopRect.anchorMax = Vector2.one;
        shopRect.offsetMin = Vector2.zero;
        shopRect.offsetMax = Vector2.zero;

        // 반투명 어두운 배경
        var shopBgImg = shopPanelGO.AddComponent<Image>();
        shopBgImg.color = new Color(0f, 0f, 0f, 0.6f);

        var shopPanelComp = shopPanelGO.AddComponent<ShopPanel>();

        // 실제 상점 콘텐츠 패널 (가운데 고정, 갈색 톤)
        var shopContentGO = new GameObject("Content");
        shopContentGO.transform.SetParent(shopPanelGO.transform, false);
        var shopContentRect = shopContentGO.AddComponent<RectTransform>();
        shopContentRect.anchorMin = new Vector2(0.5f, 0.5f);
        shopContentRect.anchorMax = new Vector2(0.5f, 0.5f);
        shopContentRect.sizeDelta = new Vector2(620f, 360f);
        var shopContentImg = shopContentGO.AddComponent<Image>();
        // 따뜻한 갈색 계열 패널
        shopContentImg.color = new Color(0.32f, 0.23f, 0.16f, 0.97f);

        // 스크롤 가능한 버튼 영역(Viewport + Grid)
        var shopViewportGO = new GameObject("Viewport");
        shopViewportGO.transform.SetParent(shopContentGO.transform, false);
        var shopViewportRect = shopViewportGO.AddComponent<RectTransform>();
        shopViewportRect.anchorMin = new Vector2(0f, 0f);
        shopViewportRect.anchorMax = new Vector2(1f, 1f);
        shopViewportRect.offsetMin = new Vector2(16f, 16f);
        shopViewportRect.offsetMax = new Vector2(-32f, -16f); // 오른쪽은 스크롤바 여유
        // RectMask2D를 사용해서 뷰포트 영역만 잘라내고, 별도의 알파 마스크는 사용하지 않습니다.
        var shopViewportImg = shopViewportGO.AddComponent<Image>();
        shopViewportImg.color = new Color(0f, 0f, 0f, 0f); // 완전 투명
        shopViewportGO.AddComponent<RectMask2D>();

        var shopGridGO = new GameObject("ButtonGrid");
        shopGridGO.transform.SetParent(shopViewportGO.transform, false);
        var shopGridRect = shopGridGO.AddComponent<RectTransform>();
        shopGridRect.anchorMin = new Vector2(0f, 1f);
        shopGridRect.anchorMax = new Vector2(0f, 1f);
        shopGridRect.pivot = new Vector2(0f, 1f);
        shopGridRect.anchoredPosition = Vector2.zero;

        var shopGrid = shopGridGO.AddComponent<GridLayoutGroup>();
        shopGrid.cellSize = new Vector2(230f, 90f);
        shopGrid.spacing = new Vector2(20f, 14f);
        shopGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        shopGrid.startAxis = GridLayoutGroup.Axis.Vertical;
        shopGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        shopGrid.constraintCount = 2;

        // 콘텐츠 영역 크기(스크롤 기준)를 버튼 개수에 맞게 설정
        const int shopColumns = 2;
        const int shopTotalButtons = 10;
        int shopRows = Mathf.CeilToInt(shopTotalButtons / (float)shopColumns);
        float shopContentWidth = shopColumns * shopGrid.cellSize.x + (shopColumns - 1) * shopGrid.spacing.x;
        float shopContentHeight = shopRows * shopGrid.cellSize.y + (shopRows - 1) * shopGrid.spacing.y;
        shopGridRect.sizeDelta = new Vector2(shopContentWidth, shopContentHeight);

        // 세로 스크롤바
        var shopScrollbarGO = new GameObject("Scrollbar Vertical");
        shopScrollbarGO.transform.SetParent(shopContentGO.transform, false);
        var shopScrollbarRect = shopScrollbarGO.AddComponent<RectTransform>();
        shopScrollbarRect.anchorMin = new Vector2(1f, 0f);
        shopScrollbarRect.anchorMax = new Vector2(1f, 1f);
        shopScrollbarRect.pivot = new Vector2(1f, 0.5f);
        shopScrollbarRect.sizeDelta = new Vector2(16f, -24f);
        shopScrollbarRect.anchoredPosition = new Vector2(-6f, 0f);
        var shopScrollbarBgImg = shopScrollbarGO.AddComponent<Image>();
        shopScrollbarBgImg.color = new Color(0f, 0f, 0f, 0.35f);
        var shopScrollbar = shopScrollbarGO.AddComponent<Scrollbar>();
        shopScrollbar.direction = Scrollbar.Direction.BottomToTop;

        var shopHandleGO = new GameObject("Handle");
        shopHandleGO.transform.SetParent(shopScrollbarGO.transform, false);
        var shopHandleRect = shopHandleGO.AddComponent<RectTransform>();
        shopHandleRect.anchorMin = Vector2.zero;
        shopHandleRect.anchorMax = Vector2.one;
        shopHandleRect.offsetMin = Vector2.zero;
        shopHandleRect.offsetMax = Vector2.zero;
        var shopHandleImg = shopHandleGO.AddComponent<Image>();
        shopHandleImg.color = new Color(0.8f, 0.8f, 0.9f, 0.9f);
        shopScrollbar.targetGraphic = shopHandleImg;
        shopScrollbar.handleRect = shopHandleRect;

        var shopScrollRect = shopContentGO.AddComponent<ScrollRect>();
        shopScrollRect.viewport = shopViewportRect;
        shopScrollRect.content = shopGridRect;
        shopScrollRect.horizontal = false;
        shopScrollRect.vertical = true;
        shopScrollRect.movementType = ScrollRect.MovementType.Clamped;
        shopScrollRect.scrollSensitivity = 25f;
        shopScrollRect.verticalScrollbar = shopScrollbar;
        shopScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

        // 숫자가 적힌 2열 x 5행 갈색 버튼 10개 생성
        for (int i = 0; i < 10; i++)
        {
            var shopBtnGO = new GameObject($"ShopButton_{i + 1}");
            shopBtnGO.transform.SetParent(shopGridGO.transform, false);
            var shopBtnRect = shopBtnGO.AddComponent<RectTransform>();
            shopBtnRect.anchorMin = new Vector2(0.5f, 0.5f);
            shopBtnRect.anchorMax = new Vector2(0.5f, 0.5f);
            shopBtnRect.sizeDelta = shopGrid.cellSize;

            var shopBtnImg = shopBtnGO.AddComponent<Image>();
            // 버튼 자체는 더 진한 갈색
            shopBtnImg.color = new Color(0.42f, 0.29f, 0.18f, 1f);
            var shopBtn = shopBtnGO.AddComponent<Button>();
            shopBtn.targetGraphic = shopBtnImg;
            var shopColors = shopBtn.colors;
            shopColors.normalColor = Color.white;
            shopColors.highlightedColor = new Color(1.05f, 1.05f, 1.05f, 1f);
            shopColors.pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            shopColors.selectedColor = Color.white;
            shopColors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.7f);
            shopBtn.colors = shopColors;

            // 숫자 텍스트
            var shopTextGO = new GameObject("Text");
            shopTextGO.transform.SetParent(shopBtnGO.transform, false);
            var shopTextRect = shopTextGO.AddComponent<RectTransform>();
            shopTextRect.anchorMin = Vector2.zero;
            shopTextRect.anchorMax = Vector2.one;
            shopTextRect.offsetMin = Vector2.zero;
            shopTextRect.offsetMax = Vector2.zero;
            var shopTxt = shopTextGO.AddComponent<Text>();
            shopTxt.text = (i + 1).ToString();
            shopTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            shopTxt.fontSize = 28;
            shopTxt.alignment = TextAnchor.MiddleCenter;
            shopTxt.color = Color.white;
        }

        // 우측 상단 X 닫기 버튼 (Content 패널 안쪽)
        var shopCloseTopGO = new GameObject("CloseButtonTopRight");
        shopCloseTopGO.transform.SetParent(shopContentGO.transform, false);
        var shopCloseTopRect = shopCloseTopGO.AddComponent<RectTransform>();
        shopCloseTopRect.anchorMin = new Vector2(1f, 1f);
        shopCloseTopRect.anchorMax = new Vector2(1f, 1f);
        shopCloseTopRect.pivot = new Vector2(1f, 1f);
        shopCloseTopRect.sizeDelta = new Vector2(52f, 52f);
        shopCloseTopRect.anchoredPosition = new Vector2(-8f, -8f);
        var shopCloseTopImg = shopCloseTopGO.AddComponent<Image>();
        shopCloseTopImg.color = new Color(0.8f, 0.2f, 0.25f, 1f);
        var shopCloseTopBtn = shopCloseTopGO.AddComponent<Button>();
        var shopCloseColors = shopCloseTopBtn.colors;
        shopCloseColors.normalColor = shopCloseTopImg.color;
        shopCloseColors.highlightedColor = new Color(0.95f, 0.35f, 0.4f, 1f);
        shopCloseColors.pressedColor = new Color(0.6f, 0.15f, 0.18f, 1f);
        shopCloseColors.selectedColor = shopCloseColors.normalColor;
        shopCloseColors.disabledColor = new Color(0.3f, 0.1f, 0.1f, 0.7f);
        shopCloseTopBtn.colors = shopCloseColors;

        var shopCloseTopTextGO = new GameObject("Text");
        shopCloseTopTextGO.transform.SetParent(shopCloseTopGO.transform, false);
        var shopCloseTopTextRect = shopCloseTopTextGO.AddComponent<RectTransform>();
        shopCloseTopTextRect.anchorMin = Vector2.zero;
        shopCloseTopTextRect.anchorMax = Vector2.one;
        shopCloseTopTextRect.offsetMin = Vector2.zero;
        shopCloseTopTextRect.offsetMax = Vector2.zero;
        var shopCloseTopText = shopCloseTopTextGO.AddComponent<Text>();
        shopCloseTopText.text = "X";
        shopCloseTopText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        shopCloseTopText.fontSize = 30;
        shopCloseTopText.alignment = TextAnchor.MiddleCenter;
        shopCloseTopText.color = Color.white;

        // ShopPanel의 closeButton 필드 연결 및 Hide 연결
        var shopSoPanel = new SerializedObject(shopPanelComp);
        shopSoPanel.FindProperty("closeButton").objectReferenceValue = shopCloseTopBtn;
        shopSoPanel.ApplyModifiedPropertiesWithoutUndo();

        UnityAction shopCloseAction = shopPanelComp.Hide;
        UnityEventTools.AddPersistentListener(shopCloseTopBtn.onClick, shopCloseAction);
        EditorUtility.SetDirty(shopCloseTopBtn);

        shopPanelGO.SetActive(false);

        SetupLobbyLayout.ApplyToOpenScene();
    }

    /// <summary>
    /// 초상화 영역 placeholder를 씬에 추가. 에디터 모드에서 게임뷰에 위치 확인용으로 보임.
    /// Tools > Setup Scene Transitions 실행 시 또는 Inspector의 Placeholder 갱신 버튼에서 호출됨.
    /// </summary>
    public static void SetupPortraitPlaceholder(CharacterSelectionController ctrl)
    {
        var so = new SerializedObject(ctrl);
        var slotSizeProp = so.FindProperty("portraitSlotSize");
        var bgColorProp = so.FindProperty("portraitAreaBgColor");
        var charactersProp = so.FindProperty("characters");

        var slotSize = slotSizeProp != null ? slotSizeProp.vector2Value : new Vector2(120, 120);
        var bgColor = bgColorProp != null ? bgColorProp.colorValue : new Color(0.2f, 0.2f, 0.25f, 0.6f);
        int count = charactersProp != null && charactersProp.isArray ? Mathf.Max(2, charactersProp.arraySize) : 2;

        // 기존 placeholder 있으면 재사용
        var existingBg = ctrl.transform.Find("PortraitAreaBg");
        GameObject bgGO;
        RectTransform gridRect;

        if (existingBg != null)
        {
            bgGO = existingBg.gameObject;
            var grid = bgGO.transform.Find("PortraitGrid");
            gridRect = grid != null ? grid.GetComponent<RectTransform>() : null;
            if (gridRect == null)
            {
                var gridGO = new GameObject("PortraitGrid");
                gridGO.transform.SetParent(bgGO.transform, false);
                gridRect = gridGO.AddComponent<RectTransform>();
                gridRect.anchorMin = Vector2.zero;
                gridRect.anchorMax = Vector2.one;
                gridRect.offsetMin = new Vector2(12, 12);
                gridRect.offsetMax = new Vector2(-12, -12);
                var hlg = gridGO.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = 16;
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
            }
        }
        else
        {
            bgGO = new GameObject("PortraitAreaBg");
            bgGO.transform.SetParent(ctrl.transform, false);
            var bgRect = bgGO.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.2f);
            bgRect.anchorMax = new Vector2(0.35f, 0.8f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = bgColor;
            bgImg.raycastTarget = false;

            var gridGO = new GameObject("PortraitGrid");
            gridGO.transform.SetParent(bgGO.transform, false);
            gridRect = gridGO.AddComponent<RectTransform>();
            gridRect.anchorMin = Vector2.zero;
            gridRect.anchorMax = Vector2.one;
            gridRect.offsetMin = new Vector2(12, 12);
            gridRect.offsetMax = new Vector2(-12, -12);
            var hlg = gridGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 16;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
        }

        // 배경색 갱신
        var bgImage = bgGO.GetComponent<Image>();
        if (bgImage != null) bgImage.color = bgColor;

        // 기존 placeholder 슬롯 제거 후 재생성
        while (gridRect.childCount > 0)
            Object.DestroyImmediate(gridRect.GetChild(0).gameObject);

        for (int i = 0; i < count; i++)
        {
            var slotGO = new GameObject($"Portrait_{i}");
            slotGO.transform.SetParent(gridRect, false);
            var slotRect = slotGO.AddComponent<RectTransform>();
            slotRect.sizeDelta = slotSize;

            var le = slotGO.AddComponent<LayoutElement>();
            le.preferredWidth = slotSize.x;
            le.preferredHeight = slotSize.y;
            le.minWidth = slotSize.x;
            le.minHeight = slotSize.y;
            le.flexibleWidth = 0;
            le.flexibleHeight = 0;

            var img = slotGO.AddComponent<Image>();
            img.color = bgColor;
            img.raycastTarget = false;
        }

        so.FindProperty("portraitGridParent").objectReferenceValue = gridRect;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject CreateButton(string label, Transform parent, Vector2 position)
    {
        var go = new GameObject(label);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(300, 50);
        rect.anchoredPosition = position;

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
}
