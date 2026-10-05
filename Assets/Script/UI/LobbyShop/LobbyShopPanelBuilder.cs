using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비 상점 UI를 코드로 만듭니다. 씬에 없어도 Play 시 자동으로 붙습니다.
/// NPC 액자·대사창·좌하단 돌아가기·우하단 구매 버튼은 LobbyPanelChrome을 씁니다.
/// </summary>
public static class LobbyShopPanelBuilder
{
    private static readonly Color CardColor = new Color(0.10f, 0.10f, 0.12f, 0.34f);

    public static LobbyShopPanel Build(RectTransform canvas)
    {
        if (canvas == null)
            return null;

        var existing = canvas.Find(LobbyShopPanel.ObjectName);
        if (existing != null)
        {
            var panel = existing.GetComponent<LobbyShopPanel>();
            if (panel != null)
            {
                RebuildInto(panel);
                return panel;
            }
        }

        var go = LobbyPanelChrome.CreateUi(LobbyShopPanel.ObjectName, canvas);
        LobbyPanelChrome.Stretch(go.GetComponent<RectTransform>());
        var created = go.AddComponent<LobbyShopPanel>();
        RebuildInto(created);
        created.gameObject.SetActive(false);
        return created;
    }

    public static void RebuildInto(LobbyShopPanel panel)
    {
        if (panel == null)
            return;

        var catalog = LobbyShopCatalogSO.Load();
        if (catalog == null)
            catalog = ScriptableObject.CreateInstance<LobbyShopCatalogSO>();

        AssignDefaultContent(catalog);
        foreach (Transform child in panel.transform) child.gameObject.SetActive(false); LobbyPanelChrome.ClearChildren(panel.transform);

        var font = LobbyPanelChrome.FindFont();
        var rightMenu = LobbyPanelChrome.FindRightMenu();

        var back = LobbyPanelChrome.CreateBackButton(panel.transform, font, catalog.backLabel);
        var buy = LobbyPanelChrome.CreateActionButton(panel.transform, font, catalog.buyLabel, "BuyButton");
        LobbyPanelChrome.CreateNpcBlock(
            panel.transform, font, catalog.npcPortrait, catalog.idleLine,
            out Image portrait, out LocalizedText lineText);
        CreateShopBody(panel.transform, font, catalog, out RectTransform tabRow, out Button[] tabs,
            out RectTransform itemContent, out LobbyShopGridFitter fitter);

        panel.AssignBuiltRefs(
            catalog, rightMenu, back, buy, portrait, lineText,
            tabRow, itemContent, fitter, tabs, font);
    }

    private static void AssignDefaultContent(LobbyShopCatalogSO catalog)
    {
        if (catalog.npcPortrait == null)
            catalog.npcPortrait = LobbyStatUpgradeCatalogSO.LoadSprite("Npc_BearCoach");

    }

    private static void CreateShopBody(
        Transform parent,
        TMP_FontAsset font,
        LobbyShopCatalogSO catalog,
        out RectTransform tabRow,
        out Button[] tabs,
        out RectTransform itemContent,
        out LobbyShopGridFitter fitter)
    {
        var body = CreateUi("ShopBody", parent).GetComponent<RectTransform>();
        // 가로 3칸. 목록 위치는 유지하고, 캐릭터만 왼쪽 빈 공간에 맞춰 둡니다.
        body.anchorMin = new Vector2(0.285f, 0.035f);
        body.anchorMax = new Vector2(0.755f, 0.925f);
        body.offsetMin = Vector2.zero;
        body.offsetMax = Vector2.zero;

        var bodyImage = body.gameObject.AddComponent<Image>(); ApplyRounded(bodyImage, new Color(.10f,.11f,.13f,.86f)); var bodyLayout = body.gameObject.AddComponent<VerticalLayoutGroup>();
        bodyLayout.childAlignment = TextAnchor.UpperCenter;
        bodyLayout.spacing = 12f;
        bodyLayout.childControlHeight = true;
        bodyLayout.childControlWidth = true;
        bodyLayout.childForceExpandHeight = false;
        bodyLayout.childForceExpandWidth = true;
        bodyLayout.padding = new RectOffset(16, 16, 14, 14);

        tabRow = CreateUi("TabRow", body).GetComponent<RectTransform>();
        var tabLe = tabRow.gameObject.AddComponent<LayoutElement>();
        tabLe.preferredHeight = 58f;
        tabLe.minHeight = 58f;
        tabLe.flexibleHeight = 0f;

        var tabLayout = tabRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabLayout.childAlignment = TextAnchor.MiddleRight;
        tabLayout.spacing = 8f;
        tabLayout.childControlHeight = true;
        tabLayout.childControlWidth = true;
        tabLayout.childForceExpandHeight = true;
        tabLayout.childForceExpandWidth = false;
        tabLayout.padding = new RectOffset(16, 16, 14, 14);

        var categories = catalog.Categories ?? new LobbyShopCategory[0];
        tabs = new Button[categories.Length];
        for (int i = 0; i < categories.Length; i++)
            tabs[i] = CreateTab(tabRow, font, categories[i], i);

        var scrollGo = CreateUi("ItemScroll", body);
        var scrollLe = scrollGo.AddComponent<LayoutElement>();
        scrollLe.flexibleHeight = 1f;
        scrollLe.minHeight = 120f;

        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;

        var viewport = CreateUi("Viewport", scrollGo.transform);
        Stretch(viewport.GetComponent<RectTransform>());
        var viewportImg = viewport.AddComponent<Image>();
        viewportImg.color = new Color(1f, 1f, 1f, 0.01f);
        viewportImg.raycastTarget = true;
        viewport.AddComponent<RectMask2D>();

        var contentGo = CreateUi("Content", viewport.transform);
        itemContent = contentGo.GetComponent<RectTransform>();
        itemContent.anchorMin = new Vector2(0f, 1f);
        itemContent.anchorMax = new Vector2(1f, 1f);
        itemContent.pivot = new Vector2(0.5f, 1f);
        itemContent.offsetMin = Vector2.zero;
        itemContent.offsetMax = Vector2.zero;

        var grid = contentGo.AddComponent<GridLayoutGroup>();
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = LobbyShopGridFitter.ColumnCount;
        grid.spacing = new Vector2(10f, 10f);
        grid.padding = new RectOffset(4, 18, 4, 4);
        grid.cellSize = new Vector2(164f, 154f);

        fitter = contentGo.AddComponent<LobbyShopGridFitter>();
        var fitterComp = contentGo.AddComponent<ContentSizeFitter>();
        fitterComp.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitterComp.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var barGo = CreateUi("Scrollbar", scrollGo.transform);
        var barRect = barGo.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(1f, 0f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot = new Vector2(1f, 1f);
        barRect.sizeDelta = new Vector2(10f, 0f);
        barRect.anchoredPosition = Vector2.zero;
        var barImg = barGo.AddComponent<Image>();
        ApplyRounded(barImg, new Color(1f, 1f, 1f, 0.10f));

        var handleGo = CreateUi("Handle", barGo.transform);
        Stretch(handleGo.GetComponent<RectTransform>(), new Vector2(1f, 4f), new Vector2(1f, 4f));
        var handleImg = handleGo.AddComponent<Image>();
        ApplyRounded(handleImg, new Color(1f, 1f, 1f, 0.45f));

        var scrollbar = barGo.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handleGo.GetComponent<RectTransform>();
        scrollbar.targetGraphic = handleImg;
        scrollbar.transition = Selectable.Transition.None;

        scrollRect.content = itemContent;
        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scrollRect.verticalScrollbarSpacing = 4f;
    }

    private static Button CreateTab(Transform parent, TMP_FontAsset font, LobbyShopCategory category, int index)
    {
        var go = CreateUi("Tab_" + index, parent); go.AddComponent<LayoutElement>().preferredWidth = 150;
        var img = go.AddComponent<Image>();
        ApplyRounded(img, new Color(0.10f, 0.10f, 0.12f, 0.28f));
        img.raycastTarget = true;
        var button = go.AddComponent<Button>();
        button.targetGraphic = img;
        button.transition = Selectable.Transition.None;

        var labelGo = CreateUi("Text", go.transform);
        Stretch(labelGo.GetComponent<RectTransform>());
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        ApplyFont(tmp, font);
        tmp.fontSize = 26f;
        tmp.color = new Color(1f, 1f, 1f, 0.62f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        if (category != null)
            tmp.text = category.displayName.korean;

        int captured = index;
        button.onClick.AddListener(() =>
        {
            var panel = go.GetComponentInParent<LobbyShopPanel>(true);
            panel?.SelectCategory(captured);
        });
        return button;
    }

    public static void Place(RectTransform rect,float x0,float y0,float x1,float y1)
    {
        rect.anchorMin=new Vector2(x0,y0); rect.anchorMax=new Vector2(x1,y1);
        rect.offsetMin=rect.offsetMax=Vector2.zero;
    }
    public static TextMeshProUGUI Label(string name,Transform parent,float size)
    {
        var tmp=CreateUi(name,parent).AddComponent<TextMeshProUGUI>();
        ApplyFont(tmp,FindFont()); tmp.fontSize=size; tmp.color=Color.white; tmp.raycastTarget=false;
        tmp.enableAutoSizing=true; tmp.fontSizeMin=size*.75f; tmp.fontSizeMax=size;
        tmp.overflowMode=TextOverflowModes.Ellipsis;
        return tmp;
    }
    public static LobbyShopCellUI CreateCell(Transform parent, TMP_FontAsset font, int index)
    {
        var go=CreateUi("Cell_"+index,parent);
        var card=go.AddComponent<Image>(); ApplyRounded(card,new Color(.13f,.14f,.16f));
        var select=go.AddComponent<Button>(); select.targetGraphic=card; select.transition=Selectable.Transition.None;
        var inner=CreateUi("CardFill",go.transform).AddComponent<Image>();
        Stretch(inner.rectTransform,new Vector2(4,4),new Vector2(4,4));
        ApplyRounded(inner,new Color(.13f,.14f,.16f)); inner.raycastTarget=false;
        var icon=CreateUi("Icon",go.transform).AddComponent<Image>(); icon.preserveAspect=true; icon.raycastTarget=false;
        Place(icon.rectTransform,.06f,.33f,.91f,.92f);
        var name=Label("Name",go.transform,29); Place(name.rectTransform,.05f,.18f,.96f,.36f);
        var coin=CreateUi("Coin",go.transform).AddComponent<Image>(); coin.preserveAspect=true; coin.raycastTarget=false;
        Place(coin.rectTransform,.05f,.035f,.14f,.19f);
        var cost=Label("Cost",go.transform,28); Place(cost.rectTransform,.16f,.025f,.6f,.20f);
        cost.color=new Color(1,.83f,.22f);
        var weight=CreateUi("WeightIcon",go.transform).AddComponent<ShopWeightGraphic>(); weight.raycastTarget=false;
        Place(weight.rectTransform,.77f,.82f,.84f,.95f);
        var value=Label("Weight",go.transform,24); Place(value.rectTransform,.86f,.8f,.99f,.97f);
        var warning=Label("EquipStatus",go.transform,21); Place(warning.rectTransform,.59f,.03f,.98f,.19f);
        warning.color=new Color(1,.58f,.12f); warning.alignment=TextAlignmentOptions.MidlineRight;
        var cell=go.AddComponent<LobbyShopCellUI>();
        cell.Setup(index,card,icon,name,cost,coin,select);
        return cell;
    }
    public static TMP_FontAsset FindFont()
    {
        return LobbyPanelChrome.FindFont();
    }

    private static void SizeLayout(GameObject go, float width, float height, bool expandWidth)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null)
            le = go.AddComponent<LayoutElement>();
        if (width > 0f)
        {
            le.preferredWidth = width;
            le.minWidth = width;
        }
        le.preferredHeight = height;
        le.minHeight = height;
        le.flexibleWidth = expandWidth ? 1f : 0f;
        le.flexibleHeight = 0f;
    }

    private static void ApplyRounded(Image image, Color color)
    {
        LobbyPanelChrome.ApplyRounded(image, color);
    }

    private static Sprite ResolveCoinSprite()
    {
        return LobbyPanelChrome.ResolveCoinSprite();
    }

    private static void ApplyFont(TextMeshProUGUI tmp, TMP_FontAsset font)
    {
        LobbyPanelChrome.ApplyFont(tmp, font, true);
    }

    private static GameObject CreateUi(string name, Transform parent)
    {
        return LobbyPanelChrome.CreateUi(name, parent);
    }

    private static void Stretch(RectTransform rect)
    {
        LobbyPanelChrome.Stretch(rect);
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        LobbyPanelChrome.Stretch(rect, min, max);
    }
}
