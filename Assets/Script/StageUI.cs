using UnityEngine;
using TMPro;

public class StageUI : MonoBehaviour
{
    [Header("�ؽ�Ʈ ����")]
    public TextMeshProUGUI startText;
    public TextMeshProUGUI successText;
    public TextMeshProUGUI timerText;

    void Start()
    {
        // ���� �� �ؽ�Ʈ�� ���α� (�ʿ� ��)
        if (startText != null) startText.gameObject.SetActive(false);
        if (successText != null) successText.gameObject.SetActive(false);
    }

    /// <summary>
    /// ���� �ؽ�Ʈ�� 1�ʰ� �����ݴϴ�
    /// </summary>
    public void ShowStartText()
    {
        if (startText != null)
            StartCoroutine(ShowTemporaryText(startText, 1f));
    }

    /// <summary>
    /// ���� �ؽ�Ʈ�� 1�ʰ� �����ݴϴ�
    /// </summary>
    public void ShowSuccessText()
    {
        if (successText != null)
            StartCoroutine(ShowTemporaryText(successText, 1f));
    }

    /// <summary>
    /// Ÿ�̸� UI ������Ʈ
    /// </summary>
    public void UpdateTimer(float timeRemaining)
    {
        if (timerText == null) return;

        int minutes = Mathf.FloorToInt(timeRemaining / 60f);
        int seconds = Mathf.FloorToInt(timeRemaining % 60f);
        timerText.text = $"{minutes:D2}:{seconds:D2}";
    }

    /// <summary>
    /// Ư�� �ؽ�Ʈ�� �־��� �ð� ���ȸ� �����ݴϴ�
    /// </summary>
    private System.Collections.IEnumerator ShowTemporaryText(TextMeshProUGUI textObj, float duration)
    {
        textObj.gameObject.SetActive(true);
        yield return new WaitForSeconds(duration);
        textObj.gameObject.SetActive(false);
    }
    /// <summary>
    /// Ÿ�̸� �ؽ�Ʈ ���� (�������� ���� �ð�)
    /// </summary>


}
