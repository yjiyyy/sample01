using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MobileJoystick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [Header("고정형 조이스틱 구성요소")]
    public RectTransform baseRect;   // 베이스 이미지(원)
    public RectTransform handleRect; // 스틱 이미지(작은 원)

    [Header("설정")]
    [Tooltip("베이스 반지름(px). 0이면 baseRect 크기의 절반을 사용")]
    public float radius = 0f;
    [Range(0f, 0.5f)] public float deadzone = 0.15f;

    private Canvas canvas;
    private Camera uiCamera;
    private int activePointerId = -100; // 활성 터치 ID (단일 터치 전용)
    private Vector2 output; // -1..1

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                uiCamera = canvas.worldCamera;
            else
                uiCamera = null;
        }
        ResetHandle();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointerId != -100) return; // 이미 사용 중
        activePointerId = eventData.pointerId;
        UpdateDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId) return;
        UpdateDrag(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId) return;
        activePointerId = -100;
        output = Vector2.zero;
        InputManager.Instance?.SetMobileMove(output);
        ResetHandle();
    }

    private void UpdateDrag(PointerEventData eventData)
    {
        if (baseRect == null || handleRect == null) return;

        Vector2 screenPos = eventData.position;
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            baseRect, screenPos, uiCamera, out localPos);

        // 반지름 결정
        float r = radius > 0f ? radius : Mathf.Min(baseRect.rect.width, baseRect.rect.height) * 0.5f;

        // 중심 기준 오프셋 → -r..r
        Vector2 offset = localPos;
        offset = Vector2.ClampMagnitude(offset, r);

        // 핸들 이동
        handleRect.anchoredPosition = offset;

        // 출력 -1..1
        Vector2 norm = offset / Mathf.Max(r, 0.0001f);
        if (norm.magnitude < deadzone) norm = Vector2.zero;

        // y축은 전/후(Vertical), x축은 좌/우(Horizontal)
        output = new Vector2(norm.x, norm.y);
        InputManager.Instance?.SetMobileMove(output);
    }

    private void ResetHandle()
    {
        if (handleRect != null)
            handleRect.anchoredPosition = Vector2.zero;
    }
}