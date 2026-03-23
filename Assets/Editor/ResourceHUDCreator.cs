using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// 씬에 ResourceHUD(Canvas·Legacy Text·연결)를 한 번에 생성합니다.
/// - 메뉴: GameObject &gt; UI &gt; Resource HUD (Money/Jam)
/// - 메뉴: Tools &gt; UI &gt; Create Resource HUD (Money/Jam)
/// </summary>
public static class ResourceHUDCreator
{
    private const string GameObjectMenu = "GameObject/UI/Resource HUD (Money/Jam)";
    private const string ToolsMenu = "Tools/UI/Create Resource HUD (Money/Jam)";

    [MenuItem(GameObjectMenu, false, 10)]
    public static void CreateResourceHUDFromGameObjectMenu(MenuCommand menuCommand)
    {
        CreateResourceHUDInternal(menuCommand != null ? menuCommand.context as GameObject : null);
    }

    [MenuItem(ToolsMenu, false, 1)]
    public static void CreateResourceHUDFromToolsMenu()
    {
        CreateResourceHUDInternal(null);
    }

    private static void CreateResourceHUDInternal(GameObject contextSelection)
    {
        var ctx = contextSelection;
        Canvas canvas = null;
        if (ctx != null)
        {
            canvas = ctx.GetComponent<Canvas>();
            if (canvas == null)
                canvas = ctx.GetComponentInParent<Canvas>();
        }
        if (canvas == null)
            canvas = Object.FindFirstObjectByType<Canvas>();

        if (canvas == null)
            canvas = CreateCanvasWithEventSystem();

        var hudRoot = new GameObject("ResourceHUD");
        Undo.RegisterCreatedObjectUndo(hudRoot, "Create Resource HUD");
        hudRoot.transform.SetParent(canvas.transform, false);

        var rootRect = hudRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 1f);
        rootRect.anchorMax = new Vector2(0.5f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.sizeDelta = new Vector2(720f, 100f);
        rootRect.anchoredPosition = new Vector2(0f, -12f);

        var moneyGO = CreateTextChild(hudRoot.transform, "MoneyText", "Money: 0", new Vector2(0f, 0f), new Vector2(1f, 0.5f));
        var jamGO = CreateTextChild(hudRoot.transform, "JamText", "Jam: 0", new Vector2(0f, 0.5f), new Vector2(1f, 1f));

        var moneyText = moneyGO.GetComponent<Text>();
        var jamText = jamGO.GetComponent<Text>();

        var hud = hudRoot.AddComponent<ResourceHUD>();
        var so = new SerializedObject(hud);
        so.FindProperty("moneyText").objectReferenceValue = moneyText;
        so.FindProperty("jamText").objectReferenceValue = jamText;
        so.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = hudRoot;
        EditorGUIUtility.PingObject(hudRoot);
        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);

        Debug.Log("[ResourceHUDCreator] ResourceHUD 생성 완료. 플레이어에 PlayerResources가 있어야 숫자가 갱신됩니다.");
    }

    private static Canvas CreateCanvasWithEventSystem()
    {
        var canvasGO = new GameObject("Canvas");
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas for Resource HUD");

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(esGO, "Create EventSystem");
            esGO.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            esGO.AddComponent<InputSystemUIInputModule>();
#else
            esGO.AddComponent<StandaloneInputModule>();
#endif
        }

        return canvas;
    }

    private static GameObject CreateTextChild(Transform parent, string name, string initialText, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0f, 0.5f);
        rect.offsetMin = new Vector2(24f, 4f);
        rect.offsetMax = new Vector2(-24f, -4f);

        var text = go.AddComponent<Text>();
        text.text = initialText;
        text.font = GetDefaultUIFont();
        text.fontSize = 32;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = Color.white;
        text.supportRichText = false;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        return go;
    }

    private static Font GetDefaultUIFont()
    {
        var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null)
            f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }
}
