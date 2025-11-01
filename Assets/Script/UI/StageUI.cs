using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class StageUI : MonoBehaviour
{
    [Header("UI 레퍼런스")]
    public Text timerText;
    public Text centerText;

    [Header("스타트 텍스트 표시 시간")]
    public float startTextDuration = 1f;

    public void ShowStartText()
    {
        if (centerText == null) return;
        centerText.text = "START!";
        centerText.enabled = true;
        StopAllCoroutines();
        StartCoroutine(HideCenterTextAfter(startTextDuration));
    }

    public void UpdateTimer(float timeRemaining)
    {
        if (timerText == null) return;
        int t = Mathf.CeilToInt(timeRemaining);
        int m = t / 60;
        int s = t % 60;
        timerText.text = $"{m:00}:{s:00}";
    }

    public void ShowSuccessText()
    {
        if (centerText == null) return;
        centerText.text = "MISSION\nSUCCESS!";
        centerText.enabled = true;
    }

    private IEnumerator HideCenterTextAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (centerText != null) centerText.enabled = false;
    }
}