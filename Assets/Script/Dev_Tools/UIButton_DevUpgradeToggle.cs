using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// UI 버튼에서 DevUpgradeSwitcher를 열고 닫는 브릿지.
/// slotIndex >= 0이면 해당 슬롯 대상으로 오버레이를 엽니다.
/// </summary>
public class UIButton_DevUpgradeToggle : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private DevUpgradeSwitcher switcher;
    [SerializeField] private int slotIndex = -1;
    [SerializeField] private bool openOnPointerClick = true;

    public void Toggle()
    {
        EnsureSwitcher();
        if (switcher == null)
            return;

        if (slotIndex >= 0)
            switcher.ToggleOverlayForSlot(slotIndex);
        else
            switcher.ToggleOverlay();
    }

    public void Open()
    {
        EnsureSwitcher();
        if (switcher == null)
            return;

        if (slotIndex >= 0)
            switcher.OpenOverlayForSlot(slotIndex);
        else
            switcher.OpenOverlay();
    }

    public void Close()
    {
        EnsureSwitcher();
        switcher?.CloseOverlay();
    }

    public void OpenForSlot(int index)
    {
        EnsureSwitcher();
        switcher?.OpenOverlayForSlot(index);
    }

    private void EnsureSwitcher()
    {
        if (switcher != null)
            return;

        switcher = Object.FindFirstObjectByType<DevUpgradeSwitcher>();
        if (switcher == null)
            Debug.LogWarning("[UIButton_DevUpgradeToggle] DevUpgradeSwitcher를 찾을 수 없습니다. GameManager에 DevUpgradeSwitcher를 추가하세요.");
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!openOnPointerClick)
            return;

        Open();
    }
}
