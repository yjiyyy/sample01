using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 하나의 고해상도 세계지도를 확대/이동해 스테이지를 보여주는 선택 화면입니다.
/// </summary>
public sealed class StageSelectPrototype : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Serializable]
    public sealed class StageEntry
    {
        public string displayName;
        public string objective;
        public bool unlocked;
        public bool completed;
        [Tooltip("START MISSION 버튼으로 이동할 씬을 드래그해 지정합니다.")]
        [SceneName] public string targetSceneName;
        [Range(0f, 1f)] public float mapX = .5f;
        [Range(0f, 1f)] public float mapY = .5f;
        [Range(1.5f, 4f)] public float focusZoom = 2.8f;
        public Sprite preview;

        public Vector2 MapPosition
        {
            get => new(mapX, mapY);
            set { mapX = Mathf.Clamp01(value.x); mapY = Mathf.Clamp01(value.y); }
        }
    }
    private static readonly Color Red = new(0.94f, 0.025f, 0.06f, 1f);
    private static readonly Color Yellow = new(1f, 0.91f, 0.015f, 1f);
    private static readonly Color Ink = new(0.035f, 0.04f, 0.045f, 1f);
    private static readonly Color Paper = new(0.972f, 0.977f, 0.982f, 1f);
    private const float TransitionDuration = 0.42f;
    private const float OverviewZoom = 1.22f;
    private const int DefaultStageCount = 22;

    private static readonly string[] StageNames =
    {
        "NO KINGS PROTEST", "PIZZA SHOP", "TEXAS CLIMATE CRISIS", "NOBEL CEREMONY",
        "SILICON VALLEY", "HALFTIME STADIUM", "BIRTH OF A NATION", "U.S. CAPITOL",
        "GREENLAND", "FBI HEADQUARTERS", "STRAIT OF HORMUZ", "GAZA STRIP"
    };

    private static readonly string[] Objectives =
    {
        "REACH THE MAIN PLAZA", "ENTER THE PIZZA SHOP", "SURVIVE THE HEAT WAVE", "REACH THE CEREMONY",
        "REACH THE MAIN CAMPUS", "ENTER THE STADIUM", "CROSS THE FILM SET", "REACH THE CAPITOL",
        "CROSS THE ICE FIELD", "REACH THE HEADQUARTERS", "SECURE THE STRAIT", "REACH THE SAFE ZONE"
    };

    // 세계지도 이미지에서의 스테이지 위치입니다. 0~1 범위이며 좌측 하단이 원점입니다.
    private static readonly Vector2[] PinPositions =
    {
        new(.1065f,.5749f), new(.2392f,.6174f), new(.1615f,.5590f), new(.5054f,.7450f),
        new(.0975f,.6121f), new(.1914f,.5590f), new(.2183f,.5802f), new(.2392f,.6174f),
        new(.350f,.805f), new(.2392f,.6174f), new(.6250f,.5537f), new(.5694f,.5802f)
    };

    // 같은 도시에 핀이 몰린 경우 월드 뷰에서만 화면상 간격을 벌립니다.
    // 확대 뷰에서는 이 값을 제거해 실제 지역 중심에 핀이 오도록 합니다.
    private static readonly Vector2[] WorldPinOffsets =
    {
        Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero,
        Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero,
        Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero
    };

    // 지역마다 필요한 확대 정도를 따로 조정합니다. 핀이 몰린 워싱턴 지역은 더 크게 확대합니다.
    private static readonly float[] StageZooms =
    {
        2.75f, 3.15f, 2.8f, 2.65f, 2.9f, 2.7f,
        2.75f, 3.2f, 2.35f, 3.2f, 2.8f, 3.0f
    };

    [SerializeField] private int selectedStage = 5;
    [SerializeField] private bool focused;
    [SerializeField] private bool hasSelection;
    [Header("Stage Setup")]
    [SerializeField, Min(1)] private int stageCount = DefaultStageCount;
    [SerializeField] private List<StageEntry> stages = new();
    [SerializeField] private Font uiFont;
    [SerializeField] private Font stageListFont;
    [SerializeField] private Sprite worldMapSprite;
    [SerializeField] private Sprite[] stagePreviewSprites = new Sprite[12];
    [SerializeField] private Sprite normalPinSprite;
    [SerializeField] private Sprite selectedPinSprite;
    [SerializeField] private Sprite completedPinSprite;
    [SerializeField] private Sprite popupFrameSprite;
    [SerializeField] private Sprite startButtonSprite;
    [SerializeField] private Sprite startButtonHighlightedSprite;
    [SerializeField] private Sprite startButtonPressedSprite;
    [SerializeField] private Sprite clearedBadgeSprite;
    [SerializeField] private Sprite worldViewButtonSprite;
    [SerializeField] private Sprite worldViewGlobeSprite;
    [SerializeField] private Sprite titleSprite;
    [Header("World View Button")]
    [SerializeField, Range(.5f, 2f)] private float worldViewButtonScale = 1.15f;
    [SerializeField, Range(.25f, 2f)] private float worldViewGlobeScale = 1.2f;
    [SerializeField] private Sprite stageListFrameSprite;
    [SerializeField] private Sprite stageListHeaderSprite;
    [SerializeField] private Sprite normalRowSprite;
    [SerializeField] private Sprite selectedRowOverlaySprite;
    [SerializeField] private Sprite scrollArrowSprite;
    [SerializeField] private Sprite scrollTrackSprite;
    [SerializeField] private Sprite stageTabSprite;

    [SerializeField] private RectTransform mapViewport;
    [SerializeField] private RectTransform mapContent;
    [SerializeField] private RectTransform popupRoot;
    [SerializeField] private RectTransform worldViewButtonRoot;
    [SerializeField] private ScrollRect stageListScroll;
    [SerializeField] private Button scrollUpButton;
    [SerializeField] private Button scrollDownButton;
    [SerializeField] private CanvasGroup popupGroup;
    [SerializeField] private StageSelectPulseGraphic selectionPulse;
    [SerializeField] private Image popupPreview;
    [SerializeField] private Text popupStage;
    [SerializeField] private Text popupTitle;
    [SerializeField] private Text popupObjective;

    private readonly List<Button> mapPins = new();
    private readonly List<Button> listButtons = new();
    private Coroutine transitionRoutine;
    private bool draggingMap;
    private Vector2 lastDragPosition;
    private float dragVelocity;
    private Scrollbar stageScrollbar;
    private float listLayoutHeight;

    public int StageCount => stageCount;
    public IReadOnlyList<StageEntry> Stages => stages;

    public void ConfigureAssets(Sprite mapSprite, Sprite[] previews,
        Sprite normalPin, Sprite selectedPin, Sprite completedPin,
        Sprite popupFrame, Sprite startButton, Sprite clearedBadge,
        Sprite worldViewButton, Sprite worldViewGlobe, Sprite title, Sprite stageListFrame, Sprite stageListHeader,
        Sprite normalRow, Sprite selectedRowOverlay, Sprite scrollArrow,
        Font condensedListFont)
    {
        worldMapSprite = mapSprite;
        normalPinSprite = normalPin;
        selectedPinSprite = selectedPin;
        completedPinSprite = completedPin;
        popupFrameSprite = popupFrame;
        startButtonSprite = startButton;
        clearedBadgeSprite = clearedBadge;
        worldViewButtonSprite = worldViewButton;
        worldViewGlobeSprite = worldViewGlobe;
        titleSprite = title;
        stageListFrameSprite = stageListFrame;
        stageListHeaderSprite = stageListHeader;
        normalRowSprite = normalRow;
        selectedRowOverlaySprite = selectedRowOverlay;
        scrollArrowSprite = scrollArrow;
        stageListFont = condensedListFont;
        stagePreviewSprites = new Sprite[12];
        if (previews != null)
            Array.Copy(previews, stagePreviewSprites, Mathf.Min(previews.Length, stagePreviewSprites.Length));
        EnsureStageData();
    }

    private void OnEnable()
    {
        // 월드 뷰에서는 이전에 선택했던 스테이지 번호만 기억하고 선택 표시는 지웁니다.
        if (!focused)
            hasSelection = false;

        if (transform.Find("StageSelectRoot") == null)
            Rebuild();
        else
        {
            CacheReferences();
            WireButtons();
            RefreshStaticVisuals();
            ApplyViewImmediate();
        }
    }

    [ContextMenu("Rebuild Stage Select UI")]
    public void Rebuild()
    {
        EnsureStageData();
        if (!focused)
            hasSelection = false;
        RectTransform previousTitle = transform.Find("StageSelectRoot/TitleGraphic") as RectTransform;
        Vector2[] titleLayout = previousTitle == null ? null : new[]
        {
            previousTitle.anchorMin, previousTitle.anchorMax, previousTitle.pivot,
            previousTitle.anchoredPosition, previousTitle.sizeDelta
        };
        Quaternion titleRotation = previousTitle != null ? previousTitle.localRotation : Quaternion.identity;
        Vector3 titleScale = previousTitle != null ? previousTitle.localScale : Vector3.one;
        Transform old = transform.Find("StageSelectRoot");
        if (old != null)
        {
            if (Application.isPlaying) Destroy(old.gameObject);
            else DestroyImmediate(old.gameObject);
        }

        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        RectTransform root = Panel("StageSelectRoot", transform, Paper, Vector2.zero, Vector2.one);
        CreateMap(root);
        CreateStageList(root);
        CreateTitleGraphic(root);
        // 사용자가 직접 조정한 제목 위치와 크기는 스타일 재적용 시에도 유지합니다.
        if (titleLayout != null && root.Find("TitleGraphic") is RectTransform title)
        {
            title.anchorMin = titleLayout[0];
            title.anchorMax = titleLayout[1];
            title.pivot = titleLayout[2];
            title.anchoredPosition = titleLayout[3];
            title.sizeDelta = titleLayout[4];
            title.localRotation = titleRotation;
            title.localScale = titleScale;
        }
        AddDecor(root);

        EnsureEventSystem();
        CacheReferences();
        WireButtons();
        RefreshStaticVisuals();
        Canvas.ForceUpdateCanvases();
        ApplyViewImmediate();
    }

    private void OnValidate()
    {
        stageCount = Mathf.Max(1, stageCount);
        EnsureStageData();
    }

    private void EnsureStageData()
    {
        stages ??= new List<StageEntry>();
        while (stages.Count < stageCount)
        {
            int index = stages.Count;
            StageEntry entry = new();
            if (index < StageNames.Length)
            {
                entry.displayName = StageNames[index];
                entry.objective = Objectives[index];
                entry.unlocked = true;
                entry.completed = index < 4;
                entry.MapPosition = PinPositions[index];
                entry.focusZoom = StageZooms[index];
                entry.preview = stagePreviewSprites != null && index < stagePreviewSprites.Length ? stagePreviewSprites[index] : null;
            }
            stages.Add(entry);
        }
        if (stages.Count > stageCount)
            stages.RemoveRange(stageCount, stages.Count - stageCount);
        selectedStage = Mathf.Clamp(selectedStage, 1, stageCount);
    }

    private void AddDecor(RectTransform root)
    {
        Panel("AccentTop", root, Red, new(.90f,.966f), new(1f,.988f)).GetComponent<Image>().raycastTarget = false;
        Panel("AccentBottom", root, Red, new(0f,.012f), new(.11f,.032f)).GetComponent<Image>().raycastTarget = false;


    }

    private void CreateTitleGraphic(RectTransform parent)
    {
        if (titleSprite != null)
        {
            Image title = Panel("TitleGraphic", parent, Color.white, new(.02f,.775f), new(.66f,1f)).GetComponent<Image>();
            title.sprite = titleSprite;
            title.preserveAspect = true;
            title.raycastTarget = false;
            title.rectTransform.localEulerAngles = new Vector3(0f, 0f, 3f);

            return;
        }

        Text sloganA = Label("SloganA", parent, "MAKE EARTH", 54, Ink, FontStyle.BoldAndItalic,
            TextAnchor.MiddleLeft, new(.055f,.9f), new(.30f,.985f));
        Text sloganB = Label("SloganB", parent, "GREAT AGAIN", 54, Red, FontStyle.BoldAndItalic,
            TextAnchor.MiddleLeft, new(.285f,.9f), new(.56f,.985f));
        sloganA.resizeTextForBestFit = sloganB.resizeTextForBestFit = true;
        sloganA.resizeTextMinSize = sloganB.resizeTextMinSize = 30;
        sloganA.resizeTextMaxSize = sloganB.resizeTextMaxSize = 54;
    }

    private void CreateMap(RectTransform root)
    {
        GameObject viewportObject = new("MapViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(Button));
        viewportObject.transform.SetParent(root, false);
        mapViewport = (RectTransform)viewportObject.transform;
        SetAnchors(mapViewport, new(0f,0f), new(.735f,.90f));
        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = Color.white;
        viewportObject.GetComponent<Button>().transition = Selectable.Transition.None;

        mapContent = Rect("MapContent", mapViewport, Vector2.zero, Vector2.one);
        mapContent.pivot = new Vector2(.5f,.5f);
        CreateMapTile("L", mapContent, new(-1f,0f), new(0f,1f));
        RectTransform centerTile = CreateMapTile("C", mapContent, Vector2.zero, Vector2.one);
        CreateMapTile("R", mapContent, new(1f,0f), new(2f,1f));

        selectionPulse = new GameObject("SelectionPulse", typeof(RectTransform), typeof(CanvasRenderer), typeof(StageSelectPulseGraphic))
            .GetComponent<StageSelectPulseGraphic>();
        selectionPulse.transform.SetParent(centerTile, false);
        selectionPulse.rectTransform.anchorMin = selectionPulse.rectTransform.anchorMax = GetStage(selectedStage).MapPosition;
        selectionPulse.rectTransform.sizeDelta = new Vector2(190f,190f);
        selectionPulse.raycastTarget = false;

        popupRoot = Rect("FocusOverlay", mapViewport, Vector2.zero, Vector2.one);
        popupGroup = popupRoot.gameObject.AddComponent<CanvasGroup>();
        Image scrim = Panel("PopupScrim", popupRoot, Color.clear, new(.40f,.02f), new(.95f,.98f)).GetComponent<Image>();
        scrim.raycastTarget = false;
        CreateMissionCard(popupRoot);
    }

    private RectTransform CreateMapTile(string suffix, Transform parent, Vector2 min, Vector2 max)
    {
        RectTransform tile = Rect($"MapTile_{suffix}", parent, min, max);
        Image mapImage = Panel("WorldMap", tile, Color.white, Vector2.zero, Vector2.one).GetComponent<Image>();
        mapImage.sprite = worldMapSprite;
        mapImage.preserveAspect = false;
        mapImage.raycastTarget = false;
        for (int i = 0; i < stageCount; i++)
            if (stages[i].unlocked)
                CreateMapPin(i + 1, tile, stages[i].MapPosition, suffix);
        return tile;
    }

    private void CreateMissionCard(RectTransform parent)
    {
        // 사진과 정보가 한 장의 인쇄물처럼 붙어 보이도록 카드의 세로 여백을 조입니다.
        // 폭이 넓은 화면에서도 프레임이 가로로 늘어나지 않도록 고정 크기를 사용합니다.
        RectTransform card = Rect("MissionCard", parent, new(.69f,.44f), new(.69f,.44f));
        card.sizeDelta = new Vector2(560f,660f);
        Image frame = Panel("Frame", card, Color.white, new(-.032f,-.035f), new(1.032f,1.035f)).GetComponent<Image>();
        frame.sprite = popupFrameSprite;
        // 완성된 종이 모서리와 테이프의 비율을 그대로 축소해 사용합니다.
        // 9-slice는 작은 팝업에서 모서리 조각이 지나치게 커 보입니다.
        frame.type = popupFrameSprite != null && popupFrameSprite.name == "PaperFrameAlpha"
            ? Image.Type.Simple
            : Image.Type.Sliced;
        frame.preserveAspect = false;
        frame.raycastTarget = false;
        Shadow frameShadow = frame.gameObject.AddComponent<Shadow>();
        frameShadow.effectColor = new Color(.05f,.065f,.07f,.28f);
        frameShadow.effectDistance = new Vector2(5f,-7f);

        RectTransform photoMount = Panel("PhotoMount", card, new Color(1f,.995f,.975f), new(.06f,.525f), new(.94f,.953f));
        photoMount.GetComponent<Image>().raycastTarget = false;
        Shadow photoShadow = photoMount.gameObject.AddComponent<Shadow>();
        photoShadow.effectColor = new Color(0f,0f,0f,.3f);
        photoShadow.effectDistance = new Vector2(2f,-5f);
        AddOutline(photoMount.gameObject, new Color(1f,.985f,.94f,.95f), 2f);

        RectTransform previewMask = MaskRect("PreviewMask", card, new(.075f,.54f), new(.925f,.94f));
        RectTransform preview = Panel("Preview", previewMask, Color.white, Vector2.zero, Vector2.one);
        popupPreview = preview.GetComponent<Image>();
        popupPreview.sprite = GetPreviewSprite(selectedStage);
        popupPreview.preserveAspect = false;
        AspectRatioFitter previewAspect = preview.gameObject.AddComponent<AspectRatioFitter>();
        previewAspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        if (popupPreview.sprite != null)
            previewAspect.aspectRatio = popupPreview.sprite.rect.width / popupPreview.sprite.rect.height;

        // 라벨이 사진 하단을 물도록 배치해 별도 상자가 아니라 붙인 표식처럼 보이게 합니다.
        Image stageTab = Panel("StageTab", card, stageTabSprite != null ? Color.white : Red, new(.065f,.47f), new(.38f,.555f)).GetComponent<Image>();
        stageTab.sprite = stageTabSprite;
        stageTab.raycastTarget = false;
        popupStage = Label("StageNumber", card, "STAGE 05", 28, Color.white, FontStyle.BoldAndItalic,
            TextAnchor.MiddleCenter, new(.065f,.47f), new(.38f,.555f));
        popupStage.font = stageListFont != null ? stageListFont : uiFont;
        popupStage.fontStyle = FontStyle.Italic;
        popupTitle = Label("StageTitle", card, "SILICON VALLEY", 58, Ink, FontStyle.Normal,
            TextAnchor.MiddleLeft, new(.07f,.33f), new(.94f,.455f));
        popupTitle.font = stageListFont != null ? stageListFont : uiFont;
        popupTitle.resizeTextForBestFit = true;
        popupTitle.resizeTextMinSize = 23;
        popupTitle.resizeTextMaxSize = 58;
        RectTransform rule = Panel("TitleRule", card, new Color(.08f,.09f,.09f,.38f), new(.07f,.315f), new(.94f,.315f));
        rule.sizeDelta = new Vector2(0f,1f);
        rule.GetComponent<Image>().raycastTarget = false;
        Panel("ObjectiveMarker", card, Red, new(.07f,.245f), new(.098f,.278f)).GetComponent<Image>().raycastTarget = false;
        popupObjective = Label("Objective", card, "REACH THE MAIN CAMPUS", 21, Ink, FontStyle.Bold,
            TextAnchor.MiddleLeft, new(.117f,.225f), new(.94f,.29f));
        popupObjective.font = stageListFont != null ? stageListFont : uiFont;
        popupObjective.fontStyle = FontStyle.Normal;
        popupObjective.resizeTextForBestFit = true;
        popupObjective.resizeTextMinSize = 15;
        popupObjective.resizeTextMaxSize = 27;

        RectTransform buttonShadow = Panel("StartMissionShadow", card, new Color(.035f,.035f,.03f,.78f), new(.205f,.062f), new(.795f,.17f));
        Image buttonShadowImage = buttonShadow.GetComponent<Image>();
        buttonShadowImage.sprite = startButtonSprite;
        buttonShadowImage.type = Image.Type.Simple;
        buttonShadowImage.preserveAspect = false;
        buttonShadowImage.raycastTarget = false;
        buttonShadow.localEulerAngles = new Vector3(0f,0f,-.65f);

        Button start = CreateButton("StartMissionButton", card, Yellow, new(.19f,.075f), new(.81f,.19f));
        Image startImage = start.GetComponent<Image>();
        startImage.sprite = startButtonSprite;
        startImage.color = startButtonSprite != null ? Color.white : Yellow;
        startImage.type = Image.Type.Simple;
        startImage.preserveAspect = false;
        if (startButtonHighlightedSprite != null && startButtonPressedSprite != null)
        {
            start.transition = Selectable.Transition.SpriteSwap;
            SpriteState states = start.spriteState;
            states.highlightedSprite = startButtonHighlightedSprite;
            states.selectedSprite = startButtonHighlightedSprite;
            states.pressedSprite = startButtonPressedSprite;
            start.spriteState = states;
        }
        Text startText = Label("Text", start.transform, "START MISSION", 38, Ink, FontStyle.Normal, TextAnchor.MiddleCenter,
            new(.04f,.10f), new(.96f,.98f));
        startText.font = stageListFont != null ? stageListFont : uiFont;
        startText.resizeTextForBestFit = true;
        startText.resizeTextMinSize = 22;
        startText.resizeTextMaxSize = 38;
    }

    private void CreateStageList(RectTransform root)
    {
        RectTransform listRoot = Rect("StageList", root, new(.738f,.05f), new(.984f,.895f));

        Image frame = Panel("PanelFrame", listRoot, Color.white, new(-.055f,-.035f), new(1.055f,1.045f)).GetComponent<Image>();
        frame.sprite = stageListFrameSprite;
        frame.type = stageListFrameSprite != null && stageListFrameSprite.name == "StageListFrameAlpha"
            ? Image.Type.Simple
            : Image.Type.Sliced;
        frame.preserveAspect = false;
        frame.raycastTarget = false;

        Image header = Panel("Header", listRoot, Color.white, new(.035f,.925f), new(.895f,.992f)).GetComponent<Image>();
        header.sprite = stageListHeaderSprite;
        header.raycastTarget = false;
        Text headerTitle = Label("HeaderTitle", listRoot, "STAGE SELECT", 40, Ink, FontStyle.Normal,
            TextAnchor.MiddleLeft, new(.055f,.928f), new(.845f,.992f));
        headerTitle.font = stageListFont != null ? stageListFont : uiFont;
        headerTitle.resizeTextForBestFit = true;
        headerTitle.resizeTextMinSize = 22;
        headerTitle.resizeTextMaxSize = 40;

        RectTransform viewport = MaskRect("ListViewport", listRoot, new(.035f,.035f), new(.895f,.925f));
        Image viewportRaycast = viewport.gameObject.AddComponent<Image>();
        viewportRaycast.color = new Color(1f,1f,1f,.001f);

        RectTransform content = Rect("Content", viewport, new(0f,1f), new(1f,1f));
        content.pivot = new Vector2(.5f,1f);
        content.sizeDelta = new Vector2(0f, stageCount * 68.5f);
        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 0f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        for (int i = 0; i < stageCount; i++)
            CreateListRow(i + 1, content);

        stageListScroll = viewport.gameObject.AddComponent<ScrollRect>();
        stageListScroll.viewport = viewport;
        stageListScroll.content = content;
        stageListScroll.horizontal = false;
        stageListScroll.vertical = true;
        stageListScroll.movementType = ScrollRect.MovementType.Clamped;
        stageListScroll.inertia = true;
        stageListScroll.decelerationRate = .12f;
        stageListScroll.scrollSensitivity = 32f;

        RectTransform scrollbarRect = Panel("Scrollbar", listRoot, new Color(.72f,.74f,.75f,1f), new(.925f,.065f), new(.965f,.925f));
        Image trackImage = scrollbarRect.GetComponent<Image>();
        trackImage.sprite = scrollTrackSprite;
        trackImage.type = Image.Type.Sliced;
        Scrollbar scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();
        RectTransform handle = Panel("Handle", scrollbarRect, Ink, Vector2.zero, Vector2.one);
        handle.GetComponent<Image>().sprite = scrollTrackSprite;
        handle.GetComponent<Image>().type = Image.Type.Sliced;
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handle.GetComponent<Image>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        // 목록 길이와 무관하게 시안의 짧은 손잡이를 유지하고 위치만 동기화합니다.
        stageScrollbar = scrollbar;
        scrollbar.size = .14f;
        stageListScroll.verticalScrollbar = null;
        stageListScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        scrollbar.value = 1f;
        stageListScroll.verticalNormalizedPosition = 1f;

        scrollUpButton = CreateScrollButton("ScrollUp", listRoot, new(.912f,.945f), new(.978f,.986f), true);
        scrollDownButton = CreateScrollButton("ScrollDown", listRoot, new(.912f,.012f), new(.978f,.053f), false);
    }

    private void CreateMapPin(int stage, Transform parent, Vector2 anchor, string tileSuffix)
    {
        StageEntry entry = GetStage(stage);
        Sprite sprite = entry.completed ? completedPinSprite : stage == selectedStage ? selectedPinSprite : normalPinSprite;
        Button button = CreateButton($"Pin_{stage:00}_{tileSuffix}", parent, Color.white, anchor, anchor);
        RectTransform rect = (RectTransform)button.transform;
        // 위치 좌표가 아이콘 중심이 아니라 핀/깃대의 끝을 가리키도록 합니다.
        rect.pivot = new Vector2(.5f,.08f);
        rect.sizeDelta = GetPinSize(stage, stage == selectedStage);
        Image image = button.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
    }

    private void CreateListRow(int stage, Transform parent)
    {
        StageEntry entry = GetStage(stage);
        GameObject rowObject = new($"List_{stage:00}",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Button));
        rowObject.transform.SetParent(parent,false);
        RectTransform rowRect = (RectTransform)rowObject.transform;
        LayoutElement element = rowObject.AddComponent<LayoutElement>();
        element.preferredHeight = 68.5f;
        element.minHeight = 68.5f;
        Image rowGraphic = rowObject.GetComponent<Image>();
        rowGraphic.sprite = normalRowSprite;
        rowGraphic.type = Image.Type.Simple;
        rowGraphic.preserveAspect = false;
        rowGraphic.color = Color.white;
        Button row = rowObject.GetComponent<Button>();
        row.targetGraphic = rowGraphic;
        row.transition = Selectable.Transition.None;
        row.interactable = entry.unlocked;

        Image selectedOverlay = Panel("SelectedOverlay", row.transform, Color.white, Vector2.zero, Vector2.one).GetComponent<Image>();
        selectedOverlay.sprite = selectedRowOverlaySprite;
        selectedOverlay.type = Image.Type.Simple;
        selectedOverlay.preserveAspect = false;
        selectedOverlay.raycastTarget = false;

        Text number = Label("Number", row.transform, stage.ToString("00"), 42, Color.white, FontStyle.Normal,
            TextAnchor.MiddleCenter, new(.018f,.06f), new(.165f,.94f));
        number.font = stageListFont != null ? stageListFont : uiFont;
        number.horizontalOverflow = HorizontalWrapMode.Overflow;
        number.verticalOverflow = VerticalWrapMode.Overflow;
        Text name = Label("Name", row.transform, entry.displayName ?? string.Empty, 35, Ink, FontStyle.Normal,
            TextAnchor.MiddleLeft, new(.225f,.06f), new(.96f,.94f));
        name.font = stageListFont != null ? stageListFont : uiFont;
        name.horizontalOverflow = HorizontalWrapMode.Overflow;
        if (entry.completed)
        {
            RectTransform badge = Panel("Cleared", row.transform, Color.white, new(.60f,-.08f), new(1.035f,1.08f));
            Image badgeImage = badge.GetComponent<Image>();
            badgeImage.sprite = clearedBadgeSprite;
            badgeImage.preserveAspect = true;
            badge.localEulerAngles = new Vector3(0f, 0f, 10f);
            // 중첩 Canvas는 행 위에 도장을 그리되 상위 Canvas의 정렬과
            // ListViewport 마스크를 그대로 따르도록 overrideSorting을 사용하지 않습니다.
            badge.gameObject.AddComponent<Canvas>().overrideSorting = false;
            if (clearedBadgeSprite != null && clearedBadgeSprite.name == "StampBorder")
            {
                Text stamp = Label("StampText", badge, "CLEARED", 23, Red, FontStyle.Normal,
                    TextAnchor.MiddleCenter, new(.04f,.06f), new(.96f,.94f));
                stamp.font = stageListFont != null ? stageListFont : uiFont;
                stamp.resizeTextForBestFit = true;
                stamp.resizeTextMinSize = 13;
                stamp.resizeTextMaxSize = 21;
            }
            badgeImage.raycastTarget = false;
        }

    }

    private Button CreateScrollButton(string name, Transform parent, Vector2 min, Vector2 max, bool pointUp)
    {
        Button button = CreateButton(name, parent, Color.white, min, max);
        Image image = button.GetComponent<Image>();
        image.sprite = scrollArrowSprite;
        image.preserveAspect = true;
        image.type = Image.Type.Simple;
        image.raycastTarget = true;
        if (pointUp) image.rectTransform.localEulerAngles = new Vector3(0f,0f,180f);
        return button;
    }

    private void CacheReferences()
    {
        mapPins.Clear();
        listButtons.Clear();
        mapViewport = transform.Find("StageSelectRoot/MapViewport") as RectTransform;
        mapContent = transform.Find("StageSelectRoot/MapViewport/MapContent") as RectTransform;
        popupRoot = transform.Find("StageSelectRoot/MapViewport/FocusOverlay") as RectTransform;
        popupGroup = popupRoot != null ? popupRoot.GetComponent<CanvasGroup>() : null;
        worldViewButtonRoot = transform.Find("StageSelectRoot/MapViewport/WorldViewButton") as RectTransform;
        stageListScroll = transform.Find("StageSelectRoot/StageList/ListViewport")?.GetComponent<ScrollRect>();
        stageScrollbar = transform.Find("StageSelectRoot/StageList/Scrollbar")?.GetComponent<Scrollbar>();
        listLayoutHeight = 0f;
        scrollUpButton = transform.Find("StageSelectRoot/StageList/ScrollUp")?.GetComponent<Button>();
        scrollDownButton = transform.Find("StageSelectRoot/StageList/ScrollDown")?.GetComponent<Button>();
        selectionPulse = transform.Find("StageSelectRoot/MapViewport/MapContent/MapTile_C/SelectionPulse")?.GetComponent<StageSelectPulseGraphic>();
        popupPreview = transform.Find("StageSelectRoot/MapViewport/FocusOverlay/MissionCard/PreviewMask/Preview")?.GetComponent<Image>();
        popupStage = transform.Find("StageSelectRoot/MapViewport/FocusOverlay/MissionCard/StageNumber")?.GetComponent<Text>();
        popupTitle = transform.Find("StageSelectRoot/MapViewport/FocusOverlay/MissionCard/StageTitle")?.GetComponent<Text>();
        popupObjective = transform.Find("StageSelectRoot/MapViewport/FocusOverlay/MissionCard/Objective")?.GetComponent<Text>();
        if (mapContent != null)
        {
            foreach (Button button in mapContent.GetComponentsInChildren<Button>(true))
                if (button.name.StartsWith("Pin_", StringComparison.Ordinal)) mapPins.Add(button);
        }
        Transform listContent = transform.Find("StageSelectRoot/StageList/ListViewport/Content");
        if (listContent != null)
        {
            foreach (Button button in listContent.GetComponentsInChildren<Button>(true))
                if (button.name.StartsWith("List_", StringComparison.Ordinal)) listButtons.Add(button);
        }
    }

    private void WireButtons()
    {
        foreach (Button pin in mapPins)
        {
            int stage = ParseStage(pin.name);
            pin.onClick.RemoveAllListeners();
            pin.onClick.AddListener(() => SelectStage(stage));
        }
        foreach (Button row in listButtons)
        {
            int stage = ParseStage(row.name);
            row.onClick.RemoveAllListeners();
            row.onClick.AddListener(() => SelectStage(stage));
        }
        Wire("StageSelectRoot/MapViewport", ExitFocus);
        Wire("StageSelectRoot/MapViewport/FocusOverlay/MissionCard/StartMissionButton", StartMission);
        if (scrollUpButton != null)
        {
            scrollUpButton.onClick.RemoveAllListeners();
            scrollUpButton.onClick.AddListener(() => ScrollStageList(.24f));
        }
        if (scrollDownButton != null)
        {
            scrollDownButton.onClick.RemoveAllListeners();
            scrollDownButton.onClick.AddListener(() => ScrollStageList(-.24f));
        }
        if (stageListScroll != null)
        {
            if (stageScrollbar != null)
            {
                stageListScroll.verticalScrollbar = null;
                stageScrollbar.onValueChanged.RemoveAllListeners();
                stageScrollbar.onValueChanged.AddListener(value => stageListScroll.verticalNormalizedPosition = value);
            }
            stageListScroll.onValueChanged.RemoveAllListeners();
            stageListScroll.onValueChanged.AddListener(_ => RefreshScrollIndicators());
        }
        RefreshScrollIndicators();
    }

    private void ScrollStageList(float amount)
    {
        if (stageListScroll == null) return;
        stageListScroll.verticalNormalizedPosition = Mathf.Clamp01(stageListScroll.verticalNormalizedPosition + amount);
        RefreshScrollIndicators();
    }

    private void RefreshScrollIndicators()
    {
        if (stageListScroll == null) return;
        if (stageScrollbar != null)
        {
            stageScrollbar.size = .14f;
            stageScrollbar.SetValueWithoutNotify(stageListScroll.verticalNormalizedPosition);
        }
        SetScrollIndicator(scrollUpButton, stageListScroll.verticalNormalizedPosition < .995f);
        SetScrollIndicator(scrollDownButton, stageListScroll.verticalNormalizedPosition > .005f);
    }

    private static void SetScrollIndicator(Button button, bool enabled)
    {
        if (button == null) return;
        button.interactable = enabled;
        Image image = button.GetComponent<Image>();
        if (image != null) image.color = enabled ? Color.white : new Color(1f,1f,1f,.62f);
    }

    private void Wire(string path, UnityEngine.Events.UnityAction action)
    {
        Button button = transform.Find(path)?.GetComponent<Button>();
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void SelectStage(int stage)
    {
        selectedStage = Mathf.Clamp(stage, 1, stageCount);
        if (!GetStage(selectedStage).unlocked) return;
        hasSelection = true;
        RefreshStaticVisuals();
        SetFocused(true, Application.isPlaying);
    }

    private void ExitFocus()
    {
        if (!focused) return;
        hasSelection = false;
        RefreshStaticVisuals();
        SetFocused(false, Application.isPlaying);
    }

    private void SetFocused(bool value, bool animate)
    {
        focused = value;
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }
        if (animate)
            transitionRoutine = StartCoroutine(AnimateView(value));
        else
            ApplyViewImmediate();
    }

    private IEnumerator AnimateView(bool targetFocused)
    {
        Canvas.ForceUpdateCanvases();
        if (popupRoot != null) popupRoot.gameObject.SetActive(true);
        if (worldViewButtonRoot != null) worldViewButtonRoot.gameObject.SetActive(targetFocused);

        float startZoom = mapContent.localScale.x;
        float targetZoom = targetFocused ? GetStage(selectedStage).focusZoom : OverviewZoom;
        Vector2 startPosition = mapContent.anchoredPosition;
        Vector2 targetPosition = targetFocused ? CalculateFocusPosition(targetZoom) : CalculateOverviewPosition();
        float startAlpha = popupGroup != null ? popupGroup.alpha : 0f;
        float targetAlpha = targetFocused ? 1f : 0f;

        SetPinVisibility(targetFocused);
        for (float elapsed = 0f; elapsed < TransitionDuration; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / TransitionDuration);
            float zoom = Mathf.Lerp(startZoom, targetZoom, t);
            mapContent.localScale = Vector3.one * zoom;
            mapContent.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, t);
            UpdatePinCounterScale(zoom);
            if (popupGroup != null) popupGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        mapContent.localScale = Vector3.one * targetZoom;
        mapContent.anchoredPosition = targetPosition;
        UpdatePinCounterScale(targetZoom);
        if (popupGroup != null) popupGroup.alpha = targetAlpha;
        if (!targetFocused)
        {
            if (popupRoot != null) popupRoot.gameObject.SetActive(false);
            SetPinVisibility(false);
        }
        transitionRoutine = null;
    }

    private void ApplyViewImmediate()
    {
        if (mapContent == null || mapViewport == null) return;
        Canvas.ForceUpdateCanvases();
        UpdateListRowHeights();
        Canvas.ForceUpdateCanvases();
        float zoom = focused ? GetStage(selectedStage).focusZoom : OverviewZoom;
        mapContent.localScale = Vector3.one * zoom;
        mapContent.anchoredPosition = focused ? CalculateFocusPosition(zoom) : CalculateOverviewPosition();
        SetPinVisibility(focused);
        UpdatePinCounterScale(zoom);
        if (popupRoot != null) popupRoot.gameObject.SetActive(focused);
        if (popupGroup != null) popupGroup.alpha = focused ? 1f : 0f;
        if (worldViewButtonRoot != null) worldViewButtonRoot.gameObject.SetActive(focused);
    }

    public void RefreshLayoutForEditor() => ApplyViewImmediate();

    private void UpdateListRowHeights()
    {
        if (stageListScroll == null || stageListScroll.viewport == null) return;
        float height = stageListScroll.viewport.rect.height;
        if (height <= 0f || Mathf.Abs(height - listLayoutHeight) < .1f) return;
        listLayoutHeight = height;
        float rowHeight = height / 12f;
        foreach (Button row in listButtons)
        {
            LayoutElement layout = row.GetComponent<LayoutElement>();
            if (layout != null) layout.minHeight = layout.preferredHeight = rowHeight;
        }
        stageListScroll.content.sizeDelta = new Vector2(0f, rowHeight * listButtons.Count);
    }

    private Vector2 CalculateOverviewPosition()
    {
        // 동아시아 영역을 덜 보여주고 미주·유럽·중동을 크게 배치합니다.
        return new Vector2(mapViewport.rect.width * .18f, -mapViewport.rect.height * .045f);
    }

    private Vector2 CalculateFocusPosition(float zoom)
    {
        Vector2 pin = GetStage(selectedStage).MapPosition;
        Vector2 target = new(.22f,.56f);
        Vector2 size = mapViewport.rect.size;
        Vector2 pinFromCenter = new((pin.x - .5f) * size.x, (pin.y - .5f) * size.y);
        Vector2 targetFromCenter = new((target.x - .5f) * size.x, (target.y - .5f) * size.y);
        return targetFromCenter - pinFromCenter * zoom;
    }

    private void SetPinVisibility(bool hideUnselected)
    {
        foreach (Button pin in mapPins)
        {
            int stage = ParseStage(pin.name);
            RectTransform pinRect = (RectTransform)pin.transform;
            pinRect.anchorMin = pinRect.anchorMax = GetStage(stage).MapPosition;
            pinRect.anchoredPosition = hideUnselected ? Vector2.zero : WorldPinOffsets[stage - 1];
            bool isCenter = pin.name.EndsWith("_C", StringComparison.Ordinal);
            bool hiddenDcDuplicate = !hideUnselected && isCenter && (stage == 8 || stage == 10);
            pin.gameObject.SetActive((!hideUnselected || isCenter) && !hiddenDcDuplicate);
        }
        if (selectionPulse != null)
        {
            selectionPulse.gameObject.SetActive(hideUnselected && hasSelection);
            selectionPulse.rectTransform.anchorMin = selectionPulse.rectTransform.anchorMax = GetStage(selectedStage).MapPosition;
            selectionPulse.rectTransform.anchoredPosition = Vector2.zero;
        }
    }

    private void UpdatePinCounterScale(float mapScale)
    {
        float inverse = mapScale > .001f ? 1f / mapScale : 1f;
        foreach (Button pin in mapPins)
            pin.transform.localScale = Vector3.one * inverse;
        if (selectionPulse != null)
            selectionPulse.transform.localScale = Vector3.one * inverse;
    }

    private void RefreshStaticVisuals()
    {
        if (mapPins.Count == 0 || listButtons.Count == 0) CacheReferences();
        if (popupStage != null) popupStage.text = $"STAGE {selectedStage:00}";
        StageEntry selectedEntry = GetStage(selectedStage);
        if (popupTitle != null) popupTitle.text = selectedEntry.displayName;
        if (popupObjective != null) popupObjective.text = selectedEntry.objective;
        if (popupPreview != null)
        {
            popupPreview.sprite = GetPreviewSprite(selectedStage);
            AspectRatioFitter previewAspect = popupPreview.GetComponent<AspectRatioFitter>();
            if (previewAspect != null && popupPreview.sprite != null)
                previewAspect.aspectRatio = popupPreview.sprite.rect.width / popupPreview.sprite.rect.height;
        }

        foreach (Button pin in mapPins)
        {
            int stage = ParseStage(pin.name);
            StageEntry entry = GetStage(stage);
            bool selected = hasSelection && (stage == selectedStage || (stage == 2 && (selectedStage == 8 || selectedStage == 10)));
            Image image = pin.GetComponent<Image>();
            image.sprite = entry.completed && !selected ? completedPinSprite : selected ? selectedPinSprite : normalPinSprite;
            ((RectTransform)pin.transform).sizeDelta = GetPinSize(stage, selected);
        }

        foreach (Button row in listButtons)
        {
            int stage = ParseStage(row.name);
            StageEntry entry = GetStage(stage);
            bool selected = hasSelection && stage == selectedStage;
            Image rowGraphic = row.GetComponent<Image>();
            if (rowGraphic != null) rowGraphic.sprite = normalRowSprite;
            Transform selectedOverlay = row.transform.Find("SelectedOverlay");
            if (selectedOverlay != null) selectedOverlay.gameObject.SetActive(selected);
            Text number = row.transform.Find("Number")?.GetComponent<Text>();
            if (number != null) number.color = selected ? Ink : Color.white;
            Transform cleared = row.transform.Find("Cleared");
            if (cleared != null) cleared.gameObject.SetActive(entry.completed);
        }
    }

    private Vector2 GetPinSize(int stage, bool selected)
    {
        if (selected) return new Vector2(78f,90f);
        return GetStage(stage).completed ? new Vector2(76f,62f) : new Vector2(54f,64f);
    }

    private void StartMission()
    {
        StageEntry entry = GetStage(selectedStage);
        if (string.IsNullOrWhiteSpace(entry.targetSceneName))
        {
            Debug.LogWarning(
                $"[StageSelectPrototype] Stage {selectedStage:00}의 Target Scene이 지정되지 않았습니다.",
                this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(entry.targetSceneName))
        {
            Debug.LogWarning(
                $"[StageSelectPrototype] '{entry.targetSceneName}' 씬을 불러올 수 없습니다. Build Profiles의 Scene List 등록 상태를 확인해 주세요.",
                this);
            return;
        }

        SceneManager.LoadScene(entry.targetSceneName);
    }

    public void ShowSelectedOverviewForEditor()
    {
        focused = false;
        hasSelection = false;
        RefreshStaticVisuals();
        ApplyViewImmediate();
    }

    public void ShowSelectedStageForEditor()
    {
        ShowFocusForValidation(selectedStage);
    }

    public void ShowOverviewForValidation()
    {
        focused = false;
        hasSelection = false;
        RefreshStaticVisuals();
        ApplyViewImmediate();
    }

    public void ShowFocusForValidation(int stage)
    {
        selectedStage = Mathf.Clamp(stage, 1, stageCount);
        focused = true;
        hasSelection = true;
        RefreshStaticVisuals();
        ApplyViewImmediate();
    }

    public bool ValidateLoopingForEditor()
    {
        ShowOverviewForValidation();
        float loopWidth = mapViewport.rect.width * mapContent.localScale.x;
        float center = CalculateOverviewPosition().x;
        mapContent.anchoredPosition = new Vector2(center + loopWidth * 1.2f, mapContent.anchoredPosition.y);
        WrapMapPosition();
        bool wrapped = Mathf.Abs(mapContent.anchoredPosition.x - center) < loopWidth;

        for (int stage = 1; stage <= stageCount; stage++)
        {
            if (!GetStage(stage).unlocked) continue;
            Vector2 expected = GetStage(stage).MapPosition;
            foreach (string suffix in new[] { "L", "C", "R" })
            {
                Transform pin = transform.Find($"StageSelectRoot/MapViewport/MapContent/MapTile_{suffix}/Pin_{stage:00}_{suffix}");
                if (pin is not RectTransform rect || Vector2.Distance(rect.anchorMin, expected) > .0001f)
                    return false;
            }
        }
        ApplyViewImmediate();
        return wrapped;
    }

    private Sprite GetPreviewSprite(int stage)
    {
        int index = stage - 1;
        StageEntry entry = GetStage(stage);
        if (entry.preview != null) return entry.preview;
        return stagePreviewSprites != null && index >= 0 && index < stagePreviewSprites.Length ? stagePreviewSprites[index] : null;
    }

    private StageEntry GetStage(int stage)
    {
        EnsureStageData();
        return stages[Mathf.Clamp(stage - 1, 0, stages.Count - 1)];
    }

    public void SetPinPositionFromEditor(int stage, Vector2 normalizedPosition)
    {
        GetStage(stage).MapPosition = normalizedPosition;
        foreach (string suffix in new[] { "L", "C", "R" })
        {
            Transform pin = transform.Find($"StageSelectRoot/MapViewport/MapContent/MapTile_{suffix}/Pin_{stage:00}_{suffix}");
            if (pin is RectTransform rect)
            {
                rect.anchorMin = rect.anchorMax = GetStage(stage).MapPosition;
                rect.anchoredPosition = Vector2.zero;
            }
        }
        if (selectionPulse != null && stage == selectedStage)
            selectionPulse.rectTransform.anchorMin = selectionPulse.rectTransform.anchorMax = GetStage(stage).MapPosition;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (focused || mapViewport == null || !RectTransformUtility.RectangleContainsScreenPoint(mapViewport, eventData.position, eventData.pressEventCamera))
            return;
        draggingMap = true;
        lastDragPosition = eventData.position;
        dragVelocity = 0f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!draggingMap || mapContent == null) return;
        Vector2 delta = eventData.position - lastDragPosition;
        lastDragPosition = eventData.position;
        mapContent.anchoredPosition += new Vector2(delta.x, 0f);
        dragVelocity = delta.x / Mathf.Max(Time.unscaledDeltaTime, .001f);
        WrapMapPosition();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        draggingMap = false;
    }

    private void LateUpdate()
    {
        UpdateListRowHeights();
        if (focused || draggingMap || mapContent == null || Mathf.Abs(dragVelocity) < 1f) return;
        Vector2 position = mapContent.anchoredPosition;
        position.x += dragVelocity * Time.unscaledDeltaTime;
        mapContent.anchoredPosition = position;
        dragVelocity = Mathf.MoveTowards(dragVelocity, 0f, 1800f * Time.unscaledDeltaTime);
        WrapMapPosition();
    }

    private void WrapMapPosition()
    {
        if (mapViewport == null || mapContent == null) return;
        float loopWidth = mapViewport.rect.width * mapContent.localScale.x;
        float center = CalculateOverviewPosition().x;
        Vector2 position = mapContent.anchoredPosition;
        if (position.x > center + loopWidth) position.x -= loopWidth;
        else if (position.x < center - loopWidth) position.x += loopWidth;
        mapContent.anchoredPosition = position;
    }

    private static int ParseStage(string value)
    {
        string[] parts = value.Split('_');
        return parts.Length > 1 && int.TryParse(parts[1], out int stage) ? stage : 1;
    }

    private static RectTransform Panel(string name, Transform parent, Color color, Vector2 min, Vector2 max)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        SetAnchors(rect, min, max);
        go.GetComponent<Image>().color = color;
        return rect;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        GameObject go = new(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        SetAnchors(rect, min, max);
        return rect;
    }

    private static RectTransform MaskRect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(RectMask2D));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        SetAnchors(rect, min, max);
        return rect;
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private Text Label(string name, Transform parent, string value, int size, Color color, FontStyle style,
        TextAnchor alignment, Vector2 min, Vector2 max)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        SetAnchors(rect, min, max);
        Text text = go.GetComponent<Text>();
        text.font = uiFont;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, Color color, Vector2 min, Vector2 max)
    {
        RectTransform rect = Panel(name, parent, color, min, max);
        Button button = rect.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f,.96f,.68f,1f);
        colors.pressedColor = new Color(1f,.76f,.12f,1f);
        button.colors = colors;
        return button;
    }

    private static void AddOutline(GameObject go, Color color, float distance)
    {
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(distance, -distance);
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        GameObject go = new("EventSystem", typeof(EventSystem));
        go.AddComponent<InputSystemUIInputModule>();
    }
}
