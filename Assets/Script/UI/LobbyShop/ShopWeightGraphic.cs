using UnityEngine;
using UnityEngine.UI;

/// <summary>추 모양 UI 아이콘. 텍스처 없이 모바일에서도 선명하게 표시합니다.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class ShopWeightGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); var r = GetPixelAdjustedRect();
        void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            int i = vh.currentVertCount;
            foreach (var v in new[] { a, b, c, d }) vh.AddVert(new Vector3(r.x + v.x * r.width, r.y + v.y * r.height), color, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
        Quad(new Vector2(.08f,.08f), new Vector2(.92f,.08f), new Vector2(.78f,.7f), new Vector2(.22f,.7f));
        for (int n = 0; n < 20; n++)
        {
            float a=n*Mathf.PI*2/20, b=(n+1)*Mathf.PI*2/20;
            Vector2 center=new Vector2(.5f,.76f);
            Quad(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.21f, center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*.21f,
                center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*.12f, center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.12f);
        }
    }
}
