using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 선택 핀 뒤에 가벼운 원형 파동을 그립니다. 별도 텍스처가 필요하지 않습니다.
/// </summary>
public sealed class StageSelectPulseGraphic : MaskableGraphic
{
    [SerializeField, Range(16, 96)] private int segments = 64;
    [SerializeField] private float lineWidth = 3f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        float maxRadius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .5f;
        AddRing(vh, maxRadius * .42f, new Color(1f,.88f,0f,.82f));
        AddRing(vh, maxRadius * .68f, new Color(1f,.88f,0f,.43f));
        AddRing(vh, maxRadius * .94f, new Color(1f,.88f,0f,.2f));
    }

    private void AddRing(VertexHelper vh, float radius, Color color)
    {
        int start = vh.currentVertCount;
        float inner = Mathf.Max(0f, radius - lineWidth);
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
            vh.AddVert(direction * inner, color, Vector2.zero);
            vh.AddVert(direction * radius, color, Vector2.zero);
        }
        for (int i = 0; i < segments; i++)
        {
            int index = start + i * 2;
            vh.AddTriangle(index, index + 1, index + 3);
            vh.AddTriangle(index, index + 3, index + 2);
        }
    }
}
