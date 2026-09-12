using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Stage00의 플레이 UI를 QHD(16:9) 디자인 기준으로 유지하면서
/// 다른 해상도에서도 같은 비율로 보이게 설정합니다.
/// </summary>
public static class Stage00ResponsiveUISetup
{
    private const string ScenePath = "Assets/Scenes/Stage/Stage00.unity";
    private static readonly Vector2 ReferenceResolution = new(2560f, 1440f);

    [MenuItem("Tools/UI/Apply Stage00 Responsive UI")]
    public static void ApplyFromMenu()
    {
        if (!EditorUtility.DisplayDialog(
                "Stage00 반응형 UI 적용",
                "QHD 화면의 현재 크기를 기준으로 플레이 UI의 해상도 대응 설정을 적용할까요?",
                "적용",
                "취소"))
            return;

        Apply();
    }

    /// <summary>배치 모드 검증이나 자동 적용에서 사용하는 진입점입니다.</summary>
    public static void ApplyFromCommandLine()
    {
        Apply();
    }

    private static void Apply()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        ConfigureScaler(scene, "Canvas(Overlay)");
        ConfigureScaler(scene, "Canvas_Upgrade(Cam)");
        EnsureSafeArea(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new System.InvalidOperationException($"Stage00 씬을 저장하지 못했습니다: {ScenePath}");

        Debug.Log(
            "[Stage00ResponsiveUISetup] QHD(2560x1440) 기준 반응형 UI와 MobileUIRoot Safe Area 적용 완료.");
    }

    private static void ConfigureScaler(Scene scene, string canvasName)
    {
        GameObject canvasObject = FindInScene(scene, canvasName);
        if (canvasObject == null)
            throw new MissingReferenceException($"Stage00에서 '{canvasName}'을 찾지 못했습니다.");

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvasObject.AddComponent<CanvasScaler>();

        Undo.RecordObject(scaler, "Apply Stage00 Responsive UI");
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;
        EditorUtility.SetDirty(scaler);
    }

    private static void EnsureSafeArea(Scene scene)
    {
        GameObject mobileUiRoot = FindInScene(scene, "MobileUIRoot");
        if (mobileUiRoot == null)
            throw new MissingReferenceException("Stage00에서 'MobileUIRoot'를 찾지 못했습니다.");

        RectTransform rect = mobileUiRoot.GetComponent<RectTransform>();
        if (rect == null)
            throw new MissingComponentException("MobileUIRoot에 RectTransform이 없습니다.");

        Undo.RecordObject(rect, "Apply Stage00 Safe Area");
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        EditorUtility.SetDirty(rect);

        if (mobileUiRoot.GetComponent<SafeAreaFitter>() == null)
            Undo.AddComponent<SafeAreaFitter>(mobileUiRoot);
    }

    private static GameObject FindInScene(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindRecursive(root.transform, objectName);
            if (found != null)
                return found.gameObject;
        }

        return null;
    }

    private static Transform FindRecursive(Transform current, string objectName)
    {
        if (current.name == objectName)
            return current;

        for (int i = 0; i < current.childCount; i++)
        {
            Transform found = FindRecursive(current.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }
}
