#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 병합 후 EventSystem UI 액션 / Screen Space Camera 카메라가 끊긴 경우 복구.
/// Menu: Tools → Fix Stage UI Touch (EventSystem + Upgrade Canvas Camera)
/// </summary>
public static class StageUiTouchFixer
{
    private const string MenuPath = "Tools/Fix Stage UI Touch (EventSystem + Upgrade Canvas Camera)";

    [MenuItem(MenuPath)]
    private static void Fix()
    {
        int fixedModules = 0;
        int fixedCameras = 0;

        var modules = Object.FindObjectsByType<InputSystemUIInputModule>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (var module in modules)
        {
            if (module == null) continue;

            Undo.RecordObject(module, "Fix InputSystemUIInputModule Actions");
            // Point / LeftClick 등이 비어 있으면 UI 터치가 안 됩니다.
            module.AssignDefaultActions();
            EditorUtility.SetDirty(module);
            fixedModules++;
        }

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cams.Length; i++)
            {
                if (cams[i] != null && cams[i].CompareTag("MainCamera"))
                {
                    mainCam = cams[i];
                    break;
                }
            }
        }

        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var canvas in canvases)
        {
            if (canvas == null) continue;
            if (canvas.renderMode != RenderMode.ScreenSpaceCamera) continue;
            if (canvas.worldCamera != null) continue;

            Undo.RecordObject(canvas, "Assign Canvas Event Camera");
            canvas.worldCamera = mainCam;
            EditorUtility.SetDirty(canvas);
            fixedCameras++;
        }

        if (Object.FindFirstObjectByType<EventSystem>() == null)
            Debug.LogWarning("[StageUiTouchFixer] EventSystem이 씬에 없습니다.");

        if (fixedModules > 0 || fixedCameras > 0)
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid())
                EditorSceneManager.MarkSceneDirty(scene);
        }

        Debug.Log(
            $"[StageUiTouchFixer] 완료 — InputSystemUIInputModule {fixedModules}개 액션 재연결, " +
            $"ScreenSpaceCamera Canvas {fixedCameras}개 카메라 할당" +
            (mainCam != null ? $" (Camera={mainCam.name})" : " (Main Camera 없음!)") +
            " — 씬을 저장하세요.");
    }
}
#endif
