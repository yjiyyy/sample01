using UnityEngine;

/// <summary>
/// 적 루트에 붙여 Head / Hair 파츠 프리팹을 지정합니다.
/// Play 시 Bip001 Head / HairSocket 에 인스턴스를 생성합니다.
/// 에디터에서는 previewInEditor 로 Play 전 미리보기 가능 (프리팹에 저장되지 않음).
/// </summary>
[DisallowMultipleComponent]
[ExecuteAlways]
public class EnemyBodyPartSlots : MonoBehaviour
{
    public const string HeadBoneName = "Bip001 Head";
    public const string HairSocketName = "HairSocket";

    [Header("파츠 프리팹")]
    [Tooltip("Bip001 Head 본에 붙일 Head 파츠 (MeshRenderer 등).")]
    public GameObject headPartPrefab;

    [Tooltip("HairSocket 자식에 붙일 Hair 파츠 (SkinnedMesh + SpringBone).")]
    public GameObject hairPartPrefab;

    [Header("스케일")]
    [Tooltip("Head·Hair 모두 적용되는 일괄 스케일 (1이면 원본 크기).")]
    public float partsScale = 1f;

    [Header("피부색 (M_Body)")]
    [Tooltip("M_Body용 Mat_PC_* (Bake·틴트 기준). 비우면 렌더러 슬롯 → 프리팹 이름 순으로 자동 탐색.")]
    public Material bodySkinMaterial;

    [Tooltip("피부 톤 처리에 마스크·틴트를 사용할지 여부 (내부용). 항상 true로 두고 필요 시 코드에서 끌 수 있습니다.")]
    [HideInInspector]
    public bool applyBodySkinTint = true;

    [Tooltip("선택된 피부 톤에서 계산된 실제 색 값 (내부용).")]
    [HideInInspector]
    public Color bodySkinColor = new Color(240f / 255f, 209f / 255f, 178f / 255f, 1f);

    [Tooltip("Bake된 피부 마스크 (에디터에서 자동 생성).")]
    public Texture2D skinMaskTexture;

#if UNITY_EDITOR
    [Tooltip("마스크 Bake 시 F0D1B2와 얼마나 비슷한 픽셀을 피부로 볼지 (0.05~0.35). 내부 튜닝용입니다.")]
    [HideInInspector]
    [Range(0.05f, 0.35f)]
    public float maskBakeColorThreshold = 0.15f;
#endif

    [Header("에디터")]
    [Tooltip("켜면 Play 전에도 Head/Hair를 Scene에 표시합니다 (저장되지 않는 미리보기).")]
    public bool previewInEditor = true;

    private bool _attached;

    private static readonly Vector3 HeadLocalOffset = Vector3.zero;
    private static readonly Vector3 HeadLocalRotationEuler = Vector3.zero;
    private static readonly Vector3 HairLocalOffset = Vector3.zero;
    private static readonly Vector3 HairLocalRotationEuler = new Vector3(-90f, 90f, 0f);

#if UNITY_EDITOR
    private bool _previewRefreshQueued;
    private bool _previewClearQueued;
#endif

    private void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            QueueEditorSkinTintApply();
            if (previewInEditor)
                QueueEditorPreviewRefresh();
        }
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            QueueEditorPreviewClear();
#endif
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;

        QueueEditorSkinTintApply();

        if (!previewInEditor)
            QueueEditorPreviewClear();
        else
            QueueEditorPreviewRefresh();
    }

    private bool _skinTintApplyQueued;

    private void QueueEditorSkinTintApply()
    {
        if (_skinTintApplyQueued) return;
        _skinTintApplyQueued = true;
        UnityEditor.EditorApplication.delayCall += OnDelayedEditorSkinTintApply;
    }

    private void OnDelayedEditorSkinTintApply()
    {
        _skinTintApplyQueued = false;
        if (this == null || Application.isPlaying) return;
        ApplyBodySkinTint();
    }
#endif

    /// <summary>EnemyFacade.Start에서 호출.</summary>
    public void TryAttachParts(EnemyFacade facade)
    {
        if (_attached) return;

#if UNITY_EDITOR
        // Play 진입 시에만 즉시 제거 (OnValidate/Inspector 콜백이 아님)
        if (Application.isPlaying)
            ClearEditorPreviewNow();
#endif

        // Head/Hair 없어도 피부 틴트는 적용 (에디터 OnValidate와 동일)
        if (headPartPrefab != null || hairPartPrefab != null)
            AttachPartsInternal(facade, isEditorPreview: false);

        ApplyBodySkinTint();
        _attached = true;
    }

    /// <summary>M_Body 머티리얼에 마스크 기반 피부 톤 적용.</summary>
    public void ApplyBodySkinTint()
    {
        if (!applyBodySkinTint || skinMaskTexture == null)
            return;

        EnemyBodySkinTintApplier.Apply(gameObject, bodySkinColor, skinMaskTexture, true, bodySkinMaterial);
    }

#if UNITY_EDITOR
    /// <summary>Inspector 버튼·자동 갱신용 (delayCall로 안전하게 처리).</summary>
    public void RefreshEditorPreview()
    {
        if (Application.isPlaying) return;
        QueueEditorPreviewRefresh();
    }

    /// <summary>Inspector 버튼용 (delayCall로 안전하게 처리).</summary>
    public void ClearEditorPreview()
    {
        if (Application.isPlaying) return;
        QueueEditorPreviewClear();
    }

    private void ClearEditorPreviewNow()
    {
        var markers = GetComponentsInChildren<EnemyBodyPartPreviewMarker>(true);
        for (int i = markers.Length - 1; i >= 0; i--)
        {
            if (markers[i] == null) continue;
            DestroyImmediate(markers[i].gameObject);
        }
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

        if (!previewInEditor) return;
        if (headPartPrefab == null && hairPartPrefab == null) return;

        AttachPartsInternal(null, isEditorPreview: true);
        ApplyBodySkinTint();
    }

    private void OnDelayedEditorPreviewClear()
    {
        _previewClearQueued = false;
        if (this == null || Application.isPlaying) return;
        ClearEditorPreviewNow();
    }
#endif

    private bool AttachPartsInternal(EnemyFacade facade, bool isEditorPreview)
    {
        if (headPartPrefab == null && hairPartPrefab == null) return false;

        Transform headBone = FindInHierarchy(HeadBoneName);
        if (headBone == null)
        {
            Debug.LogWarning($"[EnemyBodyPartSlots] '{HeadBoneName}' 본을 찾지 못했습니다. ({name})");
            return false;
        }

        if (headPartPrefab != null)
        {
            var headInstance = SpawnPartInstance(headPartPrefab, headBone, isEditorPreview);
            if (headInstance != null)
            {
                ApplyLocalTransform(headInstance.transform, HeadLocalOffset, HeadLocalRotationEuler, GetUniformPartsScale());
                if (!isEditorPreview)
                    facade?.RegisterSpawnedPart(headInstance);
            }
        }

        if (hairPartPrefab != null)
        {
            Transform hairSocket = FindInHierarchy(HairSocketName);
            if (hairSocket == null)
            {
                Debug.LogWarning(
                    $"[EnemyBodyPartSlots] '{HairSocketName}'이 없습니다. " +
                    "EnemyPrefabGenerator로 프리팹을 다시 만들거나 Bip001 Head 하위에 HairSocket을 추가하세요.");
            }
            else
            {
                var hairInstance = SpawnPartInstance(hairPartPrefab, hairSocket, isEditorPreview);
                if (hairInstance != null)
                {
                    ApplyLocalTransform(hairInstance.transform, HairLocalOffset, HairLocalRotationEuler, GetUniformPartsScale());
                    if (!isEditorPreview)
                        facade?.RegisterSpawnedPart(hairInstance);
                }
            }
        }

        return true;
    }

    private static GameObject SpawnPartInstance(GameObject prefab, Transform parent, bool isEditorPreview)
    {
        if (prefab == null || parent == null) return null;

#if UNITY_EDITOR
        if (isEditorPreview)
        {
            var instance = UnityEditor.PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null) return null;

            instance.name = prefab.name;
            instance.hideFlags = HideFlags.DontSaveInEditor | HideFlags.NotEditable;
            if (instance.GetComponent<EnemyBodyPartPreviewMarker>() == null)
                instance.AddComponent<EnemyBodyPartPreviewMarker>();
            return instance;
        }
#endif

        var go = Instantiate(prefab, parent);
        go.name = prefab.name;
        return go;
    }

    private Transform FindInHierarchy(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return null;
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
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

    private Vector3 GetUniformPartsScale()
    {
        return Vector3.one * Mathf.Max(0.0001f, partsScale);
    }

#if UNITY_EDITOR
    /// <summary>에디터: Bip001 Head 하위에 HairSocket이 없으면 생성.</summary>
    public static bool EnsureHairSocket(Transform modelRoot)
    {
        if (modelRoot == null) return false;

        Transform headBone = FindBoneRecursive(modelRoot, HeadBoneName);
        if (headBone == null) return false;

        if (FindBoneRecursive(headBone, HairSocketName) != null) return false;

        var socketGo = new GameObject(HairSocketName);
        socketGo.transform.SetParent(headBone, false);
        socketGo.transform.localPosition = Vector3.zero;
        socketGo.transform.localRotation = Quaternion.identity;
        socketGo.transform.localScale = Vector3.one;
        return true;
    }

    private static Transform FindBoneRecursive(Transform root, string boneName)
    {
        if (root == null || string.IsNullOrEmpty(boneName)) return null;
        if (root.name == boneName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindBoneRecursive(root.GetChild(i), boneName);
            if (found != null) return found;
        }
        return null;
    }
#endif
}
