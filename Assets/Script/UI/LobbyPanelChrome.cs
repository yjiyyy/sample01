using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 로비 전체 화면 패널(업그레이드·상점)이 함께 쓰는 공통 부품입니다.
/// NPC 액자와 대사창, 좌하단 돌아가기 버튼, 우하단 실행 버튼을 여기서만 만듭니다.
/// 크기·글자 크기를 바꾸려면 이 파일의 숫자만 고치면 두 화면에 같이 적용됩니다.
/// </summary>
public static class LobbyPanelChrome
{
    private const string FontResourceHint = "BlackHanSans";

    // 좌하단 돌아가기 / 우하단 실행 버튼 (좌우 대칭)
    public static readonly Vector2 ButtonSize = new Vector2(250f, 80f);
    public static readonly Vector2 ButtonMargin = new Vector2(36f, 28f);
    public const float ButtonFontSize = 50f;

    // NPC 영역: 액자는 위, 대사창은 그 아래
    public static readonly Vector2 NpcBlockSize = new Vector2(380f, 400f);
    public static readonly Vector2 NpcBlockMargin = new Vector2(-32f, -100f);
    public static readonly Vector2 NpcFrameSize = new Vector2(367f, 362f);
    public const float NpcFramePadding = 10f;
    public const float TalkCardTop = -379f;
    public const float TalkCardHeight = 239f;
    public const float TalkTextPadding = 14f;
    public const float TalkFontSize = 35f;

    private static readonly Color PanelDark = new Color(0.10f, 0.10f, 0.12f, 0.40f);
    private static readonly Color FrameColor = new Color(0.13f, 0.13f, 0.15f, 1f);
    private static readonly Color TalkCardColor = new Color(0.10f, 0.10f, 0.12f, 0.42f);
    private static readonly Color TalkTextColor = new Color(1f, 1f, 1f, 0.9f);
    private static readonly Color ActionColor = new Color(0.86f, 0.87f, 0.90f, 0.92f);
    private static readonly Color ActionTextColor = new Color(0.16f, 0.17f, 0.20f, 1f);

    /// <summary>
    /// 좌하단 돌아가기 버튼.
    /// </summary>
    public static Button CreateBackButton(Transform parent, TMP_FontAsset font, LocalizedString label)
    {
        return CreateCornerButton(parent, font, label, "BackButton", false, PanelDark, Color.white);
    }

    /// <summary>
    /// 우하단 실행 버튼(업그레이드·구매). 돌아가기 버튼과 크기·글자 크기가 같습니다.
    /// </summary>
    public static Button CreateActionButton(Transform parent, TMP_FontAsset font, LocalizedString label, string objectName)
    {
        return CreateCornerButton(parent, font, label, objectName, true, ActionColor, ActionTextColor);
    }

    private static Button CreateCornerButton(
        Transform parent,
        TMP_FontAsset font,
        LocalizedString label,
        string objectName,
        bool rightSide,
        Color background,
        Color textColor)
    {
        var go = CreateUi(objectName, parent);
        var rect = go.GetComponent<RectTransform>();
        float anchorX = rightSide ? 1f : 0f;
        rect.anchorMin = new Vector2(anchorX, 0f);
        rect.anchorMax = new Vector2(anchorX, 0f);
        rect.pivot = new Vector2(anchorX, 0f);
        rect.sizeDelta = ButtonSize;
        rect.anchoredPosition = new Vector2(rightSide ? -ButtonMargin.x : ButtonMargin.x, ButtonMargin.y);

        var img = go.AddComponent<Image>();
        ApplyRounded(img, background);
        img.raycastTarget = true;
        var button = go.AddComponent<Button>();
        button.targetGraphic = img;

        var labelGo = CreateUi("Text", go.transform);
        Stretch(labelGo.GetComponent<RectTransform>());
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        ApplyFont(tmp, font, !rightSide);
        tmp.fontSize = ButtonFontSize;
        tmp.color = textColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        var loc = labelGo.AddComponent<LocalizedText>();
        loc.SetTexts(label.korean, label.english);
        return button;
    }

    /// <summary>
    /// 우측 상단 NPC 액자 + 그 아래 대사창. 이름 칸은 쓰지 않습니다.
    /// </summary>
    public static RectTransform CreateNpcBlock(
        Transform parent,
        TMP_FontAsset font,
        Sprite portraitSprite,
        LocalizedString idleLine,
        out Image portrait,
        out LocalizedText lineText)
    {
        var root = CreateUi("NpcBlock", parent).GetComponent<RectTransform>();
        root.anchorMin = new Vector2(1f, 1f);
        root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(1f, 1f);
        root.sizeDelta = NpcBlockSize;
        root.anchoredPosition = NpcBlockMargin;

        var frame = CreateUi("Frame", root);
        var frameRect = frame.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0.5f, 1f);
        frameRect.anchorMax = new Vector2(0.5f, 1f);
        frameRect.pivot = new Vector2(0.5f, 1f);
        frameRect.sizeDelta = NpcFrameSize;
        frameRect.anchoredPosition = Vector2.zero;
        var frameImg = frame.AddComponent<Image>();
        ApplyRounded(frameImg, FrameColor);
        frameImg.raycastTarget = false;

        var inner = CreateUi("Portrait", frame.transform);
        var pad = new Vector2(NpcFramePadding, NpcFramePadding);
        Stretch(inner.GetComponent<RectTransform>(), pad, pad);
        portrait = inner.AddComponent<Image>();
        portrait.color = Color.white;
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;
        if (portraitSprite != null)
            portrait.sprite = portraitSprite;

        var talkGo = CreateUi("TalkCard", root);
        var talkRect = talkGo.GetComponent<RectTransform>();
        talkRect.anchorMin = new Vector2(0f, 1f);
        talkRect.anchorMax = new Vector2(1f, 1f);
        talkRect.pivot = new Vector2(0.5f, 1f);
        talkRect.sizeDelta = new Vector2(0f, TalkCardHeight);
        talkRect.anchoredPosition = new Vector2(0f, TalkCardTop);
        var talkImg = talkGo.AddComponent<Image>();
        ApplyRounded(talkImg, TalkCardColor);
        talkImg.raycastTarget = false;

        var lineGo = CreateUi("NpcLine", talkGo.transform);
        var linePad = new Vector2(TalkTextPadding, TalkTextPadding);
        Stretch(lineGo.GetComponent<RectTransform>(), linePad, linePad);
        var lineTmp = lineGo.AddComponent<TextMeshProUGUI>();
        ApplyFont(lineTmp, font);
        lineTmp.fontSize = TalkFontSize;
        lineTmp.color = TalkTextColor;
        lineTmp.alignment = TextAlignmentOptions.TopLeft;
        lineTmp.enableWordWrapping = true;
        lineTmp.raycastTarget = false;
        lineText = lineGo.AddComponent<LocalizedText>();
        lineText.SetTexts(idleLine.korean, idleLine.english);

        return root;
    }

    /// <summary>
    /// 로비 우측 메뉴(전체 화면 패널을 열 때 숨길 대상).
    /// </summary>
    public static GameObject FindRightMenu()
    {
        var chrome = GameObject.Find("LobbyChrome");
        if (chrome == null)
            return null;
        var menu = chrome.transform.Find("RightMenu");
        return menu != null ? menu.gameObject : null;
    }

    public static TMP_FontAsset FindFont()
    {
        var tmps = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < tmps.Length; i++)
        {
            if (tmps[i] != null && tmps[i].font != null && tmps[i].font.name.Contains(FontResourceHint))
                return tmps[i].font;
        }
        for (int i = 0; i < tmps.Length; i++)
        {
            if (tmps[i] != null && tmps[i].font != null)
                return tmps[i].font;
        }
        return TMP_Settings.defaultFontAsset;
    }

    public static void ApplyFont(TextMeshProUGUI tmp, TMP_FontAsset font)
    {
        ApplyFont(tmp, font, true);
    }

    public static void ApplyFont(TextMeshProUGUI tmp, TMP_FontAsset font, bool outline)
    {
        if (font != null)
            tmp.font = font;
        if (!outline)
            return;

        var effect = tmp.GetComponent<Outline>();
        if (effect == null)
            effect = tmp.gameObject.AddComponent<Outline>();
        effect.effectColor = new Color(0f, 0f, 0f, 0.75f);
        effect.effectDistance = new Vector2(1.5f, -1.5f);
    }

    public static void ApplyRounded(Image image, Color color)
    {
        ApplySlicedSprite(image, "UiRound", color);
    }

    public static void ApplyPip(Image image, Color color)
    {
        ApplySlicedSprite(image, "UiPip", color);
    }

    private static void ApplySlicedSprite(Image image, string fileName, Color color)
    {
        var sprite = LobbyStatUpgradeCatalogSO.LoadSprite(fileName);
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
        }
        image.color = color;
    }

    public static Sprite ResolveCoinSprite()
    {
        var coin = HudResourceIcons.Coin;
        if (coin != null)
            return coin;

#if UNITY_EDITOR
        string[] candidates =
        {
            "Assets/Arts/UI/Icon/UI/Icon_Coin.png",
            "Assets/Arts/Icon/UI/Icon_Coin.png"
        };
        for (int i = 0; i < candidates.Length; i++)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(candidates[i]);
            if (sprite != null)
                return sprite;
        }
#endif
        return null;
    }

    public static GameObject CreateUi(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return go;
    }

    public static void Stretch(RectTransform rect)
    {
        Stretch(rect, Vector2.zero, Vector2.zero);
    }

    public static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = min;
        rect.offsetMax = -max;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    public static void ClearChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i);
            if (Application.isPlaying)
                Object.Destroy(child.gameObject);
            else
                Object.DestroyImmediate(child.gameObject);
        }
    }
}
