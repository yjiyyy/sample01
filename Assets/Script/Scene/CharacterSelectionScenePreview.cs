using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Play 전 Scene View에서 캐릭터 프리팹을 CharacterSpawnPoint에 바로 보여 줍니다.
/// Inspector에 프리팹을 넣으면 자동으로 배치되며, 씬에 저장되지 않습니다.
/// </summary>
[DisallowMultipleComponent]
[ExecuteAlways]
public class CharacterSelectionScenePreview : MonoBehaviour
{
    [Header("에디터 씬 미리보기 (Play 전 Scene View)")]
    [Tooltip("여기에 프리팹을 넣으면 스폰 포인트 위치에 바로 표시됩니다.")]
    [SerializeField] private GameObject previewPrefab;

    [Tooltip("비우면 CharacterSelectionController 또는 CharacterSpawnPoint를 자동으로 찾습니다.")]
    [SerializeField] private Transform spawnPoint;

    private GameObject _previewInstance;

#if UNITY_EDITOR
    private bool _refreshQueued;
#endif

    private void OnEnable()
    {
#if UNITY_EDITOR
        QueueRefresh();
#endif
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        QueueRefresh();
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        ClearPreview();
#endif
    }

#if UNITY_EDITOR
    private void QueueRefresh()
    {
        if (_refreshQueued)
            return;

        _refreshQueued = true;
        EditorApplication.delayCall += OnDelayedRefresh;
    }

    private void OnDelayedRefresh()
    {
        _refreshQueued = false;
        if (this == null)
            return;

        RefreshPreview();
    }

    private void RefreshPreview()
    {
        ClearPreview();

        if (Application.isPlaying)
            return;

        if (previewPrefab == null)
            return;

        var targetSpawn = ResolveSpawnTransform();
        if (targetSpawn == null)
            return;

        _previewInstance = PrefabUtility.InstantiatePrefab(previewPrefab, targetSpawn) as GameObject;
        if (_previewInstance == null)
            return;

        _previewInstance.transform.localPosition = Vector3.zero;
        _previewInstance.transform.localRotation = Quaternion.identity;
        _previewInstance.transform.localScale = Vector3.one;

        PreparePreviewModel(_previewInstance);
        PlayerBodyPartPreviewMarker.EnsureMarked(_previewInstance);

        // 스폰 거리에 맞춰 배경 planeDistance도 Play 전과 같게 맞춥니다.
        if (Camera.main != null)
            CharacterSelectionCanvasLayering.Apply(Camera.main);

        SceneView.RepaintAll();
    }

    private static void PreparePreviewModel(GameObject model)
    {
        if (model == null)
            return;

        var slots = model.GetComponentsInChildren<PlayerBodyPartSlots>(true);
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                slots[i].TryAttachParts();
        }

        CharacterSelectionController.DisableGameplaySystemsForPreview(model);
        CharacterSelectionController.EnsureCharacterAnimatorOverrideForPreview(model);
        CharacterSelectionController.PrepareRenderersForPreview(model);
        CharacterSelectionController.PlaySelectAnimationForPreview(
            model,
            CharacterSelectionController.SelectAnimStateName);

        SceneView.RepaintAll();
    }

    private Transform ResolveSpawnTransform()
    {
        if (spawnPoint != null)
            return spawnPoint;

        var controller = GetComponent<CharacterSelectionController>();
        if (controller != null)
        {
            var fromController = controller.GetCharacterSpawnPoint();
            if (fromController != null)
                return fromController;
        }

        var spawnGo = GameObject.Find("CharacterSpawnPoint");
        return spawnGo != null ? spawnGo.transform : null;
    }

    /// <summary>Inspector 「미리보기 제거」 버튼용.</summary>
    public void ClearPreview()
    {
        if (_previewInstance == null)
            return;

        if (!Application.isPlaying)
            DestroyImmediate(_previewInstance);
        else
            Destroy(_previewInstance);

        _previewInstance = null;
        SceneView.RepaintAll();
    }

    /// <summary>Inspector 「미리보기 갱신」 버튼용.</summary>
    public void RefreshPreviewNow()
    {
        RefreshPreview();
    }
#endif
}
