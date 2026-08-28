using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Play 전 Scene View에서도 캔버스 깊이가 런타임과 같게 보이도록 레이어링을 적용합니다.
/// </summary>
[DisallowMultipleComponent]
[ExecuteAlways]
public class CharacterSelectionCanvasLayeringDriver : MonoBehaviour
{
#if UNITY_EDITOR
    private bool _applyQueued;

    private void OnEnable()
    {
        QueueApply();
    }

    private void OnValidate()
    {
        QueueApply();
    }

    private void QueueApply()
    {
        if (_applyQueued)
            return;

        _applyQueued = true;
        EditorApplication.delayCall += OnDelayedApply;
    }

    private void OnDelayedApply()
    {
        _applyQueued = false;
        if (this == null)
            return;

        ApplyNow();
    }

    /// <summary>Inspector·메뉴에서 즉시 적용.</summary>
    public void ApplyNow()
    {
        var cam = Camera.main;
        if (cam == null)
            return;

        CharacterSelectionCanvasLayering.Apply(cam);

        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(gameObject.scene);

        SceneView.RepaintAll();
    }
#endif
}
