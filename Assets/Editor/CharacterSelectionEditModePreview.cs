using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 캐릭터 선택 씬을 열면 Play 전에 캔버스 레이어링을 맞춰,
/// Scene View에서 런타임과 같은 UI 깊이를 바로 볼 수 있게 합니다.
/// </summary>
[InitializeOnLoad]
public static class CharacterSelectionEditModePreview
{
    private const string CharacterSelectionSceneName = "02_CharacterSelectionLevel";
    private static bool _queued;

    static CharacterSelectionEditModePreview()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (Application.isPlaying)
            return;

        if (!IsCharacterSelectionScene(scene))
            return;

        QueueApply();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        // Play 종료 후 에디터 씬에 다시 맞춰 줍니다.
        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        if (!IsCharacterSelectionSceneOpen())
            return;

        QueueApply();
    }

    private static void QueueApply()
    {
        if (_queued)
            return;

        _queued = true;
        EditorApplication.delayCall += () =>
        {
            _queued = false;
            if (Application.isPlaying)
                return;

            ApplyToOpenCharacterSelectionScene();
        };
    }

    [MenuItem("Tools/Apply Character Selection Canvas Layering")]
    public static void ApplyToOpenCharacterSelectionScene()
    {
        if (!IsCharacterSelectionSceneOpen())
        {
            Debug.LogWarning(
                "[CharacterSelectionEditModePreview] 캐릭터 선택 씬이 열려 있지 않습니다.");
            return;
        }

        EnsureDriverExists();

        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[CharacterSelectionEditModePreview] Main Camera를 찾지 못했습니다.");
            return;
        }

        CharacterSelectionCanvasLayering.Apply(cam);

        var active = SceneManager.GetActiveScene();
        if (active.IsValid())
            EditorSceneManager.MarkSceneDirty(active);

        SceneView.RepaintAll();
        Debug.Log("[CharacterSelectionEditModePreview] Canvas 레이어링을 Scene View에 적용했습니다. 씬을 저장하면 Play 전과 동일하게 유지됩니다.");
    }

    private static void EnsureDriverExists()
    {
        var controller = Object.FindFirstObjectByType<CharacterSelectionController>();
        if (controller == null)
            return;

        if (controller.GetComponent<CharacterSelectionCanvasLayeringDriver>() == null)
            Undo.AddComponent<CharacterSelectionCanvasLayeringDriver>(controller.gameObject);

        if (controller.GetComponent<CharacterSelectionScenePreview>() == null)
            Undo.AddComponent<CharacterSelectionScenePreview>(controller.gameObject);
    }

    private static bool IsCharacterSelectionSceneOpen()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (IsCharacterSelectionScene(SceneManager.GetSceneAt(i)))
                return true;
        }

        return false;
    }

    private static bool IsCharacterSelectionScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return false;

        return scene.name == CharacterSelectionSceneName
               || Object.FindFirstObjectByType<CharacterSelectionController>() != null;
    }
}
