using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MobileJoystick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [Header("������ ���̽�ƽ �������")]
    public RectTransform baseRect;
    public RectTransform handleRect;

    [Header("����")]
    [Tooltip("���̽� ������(px). 0�̸� baseRect ũ���� ������ ���")]
    public float radius = 0f;
    [Range(0f, 0.5f)] public float deadzone = 0.15f;

    private Canvas canvas;
    private Camera uiCamera;
    private int activePointerId = -100;
    private Vector2 output;

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

        DisableNestedRaycasts();
        AutoBindHandleIfNeeded();
        ResetHandle();
    }

    private void AutoBindHandleIfNeeded()
    {
        if (handleRect != null && handleRect.name == "Handle")
            return;

        var found = FindChildByName(transform, "Handle");
        if (found != null)
            handleRect = found as RectTransform;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointerId != -100) return;
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

        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            baseRect, eventData.position, uiCamera, out localPos);

        float r = radius > 0f ? radius : Mathf.Min(baseRect.rect.width, baseRect.rect.height) * 0.5f;
        Vector2 offset = Vector2.ClampMagnitude(localPos, r);
        SetHandleOffset(offset);

        Vector2 norm = offset / Mathf.Max(r, 0.0001f);
        if (norm.magnitude < deadzone) norm = Vector2.zero;

        output = new Vector2(norm.x, norm.y);
        InputManager.Instance?.SetMobileMove(output);
    }

    private void ResetHandle()
    {
        SetHandleOffset(Vector2.zero);
    }

    private void SetHandleOffset(Vector2 offsetInBase)
    {
        if (handleRect == null || baseRect == null)
            return;

        if (handleRect.parent == baseRect)
        {
            handleRect.anchoredPosition = offsetInBase;
            return;
        }

        if (handleRect.parent == null)
            return;

        Vector3 world = baseRect.TransformPoint(new Vector3(offsetInBase.x, offsetInBase.y, 0f));
        Vector3 local = handleRect.parent.InverseTransformPoint(world);
        handleRect.anchoredPosition = new Vector2(local.x, local.y);
    }

    private void DisableNestedRaycasts()
    {
        var graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null && graphics[i].gameObject != gameObject)
                graphics[i].raycastTarget = false;
        }
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        if (root == null)
            return null;
        if (root.name == name)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChildByName(root.GetChild(i), name);
            if (found != null)
                return found;
        }
        return null;
    }
}
