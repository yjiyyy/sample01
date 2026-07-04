using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StageUI : MonoBehaviour
{
    [Header("UI ??????? (??? ?????? ??? ??????? ??? ???)")]
    public Text timerText;
    public TMP_Text tmpTimerText;

    public Text levelText;
    public TMP_Text tmpLevelText;

    public Text killProgressText;
    public TMP_Text tmpKillProgressText;

    public Text startText;
    public TMP_Text tmpStartText;

    public Text successText;
    public TMP_Text tmpSuccessText;

    [Header("???? ?????? (Timer ???)")]
    public StageLevelIconBar levelIconBar;

    [Header("???? ???? ??? ????")]
    public float startTextDuration = 1f;

    private void Awake()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        tmpTimerText ??= FindChildTmp("TimerText");
        timerText ??= FindChildLegacyText("TimerText");

        tmpLevelText ??= FindChildTmp("LevelText");
        levelText ??= FindChildLegacyText("LevelText");

        tmpKillProgressText ??= FindChildTmp("KillProgressText");
        killProgressText ??= FindChildLegacyText("KillProgressText");

        tmpStartText ??= FindChildTmp("StartText");
        startText ??= FindChildLegacyText("StartText");

        tmpSuccessText ??= FindChildTmp("SuccessText");
        successText ??= FindChildLegacyText("SuccessText");

        if (levelIconBar == null)
        {
            var barObject = transform.Find("LevelIconBar");
            if (barObject != null)
                levelIconBar = barObject.GetComponent<StageLevelIconBar>();
        }

        levelIconBar ??= GetComponentInChildren<StageLevelIconBar>(true);
    }

    public void InitializeLevelIcons(StageData data)
    {
        levelIconBar?.Initialize(data);
    }

    public void ShowStartText()
    {
        SetText(tmpStartText, startText, "START!");
        SetActive(tmpStartText, startText, true);
        StopAllCoroutines();
        StartCoroutine(HideStartTextAfter(startTextDuration));
    }

    public void UpdateElapsedTime(float elapsedSeconds)
    {
        int t = Mathf.FloorToInt(Mathf.Max(0f, elapsedSeconds));
        int m = t / 60;
        int s = t % 60;
        SetText(tmpTimerText, timerText, $"{m:00}:{s:00}");
    }

    public void UpdateLevel(int displayLevel)
    {
        if (levelIconBar != null)
        {
            levelIconBar.SetIconCount(displayLevel);
            SetActive(tmpLevelText, levelText, false);
            return;
        }

        bool hasLevelUi = tmpLevelText != null || levelText != null;
        SetActive(tmpLevelText, levelText, hasLevelUi);
        if (!hasLevelUi) return;
        SetText(tmpLevelText, levelText, $"Lv. {displayLevel}");
    }

    public void SetKillProgressVisible(bool visible)
    {
        SetActive(tmpKillProgressText, killProgressText, visible);
    }

    public void UpdateKillProgress(int current, int target)
    {
        SetText(tmpKillProgressText, killProgressText, $"Kills {current}/{target}");
    }

    public void ShowSuccessText()
    {
        SetText(tmpSuccessText, successText, "MISSION\nSUCCESS!");
        SetActive(tmpSuccessText, successText, true);
    }

    public void ShowFailText()
    {
        SetText(tmpSuccessText, successText, "MISSION\nFAILED");
        SetActive(tmpSuccessText, successText, true);
    }

    private IEnumerator HideStartTextAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        SetActive(tmpStartText, startText, false);
    }

    private TMP_Text FindChildTmp(string objectName)
    {
        var tmps = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < tmps.Length; i++)
        {
            if (tmps[i] != null && tmps[i].name == objectName)
                return tmps[i];
        }
        return null;
    }

    private Text FindChildLegacyText(string objectName)
    {
        var texts = GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].name == objectName)
                return texts[i];
        }
        return null;
    }

    private static void SetText(TMP_Text tmp, Text legacy, string value)
    {
        if (tmp != null) tmp.text = value;
        else if (legacy != null) legacy.text = value;
    }

    private static void SetActive(TMP_Text tmp, Text legacy, bool active)
    {
        if (tmp != null) tmp.gameObject.SetActive(active);
        else if (legacy != null) legacy.gameObject.SetActive(active);
    }
}
