using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>기존 스테이지 데이터를 유지하면서 시안의 UI 스타일을 적용합니다.</summary>
public static class StageSelectVisualPolish
{
    private const string Folder = "Assets/Arts/UI/StageSelect/Paper";
    private static readonly Color Ink = new(.035f, .04f, .045f, 1f);
    private static readonly Color Line = new(.64f, .69f, .73f, 1f);
    private static readonly Color Yellow = new(1f, .9f, .015f, 1f);
    private static readonly Color Red = new(.94f, .025f, .06f, 1f);

    [MenuItem("Tools/Stage Select/Apply Reference Style To Open Scene")]
    public static void ApplyToOpenScene()
    {
        StageSelectPrototype prototype = UnityEngine.Object.FindFirstObjectByType<StageSelectPrototype>();
        if (prototype == null)
            throw new InvalidOperationException("04_StageSelect 씬을 먼저 열어 주세요.");
        ApplyStyle(prototype);
    }

    public static void ApplyStyle(StageSelectPrototype prototype)
    {
        BuildAssets();
        Undo.RegisterFullObjectHierarchyUndo(prototype.gameObject, "Apply Stage Select Reference Style");
        SerializedObject settings = new(prototype);
        Assign(settings, "stageListFrameSprite", "StageListFrameAlpha");
        Assign(settings, "popupFrameSprite", "PaperFrameAlpha");
        Assign(settings, "stageListHeaderSprite", "Header");
        Assign(settings, "normalRowSprite", "Row");
        Assign(settings, "selectedRowOverlaySprite", "SelectedRow");
        Assign(settings, "scrollTrackSprite", "Pill");
        Assign(settings, "scrollArrowSprite", "Arrow");
        Assign(settings, "startButtonSprite", "StartButtonNormal");
        Assign(settings, "startButtonHighlightedSprite", "StartButtonHighlighted");
        Assign(settings, "startButtonPressedSprite", "StartButtonPressed");
        Assign(settings, "stageTabSprite", "StageTab");
        Assign(settings, "clearedBadgeSprite", "ClearedStamp");
        settings.ApplyModifiedProperties();
        prototype.Rebuild();
        EditorUtility.SetDirty(prototype);
        EditorSceneManager.MarkSceneDirty(prototype.gameObject.scene);
        SceneView.RepaintAll();
    }

    private static void Assign(SerializedObject settings, string field, string asset)
    {
        settings.FindProperty(field).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/{asset}.png");
    }

    private static void BuildAssets()
    {
        Directory.CreateDirectory(Folder);
        ImportPaperAssets();
        CreateStartButtonVariants();
        Save("Pill", 32, 32, (x, y) => Rounded(x, y, 32, 32, 16, 0, Color.white, Color.white), new Vector4(15,15,15,15));
        Save("Arrow", 32, 32, (x, y) => y > 3 && y < 28 && Mathf.Abs(x-16) < (y-3)*.58f ? Ink : Color.clear);
        Save("Header", 480, 72, (x, y) => x < 446 + y*.45f ? Yellow : Color.white);
        Save("Row", 480, 72, (x, y) => y < 1.5f ? Line : x < 66 + y*.34f ? Ink : Color.white);
        Save("SelectedRow", 480, 72, (x, y) => y < 1.5f ? Line : x > 66 + y*.34f && x < 77 + y*.34f ? Red : Yellow);
        Save("StageTab", 480, 96, (x, y) => Red);
        Save("StampBorder", 160, 56, (x, y) => Rounded(x,y,160,56,2,3,Color.clear,Red), new Vector4(5,5,5,5));
    }

    private static void ImportPaperAssets()
    {
        foreach (string name in new[] { "PaperFrame", "StageListFrame", "YellowLabelSource", "ClearedStampSource" })
        {
            string path = $"{Folder}/{name}.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = name == "PaperFrame" ? new Vector4(190,190,190,190) : Vector4.zero;
            importer.spritePixelsPerUnit = 300;
            importer.maxTextureSize = 4096;
            importer.mipmapEnabled = false;
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        CreateFrameWithTransparentOutside("PaperFrame", "PaperFrameAlpha", new Vector4(190,190,190,190));
        CreateFrameWithTransparentOutside("StageListFrame", "StageListFrameAlpha", Vector4.zero);
        CreateClearedStamp();
    }

    private static void CreateFrameWithTransparentOutside(string sourceName, string outputName, Vector4 border)
    {
        Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/{sourceName}.png");
        int width = source.width;
        int height = source.height;
        Color[] pixels = source.GetPixels();
        Color background = (pixels[0] + pixels[width - 1] + pixels[(height - 1) * width] + pixels[pixels.Length - 1]) * .25f;
        bool[] outside = new bool[pixels.Length];
        Queue<int> pending = new();
        int horizontalBand = Mathf.CeilToInt(width * .12f);
        int verticalBand = Mathf.CeilToInt(height * .08f);

        void TryAdd(int x, int y)
        {
            int index = y * width + x;
            bool withinOuterBand = x < horizontalBand || x >= width - horizontalBand ||
                                   y < verticalBand || y >= height - verticalBand;
            if (!withinOuterBand) return;
            if (outside[index] || !IsFrameBackground(pixels[index], background)) return;
            outside[index] = true;
            pending.Enqueue(index);
        }

        for (int x = 0; x < width; x++) { TryAdd(x, 0); TryAdd(x, height - 1); }
        for (int y = 0; y < height; y++) { TryAdd(0, y); TryAdd(width - 1, y); }
        while (pending.Count > 0)
        {
            int index = pending.Dequeue();
            int x = index % width;
            int y = index / width;
            if (x > 0) TryAdd(x - 1, y);
            if (x + 1 < width) TryAdd(x + 1, y);
            if (y > 0) TryAdd(x, y - 1);
            if (y + 1 < height) TryAdd(x, y + 1);
        }

        for (int i = 0; i < pixels.Length; i++)
            if (outside[i]) pixels[i] = Color.clear;
        if (pixels[(height / 2) * width + width / 2].a < .95f || pixels[0].a > .05f)
            throw new InvalidOperationException($"{sourceName} 알파 처리 결과가 올바르지 않습니다.");
        SaveTextureSprite(outputName, width, height, pixels, border);
    }

    private static bool IsFrameBackground(Color color, Color background)
    {
        float difference = Mathf.Max(Mathf.Abs(color.r - background.r),
            Mathf.Max(Mathf.Abs(color.g - background.g), Mathf.Abs(color.b - background.b)));
        float chroma = Mathf.Max(color.r, Mathf.Max(color.g, color.b)) - Mathf.Min(color.r, Mathf.Min(color.g, color.b));
        return difference < .085f && chroma < .055f && color.r > .84f;
    }

    private static void CreateClearedStamp()
    {
        Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/ClearedStampSource.png");
        Color[] pixels = source.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color color = pixels[i];
            // 원본 도장 PNG의 알파가 이미 깔끔하게 분리되어 있습니다.
            // 투명 픽셀에 남은 RGB 값을 마스크로 다시 사용하면 붉은 줄무늬가 생기므로
            // 원본 알파만 보존하고 색상만 또렷한 인쇄용 빨강으로 정리합니다.
            float alpha = color.a * .92f;
            pixels[i] = alpha <= .002f
                ? Color.clear
                : new Color(Mathf.Max(color.r, .82f), color.g * .72f, color.b * .72f, alpha);
        }
        SaveTextureSprite("ClearedStamp", source.width, source.height, pixels, Vector4.zero);
    }

    private static void SaveTextureSprite(string name, int width, int height, Color[] pixels, Vector4 border)
    {
        Texture2D texture = new(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels(pixels);
        texture.Apply();
        string path = $"{Folder}/{name}.png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = border;
        importer.spritePixelsPerUnit = 100;
        importer.maxTextureSize = 4096;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
    }

    private static void CreateStartButtonVariants()
    {
        Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/YellowLabelSource.png");
        RectInt crop = new(100, 205, 1970, 310);
        SaveStartButtonVariant("StartButtonNormal", source, crop, 1f, 0f);
        SaveStartButtonVariant("StartButtonHighlighted", source, crop, 1.045f, .045f);
        SaveStartButtonVariant("StartButtonPressed", source, crop, .82f, 0f);
    }

    private static void SaveStartButtonVariant(string name, Texture2D source, RectInt crop, float brightness, float warmHighlight)
    {
        Color[] pixels = source.GetPixels(crop.x, crop.y, crop.width, crop.height);
        for (int i = 0; i < pixels.Length; i++)
        {
            Color color = pixels[i];
            color = new Color(
                Mathf.Clamp01(color.r * brightness),
                Mathf.Clamp01(color.g * brightness),
                Mathf.Clamp01(color.b * brightness),
                color.a);
            color = Color.Lerp(color, new Color(1f, .94f, .48f, color.a), warmHighlight);
            pixels[i] = color;
        }

        Texture2D texture = new(crop.width, crop.height, TextureFormat.RGBA32, false);
        texture.SetPixels(pixels);
        texture.Apply();
        string path = $"{Folder}/{name}.png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.maxTextureSize = 4096;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
    }

    private static Color Rounded(float x, float y, float w, float h, float radius, float border, Color fill, Color stroke)
    {
        Vector2 q = new(Mathf.Abs(x-w*.5f)-(w*.5f-radius), Mathf.Abs(y-h*.5f)-(h*.5f-radius));
        float distance = new Vector2(Mathf.Max(q.x,0), Mathf.Max(q.y,0)).magnitude + Mathf.Min(Mathf.Max(q.x,q.y),0)-radius;
        return distance > 0 ? Color.clear : distance > -border ? stroke : fill;
    }

    private static void Save(string name, int width, int height, Func<float,float,Color> pixel, Vector4 border = default)
    {
        // 서브픽셀 샘플링으로 작은 크기의 대각선과 둥근 모서리도 부드럽게 만듭니다.
        Texture2D texture = new(width,height,TextureFormat.RGBA32,false);
        Color[] colors = new Color[width*height];
        for (int y=0; y<height; y++)
        for (int x=0; x<width; x++)
        {
            Color sum = Color.clear;
            for (int sy=0; sy<4; sy++)
            for (int sx=0; sx<4; sx++)
            {
                Color c = pixel(x+(sx+.5f)/4f,y+(sy+.5f)/4f);
                if (name == "Header" || name == "Row" || name == "SelectedRow" || name == "StageTab")
                {
                    float grain = Mathf.PerlinNoise(x*.43f,y*.43f);
                    float fleck = Mathf.PerlinNoise(x*1.71f+13f,y*1.71f);
                    Color paper = new(.97f,.957f,.916f,c.a);
                    c = Color.Lerp(c,paper, c.r < .1f ? (fleck > .68f ? .2f : .015f) : .10f);
                    c *= 1f - grain*.055f;
                    c.a = 1f;
                    if ((name == "StageTab" || name == "Header") && (x<13 || y<8 || y>height-8) && fleck>.57f)
                        c = paper;
                }
                sum += new Color(c.r*c.a,c.g*c.a,c.b*c.a,c.a)/16f;
            }
            colors[y*width+x] = sum.a > 0 ? new Color(sum.r/sum.a,sum.g/sum.a,sum.b/sum.a,sum.a) : Color.clear;
        }
        texture.SetPixels(colors);
        texture.Apply();
        string path = $"{Folder}/{name}.png";
        File.WriteAllBytes(path,texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = border;
        importer.spritePixelsPerUnit = 100;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
    }

    // 검증 중에는 씬을 저장하지 않고 기존 데이터로 실제 렌더링을 확인합니다.
    public static void ValidateAndCapture()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/04_StageSelect.unity", OpenSceneMode.Single);
        StageSelectPrototype before = UnityEngine.Object.FindFirstObjectByType<StageSelectPrototype>();
        RectTransform originalTitle = (RectTransform)before.transform.Find("StageSelectRoot/TitleGraphic");
        Vector2 originalPosition = originalTitle.anchoredPosition;
        Vector2 originalSize = originalTitle.sizeDelta;
        Vector2 originalAnchorMin = originalTitle.anchorMin;
        Vector2 originalAnchorMax = originalTitle.anchorMax;
        Quaternion originalRotation = originalTitle.localRotation;
        ApplyToOpenScene();
        StageSelectPrototype prototype = UnityEngine.Object.FindFirstObjectByType<StageSelectPrototype>();
        RectTransform title = (RectTransform)prototype.transform.Find("StageSelectRoot/TitleGraphic");
        if (Vector2.Distance(title.anchoredPosition,originalPosition) > .01f ||
            Vector2.Distance(title.sizeDelta,originalSize) > .01f ||
            title.anchorMin != originalAnchorMin || title.anchorMax != originalAnchorMax ||
            Quaternion.Angle(title.localRotation,originalRotation) > .01f)
            throw new Exception("The user's title layout changed during style application.");
        if (!prototype.ValidateLoopingForEditor()) throw new Exception("Map looping validation failed.");
        prototype.ShowSelectedOverviewForEditor();
        Capture("Overview",1920,1080);
        Capture("Overview_Mobile",2340,1080);
        Capture("ScrollBottom",1920,1080,0f);
        Capture("ScrollTop",1920,1080,1f);
        prototype.ShowFocusForValidation(5);
        Capture("Focus",1920,1080);
        prototype.ShowFocusForValidation(3);
        Button start = prototype.transform.Find("StageSelectRoot/MapViewport/FocusOverlay/MissionCard/StartMissionButton").GetComponent<Button>();
        if (start.transition != Selectable.Transition.SpriteSwap || start.spriteState.highlightedSprite == null || start.spriteState.pressedSprite == null)
            throw new Exception("Start button vintage interaction sprites were not applied.");
        Capture("Texas",1920,1080);
        Capture("Texas_Mobile",2340,1080);
        prototype.ShowFocusForValidation(1);
        Transform selectedStamp = prototype.transform.Find("StageSelectRoot/StageList/ListViewport/Content/List_01/Cleared");
        if (selectedStamp == null || !selectedStamp.gameObject.activeSelf)
            throw new Exception("Selected completed stage stamp is not visible.");
        Capture("SelectedCompleted",1920,1080);
        prototype.ShowFocusForValidation(9);
        Capture("Greenland",1920,1080);
        Debug.Log("STAGE_SELECT_POLISH_VALIDATION_OK");
    }

    private static void Capture(string name, int width, int height, float? scrollPosition = null)
    {
        Canvas canvas = UnityEngine.Object.FindFirstObjectByType<StageSelectPrototype>().GetComponent<Canvas>();
        Camera camera = canvas.worldCamera;
        RenderTexture target = new(width,height,24);
        RenderTexture previous = RenderTexture.active;
        RenderTexture oldTarget = camera.targetTexture;
        Texture2D image = new(width,height,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            UnityEngine.Object.FindFirstObjectByType<StageSelectPrototype>().RefreshLayoutForEditor();
            if (scrollPosition.HasValue)
            {
                Scrollbar scrollbar = canvas.transform.Find("StageSelectRoot/StageList/Scrollbar").GetComponent<Scrollbar>();
                ScrollRect list = canvas.transform.Find("StageSelectRoot/StageList/ListViewport").GetComponent<ScrollRect>();
                scrollbar.value = scrollPosition.Value;
                Canvas.ForceUpdateCanvases();
                if (Mathf.Abs(list.verticalNormalizedPosition-scrollPosition.Value) > .01f)
                    throw new Exception($"Scrollbar mismatch: requested {scrollPosition.Value}, actual {list.verticalNormalizedPosition}");
            }
            Text header = canvas.transform.Find("StageSelectRoot/StageList/HeaderTitle").GetComponent<Text>();
            if (header.cachedTextGenerator.vertexCount <= 4)
                throw new Exception("Stage list header failed to render.");
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0,0,width,height),0,0);
            image.Apply();
            Directory.CreateDirectory("Logs/StageSelectPolish");
            File.WriteAllBytes($"Logs/StageSelectPolish/{name}.png",image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
