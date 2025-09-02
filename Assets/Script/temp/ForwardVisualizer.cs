using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class ForwardVisualizer : MonoBehaviour
{
    [Header("선 표시 설정")]
    public float lineLength = 5f;
    public Color lineColor = Color.red;

    private LineRenderer lineRenderer;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();

        // 라인 설정
        lineRenderer.positionCount = 2;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default")); // 기본 투명 셰이더
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
    }

    void Update()
    {
        Vector3 start = transform.position;
        Vector3 end = start + transform.forward * lineLength;

        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
    }
}
