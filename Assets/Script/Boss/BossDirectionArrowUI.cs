using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 가장자리에 보스(목표) 방향 화살표를 표시합니다.
/// Canvas 자식으로 두고 Arrow RectTransform을 연결하세요.
/// </summary>
[DisallowMultipleComponent]
public class BossDirectionArrowUI : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private RectTransform arrow;
    [SerializeField] private Canvas canvas;

    [Header("표시")]
    [SerializeField] private float edgePadding = 56f;
    [Tooltip("목표가 화면 안에 보이면 화살표를 숨깁니다.")]
    [SerializeField] private bool hideWhenTargetOnScreen = true;
    [SerializeField] private float targetHeightOffset = 2f;
    [Tooltip("화살표 스프라이트 기준각 보정값(Z). 오른쪽으로 90도 회전은 90.")]
    [SerializeField] private float rotationOffsetZ = 0f;

    private Transform worldTarget;
    private Camera worldCamera;

    private void Awake()
    {
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        if (arrow != null)
            arrow.gameObject.SetActive(false);
    }

    public void SetTarget(Transform target)
    {
        worldTarget = target;
        if (arrow == null)
            return;

        arrow.gameObject.SetActive(target != null);
    }

    public void ClearTarget()
    {
        SetTarget(null);
    }

    private void LateUpdate()
    {
        if (worldTarget == null || arrow == null || canvas == null)
            return;

        Camera cam = ResolveWorldCamera();
        if (cam == null)
            return;

        RectTransform canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        Camera uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        Vector3 worldPos = worldTarget.position + Vector3.up * targetHeightOffset;
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

        bool behindCamera = screenPos.z < 0f;
        if (behindCamera)
        {
            screenPos.x = Screen.width - screenPos.x;
            screenPos.y = Screen.height - screenPos.y;
        }

        float pad = Mathf.Max(0f, edgePadding);
        bool onScreen =
            !behindCamera &&
            screenPos.x >= pad &&
            screenPos.x <= Screen.width - pad &&
            screenPos.y >= pad &&
            screenPos.y <= Screen.height - pad;

        if (hideWhenTargetOnScreen && onScreen)
        {
            arrow.gameObject.SetActive(false);
            return;
        }

        arrow.gameObject.SetActive(true);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
            uiCam,
            out Vector2 centerLocal);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            new Vector2(screenPos.x, screenPos.y),
            uiCam,
            out Vector2 targetLocal);

        Vector2 direction = targetLocal - centerLocal;
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.up;

        Vector2 edgeLocal = GetEdgePosition(canvasRect, centerLocal, direction.normalized, pad);
        arrow.anchoredPosition = edgeLocal;

        float angleDeg = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        arrow.localRotation = Quaternion.Euler(0f, 0f, angleDeg - 90f + rotationOffsetZ);
    }

    private static Vector2 GetEdgePosition(RectTransform canvasRect, Vector2 center, Vector2 dir, float padding)
    {
        Rect rect = canvasRect.rect;
        float halfW = rect.width * 0.5f - padding;
        float halfH = rect.height * 0.5f - padding;
        halfW = Mathf.Max(8f, halfW);
        halfH = Mathf.Max(8f, halfH);

        float absX = Mathf.Abs(dir.x);
        float absY = Mathf.Abs(dir.y);
        float scale;

        if (absX < 0.0001f)
            scale = halfH / absY;
        else if (absY < 0.0001f)
            scale = halfW / absX;
        else
            scale = Mathf.Min(halfW / absX, halfH / absY);

        return center + dir * scale;
    }

    private Camera ResolveWorldCamera()
    {
        if (worldCamera != null && worldCamera.isActiveAndEnabled)
            return worldCamera;

        worldCamera = Camera.main;
        return worldCamera;
    }
}
