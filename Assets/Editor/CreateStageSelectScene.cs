using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using System.IO;

/// <summary>
/// 04_StageSelect 샘플 씬을 Unity 직렬화 방식으로 생성합니다.
/// </summary>
public static class CreateStageSelectScene
{
    private const string ScenePath = "Assets/Scenes/04_StageSelect.unity";
    private const string AssetFolder = "Assets/Arts/UI/StageSelect";
    private static readonly string[] PreviewNames =
    {
        "Stage01_NoKingsProtest.png", "Stage02_PizzaShop.png", "Stage03_TexasClimate.png",
        "Stage04_NobelCeremony.png", "Stage05_SiliconValley.png", "Stage06_HalftimeStadium.png",
        "Stage07_FilmSetting.png", "Stage08_USCapitol.png", "Stage09_Greenland.png",
        "Stage10_FBIHeadquarters.png", "Stage11_StraitOfHormuz.png", "Stage12_GazaStrip.png"
    };

    [MenuItem("Tools/Stage Select/Create 04 Stage Select Scene")]
    public static void Create()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        PrepareSpriteImports();

        Scene previousActiveScene = SceneManager.GetActiveScene();
        NewSceneMode mode = Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive;
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
        SceneManager.SetActiveScene(scene);

        GameObject cameraObject = new("StageSelectCamera", typeof(Camera));
        Camera stageSelectCamera = cameraObject.GetComponent<Camera>();
        stageSelectCamera.clearFlags = CameraClearFlags.SolidColor;
        stageSelectCamera.backgroundColor = new Color(0.965f, 0.97f, 0.975f, 1f);
        stageSelectCamera.cullingMask = ~0;
        stageSelectCamera.orthographic = true;
        stageSelectCamera.orthographicSize = 5f;
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        cameraObject.tag = "MainCamera";

        GameObject canvasObject = new("StageSelectCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = stageSelectCamera;
        canvas.planeDistance = 1f;
        canvas.pixelPerfect = false;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        StageSelectPrototype prototype = canvasObject.AddComponent<StageSelectPrototype>();
        Sprite[] previews = new Sprite[PreviewNames.Length];
        for (int i = 0; i < PreviewNames.Length; i++)
            previews[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"{AssetFolder}/{PreviewNames[i]}");
        prototype.ConfigureAssets(
            AssetDatabase.LoadAssetAtPath<Sprite>($"{AssetFolder}/WorldMap_HighRes.png"),
            previews,
            AssetDatabase.LoadAssetAtPath<Sprite>($"{AssetFolder}/Pin_Normal.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>($"{AssetFolder}/Pin_Selected.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>($"{AssetFolder}/Pin_CompletedFlag.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>($"{AssetFolder}/Frame_MissionCardPortrait.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>($"{AssetFolder}/Button_StartMission.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>($"{AssetFolder}/Concepts/Badge_ClearedStamp_RedV2.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>($"{AssetFolder}/Concepts/WorldButton_GrayRound_Concept.png"),
            null,
            AssetDatabase.LoadAssetAtPath<Sprite>($"{AssetFolder}/Concepts/Title_ProtestPlacard_TrimmedV2.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>($"{AssetFolder}/Concepts/Frame_StageList_MinimalV5.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>($"{AssetFolder}/Header_StageList_ReferenceV2.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>($"{AssetFolder}/Button_StageRow_Normal_WideNumberV3.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>($"{AssetFolder}/Overlay_StageRow_SelectedWideV4.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>($"{AssetFolder}/Icon_ScrollChevronV4.png"),
            AssetDatabase.LoadAssetAtPath<Font>("Assets/DownloadedAssets/TextMesh Pro/Examples & Extras/Fonts/Anton.ttf"));
        StageSelectVisualPolish.ApplyStyle(prototype);

        bool hasLocalEventSystem = false;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<EventSystem>(true) != null)
            {
                hasLocalEventSystem = true;
                break;
            }
        }

        if (!hasLocalEventSystem)
        {
            GameObject eventSystem = new("EventSystem", typeof(EventSystem));
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        if (!Application.isBatchMode)
        {
            EditorSceneManager.CloseScene(scene, true);
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(previousActiveScene);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[CreateStageSelectScene] Created: {ScenePath}");
    }

    [MenuItem("Tools/Stage Select/Create And Capture QHD Preview")]
    public static void CreateAndCaptureQhd()
    {
        Create();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Camera camera = Object.FindFirstObjectByType<Camera>();
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (camera == null || canvas == null)
            throw new MissingReferenceException("Stage Select 카메라 또는 캔버스를 찾지 못했습니다.");
        if (Object.FindObjectsByType<StandaloneInputModule>(FindObjectsSortMode.None).Length != 0)
            throw new System.InvalidOperationException("구형 StandaloneInputModule이 씬에 남아 있습니다.");
        if (Object.FindObjectsByType<InputSystemUIInputModule>(FindObjectsSortMode.None).Length != 1)
            throw new System.InvalidOperationException("InputSystemUIInputModule 구성이 올바르지 않습니다.");

        StageSelectPrototype prototype = Object.FindFirstObjectByType<StageSelectPrototype>();
        Transform listContent = prototype.transform.Find("StageSelectRoot/StageList/ListViewport/Content");
        if (prototype.StageCount != 22 || listContent == null || listContent.childCount != 22)
            throw new System.InvalidOperationException("Stage Count 또는 스크롤 버튼 생성 수가 22개가 아닙니다.");
        if (!prototype.ValidateLoopingForEditor())
            throw new System.InvalidOperationException("월드맵 또는 핀 루핑 검증에 실패했습니다.");
        prototype.ShowFocusForValidation(5);
        CaptureCurrentView(camera, canvas, "04_StageSelect_Focus_QHD.png", 2560, 1440);
        prototype.ShowOverviewForValidation();
        CaptureCurrentView(camera, canvas, "04_StageSelect_Overview_QHD.png", 2560, 1440);
        for (int stage = 1; stage <= 12; stage++)
        {
            prototype.ShowFocusForValidation(stage);
            CaptureCurrentView(camera, canvas, $"04_StageSelect_PinCheck_{stage:00}.png", 1280, 720);
        }
    }

    private static void CaptureCurrentView(Camera camera, Canvas canvas, string fileName, int width, int height)
    {
        RenderTexture renderTexture = new(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D screenshot = new(width, height, TextureFormat.RGBA32, false);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;

        camera.targetTexture = renderTexture;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = renderTexture;
        screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        screenshot.Apply();

        string previewDirectory = Path.Combine(Path.GetTempPath(), "CodexStageSelectValidation");
        Directory.CreateDirectory(previewDirectory);
        string previewPath = Path.Combine(previewDirectory, fileName);
        File.WriteAllBytes(previewPath, screenshot.EncodeToPNG());

        camera.targetTexture = previousTarget;
        RenderTexture.active = previousActive;
        Object.DestroyImmediate(renderTexture);
        Object.DestroyImmediate(screenshot);
        Debug.Log($"[CreateStageSelectScene] Preview captured: {previewPath}");
    }

    private static void PrepareSpriteImports()
    {
        ConfigureSprite($"{AssetFolder}/WorldMap_Background.png");
        ConfigureSprite($"{AssetFolder}/WorldMap_HighRes.png", Vector4.zero, 4096);
        ConfigureSprite($"{AssetFolder}/Map_WesternUS_Focus.png", Vector4.zero, 2048);
        ConfigureSprite($"{AssetFolder}/Pin_Normal.png");
        ConfigureSprite($"{AssetFolder}/Pin_Selected.png");
        ConfigureSprite($"{AssetFolder}/Pin_CompletedFlag.png", Vector4.zero, 512);
        ConfigureSprite($"{AssetFolder}/Frame_StagePopup.png", new Vector4(120f, 100f, 120f, 100f));
        ConfigureSprite($"{AssetFolder}/Frame_MissionCardPortrait.png", Vector4.zero, 2048);
        ConfigureSprite($"{AssetFolder}/Button_StartMission.png", new Vector4(180f, 90f, 180f, 90f));
        ConfigureSprite($"{AssetFolder}/Badge_Cleared.png", Vector4.zero, 256);
        ConfigureSprite($"{AssetFolder}/Badge_Cleared_Designed.png", Vector4.zero, 256);
        ConfigureSprite($"{AssetFolder}/Button_WorldView.png", Vector4.zero, 2048);
        ConfigureSprite($"{AssetFolder}/Button_WorldView_BackgroundV2.png", Vector4.zero, 2048);
        ConfigureSprite($"{AssetFolder}/Icon_WorldView_GlobeV2.png", Vector4.zero, 2048);
        ConfigureSprite($"{AssetFolder}/Button_WorldView_Background_TrimmedV2.png", Vector4.zero, 2048);
        ConfigureSprite($"{AssetFolder}/Icon_WorldView_Globe_TrimmedV2.png", Vector4.zero, 2048);
        ConfigureSprite($"{AssetFolder}/Icon_WorldView_Globe_WhiteV3.png", Vector4.zero, 2048);
        ConfigureSprite($"{AssetFolder}/Button_WorldView_StrongV3.png", new Vector4(120f, 80f, 120f, 80f), 4096);
        ConfigureSprite($"{AssetFolder}/Frame_StageList_ReferenceV2.png", Vector4.zero, 2048);
        ConfigureSprite($"{AssetFolder}/Frame_StageList_TrimmedV3.png", new Vector4(80f, 100f, 80f, 100f), 2048);
        ConfigureSprite($"{AssetFolder}/Frame_StageList_ScrollV4.png", new Vector4(64f, 80f, 64f, 80f), 2048);
        ConfigureSprite($"{AssetFolder}/Header_StageList_ReferenceV2.png", Vector4.zero, 4096);
        ConfigureSprite($"{AssetFolder}/Button_StageRow_Normal_ReferenceV2.png", Vector4.zero, 4096);
        ConfigureSprite($"{AssetFolder}/Button_StageRow_Normal.png", Vector4.zero, 4096);
        ConfigureSprite($"{AssetFolder}/Button_StageRow_Normal_TrimmedV2.png", Vector4.zero, 4096);
        ConfigureSprite($"{AssetFolder}/Button_StageRow_Selected_TrimmedV2.png", Vector4.zero, 4096);
        ConfigureSprite($"{AssetFolder}/Overlay_StageRow_SelectedV3.png", Vector4.zero, 4096);
        ConfigureSprite($"{AssetFolder}/Button_StageRow_Normal_WideNumberV3.png", new Vector4(160f, 70f, 160f, 70f), 4096);
        ConfigureSprite($"{AssetFolder}/Overlay_StageRow_SelectedWideV4.png", new Vector4(160f, 70f, 160f, 70f), 4096);
        ConfigureSprite($"{AssetFolder}/Icon_ScrollDownV3.png", Vector4.zero, 2048);
        ConfigureSprite($"{AssetFolder}/Icon_ScrollChevronV4.png", Vector4.zero, 512);
        ConfigureSprite($"{AssetFolder}/Concepts/WorldButton_GrayRound_Concept.png", Vector4.zero, 2048);
        ConfigureSprite($"{AssetFolder}/Concepts/Title_ModernBroadcast_Concept.png", Vector4.zero, 4096);
        ConfigureSprite($"{AssetFolder}/Concepts/Title_ModernBroadcast_TrimmedV2.png", Vector4.zero, 4096);
        ConfigureSprite($"{AssetFolder}/Concepts/Title_ProtestPlacard_TrimmedV2.png", Vector4.zero, 4096);
        ConfigureSprite($"{AssetFolder}/Concepts/Badge_ClearedStamp_RedV2.png", Vector4.zero, 1024);
        ConfigureSprite($"{AssetFolder}/Concepts/Frame_StageList_MinimalV5.png", new Vector4(58f, 135f, 58f, 135f), 4096);
        ConfigureSprite($"{AssetFolder}/Button_StageRow_Selected_ReferenceV2.png", Vector4.zero, 4096);
        foreach (string previewName in PreviewNames)
            ConfigureSprite($"{AssetFolder}/{previewName}");
        AssetDatabase.Refresh();
    }

    private static void ConfigureSprite(string path, Vector4 border = default, int maxSize = 1024)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = maxSize;
        importer.alphaIsTransparency = true;
        importer.spriteBorder = border;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }
}
