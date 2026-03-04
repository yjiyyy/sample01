using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비에서 상점 버튼을 눌렀을 때 표시되는 2x2 고정 상점 패널.
/// 동적 생성 없이, 에디터에서 배치된 버튼들을 단순히 보여주고 숨기는 역할만 합니다.
/// </summary>
public class ShopPanel : MonoBehaviour
{
    [Header("UI 레퍼런스")]
    [SerializeField] private Button closeButton;

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }
    }

    /// <summary>
    /// 패널을 표시합니다.
    /// </summary>
    public void Show()
    {
        gameObject.SetActive(true);
    }

    /// <summary>
    /// 패널을 숨깁니다.
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }
}

