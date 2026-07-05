using UnityEngine;

/// <summary>
/// 아이템 박스 맵 스폰에 쓸 층(영역) 하나를 표시합니다.
/// 같은 GameObject에 BoxCollider를 두고, ItemBoxSpawner와 같은 아트 씬에 배치하세요.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class ItemBoxSpawnFloorArea : MonoBehaviour
{
    [Tooltip("Scene 뷰 Gizmo 색. 비어 있으면 ItemBoxSpawner가 층마다 자동 색을 씁니다.")]
    [SerializeField] private Color gizmoColor = new Color(0.3f, 1f, 0.45f, 0.85f);

    public BoxCollider Box
    {
        get
        {
            if (boxCollider == null)
                boxCollider = GetComponent<BoxCollider>();
            return boxCollider;
        }
    }

    private BoxCollider boxCollider;

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();
    }

    private void OnValidate()
    {
        if (boxCollider == null)
            boxCollider = GetComponent<BoxCollider>();
    }

#if UNITY_EDITOR
    public Color GizmoColor => gizmoColor;

    private void OnDrawGizmos()
    {
        BoxCollider col = boxCollider != null ? boxCollider : GetComponent<BoxCollider>();
        if (col == null)
            return;

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
#endif
}
