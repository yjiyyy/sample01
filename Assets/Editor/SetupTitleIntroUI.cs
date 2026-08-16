using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 00_Title 씬에 배경/로고/페이드 UI와 TitleIntroController를 연결합니다.
/// 메뉴: Tools → Setup Title Intro UI
/// </summary>
public static class SetupTitleIntroUI
{
    private const string MenuPath = "Tools/Setup Title Intro UI";
    private const string TitleScenePath = "Assets/Scenes/00_Title.unity";
    private const string AutoRunFlagPath = "Assets/Editor/SetupTitleIntroUI.run";
    private const string BackgroundSpritePath = "Assets/Arts/Title/Title_Image.png";
    private const string LogoSpritePath = "Assets/Arts/Title/Title_01.png";

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
        Debug.Log("[SetupTitleIntroUI] 00_Title 인트로 UI 구성 완료.");
    }

    public static void SetupInOpenScene()
    {
        var canvas = GameObject.Find("Canvas");
        if (canvas == null)
        {
            Debug.LogError("[SetupTitleIntroUI] Canvas를 찾지 못했습니다.");
            return;
        }

        var canvasRect = canvas.GetComponent<RectTransform>();
        var bg = EnsureBackground(canvasRect);
        var logo = EnsureLogo(canvasRect);
        var fade = EnsureFade(canvasRect);
        var menu = GameObject.Find("Menu");

        bg.transform.SetSiblingIndex(0);
        if (logo != null)
            logo.transform.SetSiblingIndex(1);
        if (menu != null)
            menu.transform.SetSiblingIndex(canvasRect.childCount - 2);
        fade.transform.SetAsLastSibling();

        var intro = canvas.GetComponent<TitleIntroController>();
        if (intro == null)
            intro = Undo.AddComponent<TitleIntroController>(canvas);

        var so = new SerializedObject(intro);
        so.FindProperty("logoRect").objectReferenceValue = logo != null ? logo.GetComponent<RectTransform>() : null;
        so.FindProperty("menuRoot").objectReferenceValue = menu;
        so.FindProperty("fadeCanvasGroup").objectReferenceValue = fade.GetComponent<CanvasGroup>();
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(intro);

        var camera = Camera.main;
        if (camera != null)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            EditorUtility.SetDirty(camera);
        }
    }

    private static GameObject EnsureBackground(RectTransform canvas)
    {
        var existing = canvas.Find("Title_Image");
        bool created = existing == null;
        GameObject go = created ? CreateUiObject("Title_Image", canvas) : existing.gameObject;
        if (created)
            StretchFull(go.GetComponent<RectTransform>());

        var image = go.GetComponent<Image>();
        if (image == null)
            image = go.AddComponent<Image>();

        if (image.sprite == null)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundSpritePath);
            if (sprite == null)
                Debug.LogWarning($"[SetupTitleIntroUI] 배경 스프라이트를 찾지 못했습니다: {BackgroundSpritePath}");
            else
                image.sprite = sprite;
        }

        image.color = Color.white;
        image.preserveAspect = false;
        image.raycastTarget = false;
        EditorUtility.SetDirty(image);
        return go;
    }

    private static GameObject EnsureLogo(RectTransform canvas)
    {
        var existing = canvas.Find("Title_01");
        bool created = existing == null;
        GameObject go = created ? CreateUiObject("Title_01", canvas) : existing.gameObject;
        var rect = go.GetComponent<RectTransform>();

        var image = go.GetComponent<Image>();
        if (image == null)
            image = go.AddComponent<Image>();

        var sprite = image.sprite;
        if (sprite == null)
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LogoSpritePath);

        if (sprite == null)
            Debug.LogWarning($"[SetupTitleIntroUI] 로고 스프라이트를 찾지 못했습니다: {LogoSpritePath}");
        else
            image.sprite = sprite;

        if (created)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 220f);
            rect.localScale = Vector3.one;

            if (sprite != null)
            {
                Vector2 size = sprite.rect.size;
                const float maxWidth = 900f;
                if (size.x > maxWidth)
                    size *= maxWidth / size.x;
                rect.sizeDelta = size;
            }
            else
            {
                rect.sizeDelta = new Vector2(800f, 400f);
            }
        }

        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        EditorUtility.SetDirty(image);
        return go;
    }

    private static GameObject EnsureFade(RectTransform canvas)
    {
        var existing = canvas.Find("Fade");
        bool created = existing == null;
        GameObject go = created ? CreateUiObject("Fade", canvas) : existing.gameObject;
        if (created)
            StretchFull(go.GetComponent<RectTransform>());

        var image = go.GetComponent<Image>();
        if (image == null)
            image = go.AddComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        image.type = Image.Type.Sliced;
        image.color = Color.black;
        image.raycastTarget = true;

        var group = go.GetComponent<CanvasGroup>();
        if (group == null)
            group = go.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        EditorUtility.SetDirty(image);
        EditorUtility.SetDirty(group);
        return go;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        return go;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
    }
}
