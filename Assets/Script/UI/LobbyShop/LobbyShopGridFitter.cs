using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상점 그리드가 가로 2칸을 지키도록 칸 크기를 맞춥니다.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(GridLayoutGroup))]
public class LobbyShopGridFitter : MonoBehaviour
{
    public const int ColumnCount = 2;

    [SerializeField] private float minCellHeight = 136f;
    [SerializeField] private float heightFromWidth = 0.61f;

    private GridLayoutGroup _grid;
    private RectTransform _rect;
    private float _lastWidth = -1f;

    private void OnEnable()
    {
        Apply();
    }

    private void OnRectTransformDimensionsChange()
    {
        Apply();
    }

    public void ForceApply()
    {
        _lastWidth = -1f;
        Apply();
    }

    public void Apply()
    {
        if (_grid == null)
            _grid = GetComponent<GridLayoutGroup>();
        if (_rect == null)
            _rect = transform as RectTransform;
        if (_grid == null || _rect == null)
            return;

        float width = _rect.rect.width;
        if (width < 8f)
            return;
        if (Mathf.Abs(width - _lastWidth) < 0.5f && _grid.constraintCount == ColumnCount)
            return;

        _lastWidth = width;
        _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _grid.constraintCount = ColumnCount;

        float pad = _grid.padding.left + _grid.padding.right;
        float space = _grid.spacing.x * (ColumnCount - 1);
        float cellW = Mathf.Floor((width - pad - space) / ColumnCount);
        if (cellW < 8f)
            return;

        float cellH = Mathf.Max(minCellHeight, cellW * heightFromWidth);
        _grid.cellSize = new Vector2(cellW, cellH);
    }
}
