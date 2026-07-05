using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Game 뷰에 Dead Zone 사각형을 화면 오버레이로 표시합니다.
/// </summary>
[DisallowMultipleComponent]
public class DiabloStyleCameraDeadZoneOverlay : MonoBehaviour
{
    private static Sprite whiteSprite;

    private DiabloStyleCamera owner;
    private Canvas canvas;
    private RectTransform frameRoot;
    private Image fillImage;
    private Image borderTop;
    private Image borderBottom;
    private Image borderLeft;
    private Image borderRight;

    private float borderThickness = 2f;
    private Color borderColor = new Color(1f, 0.85f, 0.1f, 0.9f);
    private Color fillColor = new Color(1f, 0.85f, 0.1f, 0.08f);

    public void Initialize(DiabloStyleCamera camera)
    {
        owner = camera;
        EnsureUi();
        RefreshVisibility();
    }

    public void ApplyStyle(float thickness, Color border, Color fill)
    {
        borderThickness = Mathf.Max(1f, thickness);
        borderColor = border;
        fillColor = fill;
        ApplyColors();

        if (Application.isPlaying)
            RefreshLayout();
    }

    public void RefreshVisibility()
    {
        if (canvas != null)
            canvas.enabled = owner != null && owner.ShowDeadZoneInGameView;
    }

    private void LateUpdate()
    {
        if (owner == null || canvas == null || !canvas.enabled)
            return;

        RefreshLayout();
    }

    private void EnsureUi()
    {
        if (canvas != null)
            return;

        var canvasObject = new GameObject("DeadZoneOverlay");
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        canvas.pixelPerfect = true;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        var group = canvasObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        frameRoot = CreateStretchChild(canvasObject.transform, "Frame");

        fillImage = CreateImage(frameRoot, "Fill");
        borderTop = CreateImage(frameRoot, "BorderTop");
        borderBottom = CreateImage(frameRoot, "BorderBottom");
        borderLeft = CreateImage(frameRoot, "BorderLeft");
        borderRight = CreateImage(frameRoot, "BorderRight");

        ApplyColors();
    }

    private void ApplyColors()
    {
        if (fillImage == null)
            return;

        fillImage.color = fillColor;
        borderTop.color = borderColor;
        borderBottom.color = borderColor;
        borderLeft.color = borderColor;
        borderRight.color = borderColor;
    }

    private void RefreshLayout()
    {
        if (owner == null || frameRoot == null)
            return;

        owner.GetDeadZonePixelSize(out float zoneW, out float zoneH);
        float halfW = zoneW * 0.5f;
        float halfH = zoneH * 0.5f;
        float t = borderThickness;

        frameRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, zoneW);
        frameRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, zoneH);
        frameRoot.anchoredPosition = Vector2.zero;

        SetBar(borderTop, zoneW + t * 2f, t, new Vector2(0f, halfH + t * 0.5f));
        SetBar(borderBottom, zoneW + t * 2f, t, new Vector2(0f, -halfH - t * 0.5f));
        SetBar(borderLeft, t, zoneH, new Vector2(-halfW - t * 0.5f, 0f));
        SetBar(borderRight, t, zoneH, new Vector2(halfW + t * 0.5f, 0f));

        fillImage.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, zoneW);
        fillImage.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, zoneH);
        fillImage.rectTransform.anchoredPosition = Vector2.zero;
    }

    private static void SetBar(Image image, float width, float height, Vector2 anchoredPosition)
    {
        RectTransform rect = image.rectTransform;
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        rect.anchoredPosition = anchoredPosition;
    }

    private static RectTransform CreateStretchChild(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }

    private static Image CreateImage(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;

        var image = go.GetComponent<Image>();
        image.sprite = GetWhiteSprite();
        image.raycastTarget = false;
        return image;
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null)
            return whiteSprite;

        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return whiteSprite;
    }
}
