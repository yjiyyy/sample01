using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 타이머 하단에 스테이지 레벨 아이콘을 왼쪽→오른쪽으로 추가 표시합니다.
/// 에디터에서는 미리보기 아이콘으로 위치를 확인할 수 있습니다.
/// </summary>
[DisallowMultipleComponent]
[ExecuteAlways]
public class StageLevelIconBar : MonoBehaviour
{
    private const string IconNamePrefix = "LevelIcon_";

    [Header("컨테이너")]
    [Tooltip("비어 있으면 이 오브젝트의 RectTransform을 사용합니다.")]
    [SerializeField] private RectTransform iconContainer;

    [Header("아이콘 모양")]
    [SerializeField] private float iconSize = 36f;
    [SerializeField] private float spacing = 8f;

    [Header("기본 아이콘 (StageData에 없을 때 사용)")]
    [SerializeField] private Sprite defaultIcon;

    [Header("에디터 미리보기 (플레이 전 위치 확인용)")]
    [SerializeField] private bool showEditorPreview = true;
    [SerializeField] private int editorPreviewIconCount = 1;

    private readonly List<Image> _icons = new List<Image>();
    private StageData _stageData;
    private int _shownCount;
    private HorizontalLayoutGroup _layoutGroup;

    private void OnEnable()
    {
        EnsureContainer();
        if (!Application.isPlaying)
            ScheduleEditorRefresh();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            ClearIcons();
    }

    private void OnValidate()
    {
        EnsureContainer();

        if (Application.isPlaying)
        {
            ApplyLayoutToAllIcons();
            return;
        }

        ScheduleEditorRefresh();
    }

    private void Awake()
    {
        EnsureContainer();
        if (Application.isPlaying)
            ClearIcons();
    }

    public void Initialize(StageData data)
    {
        _stageData = data;
        EnsureContainer();
        ClearIcons();
        SetIconCount(1);
    }

    /// <summary>표시 레벨(1부터)만큼 아이콘을 채웁니다.</summary>
    public void SetIconCount(int displayLevel)
    {
        EnsureContainer();
        if (iconContainer == null)
            return;

        int target = Mathf.Max(0, displayLevel);

        while (_shownCount < target)
        {
            AddIcon(_shownCount);
            _shownCount++;
        }

        while (_shownCount > target)
        {
            RemoveLastIcon();
            _shownCount--;
        }

        ApplyLayoutToAllIcons();
    }

    private void RefreshEditorPreview()
    {
        if (Application.isPlaying)
            return;

        EnsureContainer();
        ClearIcons();

        if (!showEditorPreview || defaultIcon == null)
            return;

        int count = Mathf.Max(1, editorPreviewIconCount);
        SetIconCount(count);
    }

    private void EnsureContainer()
    {
        if (iconContainer == null)
            iconContainer = transform as RectTransform;

        if (iconContainer == null)
            return;

        _layoutGroup = iconContainer.GetComponent<HorizontalLayoutGroup>();
        if (_layoutGroup == null)
            _layoutGroup = iconContainer.gameObject.AddComponent<HorizontalLayoutGroup>();

        _layoutGroup.childAlignment = TextAnchor.MiddleCenter;
        _layoutGroup.childControlWidth = false;
        _layoutGroup.childControlHeight = false;
        _layoutGroup.childForceExpandWidth = false;
        _layoutGroup.childForceExpandHeight = false;
        _layoutGroup.spacing = spacing;
    }

    private void ApplyLayoutToAllIcons()
    {
        if (_layoutGroup != null)
            _layoutGroup.spacing = spacing;

        float size = Mathf.Max(1f, iconSize);
        for (int i = 0; i < _icons.Count; i++)
        {
            Image image = _icons[i];
            if (image == null)
                continue;

            RectTransform rect = image.rectTransform;
            rect.sizeDelta = new Vector2(size, size);

            LayoutElement layout = image.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.preferredWidth = size;
                layout.preferredHeight = size;
            }
        }

        if (iconContainer != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(iconContainer);
    }

    private void AddIcon(int zeroBasedIndex)
    {
        var go = new GameObject($"{IconNamePrefix}{zeroBasedIndex + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(iconContainer, false);

        var rect = go.GetComponent<RectTransform>();
        float size = Mathf.Max(1f, iconSize);
        rect.sizeDelta = new Vector2(size, size);

        var layout = go.AddComponent<LayoutElement>();
        layout.preferredWidth = size;
        layout.preferredHeight = size;

        var image = go.GetComponent<Image>();
        image.sprite = ResolveIcon(zeroBasedIndex);
        image.preserveAspect = true;
        image.raycastTarget = false;

        if (image.sprite == null)
            image.color = new Color(1f, 1f, 1f, 0.35f);

        _icons.Add(image);
    }

    private void RemoveLastIcon()
    {
        if (_icons.Count == 0)
            return;

        int lastIndex = _icons.Count - 1;
        Image image = _icons[lastIndex];
        _icons.RemoveAt(lastIndex);

        if (image != null)
            DestroyIconObject(image.gameObject);
    }

    private Sprite ResolveIcon(int zeroBasedIndex)
    {
        if (_stageData != null && _stageData.levelIcons != null &&
            zeroBasedIndex >= 0 && zeroBasedIndex < _stageData.levelIcons.Length &&
            _stageData.levelIcons[zeroBasedIndex] != null)
        {
            return _stageData.levelIcons[zeroBasedIndex];
        }

        if (defaultIcon != null)
            return defaultIcon;

        return null;
    }

    private void ClearIcons()
    {
        for (int i = _icons.Count - 1; i >= 0; i--)
        {
            if (_icons[i] != null)
                DestroyIconObject(_icons[i].gameObject);
        }
        _icons.Clear();
        _shownCount = 0;

        if (iconContainer == null)
            return;

        for (int i = iconContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = iconContainer.GetChild(i);
            if (child != null && child.name.StartsWith(IconNamePrefix))
                DestroyIconObject(child.gameObject);
        }
    }

    private void DestroyIconObject(GameObject go)
    {
        if (go == null) return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(go);
        else
            Destroy(go);
#else
        Destroy(go);
#endif
    }

#if UNITY_EDITOR
    private void ScheduleEditorRefresh()
    {
        EditorApplication.delayCall -= HandleDelayedEditorRefresh;
        EditorApplication.delayCall += HandleDelayedEditorRefresh;
    }

    private void HandleDelayedEditorRefresh()
    {
        if (this == null)
            return;

        RefreshEditorPreview();
    }
#endif
}
