using UnityEngine;

/// <summary>
/// Safe Area Fitter
/// - Screen.safeArea를 기준으로 이 RectTransform의 anchorMin/anchorMax를 조정합니다.
/// - HUD의 루트(또는 HP UI 루트)에 붙이면 Notch/screen cutout를 피할 수 있습니다.
/// - 사용법: HP UI 루트에 자동으로 추가되며, 필요하면 다른 HUD 패널에도 붙이세요.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    RectTransform _rect;
    Rect _lastSafeArea = Rect.zero;
    Vector2 _lastScreenSize = Vector2.zero;
    ScreenOrientation _lastOrientation = ScreenOrientation.AutoRotation;

    void Awake()
    {
        _rect = GetComponent<RectTransform>();
        Apply();
    }

    void Start()
    {
        Apply();
    }

    void OnEnable()
    {
        Apply();
    }

    void Update()
    {
        // 화면 크기/안전영역/회전이 바뀌면 재적용
        if (_rect == null) return;

        Rect safeArea = Screen.safeArea;
        Vector2 screenSize = new Vector2(Screen.width, Screen.height);
        ScreenOrientation orientation = Screen.orientation;

        if (safeArea != _lastSafeArea || screenSize != _lastScreenSize || orientation != _lastOrientation)
        {
            Apply();
            _lastSafeArea = safeArea;
            _lastScreenSize = screenSize;
            _lastOrientation = orientation;
        }
    }

    void Apply()
    {
        if (_rect == null) _rect = GetComponent<RectTransform>();

        Rect safeArea = Screen.safeArea;
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        Vector2 screenSize = new Vector2(Screen.width, Screen.height);
        if (screenSize.x <= 0 || screenSize.y <= 0) return;

        anchorMin.x /= screenSize.x;
        anchorMin.y /= screenSize.y;
        anchorMax.x /= screenSize.x;
        anchorMax.y /= screenSize.y;

        // Safe area가 전체 화면이면 변경하지 않음
        if (anchorMin == _rect.anchorMin && anchorMax == _rect.anchorMax) return;

        _rect.anchorMin = anchorMin;
        _rect.anchorMax = anchorMax;
        _rect.offsetMin = Vector2.zero;
        _rect.offsetMax = Vector2.zero;
    }
}