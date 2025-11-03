using UnityEngine;
using UnityEngine.UI;

// 슬롯 뷰: Icon + Count만 표시 (표시 전용)
public class SlotView : MonoBehaviour
{
    [Header("UI References")]
    public Image iconImage;
    public Text countText;
    public GameObject emptyRoot; // 빈 슬롯일 때 표시할 오브젝트 (Optional)
    public GameObject filledRoot; // 채워진 슬롯일 때 표시할 오브젝트 (Optional)

    private string currentId;

    // 데이터 세팅
    public void SetData(WeaponDataSO data, int count)
    {
        currentId = data?.id;

        if (data == null)
        {
            // 빈 슬롯 처리
            if (emptyRoot != null) emptyRoot.SetActive(true);
            if (filledRoot != null) filledRoot.SetActive(false);
            if (iconImage != null) iconImage.sprite = null;
            if (countText != null) countText.text = "";
            return;
        }

        if (emptyRoot != null) emptyRoot.SetActive(false);
        if (filledRoot != null) filledRoot.SetActive(true);

        if (iconImage != null) iconImage.sprite = data.icon;
        if (countText != null)
        {
            if (count > 1) countText.text = count.ToString();
            else if (count == 1) countText.text = ""; // 단일 장착은 개수 숨김
            else countText.text = "";
        }
    }

    public void Clear()
    {
        SetData(null, 0);
    }

    // 필요 시 슬롯 클릭 시 호출될 함수(선택/하이라이트 처리 등)
    public void OnClick()
    {
        // 현재는 표시 전용. 필요하면 여기서 이벤트를 트리거하도록 구현.
        Debug.Log($"Slot clicked: {currentId}");
    }
}