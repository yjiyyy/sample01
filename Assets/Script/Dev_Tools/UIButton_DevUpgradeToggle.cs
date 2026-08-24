using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 과거 업그레이드 슬롯 터치 치트와의 프리팹 호환용 컴포넌트.
/// 치트 메뉴는 플레이어 초상화에서만 열리므로 터치/인스펙터 호출은 동작하지 않습니다.
/// </summary>
public class UIButton_DevUpgradeToggle : MonoBehaviour, IPointerClickHandler
{
    public void Toggle()
    {
        // 의도적으로 비워 둠: 업그레이드 슬롯 터치 치트 제거.
    }

    public void Open()
    {
        // 의도적으로 비워 둠: 업그레이드 슬롯 터치 치트 제거.
    }

    public void Close()
    {
        // 의도적으로 비워 둠: 과거 터치 치트 연결 제거.
    }

    public void OpenForSlot(int index)
    {
        // 의도적으로 비워 둠: 업그레이드 슬롯 터치 치트 제거.
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 의도적으로 비워 둠: 업그레이드 슬롯 터치 치트 제거.
    }
}
