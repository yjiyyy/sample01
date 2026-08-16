using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PC용 외형 파츠 슬롯. 프리팹을 지정한 본에 붙입니다.
/// (무기/장비는 PlayerEquipmentController 담당)
/// </summary>
[System.Serializable]
public class PlayerPartSlot
{
    [Tooltip("파츠가 붙을 본 이름. 예: 'Bip001 Head'")]
    public string boneName = "";

    [Tooltip("생성할 파츠 프리팹.")]
    public GameObject partPrefab;

    [Tooltip("부착 후 로컬 위치 오프셋.")]
    public Vector3 localOffset = Vector3.zero;

    [Tooltip("부착 후 로컬 회전(오일러 각도).")]
    public Vector3 localRotationEuler = Vector3.zero;

    [Tooltip("부착 후 로컬 스케일.")]
    public Vector3 localScale = Vector3.one;
}

/// <summary>
/// PC 루트에 붙입니다. Play 시 partSlots를 해당 본에 인스턴스합니다.
/// 에디터 미리보기는 Prefab 수정 창에서만 「미리보기 갱신」으로 확인.
/// 미리보기는 [TEMP_Preview]_ 접두사·마커·DontSave로 표시되며, 저장 직전에 자동 제거됩니다.
/// </summary>
[DisallowMultipleComponent]
[ExecuteAlways]
public class PlayerBodyPartSlots : MonoBehaviour
{
    [Header("파츠 슬롯")]
    [Tooltip("붙일 파츠 목록 (프리팹 + 본 이름 + 오프셋).")]
    public PlayerPartSlot[] partSlots = System.Array.Empty<PlayerPartSlot>();

    [Header("에디터")]
    [Tooltip("켜 두면 Prefab 수정 창 Inspector 「미리보기 갱신」 버튼으로 파츠를 표시할 수 있습니다.")]
    public bool previewInEditor = true;

    private bool _attached;

#if UNITY_EDITOR
    private bool _previewRefreshQueued;
    private bool _previewClearQueued;
    private readonly List<GameObject> _editorPreviewInstances = new List<GameObject>();
    private static bool _saveHooksRegistered;
#endif

#if UNITY_EDITOR
    private void OnEnable()
    {
        if (Application.isPlaying) return;
        EnsureSaveHooksRegistered();
        UnityEditor.SceneManagement.PrefabStage.prefabStageClosing -= OnPrefabStageClosing;
        UnityEditor.SceneManagement.PrefabStage.prefabStageClosing += OnPrefabStageClosing;

        // 예전에 잘못 저장된 미리보기가 있으면 Prefab 창 진입 시 제거
        if (UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject) != null)
            DestroyPreviewObjectsUnder(transform);
    }
#endif

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.SceneManagement.PrefabStage.prefabStageClosing -= OnPrefabStageClosing;
        if (!Application.isPlaying)
            QueueEditorPreviewClear();
#endif
    }

    private void Start()
    {
        if (!Application.isPlaying) return;
        TryAttachParts();
    }

    /// <summary>Play 시 파츠를 본에 붙입니다. 이미 붙였으면 무시합니다.</summary>
    public void TryAttachParts()
    {
        if (_attached) return;

#if UNITY_EDITOR
        if (Application.isPlaying)
            ClearEditorPreviewNow();
#endif

        AttachPartsInternal(isEditorPreview: false);
        _attached = true;
    }

#if UNITY_EDITOR
    /// <summary>Inspector 「미리보기 갱신」 버튼용.</summary>
    public void RefreshEditorPreview()
    {
        if (Application.isPlaying) return;
        if (!IsEditorPreviewAllowed(out string reason))
        {
            Debug.LogWarning($"[PlayerBodyPartSlots] 미리보기 불가: {reason} ({name})");
            return;
        }

        QueueEditorPreviewRefresh();
    }

    /// <summary>Inspector 「미리보기 제거」 버튼용.</summary>
    public void ClearEditorPreview()
    {
        if (Application.isPlaying) return;
        QueueEditorPreviewClear();
    }

    /// <summary>Prefab 수정 창에서만 수동 미리보기 허용.</summary>
    public bool IsEditorPreviewAllowed(out string reason)
    {
        if (!previewInEditor)
        {
            reason = "previewInEditor가 꺼져 있습니다";
            return false;
        }

        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject);
        if (stage == null)
        {
            reason = "Prefab 수정 창에서만 미리보기할 수 있습니다 (씬 배치·Project 창 선택에서는 불가)";
            return false;
        }

        reason = null;
        return true;
    }

    private static void EnsureSaveHooksRegistered()
    {
        if (_saveHooksRegistered) return;
        _saveHooksRegistered = true;
        UnityEditor.SceneManagement.PrefabStage.prefabSaving -= OnAnyPrefabSaving;
        UnityEditor.SceneManagement.PrefabStage.prefabSaving += OnAnyPrefabSaving;
    }

    /// <summary>프리팹 저장 직전: 미리보기를 모두 제거해 저장에 포함되지 않게 합니다.</summary>
    private static void OnAnyPrefabSaving(GameObject contentsRoot)
    {
        if (contentsRoot == null) return;
        DestroyPreviewObjectsUnder(contentsRoot.transform);
    }

    private void OnPrefabStageClosing(UnityEditor.SceneManagement.PrefabStage stage)
    {
        if (Application.isPlaying || stage == null) return;
        if (UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject) != stage)
            return;
        ClearEditorPreviewNow();
    }

    private void ClearEditorPreviewNow()
    {
        for (int i = _editorPreviewInstances.Count - 1; i >= 0; i--)
        {
            var go = _editorPreviewInstances[i];
            if (go != null)
                DestroyImmediate(go);
        }

        _editorPreviewInstances.Clear();
        DestroyPreviewObjectsUnder(transform);
    }

    /// <summary>마커·이름 접두사로 미리보기 오브젝트를 찾아 제거합니다.</summary>
    private static void DestroyPreviewObjectsUnder(Transform root)
    {
        if (root == null) return;

        var toDestroy = new List<GameObject>();

        var markers = root.GetComponentsInChildren<PlayerBodyPartPreviewMarker>(true);
        for (int i = 0; i < markers.Length; i++)
        {
            if (markers[i] == null) continue;
            var go = markers[i].gameObject;
            if (go != null && !toDestroy.Contains(go))
                toDestroy.Add(go);
        }

        var transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            var t = transforms[i];
            if (t == null) continue;
            if (!PlayerBodyPartPreviewMarker.IsPreviewObject(t)) continue;
            if (!toDestroy.Contains(t.gameObject))
                toDestroy.Add(t.gameObject);
        }

        for (int i = toDestroy.Count - 1; i >= 0; i--)
        {
            if (toDestroy[i] != null)
                Object.DestroyImmediate(toDestroy[i]);
        }
    }

    /// <summary>남은 미리보기 고아 오브젝트 일괄 제거.</summary>
    public static void ClearAllEditorPreviewOrphansInOpenScenes()
    {
        var markers = Object.FindObjectsByType<PlayerBodyPartPreviewMarker>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        int removed = 0;
        for (int i = markers.Length - 1; i >= 0; i--)
        {
            if (markers[i] == null) continue;
            Object.DestroyImmediate(markers[i].gameObject);
            removed++;
        }

        // 마커가 빠진 채 이름만 남은 경우
        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = all.Length - 1; i >= 0; i--)
        {
            var t = all[i];
            if (t == null) continue;
            if (!t.name.StartsWith(PlayerBodyPartPreviewMarker.NamePrefix, System.StringComparison.Ordinal))
                continue;
            Object.DestroyImmediate(t.gameObject);
            removed++;
        }

        if (removed > 0)
            Debug.Log($"[PlayerBodyPartSlots] 남아 있던 미리보기 고아 {removed}개를 제거했습니다.");
        else
            Debug.Log("[PlayerBodyPartSlots] 제거할 미리보기 고아가 없습니다.");
    }

    private void QueueEditorPreviewRefresh()
    {
        _previewClearQueued = false;
        if (_previewRefreshQueued) return;
        _previewRefreshQueued = true;
        UnityEditor.EditorApplication.delayCall += OnDelayedEditorPreviewRefresh;
    }

    private void QueueEditorPreviewClear()
    {
        _previewRefreshQueued = false;
        if (_previewClearQueued) return;
        _previewClearQueued = true;
        UnityEditor.EditorApplication.delayCall += OnDelayedEditorPreviewClear;
    }

    private void OnDelayedEditorPreviewRefresh()
    {
        _previewRefreshQueued = false;
        if (this == null || Application.isPlaying) return;

        ClearEditorPreviewNow();

        if (!IsEditorPreviewAllowed(out _)) return;
        if (partSlots == null || partSlots.Length == 0) return;

        AttachPartsInternal(isEditorPreview: true);
    }

    private void OnDelayedEditorPreviewClear()
    {
        _previewClearQueued = false;
        if (this == null || Application.isPlaying) return;
        ClearEditorPreviewNow();
    }
#endif

    private void AttachPartsInternal(bool isEditorPreview)
    {
        if (partSlots == null || partSlots.Length == 0) return;

        for (int i = 0; i < partSlots.Length; i++)
        {
            var slot = partSlots[i];
            if (slot == null) continue;

            if (slot.partPrefab == null)
            {
                if (!string.IsNullOrEmpty(slot.boneName))
                    Debug.LogWarning($"[PlayerBodyPartSlots] 슬롯(bone='{slot.boneName}')에 partPrefab이 없습니다. ({name})");
                continue;
            }

            if (string.IsNullOrEmpty(slot.boneName))
            {
                Debug.LogWarning($"[PlayerBodyPartSlots] 슬롯(prefab='{slot.partPrefab.name}')에 boneName이 비어 있습니다. ({name})");
                continue;
            }

            Transform bone = FindInHierarchy(slot.boneName);
            if (bone == null)
            {
                Debug.LogWarning($"[PlayerBodyPartSlots] 본 '{slot.boneName}'을 찾지 못했습니다. ({name})");
                continue;
            }

            var instance = SpawnPartInstance(slot.partPrefab, bone, isEditorPreview);
            if (instance == null) continue;

            ApplyLocalTransform(instance.transform, slot.localOffset, slot.localRotationEuler, slot.localScale);

#if UNITY_EDITOR
            if (isEditorPreview)
                RegisterEditorPreviewInstance(instance);
#endif
        }
    }

    private static GameObject SpawnPartInstance(GameObject prefab, Transform parent, bool isEditorPreview)
    {
        if (prefab == null || parent == null) return null;

#if UNITY_EDITOR
        if (isEditorPreview)
        {
            if (UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(parent.gameObject) == null)
            {
                Debug.LogWarning(
                    $"[PlayerBodyPartSlots] Prefab 수정 창이 아니라서 '{parent.name}'에 미리보기를 붙일 수 없습니다.");
                return null;
            }

            // Prefab 연결 없는 일반 Instantiate → Prefab Mode 저장에 덜 묶임
            var instance = Object.Instantiate(prefab, parent);
            if (instance == null)
                return null;

            if (instance.transform.parent != parent)
            {
                Object.DestroyImmediate(instance);
                Debug.LogWarning(
                    $"[PlayerBodyPartSlots] '{prefab.name}' 미리보기 부착에 실패해 생성을 취소했습니다.");
                return null;
            }

            instance.name = prefab.name;
            PlayerBodyPartPreviewMarker.EnsureMarked(instance);
            return instance;
        }
#endif

        var go = Instantiate(prefab, parent);
        go.name = prefab.name;
        return go;
    }

#if UNITY_EDITOR
    private void RegisterEditorPreviewInstance(GameObject instance)
    {
        if (instance == null) return;
        if (!_editorPreviewInstances.Contains(instance))
            _editorPreviewInstances.Add(instance);
    }
#endif

    private Transform FindInHierarchy(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return null;
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            // 잘못 저장된 미리보기가 있어도 본 검색에서 제외
            if (PlayerBodyPartPreviewMarker.IsUnderPreview(t)) continue;
            if (t.name == objectName) return t;
        }
        return null;
    }

    private static void ApplyLocalTransform(Transform tr, Vector3 pos, Vector3 euler, Vector3 scale)
    {
        tr.localPosition = pos;
        tr.localRotation = Quaternion.Euler(euler);
        tr.localScale = scale;
    }
}
