using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// 타이틀 씬이 없을 때(스테이지 바로 Play) 옵션 창과 언어 관리를 만듭니다.
/// </summary>
public static class OptionsUIRuntimeFactory
{
    public static OptionsUI Create()
    {
        if (OptionsUI.Instance != null)
            return OptionsUI.Instance;

        EnsureEventSystem();

        var persistent = new GameObject("PersistentSystems");
        Object.DontDestroyOnLoad(persistent);

        var canvasGo = CreateCanvas(persistent.transform);
        var panel = CreatePanel(canvasGo.transform);
        panel.SetActive(false);

        persistent.AddComponent<LanguageManager>();
        return persistent.AddComponent<OptionsUI>();
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
        Object.DontDestroyOnLoad(esGo);
    }

    private static GameObject CreateCanvas(Transform parent)
    {
        var go = new GameObject("OptionsCanvas", typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        canvas.overrideSorting = true;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();

        var rect = go.GetComponent<RectTransform>();
        rect.localScale = Vector3.one;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        return go;
    }

    private static GameObject CreatePanel(Transform canvas)
    {
        var panel = CreateUi("OptionsPanel", canvas);
        StretchFull(panel.GetComponent<RectTransform>());
        var dim = panel.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);
        dim.raycastTarget = true;

        var box = CreateUi("Content", panel.transform);
        var boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(420f, 320f);
        boxRect.anchoredPosition = Vector2.zero;
        var boxImg = box.AddComponent<Image>();
        boxImg.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

        CreateLocalizedLabel("Text_Language", box.transform, "언어", "Language", new Vector2(0f, 110f), 28);
        CreateOptionButton("Button_Korean", box.transform, "한국어", "한국어", new Vector2(0f, 40f));
        CreateOptionButton("Button_English", box.transform, "English", "English", new Vector2(0f, -30f));
        CreateOptionButton("Button_Back", box.transform, "뒤로", "Back", new Vector2(0f, -100f));
        return panel;
    }

    private static void CreateLocalizedLabel(string name, Transform parent, string korean, string english, Vector2 pos, int fontSize)
    {
        var go = CreateUi(name, parent);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(360f, 40f);
        rect.anchoredPosition = pos;

        var uiText = go.AddComponent<Text>();
        uiText.text = korean;
        uiText.font = GetUiFont();
        uiText.fontSize = fontSize;
        uiText.alignment = TextAnchor.MiddleCenter;
        uiText.color = Color.white;
        uiText.raycastTarget = false;

        go.AddComponent<LocalizedText>().SetTexts(korean, english);
    }

    private static GameObject CreateOptionButton(string name, Transform parent, string korean, string english, Vector2 pos)
    {
        var go = CreateUi(name, parent);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(300f, 50f);
        rect.anchoredPosition = pos;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.25f, 0.25f, 0.95f);
        go.AddComponent<Button>();

        var textGo = CreateUi("Text", go.transform);
        StretchFull(textGo.GetComponent<RectTransform>());
        var uiText = textGo.AddComponent<Text>();
        uiText.text = korean;
        uiText.font = GetUiFont();
        uiText.fontSize = 24;
        uiText.alignment = TextAnchor.MiddleCenter;
        uiText.color = Color.white;

        textGo.AddComponent<LocalizedText>().SetTexts(korean, english);
        return go;
    }

    private static GameObject CreateUi(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static Font GetUiFont()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }
}
