using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI_PlayerToast 프리팹과 Resources 참조를 만듭니다.
/// 메뉴: Tools → UI → Create Player Toast Prefab
/// </summary>
public static class SetupPlayerToastPrefab
{
    private const string MenuPath = "Tools/UI/Create Player Toast Prefab";
    private const string AutoRunFlagPath = "Assets/Editor/SetupPlayerToastPrefab.run";
    private const string PrefabPath = PlayerToastUI.PrefabEditorPath;
    private const string RefsPath = "Assets/Resources/UI/PlayerToastRefs.asset";
    private const string FontPath = "Assets/Arts/Fonts/BlackHanSans-Regular SDF.asset";

    [InitializeOnLoadMethod]
    private static void AutoRunIfFlagExists()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(AutoRunFlagPath))
                return;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += () =>
                {
                    if (File.Exists(AutoRunFlagPath))
                        Run();
                };
                return;
            }

            Run();
        };
    }

    private static void Run()
    {
        try { File.Delete(AutoRunFlagPath); }
        catch { /* ignore */ }

        Create();
    }

    [MenuItem(MenuPath, false, 21)]
    public static void Create()
    {
        EnsureFolder("Assets/Arts/UI/Popup");
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/UI");

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        var root = BuildPrefabRoot(font);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var refs = AssetDatabase.LoadAssetAtPath<PlayerToastRefs>(RefsPath);
        if (refs == null)
        {
            refs = ScriptableObject.CreateInstance<PlayerToastRefs>();
            AssetDatabase.CreateAsset(refs, RefsPath);
        }

        refs.toastPrefab = prefab;
        EditorUtility.SetDirty(refs);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (prefab != null)
        {
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }

        Debug.Log("[SetupPlayerToastPrefab] 생성 완료: " + PrefabPath + " / " + RefsPath);
    }

    private static GameObject BuildPrefabRoot(TMP_FontAsset font)
    {
        var root = new GameObject("UI_PlayerToast", typeof(RectTransform));
        root.layer = 5;

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        canvas.overrideSorting = true;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.layer = 5;
        panel.transform.SetParent(root.transform, false);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(920f, 120f);

        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.07f, 0.85f);
        bg.raycastTarget = false;

        var canvasGroup = panel.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.layer = 5;
        textGo.transform.SetParent(panel.transform, false);
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(28f, 16f);
        textRect.offsetMax = new Vector2(-28f, -16f);

        var label = textGo.AddComponent<TextMeshProUGUI>();
        if (font != null)
            label.font = font;
        label.text = "근력이 부족합니다. (무게 0 / 근력 0)";
        label.fontSize = 36f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;

        var toast = root.AddComponent<PlayerToastUI>();
        var so = new SerializedObject(toast);
        so.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("insufficientStrengthMessage").FindPropertyRelative("korean").stringValue =
            "근력이 부족합니다. (무게 {0} / 근력 {1})";
        so.FindProperty("insufficientStrengthMessage").FindPropertyRelative("english").stringValue =
            "Not enough Strength. (Weight {0} / STR {1})";
        so.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
