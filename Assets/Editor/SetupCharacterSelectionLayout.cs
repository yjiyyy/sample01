using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// 02_CharacterSelectionLevel UI 레이아웃을 참고 이미지 구조로 구성합니다.
/// 메뉴: Tools → Setup Character Selection Layout
/// </summary>
public static class SetupCharacterSelectionLayout
{
    private const string MenuPath = "Tools/Setup Character Selection Layout";
    private const string RemoveNavButtonsMenuPath = "Tools/Remove Character Selection Nav Buttons";
    private const string ScenePath = "Assets/Scenes/02_CharacterSelectionLevel.unity";
    private const string AutoRunFlagPath = "Assets/Editor/SetupCharacterSelectionLayout.run";
    private const string FontPath = "Assets/Arts/Fonts/BlackHanSans-Regular SDF.asset";
    private const string CharacterDataRoot = "Assets/Data/PlayerSelect";
    private const string MeleeIconPath = "Assets/Arts/UI/CharacterSelectScreen/Icon_Melee.Png";
    private const string RangedIconPath = "Assets/Arts/UI/CharacterSelectScreen/Icon_Ranged.Png";

    private const int SlotCount = 5;
    private const int StatSegmentCount = 5;

    private static readonly string[] StatRowNames =
    {
        "HpRow", "StRow", "SpdRow", "StrRow", "MeleeAtkRow", "RangedAtkRow"
    };

    private static readonly string[] StatRowLabels =
    {
        "HP", "ST", "SPD", "STR", "Melee ATK", "Ranged ATK"
    };

    private static readonly string[] StatUiPropertyNames =
    {
        "hpRow", "stRow", "spdRow", "strRow", "meleeAtkRow", "rangedAtkRow"
    };

    private static readonly Color BgPlaceholder = new Color(0.1f, 0.07f, 0.14f, 1f);
    private static readonly Color IllustrationPlaceholder = new Color(0.22f, 0.32f, 0.52f, 0.38f);
    private static readonly Color SlotPlaceholder = new Color(0.18f, 0.18f, 0.22f, 0.92f);
    private static readonly Color SelectFrameColor = new Color(1f, 0.18f, 0.58f, 1f);
    private static readonly Color BottomBarColor = new Color(0.04f, 0.04f, 0.05f, 0.96f);
    private static readonly Color StatFilled = new Color(1f, 0.28f, 0.55f, 1f);
    private static readonly Color StatEmpty = new Color(0.28f, 0.28f, 0.32f, 0.55f);

    [InitializeOnLoadMethod]
    private static void AutoRunIfFlagExists()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(AutoRunFlagPath))
                return;

            try { File.Delete(AutoRunFlagPath); }
            catch { /* ignore */ }

            Setup();
        };
    }

    [MenuItem(RemoveNavButtonsMenuPath)]
    public static void RemoveNavButtons()
    {
        RemoveCarouselNavButtons();
        ClearNavButtonReferences();

        var scene = EditorSceneManager.GetActiveScene();
        if (scene.IsValid())
            EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log("[SetupCharacterSelectionLayout] PrevButton / NextButton 제거 완료.");
    }

    [MenuItem(MenuPath)]
    public static void Setup()
    {
        if (!File.Exists(ScenePath))
        {
            Debug.LogError("[SetupCharacterSelectionLayout] 씬을 찾지 못했습니다: " + ScenePath);
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ApplyToOpenScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[SetupCharacterSelectionLayout] 02_CharacterSelectionLevel 레이아웃 구성 완료.");
    }

    public static void ApplyToOpenScene()
    {
        EnsureMainCamera();
        EnsureEventSystem();
        var spawnPoint = EnsureCharacterSpawnPoint();
        RemoveCarouselNavButtons();

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        var canvasGo = EnsureCanvas(out var canvasRect);
        CleanupOldUi(canvasRect);

        var backgroundCanvasRect = EnsureBackgroundCanvas(out _);
        var background = CreateStretchImage("Background", backgroundCanvasRect, BgPlaceholder);
        Stretch(background.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

        var chrome = CreateUiObject("CharacterSelectionChrome", canvasRect);
        Stretch(chrome.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

        // 2D 일러스트는 배경 캔버스에 두어 3D 캐릭터보다 뒤에 보이게 합니다.
        var illustration = CreateStretchImage("CharacterIllustration", backgroundCanvasRect, IllustrationPlaceholder);
        var illRect = illustration.GetComponent<RectTransform>();
        illRect.anchorMin = new Vector2(0.18f, 0.22f);
        illRect.anchorMax = new Vector2(0.72f, 0.82f);
        illRect.offsetMin = Vector2.zero;
        illRect.offsetMax = Vector2.zero;
        illRect.localPosition = new Vector3(illRect.localPosition.x, illRect.localPosition.y, 0f);
        var illImg = illustration.GetComponent<Image>();
        illImg.preserveAspect = true;
        illustration.SetActive(true);
        illustration.transform.SetAsLastSibling();

        // 이전 폭의 약 절반, 오른쪽 모서리에 붙임 (예전: 0.56~0.97 → 지금: 0.80~0.99)
        var rightPanel = CreateUiObject("RightPanel", chrome.transform);
        var rightRect = rightPanel.GetComponent<RectTransform>();
        rightRect.anchorMin = new Vector2(0.80f, 0.22f);
        rightRect.anchorMax = new Vector2(0.99f, 0.82f);
        rightRect.offsetMin = Vector2.zero;
        rightRect.offsetMax = Vector2.zero;

        var nameText = CreateTmpLabel("NameText", rightPanel.transform, font, "CHARACTER", 56f,
            TextAlignmentOptions.TopLeft, new Color(1f, 0.45f, 0.75f, 1f));
        var nameRect = nameText.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0.86f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.offsetMin = Vector2.zero;
        nameRect.offsetMax = Vector2.zero;

        var statsRoot = CreateUiObject("Stats", rightPanel.transform);
        Stretch(statsRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
        var statsRect = statsRoot.GetComponent<RectTransform>();
        statsRect.anchorMin = new Vector2(0f, 0.22f);
        statsRect.anchorMax = new Vector2(1f, 0.84f);
        statsRect.offsetMin = Vector2.zero;
        statsRect.offsetMax = Vector2.zero;

        var statsLayout = statsRoot.AddComponent<VerticalLayoutGroup>();
        statsLayout.spacing = 6f;
        statsLayout.padding = new RectOffset(0, 0, 4, 4);
        statsLayout.childAlignment = TextAnchor.UpperLeft;
        statsLayout.childControlWidth = true;
        statsLayout.childControlHeight = true;
        statsLayout.childForceExpandWidth = true;
        statsLayout.childForceExpandHeight = false;

        var statRows = new GameObject[StatRowNames.Length];
        var meleeIcon = AssetDatabase.LoadAssetAtPath<Sprite>(MeleeIconPath);
        var rangedIcon = AssetDatabase.LoadAssetAtPath<Sprite>(RangedIconPath);
        for (int i = 0; i < StatRowNames.Length; i++)
        {
            Sprite icon = null;
            if (StatRowNames[i] == "MeleeAtkRow")
                icon = meleeIcon;
            else if (StatRowNames[i] == "RangedAtkRow")
                icon = rangedIcon;

            statRows[i] = CreateStatRow(statsRoot.transform, font, StatRowNames[i], StatRowLabels[i], StatFilled, icon);
        }

        var descriptionText = CreateTmpLabel("DescriptionText", rightPanel.transform, font,
            "캐릭터 설명이 여기에 표시됩니다.", 22f, TextAlignmentOptions.TopLeft, new Color(1f, 1f, 1f, 0.88f));
        var descRect = descriptionText.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 0f);
        descRect.anchorMax = new Vector2(1f, 0.2f);
        descRect.offsetMin = Vector2.zero;
        descRect.offsetMax = Vector2.zero;
        descriptionText.textWrappingMode = TextWrappingModes.Normal;
        descriptionText.overflowMode = TextOverflowModes.Ellipsis;

        var carousel = CreateUiObject("BottomCarousel", chrome.transform);
        var carouselRect = carousel.GetComponent<RectTransform>();
        carouselRect.anchorMin = new Vector2(0.16f, 0.14f);
        carouselRect.anchorMax = new Vector2(0.84f, 0.38f);
        carouselRect.offsetMin = Vector2.zero;
        carouselRect.offsetMax = Vector2.zero;

        var slotRow = CreateUiObject("SlotRow", carousel.transform);
        var slotRowRect = slotRow.GetComponent<RectTransform>();
        slotRowRect.anchorMin = new Vector2(0f, 0f);
        slotRowRect.anchorMax = new Vector2(1f, 1f);
        slotRowRect.offsetMin = Vector2.zero;
        slotRowRect.offsetMax = Vector2.zero;

        var slotLayout = slotRow.AddComponent<HorizontalLayoutGroup>();
        slotLayout.spacing = 18f;
        slotLayout.childAlignment = TextAnchor.MiddleCenter;
        slotLayout.childControlWidth = false;
        slotLayout.childControlHeight = false;
        slotLayout.childForceExpandWidth = false;
        slotLayout.childForceExpandHeight = false;

        var slotObjects = new GameObject[SlotCount];
        for (int i = 0; i < SlotCount; i++)
            slotObjects[i] = CreateCarouselSlot(slotRow.transform, i);

        var bottomBar = CreateUiObject("BottomBar", chrome.transform);
        var barRect = bottomBar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0.1f);
        barRect.offsetMin = Vector2.zero;
        barRect.offsetMax = Vector2.zero;
        var barBg = bottomBar.AddComponent<Image>();
        barBg.color = BottomBarColor;
        barBg.raycastTarget = true;

        var returnButton = CreateBarButton("ReturnButton", bottomBar.transform, font, "RETURN",
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(48f, 0f), new Vector2(260f, 64f),
            TextAlignmentOptions.MidlineLeft);
        var confirmButton = CreateBarButton("ConfirmButton", bottomBar.transform, font, "CONFIRM",
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-48f, 0f), new Vector2(260f, 64f),
            TextAlignmentOptions.MidlineRight);

        var controllerGo = GameObject.Find("CharacterSelectionController");
        if (controllerGo == null)
            controllerGo = new GameObject("CharacterSelectionController");

        if (controllerGo.transform.parent != null)
            controllerGo.transform.SetParent(null, false);

        var controller = controllerGo.GetComponent<CharacterSelectionController>();
        if (controller == null)
            controller = controllerGo.AddComponent<CharacterSelectionController>();

        var scenePreview = controllerGo.GetComponent<CharacterSelectionScenePreview>();
        if (scenePreview == null)
            scenePreview = controllerGo.AddComponent<CharacterSelectionScenePreview>();

        var layeringDriver = controllerGo.GetComponent<CharacterSelectionCanvasLayeringDriver>();
        if (layeringDriver == null)
            layeringDriver = controllerGo.AddComponent<CharacterSelectionCanvasLayeringDriver>();

        var ui = chrome.GetComponent<CharacterSelectionUI>();
        if (ui == null)
            ui = chrome.AddComponent<CharacterSelectionUI>();

        WireController(controller, ui, LoadCharacterAssets(), spawnPoint);
        WireScenePreview(scenePreview, spawnPoint);
        WireUi(ui, illustration.GetComponent<Image>(), nameText, descriptionText,
            statRows, slotObjects, returnButton, confirmButton);

        // Play 전 Scene View에서도 런타임과 같은 캔버스 깊이가 보이게 적용합니다.
        CharacterSelectionCanvasLayering.Apply(Camera.main);

        EditorUtility.SetDirty(canvasGo);
        EditorUtility.SetDirty(controllerGo);
        EditorUtility.SetDirty(chrome);
    }

    private static void WireController(
        CharacterSelectionController controller,
        CharacterSelectionUI ui,
        CharacterDataSO[] characters,
        Transform spawnPoint)
    {
        var so = new SerializedObject(controller);
        var charsProp = so.FindProperty("characters");
        charsProp.arraySize = SlotCount;
        for (int i = 0; i < SlotCount; i++)
            charsProp.GetArrayElementAtIndex(i).objectReferenceValue = i < characters.Length ? characters[i] : null;

        so.FindProperty("nextScene").stringValue = SceneNames.Lobby;
        so.FindProperty("returnScene").stringValue = SceneNames.Lobby;
        so.FindProperty("ui").objectReferenceValue = ui;
        so.FindProperty("characterSpawnPoint").objectReferenceValue = spawnPoint;
        so.FindProperty("selectAnimStateName").stringValue = CharacterSelectionController.SelectAnimStateName;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
    }

    private static void WireScenePreview(CharacterSelectionScenePreview preview, Transform spawnPoint)
    {
        if (preview == null)
            return;

        var so = new SerializedObject(preview);
        so.FindProperty("spawnPoint").objectReferenceValue = spawnPoint;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(preview);
    }

    private static void WireUi(CharacterSelectionUI ui, Image illustration, TextMeshProUGUI nameText,
        TextMeshProUGUI descriptionText, GameObject[] statRows, GameObject[] slotObjects,
        Button returnButton, Button confirmButton)
    {
        var so = new SerializedObject(ui);
        so.FindProperty("illustrationImage").objectReferenceValue = illustration;
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("descriptionText").objectReferenceValue = descriptionText;
        so.FindProperty("returnButton").objectReferenceValue = returnButton;
        so.FindProperty("confirmButton").objectReferenceValue = confirmButton;

        for (int i = 0; i < StatUiPropertyNames.Length && i < statRows.Length; i++)
            WireStatRow(so.FindProperty(StatUiPropertyNames[i]), statRows[i]);

        var slotsProp = so.FindProperty("slots");
        slotsProp.arraySize = SlotCount;
        for (int i = 0; i < SlotCount; i++)
        {
            var slotTf = slotObjects[i].transform;
            var entry = slotsProp.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("button").objectReferenceValue = slotTf.GetComponent<Button>();
            entry.FindPropertyRelative("portrait").objectReferenceValue = slotTf.Find("Portrait")?.GetComponent<Image>();
            entry.FindPropertyRelative("selectFrame").objectReferenceValue = null;
            var lockTf = slotTf.Find("LockOverlay");
            entry.FindPropertyRelative("lockOverlay").objectReferenceValue = lockTf != null ? lockTf.gameObject : null;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ui);
    }

    private static void WireStatRow(SerializedProperty rowProp, GameObject rowGo)
    {
        var segments = rowProp.FindPropertyRelative("segments");
        segments.arraySize = StatSegmentCount;
        var bar = rowGo.transform.Find("Bar");
        for (int i = 0; i < StatSegmentCount; i++)
        {
            var segTf = bar != null ? bar.Find($"Segment_{i:00}") : null;
            segments.GetArrayElementAtIndex(i).objectReferenceValue =
                segTf != null ? segTf.GetComponent<Image>() : null;
        }
    }

    private static CharacterDataSO[] LoadCharacterAssets()
    {
        var guids = AssetDatabase.FindAssets("t:CharacterDataSO", new[] { CharacterDataRoot });
        var list = new CharacterDataSO[SlotCount];
        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var asset = AssetDatabase.LoadAssetAtPath<CharacterDataSO>(path);
            if (asset == null)
                continue;

            // 001_ ~ 005_ 파일명 기준으로 슬롯에 넣습니다.
            for (int slot = 0; slot < SlotCount; slot++)
            {
                string prefix = $"{slot + 1:000}_";
                if (asset.name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                {
                    list[slot] = asset;
                    break;
                }
            }
        }
        return list;
    }

    private static GameObject CreateCarouselSlot(Transform parent, int index)
    {
        var slot = CreateUiObject($"Slot_{index:00}", parent);
        var le = slot.AddComponent<LayoutElement>();
        le.preferredWidth = 132f;
        le.preferredHeight = 168f;
        le.minWidth = 132f;
        le.minHeight = 168f;

        var btn = slot.AddComponent<Button>();
        var btnImg = slot.AddComponent<Image>();
        btnImg.color = SlotPlaceholder;
        btn.targetGraphic = btnImg;

        var portrait = CreateStretchImage("Portrait", slot.transform, Color.white);
        var portraitRect = portrait.GetComponent<RectTransform>();
        portraitRect.offsetMin = new Vector2(8f, 8f);
        portraitRect.offsetMax = new Vector2(-8f, -8f);
        portrait.GetComponent<Image>().preserveAspect = true;
        portrait.GetComponent<Image>().raycastTarget = false;

        var lockOverlay = CreateUiObject("LockOverlay", slot.transform);
        Stretch(lockOverlay.GetComponent<RectTransform>(), new Vector2(6f, 6f), new Vector2(6f, 6f));
        var lockBg = lockOverlay.AddComponent<Image>();
        lockBg.color = new Color(0f, 0f, 0f, 0.55f);
        lockBg.raycastTarget = false;

        var lockTextGo = CreateUiObject("LockIcon", lockOverlay.transform);
        Stretch(lockTextGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
        var lockTmp = lockTextGo.AddComponent<TextMeshProUGUI>();
        lockTmp.text = "🔒";
        lockTmp.fontSize = 42f;
        lockTmp.alignment = TextAlignmentOptions.Center;
        lockTmp.raycastTarget = false;
        lockOverlay.SetActive(false);

        return slot;
    }

    private static GameObject CreateStatRow(
        Transform parent,
        TMP_FontAsset font,
        string name,
        string label,
        Color filledPreview,
        Sprite labelIcon = null)
    {
        var row = CreateUiObject(name, parent);
        var rowLe = row.AddComponent<LayoutElement>();
        rowLe.minHeight = 36f;
        rowLe.preferredHeight = 40f;
        rowLe.flexibleWidth = 1f;

        if (labelIcon != null)
        {
            var iconGo = CreateUiObject("LabelIcon", row.transform);
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.1f);
            iconRect.anchorMax = new Vector2(0.28f, 0.9f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite = labelIcon;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            iconImg.color = Color.white;
        }
        else
        {
            var labelTmp = CreateTmpLabel("Label", row.transform, font, label, 22f, TextAlignmentOptions.MidlineLeft, Color.white);
            var labelRect = labelTmp.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(0.34f, 1f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        var bar = CreateUiObject("Bar", row.transform);
        var barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0.36f, 0.15f);
        barRect.anchorMax = new Vector2(1f, 0.85f);
        barRect.offsetMin = Vector2.zero;
        barRect.offsetMax = Vector2.zero;

        var hlg = bar.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        for (int i = 0; i < StatSegmentCount; i++)
        {
            var seg = CreateUiObject($"Segment_{i:00}", bar.transform);
            var segImg = seg.AddComponent<Image>();
            segImg.color = i < 2 ? filledPreview : StatEmpty;
            segImg.raycastTarget = false;
            var segLe = seg.AddComponent<LayoutElement>();
            segLe.flexibleWidth = 1f;
            segLe.minHeight = 18f;
        }

        return row;
    }

    private static Button CreateArrowButton(string name, Transform parent, TMP_FontAsset font, string label,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
    {
        var go = CreateUiObject(name, parent);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = anchorMin.x > 0.5f ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPos;

        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.08f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var textGo = CreateUiObject("Text", go.transform);
        Stretch(textGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = label;
        tmp.fontSize = 36f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return btn;
    }

    private static Button CreateBarButton(string name, Transform parent, TMP_FontAsset font, string label,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, TextAlignmentOptions align)
    {
        var go = CreateUiObject(name, parent);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = anchorMin.x > 0.5f ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPos;

        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var textGo = CreateUiObject("Text", go.transform);
        Stretch(textGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = label;
        tmp.fontSize = 32f;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return btn;
    }

    private static GameObject CreateStretchImage(string name, Transform parent, Color color)
    {
        var go = CreateUiObject(name, parent);
        Stretch(go.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return go;
    }

    private static TextMeshProUGUI CreateTmpLabel(string name, Transform parent, TMP_FontAsset font, string text,
        float fontSize, TextAlignmentOptions align, Color color)
    {
        var go = CreateUiObject(name, parent);
        Stretch(go.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static GameObject EnsureCanvas(out RectTransform canvasRect)
    {
        var canvasGo = GameObject.Find(CharacterSelectionCanvasLayering.ForegroundCanvasName);
        if (canvasGo == null)
            canvasGo = GameObject.Find("Canvas");
        if (canvasGo == null)
            canvasGo = new GameObject(CharacterSelectionCanvasLayering.ForegroundCanvasName);

        canvasGo.name = CharacterSelectionCanvasLayering.ForegroundCanvasName;
        canvasGo.layer = 5;

        var cam = Camera.main;
        var canvas = canvasGo.GetComponent<Canvas>();
        if (canvas == null)
            canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = CharacterSelectionCanvasLayering.ForegroundPlaneDistance;
        canvas.sortingOrder = CharacterSelectionCanvasLayering.ForegroundSortOrder;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        if (canvasGo.GetComponent<GraphicRaycaster>() == null)
            canvasGo.AddComponent<GraphicRaycaster>();

        canvasRect = canvasGo.GetComponent<RectTransform>();
        if (canvasRect == null)
            canvasRect = canvasGo.AddComponent<RectTransform>();
        Stretch(canvasRect, Vector2.zero, Vector2.zero);

        canvas.additionalShaderChannels =
            AdditionalCanvasShaderChannels.TexCoord1 |
            AdditionalCanvasShaderChannels.Normal |
            AdditionalCanvasShaderChannels.Tangent;

        return canvasGo;
    }

    private static RectTransform EnsureBackgroundCanvas(out Canvas canvas)
    {
        var canvasGo = GameObject.Find(CharacterSelectionCanvasLayering.BackgroundCanvasName);
        if (canvasGo == null)
            canvasGo = new GameObject(CharacterSelectionCanvasLayering.BackgroundCanvasName);

        canvasGo.layer = 5;
        canvas = canvasGo.GetComponent<Canvas>();
        if (canvas == null)
            canvas = canvasGo.AddComponent<Canvas>();

        var cam = Camera.main;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = CharacterSelectionCanvasLayering.ResolveBackgroundPlaneDistance(cam);
        canvas.sortingOrder = CharacterSelectionCanvasLayering.BackgroundSortOrder;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var raycaster = canvasGo.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
            raycaster = canvasGo.AddComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        var rect = canvasGo.GetComponent<RectTransform>();
        if (rect == null)
            rect = canvasGo.AddComponent<RectTransform>();
        Stretch(rect, Vector2.zero, Vector2.zero);
        return rect;
    }

    private static void EnsureMainCamera()
    {
        var cam = Camera.main;
        if (cam != null)
            return;

        var camGo = GameObject.Find("Main Camera") ?? new GameObject("Main Camera");
        cam = camGo.GetComponent<Camera>() ?? camGo.AddComponent<Camera>();
        if (camGo.GetComponent<AudioListener>() == null)
            camGo.AddComponent<AudioListener>();
        camGo.tag = "MainCamera";
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
            return;

        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        esGo.AddComponent<InputSystemUIInputModule>();
#else
        esGo.AddComponent<StandaloneInputModule>();
#endif
    }

    private static Transform EnsureCharacterSpawnPoint()
    {
        DestroyIfExists("Cube");

        var spawnGo = GameObject.Find("CharacterSpawnPoint");
        if (spawnGo == null)
            spawnGo = new GameObject("CharacterSpawnPoint");

        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            forward.Normalize();

            Vector3 pos = cam.transform.position + forward * 3.2f;
            pos.y = 0f;
            spawnGo.transform.position = pos;
            spawnGo.transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);
        }
        else
        {
            spawnGo.transform.position = Vector3.zero;
            spawnGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        }

        EditorUtility.SetDirty(spawnGo);
        return spawnGo.transform;
    }

    private static void CleanupOldUi(RectTransform canvasRect)
    {
        DestroyNamedChild(canvasRect, "CharacterSelectionChrome");
        DestroyNamedChild(canvasRect, "PortraitAreaBg");

        var bgCanvas = GameObject.Find(CharacterSelectionCanvasLayering.BackgroundCanvasName);
        if (bgCanvas != null)
        {
            for (int i = bgCanvas.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(bgCanvas.transform.GetChild(i).gameObject);
        }

        DestroyIfExists("CharacterModelStage");

        var ctrl = canvasRect.Find("CharacterSelectionController");
        if (ctrl != null)
        {
            Object.DestroyImmediate(ctrl.gameObject);
        }

        var legacyCtrl = GameObject.Find("CharacterSelectionController");
        if (legacyCtrl != null)
        {
            for (int i = legacyCtrl.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(legacyCtrl.transform.GetChild(i).gameObject);

            var legacyRect = legacyCtrl.GetComponent<RectTransform>();
            if (legacyRect != null)
                Object.DestroyImmediate(legacyRect);
        }

        var confirm = canvasRect.Find("Confirm");
        if (confirm != null)
            Object.DestroyImmediate(confirm.gameObject);
    }

    private static void RemoveCarouselNavButtons()
    {
        DestroyIfExists("PrevButton");
        DestroyIfExists("NextButton");
    }

    private static void ClearNavButtonReferences()
    {
        var ui = Object.FindFirstObjectByType<CharacterSelectionUI>();
        if (ui == null)
            return;

        var so = new SerializedObject(ui);
        var prevProp = so.FindProperty("prevButton");
        if (prevProp != null)
            prevProp.objectReferenceValue = null;
        var nextProp = so.FindProperty("nextButton");
        if (nextProp != null)
            nextProp.objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ui);
    }

    private static void DestroyIfExists(string objectName)
    {
        var go = GameObject.Find(objectName);
        if (go != null)
            Object.DestroyImmediate(go);
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = -offsetMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localPosition = new Vector3(rect.localPosition.x, rect.localPosition.y, 0f);
    }

    private static void DestroyNamedChild(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null)
            Object.DestroyImmediate(child.gameObject);
    }
}
