using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>제목과 뒤로 버튼은 고정하고 치트 목록만 휠/터치로 스크롤합니다.</summary>
public class DevCheatMenuView : MonoBehaviour
{
    private DevCheatConsole controller;
    private RectTransform safeArea;
    private Rect lastSafeArea;
    private Vector2Int lastScreen;
    private ScrollRect scroll;
    private TextMeshProUGUI balance;
    private LocalizedText closeLabel;
    private PlayerResources wallet;
    private readonly List<GameObject> battleRows = new List<GameObject>();
    private readonly List<Button> moneyButtons = new List<Button>();
    private TMP_FontAsset font;

    public static DevCheatMenuView Create(DevCheatConsole owner)
    {
        var go = new GameObject("DeveloperCheatCanvas", typeof(RectTransform));
        go.SetActive(false);
        go.transform.SetParent(owner.transform, false);
        var view = go.AddComponent<DevCheatMenuView>();
        view.controller = owner;
        view.Build();
        return view;
    }

    public void Show(bool battle, bool fromOptions)
    {
        foreach (var row in battleRows) row.SetActive(battle);
        closeLabel.SetTexts(fromOptions ? "옵션으로 돌아가기" : "닫기", fromOptions ? "Back to Options" : "Close");
        gameObject.SetActive(true);
        ApplySafeArea();
        BindWallet();
        Canvas.ForceUpdateCanvases();
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 1f;
    }

    private void OnEnable()
    {
        LanguageManager.LanguageChanged += RefreshBalance;
        BindWallet();
    }
    private void OnDisable()
    {
        LanguageManager.LanguageChanged -= RefreshBalance;
        if (wallet != null) wallet.OnResourcesChanged -= OnResourcesChanged;
        wallet = null;
    }
    private void Update()
    {
        if (lastScreen.x != Screen.width || lastScreen.y != Screen.height || lastSafeArea != Screen.safeArea) ApplySafeArea();
        if (wallet != PlayerResources.Instance) BindWallet();
    }
    private void BindWallet()
    {
        if (wallet != null) wallet.OnResourcesChanged -= OnResourcesChanged;
        wallet = PlayerResources.Instance;
        if (wallet != null) wallet.OnResourcesChanged += OnResourcesChanged;
        RefreshBalance();
    }
    private void OnResourcesChanged(int money, int gem) => RefreshBalance();
    private void RefreshBalance()
    {
        bool korean = LanguageManager.Instance == null || LanguageManager.Instance.CurrentLanguage == GameLanguage.Korean;
        if (balance != null) balance.text = wallet != null
            ? (korean ? $"돈 {wallet.Money:N0}    ·    젬 {wallet.Gem:N0}" : $"Gold {wallet.Money:N0}    ·    Gems {wallet.Gem:N0}")
            : (korean ? "캐릭터가 있는 로비 또는 전투에서 사용할 수 있습니다." : "Available with a character in the lobby or battle.");
        foreach (var button in moneyButtons) button.interactable = wallet != null;
    }
    private void ApplySafeArea()
    {
        if (safeArea == null || Screen.width <= 0 || Screen.height <= 0) return;
        lastSafeArea = Screen.safeArea;
        lastScreen = new Vector2Int(Screen.width, Screen.height);
        safeArea.anchorMin = new Vector2(lastSafeArea.xMin / Screen.width, lastSafeArea.yMin / Screen.height);
        safeArea.anchorMax = new Vector2(lastSafeArea.xMax / Screen.width, lastSafeArea.yMax / Screen.height);
        safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
    }

    private void Build()
    {
        font = LobbyPanelChrome.FindFont();
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        gameObject.AddComponent<GraphicRaycaster>();
        var dim = Image("InputBlocker", transform, new Color(0f, 0f, 0f, 0.65f));
        Stretch(dim.rectTransform); dim.raycastTarget = true;
        safeArea = Node("SafeArea", transform); Stretch(safeArea);
        var panel = Image("Window", safeArea, new Color(0.10f, 0.12f, 0.15f, 1f));
        panel.raycastTarget = true;
        float width = Mathf.Clamp(controller.overlayWidthPercent, 0.35f, 0.95f);
        float height = Mathf.Clamp(controller.overlayHeightPercent, 0.4f, 0.92f);
        float top = Mathf.Clamp(controller.overlayTopMarginPercent, 0.02f, 0.98f - height);
        panel.rectTransform.anchorMin = new Vector2((1f - width) * 0.5f, 1f - top - height);
        panel.rectTransform.anchorMax = new Vector2((1f + width) * 0.5f, 1f - top);
        panel.rectTransform.offsetMin = panel.rectTransform.offsetMax = Vector2.zero;
        var title = Label("Title", panel.transform, "개발자 치트", "Developer Cheats", 34f);
        Top(title.rectTransform, 18f, 55f);
        balance = Label("Balance", panel.transform, "", "", 25f);
        Top(balance.rectTransform, 77f, 42f);
        // 재화 표시는 언어 변경과 잔액 변경 시 직접 갱신합니다.
        Destroy(balance.GetComponent<LocalizedText>());

        var list = Node("ScrollList", panel.transform);
        Stretch(list, new Vector2(22f, 126f), new Vector2(22f, 132f));
        scroll = list.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45f;
        var viewport = Image("Viewport", list, new Color(0f, 0f, 0f, 0.05f));
        viewport.raycastTarget = true;
        Stretch(viewport.rectTransform, Vector2.zero, new Vector2(24f, 0f));
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = Node("Content", viewport.transform);
        content.anchorMin = new Vector2(0f, 1f); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f); content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12f; layout.padding = new RectOffset(2, 2, 4, 4);
        layout.childControlHeight = layout.childControlWidth = true;
        layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport.rectTransform; scroll.content = content;
        AddMoneyButton(content, "Money100", "돈 +100", "Gold +100", () => controller.AddMoney(100));
        AddMoneyButton(content, "Money1000", "돈 +1,000", "Gold +1,000", () => controller.AddMoney(1000));
        AddMoneyButton(content, "Gem100", "젬 +100", "Gems +100", () => controller.AddGem(100));
        AddMoneyButton(content, "Gem1000", "젬 +1,000", "Gems +1,000", () => controller.AddGem(1000));
        AddBattleButton(content, "OpenShop", "상점 열기", "Open Shop", controller.OpenShop);
        AddBattleButton(content, "DropTicket", "상점 티켓 드랍", "Drop Shop Ticket", controller.DropShopTicket);
        AddBattleButton(content, "Damage50", "HP -50", "HP -50", controller.ExecuteCheatDamage50);
        AddBattleButton(content, "Evade50", "회피 게이지 -50", "Evade Gauge -50", controller.ExecuteCheatEvadeCost50);
        AddBattleButton(content, "Weapons", "무기 선택", "Select Weapon", controller.OpenWeaponMenu);
        AddBattleButton(content, "Upgrades", "업그레이드 선택", "Select Upgrade", controller.OpenUpgradeMenu);
        AddMoneyButton(content, "ResetResources", "돈·젬 전부 0", "Reset Gold and Gems", controller.ResetResources);

        var track = Image("Scrollbar", list, new Color(1f, 1f, 1f, 0.10f));
        track.rectTransform.anchorMin = new Vector2(1f, 0f); track.rectTransform.anchorMax = Vector2.one;
        track.rectTransform.pivot = new Vector2(1f, 0.5f); track.rectTransform.sizeDelta = new Vector2(16f, 0f);
        var handle = Image("Handle", track.transform, new Color(0.55f, 0.68f, 0.75f)); Stretch(handle.rectTransform);
        var scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop; scrollbar.handleRect = handle.rectTransform; scrollbar.targetGraphic = handle;
        track.raycastTarget = handle.raycastTarget = true;
        scroll.verticalScrollbar = scrollbar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        var hint = Label("ScrollHint", panel.transform, "목록을 위아래로 드래그하거나 휠을 사용하세요", "Drag the list or use the mouse wheel", 20f);
        Bottom(hint.rectTransform, 94f, 28f);
        var close = Button("Close", panel.transform, "닫기", "Close", controller.CloseOverlay);
        Bottom(close.GetComponent<RectTransform>(), 18f, 64f);
        closeLabel = close.GetComponentInChildren<LocalizedText>();
    }

    private void AddMoneyButton(Transform parent, string name, string ko, string en, UnityAction action) =>
        moneyButtons.Add(ListButton(parent, name, ko, en, action));
    private void AddBattleButton(Transform parent, string name, string ko, string en, UnityAction action) =>
        battleRows.Add(ListButton(parent, name, ko, en, action).gameObject);
    private Button ListButton(Transform parent, string name, string ko, string en, UnityAction action)
    {
        var button = Button(name, parent, ko, en, action);
        var size = button.gameObject.AddComponent<LayoutElement>(); size.minHeight = size.preferredHeight = 64f;
        return button;
    }
    private Button Button(string name, Transform parent, string ko, string en, UnityAction action)
    {
        var background = Image(name, parent, new Color(0.22f, 0.27f, 0.32f)); background.raycastTarget = true;
        var button = background.gameObject.AddComponent<Button>(); button.targetGraphic = background;
        button.onClick.AddListener(action);
        var label = Label("Label", background.transform, ko, en, 28f);
        Stretch(label.rectTransform, new Vector2(12f, 4f), new Vector2(12f, 4f));
        return button;
    }
    private TextMeshProUGUI Label(string name, Transform parent, string ko, string en, float size)
    {
        var rect = Node(name, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size; text.enableAutoSizing = true; text.fontSizeMin = size * 0.7f; text.fontSizeMax = size;
        text.alignment = TextAlignmentOptions.Center; text.color = Color.white; text.raycastTarget = false;
        rect.gameObject.AddComponent<LocalizedText>().SetTexts(ko, en);
        return text;
    }
    private static Image Image(string name, Transform parent, Color color)
    {
        var image = Node(name, parent).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
    }
    private static RectTransform Node(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
        go.transform.SetParent(parent, false); return (RectTransform)go.transform;
    }
    private static void Stretch(RectTransform rect) => Stretch(rect, Vector2.zero, Vector2.zero);
    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = min; rect.offsetMax = -max;
    }
    private static void Top(RectTransform rect, float top, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 1f); rect.sizeDelta = new Vector2(-44f, height); rect.anchoredPosition = new Vector2(0f, -top);
    }
    private static void Bottom(RectTransform rect, float bottom, float height)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f); rect.sizeDelta = new Vector2(-44f, height); rect.anchoredPosition = new Vector2(0f, bottom);
    }
}
