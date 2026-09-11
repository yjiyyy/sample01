using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 별도 지도 텍스처 없이 단순한 세계 지도 실루엣과 격자를 그립니다.
/// 샘플 UI용이므로 지리적 정밀도보다 가벼운 표현에 초점을 둡니다.
/// </summary>
public sealed class StageSelectMapGraphic : MaskableGraphic
{
    [SerializeField] private Color gridColor = new(0.78f, 0.81f, 0.84f, 0.42f);
    [SerializeField] private Color landColor = new(0.72f, 0.75f, 0.78f, 0.82f);

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;

        for (int i = 1; i < 12; i++)
        {
            float x = Mathf.Lerp(r.xMin, r.xMax, i / 12f);
            AddQuad(vh, new Rect(x - 1f, r.yMin, 2f, r.height), gridColor);
        }

        for (int i = 1; i < 6; i++)
        {
            float y = Mathf.Lerp(r.yMin, r.yMax, i / 6f);
            AddQuad(vh, new Rect(r.xMin, y - 1f, r.width, 2f), gridColor);
        }

        AddPolygon(vh, r, new[] { // 북아메리카
            new Vector2(.06f,.73f), new Vector2(.16f,.91f), new Vector2(.31f,.86f),
            new Vector2(.37f,.68f), new Vector2(.30f,.51f), new Vector2(.17f,.48f), new Vector2(.08f,.60f)
        });
        AddPolygon(vh, r, new[] { // 남아메리카
            new Vector2(.25f,.48f), new Vector2(.36f,.44f), new Vector2(.39f,.29f),
            new Vector2(.34f,.08f), new Vector2(.28f,.23f)
        });
        AddPolygon(vh, r, new[] { // 유럽/아시아
            new Vector2(.43f,.77f), new Vector2(.52f,.88f), new Vector2(.72f,.86f),
            new Vector2(.93f,.73f), new Vector2(.89f,.53f), new Vector2(.72f,.48f),
            new Vector2(.59f,.61f), new Vector2(.48f,.60f)
        });
        AddPolygon(vh, r, new[] { // 아프리카
            new Vector2(.46f,.58f), new Vector2(.61f,.58f), new Vector2(.65f,.39f),
            new Vector2(.56f,.19f), new Vector2(.48f,.36f)
        });
        AddPolygon(vh, r, new[] { // 오스트레일리아
            new Vector2(.78f,.26f), new Vector2(.91f,.29f), new Vector2(.94f,.17f), new Vector2(.82f,.13f)
        });
        AddPolygon(vh, r, new[] { // 그린란드
            new Vector2(.34f,.90f), new Vector2(.42f,.96f), new Vector2(.46f,.87f), new Vector2(.39f,.79f)
        });
    }

    private void AddPolygon(VertexHelper vh, Rect rect, Vector2[] points)
    {
        int start = vh.currentVertCount;
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 p = new(
                Mathf.Lerp(rect.xMin, rect.xMax, points[i].x),
                Mathf.Lerp(rect.yMin, rect.yMax, points[i].y));
            vh.AddVert(p, landColor, Vector2.zero);
        }

        for (int i = 1; i < points.Length - 1; i++)
            vh.AddTriangle(start, start + i, start + i + 1);
    }

    private static void AddQuad(VertexHelper vh, Rect rect, Color color)
    {
        int start = vh.currentVertCount;
        vh.AddVert(new Vector2(rect.xMin, rect.yMin), color, Vector2.zero);
        vh.AddVert(new Vector2(rect.xMin, rect.yMax), color, Vector2.zero);
        vh.AddVert(new Vector2(rect.xMax, rect.yMax), color, Vector2.zero);
        vh.AddVert(new Vector2(rect.xMax, rect.yMin), color, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }
}
