using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>씬 YAML 변경 없이 편집 모드와 실행 중 같은 UI를 만듭니다.</summary>
public static class LobbyUpgradePanelBuilder
{
    private static readonly Color Card = new Color(0.17f, 0.18f, 0.20f, 1f);

    public static LobbyUpgradePanel Build(RectTransform canvas)
    {
        if (canvas == null) return null;
        var existing = canvas.Find(LobbyUpgradePanel.ObjectName);
        var panel = existing != null ? existing.GetComponent<LobbyUpgradePanel>() : null;
        if (panel == null)
        {
            var go = LobbyPanelChrome.CreateUi(LobbyUpgradePanel.ObjectName, canvas);
            go.SetActive(false);
            LobbyPanelChrome.Stretch(go.GetComponent<RectTransform>());
            panel = go.AddComponent<LobbyUpgradePanel>();
        }
        panel.EnsureBuilt(true);
        return panel;
    }

    public static void RebuildInto(LobbyUpgradePanel panel)
    {
        var catalog = panel.Catalog;
        if (catalog == null) return;
        // Destroy는 프레임 끝에 처리되므로 이전 UI는 즉시 비활성화합니다.
        for (int i = 0; i < panel.transform.childCount; i++) panel.transform.GetChild(i).gameObject.SetActive(false);
        LobbyPanelChrome.ClearChildren(panel.transform);
        var font = LobbyPanelChrome.FindFont();
        var back = LobbyPanelChrome.CreateBackButton(panel.transform, font, catalog.backLabel);
        var upgrade = LobbyPanelChrome.CreateActionButton(panel.transform, font, catalog.upgradeLabel, "UpgradeButton");
        var buttonRect = upgrade.GetComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(520f, 94f);
        upgrade.GetComponent<Image>().color = LobbyUpgradeRowUI.Gold;
        var colors = upgrade.colors;
        colors.normalColor = colors.highlightedColor = colors.selectedColor = Color.white;
        colors.pressedColor = new Color(0.88f, 0.88f, 0.88f);
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.7f);
        colors.fadeDuration = 0f;
        upgrade.colors = colors;
        var buttonText = upgrade.GetComponentInChildren<LocalizedText>(true);
        var buttonTmp = buttonText.GetComponent<TextMeshProUGUI>();
        Place(buttonTmp.rectTransform, 0.025f, 0.12f, 0.69f, 0.88f);
        buttonTmp.fontSize = 34f; buttonTmp.enableAutoSizing = true; buttonTmp.fontSizeMin = 22f; buttonTmp.fontSizeMax = 34f;
        var actionCoin = Picture("Coin", upgrade.transform, LobbyPanelChrome.ResolveCoinSprite());
        Place(actionCoin.rectTransform, 0.70f, 0.22f, 0.80f, 0.78f);
        var actionPrice = Label("Price", upgrade.transform, font, 34f, TextAlignmentOptions.Center);
        Place(actionPrice.rectTransform, 0.8f, 0.1f, 0.99f, 0.9f);
        actionPrice.color = new Color(0.12f, 0.13f, 0.15f);
        var status = Label("PurchaseStatus", panel.transform, font, 24f, TextAlignmentOptions.BottomRight);
        var sr = status.rectTransform; sr.anchorMin = sr.anchorMax = new Vector2(1f, 0f); sr.pivot = new Vector2(1f, 0f);
        sr.sizeDelta = new Vector2(520f, 35f); sr.anchoredPosition = new Vector2(-36f, 132f);

        LobbyPanelChrome.CreateNpcBlock(panel.transform, font, catalog.npcPortrait, catalog.idleLine,
            out Image portrait, out LocalizedText line);
        var title = Label("CharacterTitle", panel.transform, font, 38f, TextAlignmentOptions.Center);
        Place(title.rectTransform, 0.30f, 0.865f, 0.755f, 0.925f);

        var list = LobbyPanelChrome.CreateUi("StatList", panel.transform);
        Place(list.GetComponent<RectTransform>(), 0.30f, 0.205f, 0.755f, 0.855f);
        var viewport = LobbyPanelChrome.CreateUi("Viewport", list.transform).GetComponent<RectTransform>();
        LobbyPanelChrome.Stretch(viewport); viewport.gameObject.AddComponent<RectMask2D>();
        var content = LobbyPanelChrome.CreateUi("Content", viewport).GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f); content.offsetMin = content.offsetMax = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 16f; layout.childControlWidth = true; layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = list.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30f;
        var rows = new List<LobbyUpgradeRowUI>();
        var seen = new HashSet<LobbyStatId>();
        var entries = panel.Entries;
        if (entries != null)
            foreach (var entry in entries)
                if (entry != null && seen.Add(entry.id)) rows.Add(CreateRow(content, font, entry));

        var legend = Label("Legend", panel.transform, font, 24f, TextAlignmentOptions.Center);
        Place(legend.rectTransform, 0.30f, 0.145f, 0.755f, 0.19f);
        panel.AssignBuiltRefs(catalog, LobbyPanelChrome.FindRightMenu(), back, upgrade, portrait, line,
            rows.ToArray(), title, legend, actionPrice, actionCoin, status);
    }

    private static LobbyUpgradeRowUI CreateRow(Transform parent, TMP_FontAsset font, LobbyStatUpgradeEntry entry)
    {
        var root = LobbyPanelChrome.CreateUi("Row_" + entry.id, parent);
        var size = root.AddComponent<LayoutElement>(); size.minHeight = size.preferredHeight = 156f;
        var border = root.AddComponent<Image>(); LobbyPanelChrome.ApplyRounded(border, Color.clear);
        var button = root.AddComponent<Button>(); button.targetGraphic = border; button.transition = Selectable.Transition.None;
        var body = Picture("Card", root.transform, null); LobbyPanelChrome.ApplyRounded(body, Card);
        LobbyPanelChrome.Stretch(body.rectTransform, new Vector2(4f, 4f), new Vector2(4f, 4f));
        var icon = Picture("Icon", root.transform, entry.icon); Place(icon.rectTransform, 0.025f, 0.37f, 0.13f, 0.88f);
        var name = Label("Name", root.transform, font, 35f, TextAlignmentOptions.Left);
        Place(name.rectTransform, 0.155f, 0.66f, 0.36f, 0.94f);
        var value = Label("Value", root.transform, font, 37f, TextAlignmentOptions.Right);
        Place(value.rectTransform, 0.36f, 0.66f, 0.96f, 0.94f);
        var bar = Picture("Bar", root.transform, null); LobbyPanelChrome.ApplyRounded(bar, new Color(0.04f, 0.05f, 0.06f, 0.65f));
        Place(bar.rectTransform, 0.155f, 0.47f, 0.96f, 0.59f);
        var total = Picture("GrowthFill", bar.transform, null); LobbyPanelChrome.ApplyRounded(total, LobbyUpgradeRowUI.Growth);
        var basis = Picture("BaseFill", bar.transform, null); LobbyPanelChrome.ApplyRounded(basis, new Color(0.95f, 0.94f, 0.90f));
        var detail = Label("Details", root.transform, font, 21f, TextAlignmentOptions.Left);
        Place(detail.rectTransform, 0.155f, 0.265f, 0.97f, 0.45f);
        detail.color = new Color(0.8f, 0.82f, 0.84f);
        var level = Label("Level", root.transform, font, 24f, TextAlignmentOptions.Left);
        Place(level.rectTransform, 0.155f, 0.04f, 0.65f, 0.25f);
        var coin = Picture("Coin", root.transform, LobbyPanelChrome.ResolveCoinSprite());
        Place(coin.rectTransform, 0.76f, 0.06f, 0.82f, 0.27f);
        var cost = Label("Cost", root.transform, font, 29f, TextAlignmentOptions.Right);
        Place(cost.rectTransform, 0.82f, 0.035f, 0.96f, 0.29f);
        var row = root.AddComponent<LobbyUpgradeRowUI>();
        row.Setup(entry.id, icon, border, basis, total, coin, name, value, detail, level, cost, button);
        return row;
    }

    private static Image Picture(string name, Transform parent, Sprite sprite)
    {
        var image = LobbyPanelChrome.CreateUi(name, parent).AddComponent<Image>();
        image.sprite = sprite; image.preserveAspect = sprite != null; image.raycastTarget = false;
        return image;
    }
    private static TextMeshProUGUI Label(string name, Transform parent, TMP_FontAsset font, float size, TextAlignmentOptions align)
    {
        var text = LobbyPanelChrome.CreateUi(name, parent).AddComponent<TextMeshProUGUI>();
        LobbyPanelChrome.ApplyFont(text, font, size >= 30f); text.fontSize = size; text.color = Color.white;
        text.alignment = align; text.raycastTarget = false; text.enableWordWrapping = false;
        text.enableAutoSizing = true; text.fontSizeMin = size * 0.75f; text.fontSizeMax = size;
        return text;
    }
    private static void Place(RectTransform rect, float x1, float y1, float x2, float y2)
    {
        rect.anchorMin = new Vector2(x1, y1); rect.anchorMax = new Vector2(x2, y2);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
