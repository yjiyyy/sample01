using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 토글 버튼(왼쪽 작은 아이콘)에 붙이는 스크립트.
/// - 오버레이가 열려 있으면 이 버튼을 비활성화(또는 클릭 무시)하여 툴이 열려있는 동안 토글버튼이 작동하지 않도록 함.
/// </summary>
[RequireComponent(typeof(Button))]
public class UIButton_DevWeaponToggle : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("토글 대상 DevWeaponSwitcher (비워두면 씬에서 자동 탐색)")]
    public DevWeaponSwitcher target;

    Button uiButton;

    void Start()
    {
        uiButton = GetComponent<Button>();
        if (target == null)
            target = FindFirstObjectByType<DevWeaponSwitcher>();
    }

    void Update()
    {
        // DevWeaponSwitcher가 열려 있는 동안 이 버튼을 비활성화(인터랙트 끔)
        if (target != null && uiButton != null)
        {
            bool overlayOpen = target.IsOverlayOpen;
            // 인터랙션 금지
            uiButton.interactable = !overlayOpen;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 클릭 시 오버레이가 열려 있으면 무시 (안전장치)
        if (target != null && target.IsOverlayOpen)
            return;

        if (target == null)
            target = FindFirstObjectByType<DevWeaponSwitcher>();

        if (target != null)
            target.ToggleOverlay();
        else
            Debug.LogWarning("[UIButton_DevWeaponToggle] DevWeaponSwitcher를 찾을 수 없습니다.");
    }

    // 인스펙터용: Button.OnClick()에 연결 가능
    public void ToggleFromInspector()
    {
        if (target == null) target = FindFirstObjectByType<DevWeaponSwitcher>();
        if (target != null && !target.IsOverlayOpen) target.ToggleOverlay();
    }
}