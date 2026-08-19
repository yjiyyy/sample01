using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Inspector에 한/영 문구를 직접 넣고, 현재 언어에 맞는 쪽만 표시합니다.
/// 자막처럼 긴 글도 TextArea로 입력할 수 있습니다.
/// </summary>
[DisallowMultipleComponent]
public class LocalizedText : MonoBehaviour
{
    [SerializeField] private LocalizedString texts;

    [Header("에디터 미리보기")]
    [Tooltip("Play 중이 아닐 때 Inspector/Scene에서 어떤 언어로 미리 볼지")]
    [SerializeField] private GameLanguage editorPreviewLanguage = GameLanguage.Korean;

    private Text _uiText;
    private TextMeshProUGUI _tmpText;

    private void Awake()
    {
        CacheRefs();
    }

    private void OnEnable()
    {
        LanguageManager.LanguageChanged += Apply;
        Apply();
    }

    private void OnDisable()
    {
        LanguageManager.LanguageChanged -= Apply;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheRefs();
        if (!Application.isPlaying)
            SetVisibleText(texts.Get(editorPreviewLanguage));
    }
#endif

    public void Apply()
    {
        CacheRefs();

        GameLanguage language = LanguageManager.Instance != null
            ? LanguageManager.Instance.CurrentLanguage
            : GameLanguage.Korean;

        SetVisibleText(texts.Get(language));
    }

    public void SetTexts(string korean, string english)
    {
        texts.korean = korean ?? string.Empty;
        texts.english = english ?? string.Empty;
        Apply();
    }

    private void CacheRefs()
    {
        if (_uiText == null)
            _uiText = GetComponent<Text>();
        if (_tmpText == null)
            _tmpText = GetComponent<TextMeshProUGUI>();
    }

    private void SetVisibleText(string value)
    {
        // 임시 글자 없이, 해당 언어 칸 내용만 출력(비어 있으면 빈 문자열)
        value ??= string.Empty;

        if (_tmpText != null)
            _tmpText.text = value;
        if (_uiText != null)
            _uiText.text = value;
    }
}
