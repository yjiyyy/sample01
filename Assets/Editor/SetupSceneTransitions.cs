using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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

    [MenuItem(MenuPath)]
    public static void Setup()
    {
        UpdateBuildSettings();
        SetupCharacterSelectionScene();
        SetupLobbyScene();
        AssetDatabase.Refresh();
        Debug.Log("[SetupSceneTransitions] Build Settings 및 씬 전환 설정 완료.");
    }

    /// <summary>
    /// Build Settings에 00_Title, 01_Story, 02_CharacterSelectionLevel, 03_Lobby, DemoScene 순서로 등록
    /// </summary>
    private static void UpdateBuildSettings()
    {
        var paths = new[]
        {
            $"{ScenesRoot}/00_Title.unity",
            $"{ScenesRoot}/01_Story.unity",
            $"{ScenesRoot}/02_CharacterSelectionLevel.unity",
            $"{ScenesRoot}/03_Lobby.unity",
            $"{ScenesRoot}/DemoScene.unity"
        };

        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>();
        foreach (var p in paths)
        {
            if (System.IO.File.Exists(p))
                list.Add(new EditorBuildSettingsScene(p, true));
        }

        EditorBuildSettings.scenes = list.ToArray();
        Debug.Log($"[SetupSceneTransitions] Build Settings에 {list.Count}개 씬 등록됨.");
    }

    /// <summary>
    /// Character Selection 씬에 Canvas, EventSystem, CharacterSelectionController, SpawnPoint 추가
    /// </summary>
    private static void SetupCharacterSelectionScene()
    {
        var path = $"{ScenesRoot}/02_CharacterSelectionLevel.unity";
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

        // 3D 스폰 포인트: 카메라 오른쪽에 더미 배치
        var spawnGO = GameObject.Find("CharacterSpawnPoint");
        if (spawnGO == null)
        {
            spawnGO = new GameObject("CharacterSpawnPoint");
            var pos = cam != null ? cam.transform.position + cam.transform.right * 2f + Vector3.forward * 3f : new Vector3(2f, 1f, 3f);
            spawnGO.transform.position = pos;
        }

        var canvasGO = GameObject.Find("Canvas");
        if (canvasGO == null)
        {
            canvasGO = new GameObject("Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();

            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                esGO.AddComponent<InputSystemUIInputModule>();
#endif
            }
        }

        var ctrlGO = GameObject.Find("CharacterSelectionController");
        CharacterSelectionController ctrl;
        if (ctrlGO == null)
        {
            ctrlGO = new GameObject("CharacterSelectionController");
            ctrlGO.transform.SetParent(canvasGO.transform, false);
            var rect = ctrlGO.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            ctrl = ctrlGO.AddComponent<CharacterSelectionController>();
            var so = new SerializedObject(ctrl);
            so.FindProperty("nextScene").stringValue = "03_Lobby";
            so.FindProperty("spawnPoint").objectReferenceValue = spawnGO.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 확인 버튼 (하단 중앙)
            var btnGO = CreateButton("Confirm", ctrlGO.transform, new Vector2(0, -300));
            var btn = btnGO.GetComponent<Button>();
            so.FindProperty("confirmButton").objectReferenceValue = btn;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            ctrl = ctrlGO.GetComponent<CharacterSelectionController>();
        }

        if (ctrl != null)
        {
            SetupPortraitPlaceholder(ctrl);
            var so = new SerializedObject(ctrl);
            if (so.FindProperty("spawnPoint").objectReferenceValue == null && spawnGO != null)
                so.FindProperty("spawnPoint").objectReferenceValue = spawnGO.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
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

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SetupSceneTransitions] 로비 씬 설정 완료.");
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
