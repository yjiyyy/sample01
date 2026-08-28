using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 선택 화면 UI를
/// Background·일러스트(뒤) → 3D 캐릭터(중간) → Foreground UI(앞) 순으로 보이게 합니다.
/// Screen Space - Camera 의 planeDistance 로 깊이를 나눕니다.
/// </summary>
public static class CharacterSelectionCanvasLayering
{
    public const string BackgroundCanvasName = "Canvas_Background";
    public const string ForegroundCanvasName = "Canvas_Foreground";
    public const string CharacterIllustrationName = "CharacterIllustration";

    /// <summary>
    /// 배경 UI 최소 거리. 스폰 포인트가 더 멀면 그 뒤로 자동 보정합니다.
    /// (캐릭터 ~12m 뒤에 두는 현재 연출 기준)
    /// </summary>
    public const float BackgroundPlaneDistance = 16f;

    /// <summary>배경을 캐릭터보다 얼마나 더 뒤에 둘지.</summary>
    public const float BackgroundBehindCharacterMargin = 2f;

    /// <summary>패널·버튼 UI. 스폰 캐릭터보다 카메라에 가깝게 둡니다.</summary>
    public const float ForegroundPlaneDistance = 2f;

    public const int BackgroundSortOrder = 0;
    public const int ForegroundSortOrder = 10;

    public static void Apply(Camera camera)
    {
        if (camera == null)
            return;

        Transform chrome = FindChromeRoot();
        if (chrome == null)
            return;

        var foregroundCanvas = chrome.GetComponentInParent<Canvas>();
        if (foregroundCanvas == null)
            return;

        ConfigureForegroundCanvas(foregroundCanvas, camera);

        float backgroundDistance = ResolveBackgroundPlaneDistance(camera);
        Canvas backgroundCanvas = EnsureBackgroundCanvas(camera, foregroundCanvas, backgroundDistance);

        Transform backgroundTf = FindNamedUnder(chrome, "Background")
                                 ?? backgroundCanvas.transform.Find("Background");
        if (backgroundTf != null && backgroundTf.parent != backgroundCanvas.transform)
            backgroundTf.SetParent(backgroundCanvas.transform, false);

        Transform bgUnderCanvas = backgroundCanvas.transform.Find("Background");
        if (bgUnderCanvas != null)
        {
            StretchFullScreen(bgUnderCanvas as RectTransform);
            bgUnderCanvas.SetAsFirstSibling();
        }

        // 2D 일러스트도 배경 캔버스에 두어 3D 캐릭터보다 뒤에 보이게 합니다.
        Transform illustration = FindCharacterIllustration(chrome);
        if (illustration != null)
        {
            if (illustration.parent != backgroundCanvas.transform)
                illustration.SetParent(backgroundCanvas.transform, false);

            ResetLocalZ(illustration as RectTransform);
            illustration.SetAsLastSibling();
        }

        RemoveRenderTextureArtifacts(illustration);
        CleanupLegacyStage();
    }

    /// <summary>Background 캔버스 또는 Chrome 아래에서 CharacterIllustration을 찾습니다.</summary>
    public static Transform FindCharacterIllustration(Transform chrome = null)
    {
        var bgGo = GameObject.Find(BackgroundCanvasName);
        if (bgGo != null)
        {
            var underBg = bgGo.transform.Find(CharacterIllustrationName);
            if (underBg != null)
                return underBg;
        }

        if (chrome == null)
            chrome = FindChromeRoot();

        return chrome != null ? chrome.Find(CharacterIllustrationName) : null;
    }

    /// <summary>스폰 포인트보다 뒤에 오도록 배경 planeDistance를 정합니다.</summary>
    public static float ResolveBackgroundPlaneDistance(Camera camera)
    {
        float distance = BackgroundPlaneDistance;
        if (camera == null)
            return distance;

        var spawnGo = GameObject.Find("CharacterSpawnPoint");
        if (spawnGo == null)
            return distance;

        float characterDistance = Vector3.Distance(camera.transform.position, spawnGo.transform.position);
        return Mathf.Max(distance, characterDistance + BackgroundBehindCharacterMargin);
    }

    private static Transform FindChromeRoot()
    {
        var ui = Object.FindFirstObjectByType<CharacterSelectionUI>();
        if (ui != null)
            return ui.transform;

        var chromeGo = GameObject.Find("CharacterSelectionChrome");
        return chromeGo != null ? chromeGo.transform : null;
    }

    private static Transform FindNamedUnder(Transform parent, string name)
    {
        return parent != null ? parent.Find(name) : null;
    }

    private static void ConfigureForegroundCanvas(Canvas canvas, Camera camera)
    {
        if (canvas.gameObject.name == "Canvas")
            canvas.gameObject.name = ForegroundCanvasName;

        ApplyCanvasSettings(canvas, camera, ForegroundPlaneDistance, ForegroundSortOrder);

        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();
    }

    private static Canvas EnsureBackgroundCanvas(Camera camera, Canvas referenceCanvas, float planeDistance)
    {
        var existingGo = GameObject.Find(BackgroundCanvasName);
        Canvas canvas;
        if (existingGo != null)
        {
            canvas = existingGo.GetComponent<Canvas>();
            if (canvas == null)
                canvas = existingGo.AddComponent<Canvas>();
        }
        else
        {
            var go = new GameObject(BackgroundCanvasName, typeof(RectTransform));
            canvas = go.AddComponent<Canvas>();
            go.AddComponent<GraphicRaycaster>();
        }

        ApplyCanvasSettings(canvas, camera, planeDistance, BackgroundSortOrder);

        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        CopyCanvasScaler(referenceCanvas, scaler);

        var raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
            raycaster.enabled = false;

        StretchFullScreen(canvas.GetComponent<RectTransform>());
        return canvas;
    }

    private static void ApplyCanvasSettings(Canvas canvas, Camera camera, float planeDistance, int sortOrder)
    {
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = planeDistance;
        canvas.sortingOrder = sortOrder;
        canvas.pixelPerfect = false;
    }

    private static void CopyCanvasScaler(Canvas sourceCanvas, CanvasScaler targetScaler)
    {
        if (sourceCanvas == null || targetScaler == null)
            return;

        var sourceScaler = sourceCanvas.GetComponent<CanvasScaler>();
        if (sourceScaler == null)
            return;

        targetScaler.uiScaleMode = sourceScaler.uiScaleMode;
        targetScaler.referenceResolution = sourceScaler.referenceResolution;
        targetScaler.matchWidthOrHeight = sourceScaler.matchWidthOrHeight;
        targetScaler.screenMatchMode = sourceScaler.screenMatchMode;
    }

    private static void StretchFullScreen(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        ResetLocalZ(rect);
    }

    private static void ResetLocalZ(RectTransform rect)
    {
        if (rect == null)
            return;

        // 로컬 z가 크면 레이어 순서가 깨집니다.
        rect.localScale = Vector3.one;
        rect.localPosition = new Vector3(rect.localPosition.x, rect.localPosition.y, 0f);
    }

    private static void RemoveRenderTextureArtifacts(Transform illustration)
    {
        if (illustration == null)
            return;

        var modelDisplay = illustration.Find("ModelDisplay");
        if (modelDisplay == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Object.DestroyImmediate(modelDisplay.gameObject);
            return;
        }
#endif
        Object.Destroy(modelDisplay.gameObject);
    }

    private static void CleanupLegacyStage()
    {
        var stage = GameObject.Find("CharacterModelStage");
        if (stage == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Object.DestroyImmediate(stage);
            return;
        }
#endif
        Object.Destroy(stage);
    }
}
