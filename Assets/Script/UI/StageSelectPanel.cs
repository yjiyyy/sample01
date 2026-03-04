using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비에서 스테이지를 선택하는 패널.
/// 현재 버전은 **동적 생성 없이**
/// 씬에 배치된 고정 10개 버튼을 보여주는 역할만 합니다.
/// </summary>
public class StageSelectPanel : MonoBehaviour
{
    [Header("UI 레퍼런스")]
    [SerializeField] private Button closeButton;

    private void Awake()
    {
        if (closeButton != null)
        {
            // 닫기 버튼은 항상 Hide만 호출하도록 고정
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }
    }

    /// <summary>
    /// 패널을 표시합니다. (버튼은 이미 씬에 고정 배치되어 있다고 가정)
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

