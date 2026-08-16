using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 00_Title 씬의 PersistentSystems / OptionsCanvas에 옵션 패널을 만들고,
/// 메뉴 LocalizedText·Option 버튼 연결까지 한 번에 설정합니다.
/// 메뉴: Tools → Setup Title Options UI
/// (씬 연결·한영 문구가 비면 Tools → Setup Title Options UI 다시 실행)
/// </summary>
public static class SetupTitleOptionsUI
{
    private const string MenuPath = "Tools/Setup Title Options UI";
    private const string TitleScenePath = "Assets/Scenes/00_Title.unity";
    private const string AutoRunFlagPath = "Assets/Editor/SetupTitleOptionsUI.run";

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
        var scene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
        SetupInOpenScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[SetupTitleOptionsUI] 00_Title 옵션 UI 구성 완료.");
    }

    public static void SetupInOpenScene()
    {
        var persistent = GameObject.Find("PersistentSystems");
        if (persistent == null)
        {
            persistent = new GameObject("PersistentSystems");
            Undo.RegisterCreatedObjectUndo(persistent, "Create PersistentSystems");
        }

        if (persistent.GetComponent<LanguageManager>() == null)
            Undo.AddComponent<LanguageManager>(persistent);
        var optionsUI = persistent.GetComponent<OptionsUI>();
        if (optionsUI == null)
            optionsUI = Undo.AddComponent<OptionsUI>(persistent);

        var optionsCanvasTf = persistent.transform.Find("OptionsCanvas");
        GameObject optionsCanvasGo;
        if (optionsCanvasTf == null)
        {
            optionsCanvasGo = CreateOptionsCanvas(persistent.transform);
            Undo.RegisterCreatedObjectUndo(optionsCanvasGo, "Create OptionsCanvas");
        }
        else
        {
            optionsCanvasGo = optionsCanvasTf.gameObject;
            EnsureCanvasSetup(optionsCanvasGo);
        }

        // 기존 패널이 있으면 지우고 새로 구성 (중복 방지)
        var existingPanel = optionsCanvasGo.transform.Find("OptionsPanel");
        if (existingPanel != null)
            Undo.DestroyObjectImmediate(existingPanel.gameObject);

        var panel = CreateOptionsPanel(optionsCanvasGo.transform);
        Undo.RegisterCreatedObjectUndo(panel, "Create OptionsPanel");

        var koreanBtn = panel.transform.Find("Button_Korean").GetComponent<Button>();
        var englishBtn = panel.transform.Find("Button_English").GetComponent<Button>();
        var backBtn = panel.transform.Find("Button_Back").GetComponent<Button>();

        var so = new SerializedObject(optionsUI);
        so.FindProperty("panelRoot").objectReferenceValue = panel;
        so.FindProperty("koreanButton").objectReferenceValue = koreanBtn;
        so.FindProperty("englishButton").objectReferenceValue = englishBtn;
        so.FindProperty("backButton").objectReferenceValue = backBtn;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(optionsUI);

        // 시작 시 닫힌 상태로
        panel.SetActive(false);

        SetupMenuLocalizedTextsAndOptionButton();
    }

    private static GameObject CreateOptionsCanvas(Transform parent)
    {
        var go = new GameObject("OptionsCanvas", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        EnsureCanvasSetup(go);
        return go;
    }

    private static void EnsureCanvasSetup(GameObject go)
    {
        var canvas = go.GetComponent<Canvas>();
        if (canvas == null)
            canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        canvas.overrideSorting = true;

        var scaler = go.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        if (go.GetComponent<GraphicRaycaster>() == null)
            go.AddComponent<GraphicRaycaster>();

        // Overlay Canvas가 일반 Transform 자식일 때 scale 0이 되는 문제 방지
        go.transform.localScale = Vector3.one;

        var rect = go.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.localScale = Vector3.one;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }
    }

    private static GameObject CreateOptionsPanel(Transform canvas)
    {
        var panel = CreateUIObject("OptionsPanel", canvas);
        var panelRect = panel.GetComponent<RectTransform>();
        StretchFull(panelRect);
        var dim = panel.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);
        dim.raycastTarget = true;

        var box = CreateUIObject("Content", panel.transform);
        var boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(420, 320);
        boxRect.anchoredPosition = Vector2.zero;
        var boxImg = box.AddComponent<Image>();
        boxImg.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

        CreateLocalizedLabel("Text_Language", box.transform, "언어", "Language", new Vector2(0, 110), 28);

        CreateOptionButton("Button_Korean", box.transform, "한국어", "한국어", new Vector2(0, 40));
        CreateOptionButton("Button_English", box.transform, "English", "English", new Vector2(0, -30));
        CreateOptionButton("Button_Back", box.transform, "뒤로", "Back", new Vector2(0, -100));

        return panel;
    }

    private static void CreateLocalizedLabel(string name, Transform parent, string korean, string english, Vector2 pos, int fontSize)
    {
        var go = CreateUIObject(name, parent);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(360, 40);
        rect.anchoredPosition = pos;

        var uiText = go.AddComponent<Text>();
        uiText.text = korean;
        uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        uiText.fontSize = fontSize;
        uiText.alignment = TextAnchor.MiddleCenter;
        uiText.color = Color.white;
        uiText.raycastTarget = false;

        var loc = go.AddComponent<LocalizedText>();
        SetLocalizedStrings(loc, korean, english);
    }

    private static GameObject CreateOptionButton(string name, Transform parent, string korean, string english, Vector2 pos)
    {
        var go = CreateUIObject(name, parent);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(300, 50);
        rect.anchoredPosition = pos;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.25f, 0.25f, 0.95f);
        go.AddComponent<Button>();

        var textGo = CreateUIObject("Text", go.transform);
        StretchFull(textGo.GetComponent<RectTransform>());
        var uiText = textGo.AddComponent<Text>();
        uiText.text = korean;
        uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        uiText.fontSize = 24;
        uiText.alignment = TextAnchor.MiddleCenter;
        uiText.color = Color.white;

        var loc = textGo.AddComponent<LocalizedText>();
        SetLocalizedStrings(loc, korean, english);
        return go;
    }

    private static void SetupMenuLocalizedTextsAndOptionButton()
    {
        var menu = GameObject.Find("Menu");
        if (menu == null)
        {
            Debug.LogWarning("[SetupTitleOptionsUI] Menu 오브젝트를 찾지 못했습니다.");
            return;
        }

        BindMenuButtonText(menu.transform, "New Game", "새 게임", "New Game");
        BindMenuButtonText(menu.transform, "Load Game", "불러오기", "Load Game");
        BindMenuButtonText(menu.transform, "Option", "옵션", "Option");
        BindMenuButtonText(menu.transform, "Exit", "종료", "Exit");

        var titleCtrl = menu.GetComponent<TitleMenuController>();
        var optionGo = menu.transform.Find("Option");
        if (titleCtrl == null || optionGo == null)
            return;

        var optionBtn = optionGo.GetComponent<Button>();
        if (optionBtn == null)
            return;

        optionBtn.interactable = true;

        // 기존 클릭 리스너 비우고 OnOption 연결
        while (optionBtn.onClick.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(optionBtn.onClick, 0);

        UnityEventTools.AddPersistentListener(optionBtn.onClick, titleCtrl.OnOption);
        EditorUtility.SetDirty(optionBtn);
        EditorUtility.SetDirty(titleCtrl);
    }

    private static void BindMenuButtonText(Transform menu, string buttonName, string korean, string english)
    {
        var btn = menu.Find(buttonName);
        if (btn == null)
            return;

        var textTf = btn.Find("Text");
        if (textTf == null)
            return;

        var loc = textTf.GetComponent<LocalizedText>();
        if (loc == null)
            loc = Undo.AddComponent<LocalizedText>(textTf.gameObject);

        SetLocalizedStrings(loc, korean, english);
        EditorUtility.SetDirty(loc);
    }

    private static void SetLocalizedStrings(LocalizedText loc, string korean, string english)
    {
        var so = new SerializedObject(loc);
        so.FindProperty("texts").FindPropertyRelative("korean").stringValue = korean;
        so.FindProperty("texts").FindPropertyRelative("english").stringValue = english;
        so.FindProperty("editorPreviewLanguage").enumValueIndex = (int)GameLanguage.Korean;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(loc);
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = 5; // UI
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
}
