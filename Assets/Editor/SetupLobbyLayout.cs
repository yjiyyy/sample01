using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 03_Lobby를 왼쪽 캐릭터 + 오른쪽 텍스트 메뉴 + 상단 바 레이아웃으로 구성합니다.
/// 메뉴: Tools → Setup Lobby Layout
/// </summary>
public static class SetupLobbyLayout
{
    private const string MenuPath = "Tools/Setup Lobby Layout";
    private const string ScenePath = "Assets/Scenes/03_Lobby.unity";
    private const string AutoRunFlagPath = "Assets/Editor/SetupLobbyLayout.run";
    private const string FontPath = "Assets/Arts/Fonts/BlackHanSans-Regular SDF.asset";
    private const string FallbackCharacterPath = "Assets/Data/PlayerData/001_SO.asset";

    private static readonly string[] OldButtonNames =
    {
        "캐릭터 변경",
        "인벤토리",
        "캐릭터 업그레이드",
        "상점",
        "전투 시작"
    };

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

    [MenuItem(MenuPath)]
    public static void Setup()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ApplyToOpenScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[SetupLobbyLayout] 03_Lobby 레이아웃 구성 완료.");
    }

    public static void ApplyToOpenScene()
    {
        var canvasGo = GameObject.Find("LobbyCanvas");
        if (canvasGo == null)
        {
            Debug.LogError("[SetupLobbyLayout] LobbyCanvas를 찾지 못했습니다.");
            return;
        }

        var canvas = canvasGo.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.additionalShaderChannels =
                AdditionalCanvasShaderChannels.TexCoord1 |
                AdditionalCanvasShaderChannels.Normal |
                AdditionalCanvasShaderChannels.Tangent;
        }

        var canvasRect = canvasGo.GetComponent<RectTransform>();
        DestroyNamedChildren(canvasRect, OldButtonNames);
        DestroyNamedChild(canvasRect, "TopBar");
        DestroyNamedChild(canvasRect, "RightMenu");
        DestroyNamedChild(canvasRect, "LobbyChrome");

        var existingMenuUi = canvasGo.GetComponent<LobbyMenuUI>();
        if (existingMenuUi != null)
            Object.DestroyImmediate(existingMenuUi);

        PlaceCharacterOnTheLeft();
        AssignFallbackCharacter();

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        var chrome = CreateUiObject("LobbyChrome", canvasRect);
        Stretch(chrome.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

        var topBar = CreateTopBar(chrome.transform, font);
        CreateRightMenu(chrome.transform, font);
        var menuUi = chrome.AddComponent<LobbyMenuUI>();

        WireMenuUi(menuUi, chrome.transform, topBar);

        chrome.transform.SetSiblingIndex(0);

        var shop = canvasRect.Find("ShopPanel");
        var stage = canvasRect.Find("StageSelectPanel");
        if (shop != null) shop.SetAsLastSibling();
        if (stage != null) stage.SetAsLastSibling();

        EditorUtility.SetDirty(canvasGo);
        if (menuUi != null)
            EditorUtility.SetDirty(menuUi);
    }

    private static void PlaceCharacterOnTheLeft()
    {
        var spawn = GameObject.Find("CharacterSpawnPoint");
        if (spawn == null)
            return;

        spawn.transform.position = new Vector3(-1.35f, 0f, -3.2f);
        spawn.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        EditorUtility.SetDirty(spawn);
    }

    private static void AssignFallbackCharacter()
    {
        var lobby = Object.FindFirstObjectByType<LobbyController>();
        if (lobby == null)
            return;

        var fallback = AssetDatabase.LoadAssetAtPath<CharacterDataSO>(FallbackCharacterPath);
        var so = new SerializedObject(lobby);
        if (so.FindProperty("fallbackCharacter") != null)
            so.FindProperty("fallbackCharacter").objectReferenceValue = fallback;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(lobby);
    }

    private static GameObject CreateTopBar(Transform canvas, TMP_FontAsset font)
    {
        var topBar = CreateUiObject("TopBar", canvas);
        var rect = topBar.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, 80f);
        rect.anchoredPosition = Vector2.zero;

        var bg = topBar.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.09f, 1f);
        bg.raycastTarget = true;

        var line = CreateUiObject("BottomLine", topBar.transform);
        var lineRect = line.GetComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0f, 0f);
        lineRect.anchorMax = new Vector2(1f, 0f);
        lineRect.pivot = new Vector2(0.5f, 0f);
        lineRect.sizeDelta = new Vector2(0f, 2f);
        lineRect.anchoredPosition = Vector2.zero;
        var lineImg = line.AddComponent<Image>();
        lineImg.color = new Color(1f, 1f, 1f, 0.18f);
        lineImg.raycastTarget = false;

        var moneyLabel = CreateTmpLabel("MoneyText", topBar.transform, font, "0", 30,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(36f, 0f), new Vector2(280f, 48f),
            TextAlignmentOptions.MidlineLeft);
        HudResourceIcons.GetOrCreateIcon(moneyLabel, HudResourceIcons.Coin, HudResourceIcons.CoinChildName, 40f);

        var gemLabel = CreateTmpLabel("GemText", topBar.transform, font, "0", 30,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(330f, 0f), new Vector2(240f, 48f),
            TextAlignmentOptions.MidlineLeft);
        HudResourceIcons.GetOrCreateIcon(gemLabel, HudResourceIcons.Gem, HudResourceIcons.GemChildName, 40f);

        var options = CreateTextButton("OptionsButton", topBar.transform, font, "옵션", 28,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(160f, 52f),
            TextAlignmentOptions.MidlineRight);
        SetLocalized(options.transform.Find("Text"), "옵션", "Options");

        return topBar;
    }

    private static GameObject CreateRightMenu(Transform canvas, TMP_FontAsset font)
    {
        var menu = CreateUiObject("RightMenu", canvas);
        var rect = menu.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.52f, 0.08f);
        rect.anchorMax = new Vector2(0.97f, 0.82f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(1f, 0.5f);

        var layout = menu.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.spacing = 6f;
        layout.padding = new RectOffset(12, 8, 8, 8);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateMenuItem(menu.transform, font, "Menu_CharacterChange", "캐릭터 변경", "Change Character", false);
        CreateMenuItem(menu.transform, font, "Menu_Upgrade", "업그레이드", "Upgrade", false);
        CreateMenuItem(menu.transform, font, "Menu_Shop", "상점", "Shop", false);
        CreateMenuItem(menu.transform, font, "Menu_Inventory", "인벤토리", "Inventory", false);
        CreateMenuItem(menu.transform, font, "Menu_StartBattle", "전투 시작", "Play", true);

        return menu;
    }

    private static void CreateMenuItem(Transform parent, TMP_FontAsset font, string name, string korean, string english, bool selected)
    {
        var go = CreateUiObject(name, parent);
        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0f);
        img.raycastTarget = true;

        var button = go.AddComponent<Button>();
        button.targetGraphic = img;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 56f;
        le.preferredHeight = selected ? 110f : 64f;
        le.flexibleWidth = 1f;

        var textGo = CreateUiObject("Text", go.transform);
        Stretch(textGo.GetComponent<RectTransform>(), new Vector2(8f, 4f), new Vector2(8f, 4f));

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = korean;
        tmp.fontSize = selected ? 72f : 42f;
        tmp.color = selected ? Color.white : new Color(1f, 1f, 1f, 0.62f);
        tmp.alignment = TextAlignmentOptions.MidlineRight;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;

        var outline = textGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(2f, -2f);

        SetLocalized(textGo.transform, korean, english);
    }

    private static void WireMenuUi(LobbyMenuUI menuUi, Transform chrome, GameObject topBar)
    {
        if (menuUi == null || topBar == null || chrome == null)
            return;

        var items = new[]
        {
            chrome.Find("RightMenu/Menu_CharacterChange"),
            chrome.Find("RightMenu/Menu_Upgrade"),
            chrome.Find("RightMenu/Menu_Shop"),
            chrome.Find("RightMenu/Menu_Inventory"),
            chrome.Find("RightMenu/Menu_StartBattle")
        };

        var so = new SerializedObject(menuUi);
        var entries = so.FindProperty("entries");
        entries.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var e = entries.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("button").objectReferenceValue = item != null ? item.GetComponent<Button>() : null;
            e.FindPropertyRelative("label").objectReferenceValue = item != null ? item.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            e.FindPropertyRelative("layoutElement").objectReferenceValue = item != null ? item.GetComponent<LayoutElement>() : null;
            e.FindPropertyRelative("action").enumValueIndex = i;
        }

        so.FindProperty("defaultSelectedIndex").intValue = 4;
        so.FindProperty("moneyText").objectReferenceValue = topBar.transform.Find("MoneyText").GetComponent<TextMeshProUGUI>();
        var gemLabelTf = topBar.transform.Find("GemText") ?? topBar.transform.Find("JamText");
        so.FindProperty("gemText").objectReferenceValue = gemLabelTf != null ? gemLabelTf.GetComponent<TextMeshProUGUI>() : null;

        var moneyTextTf = topBar.transform.Find("MoneyText");
        var moneyIconTf = moneyTextTf != null ? moneyTextTf.Find("Icon_Coin") : null;
        var gemIconTf = gemLabelTf != null ? gemLabelTf.Find("Icon_Gem") : null;
        so.FindProperty("moneyIcon").objectReferenceValue = moneyIconTf != null ? moneyIconTf.GetComponent<Image>() : null;
        so.FindProperty("gemIcon").objectReferenceValue = gemIconTf != null ? gemIconTf.GetComponent<Image>() : null;
        so.FindProperty("optionsButton").objectReferenceValue = topBar.transform.Find("OptionsButton").GetComponent<Button>();
        so.FindProperty("characterSelectScene").stringValue = SceneNames.CharacterSelection;

        var canvas = chrome.parent;
        var shop = canvas != null ? canvas.Find("ShopPanel") : null;
        var stage = canvas != null ? canvas.Find("StageSelectPanel") : null;
        so.FindProperty("shopPanel").objectReferenceValue = shop != null ? shop.GetComponent<ShopPanel>() : null;
        so.FindProperty("stageSelectPanel").objectReferenceValue = stage != null ? stage.GetComponent<StageSelectPanel>() : null;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject CreateTextButton(string name, Transform parent, TMP_FontAsset font, string label, float fontSize,
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
        img.raycastTarget = true;
        go.AddComponent<Button>().targetGraphic = img;

        var textGo = CreateUiObject("Text", go.transform);
        Stretch(textGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = align;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;

        var outline = textGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        return go;
    }

    private static TextMeshProUGUI CreateTmpLabel(string name, Transform parent, TMP_FontAsset font, string text, float fontSize,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, TextAlignmentOptions align)
    {
        var go = CreateUiObject(name, parent);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPos;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = align;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        return tmp;
    }

    private static void SetLocalized(Transform textTf, string korean, string english)
    {
        if (textTf == null)
            return;

        var loc = textTf.GetComponent<LocalizedText>();
        if (loc == null)
            loc = textTf.gameObject.AddComponent<LocalizedText>();

        var so = new SerializedObject(loc);
        so.FindProperty("texts").FindPropertyRelative("korean").stringValue = korean;
        so.FindProperty("texts").FindPropertyRelative("english").stringValue = english;
        so.FindProperty("editorPreviewLanguage").enumValueIndex = (int)GameLanguage.Korean;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(loc);
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
    }

    private static void DestroyNamedChildren(Transform parent, string[] names)
    {
        foreach (var name in names)
            DestroyNamedChild(parent, name);
    }

    private static void DestroyNamedChild(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null)
            Object.DestroyImmediate(child.gameObject);
    }
}
