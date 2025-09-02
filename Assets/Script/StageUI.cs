using UnityEngine;
using TMPro;

public class StageUI : MonoBehaviour
{
    [Header("텍스트 연결")]
    public TextMeshProUGUI startText;
    public TextMeshProUGUI successText;
    public TextMeshProUGUI timerText;

    void Start()
    {
        // 시작 시 텍스트들 꺼두기 (필요 시)
        if (startText != null) startText.gameObject.SetActive(false);
        if (successText != null) successText.gameObject.SetActive(false);
    }

    /// <summary>
    /// 시작 텍스트를 1초간 보여줍니다
    /// </summary>
    public void ShowStartText()
    {
        if (startText != null)
            StartCoroutine(ShowTemporaryText(startText, 1f));
    }

    /// <summary>
    /// 성공 텍스트를 1초간 보여줍니다
    /// </summary>
    public void ShowSuccessText()
    {
        if (successText != null)
            StartCoroutine(ShowTemporaryText(successText, 1f));
    }

    /// <summary>
    /// 타이머 UI 업데이트
    /// </summary>
    public void UpdateTimer(float timeRemaining)
    {
        if (timerText == null) return;

        int minutes = Mathf.FloorToInt(timeRemaining / 60f);
        int seconds = Mathf.FloorToInt(timeRemaining % 60f);
        timerText.text = $"{minutes:D2}:{seconds:D2}";
    }

    /// <summary>
    /// 특정 텍스트를 주어진 시간 동안만 보여줍니다
    /// </summary>
    private System.Collections.IEnumerator ShowTemporaryText(TextMeshProUGUI textObj, float duration)
    {
        textObj.gameObject.SetActive(true);
        yield return new WaitForSeconds(duration);
        textObj.gameObject.SetActive(false);
    }
    /// <summary>
    /// 타이머 텍스트 갱신 (스테이지 남은 시간)
    /// </summary>


}
