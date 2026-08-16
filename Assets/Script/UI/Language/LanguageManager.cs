using System;
using UnityEngine;

/// <summary>
/// 표시 언어를 저장·불러오고, 변경 시 UI에 알립니다.
/// 문구 내용은 LocalizedText / LocalizedString에 두고, 여기서는 언어 상태만 관리합니다.
/// </summary>
public class LanguageManager : MonoBehaviour
{
    public const string PlayerPrefsKey = "GameLanguage";

    public static LanguageManager Instance { get; private set; }

    [Header("시작 언어 (저장된 값이 없을 때만)")]
    [SerializeField] private GameLanguage defaultLanguage = GameLanguage.Korean;

    public GameLanguage CurrentLanguage { get; private set; }

    /// <summary>언어가 바뀌거나 최초 로드될 때 호출됩니다.</summary>
    public static event Action LanguageChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadLanguage();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        // 씬에 늦게 켜진 LocalizedText도 받을 수 있게 한 번 더 알림
        LanguageChanged?.Invoke();
    }

    public void SetLanguage(GameLanguage language)
    {
        if (CurrentLanguage == language)
        {
            LanguageChanged?.Invoke();
            return;
        }

        CurrentLanguage = language;
        PlayerPrefs.SetInt(PlayerPrefsKey, (int)language);
        PlayerPrefs.Save();
        LanguageChanged?.Invoke();
    }

    public void SetKorean() => SetLanguage(GameLanguage.Korean);

    public void SetEnglish() => SetLanguage(GameLanguage.English);

    private void LoadLanguage()
    {
        if (PlayerPrefs.HasKey(PlayerPrefsKey))
            CurrentLanguage = (GameLanguage)PlayerPrefs.GetInt(PlayerPrefsKey, (int)defaultLanguage);
        else
            CurrentLanguage = defaultLanguage;
    }
}
