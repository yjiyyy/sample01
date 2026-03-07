using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 이미지+텍스트 단위. 한 이미지당 여러 텍스트를 순서대로 표시합니다.
/// 번역 적용 시 string을 LocalizationKey 등으로 교체 가능.
/// </summary>
[Serializable]
public class StoryPage
{
    [Tooltip("표시할 이미지")]
    public Sprite image;

    [Tooltip("이 이미지에서 순서대로 표시할 텍스트. 클릭 시 다음 텍스트로 교체, 마지막 후 다음 페이지")]
    public string[] texts = Array.Empty<string>();
}

/// <summary>
/// 스토리 시퀀스. 풀스크린 이미지와 하단 텍스트를 표시하고,
/// 클릭 시: 텍스트 다음 → 페이지의 텍스트 모두 끝나면 다음 이미지 → 마지막 페이지 마지막 텍스트 후 다음 씬.
/// storyPages를 사용하며, 비어 있으면 기존 storyImages 방식으로 동작(이미지당 클릭으로 바로 다음 이미지).
/// </summary>
[RequireComponent(typeof(Image))]
public class StorySequenceController : MonoBehaviour
{
    [Header("스토리 페이지 (이미지 + 텍스트)")]
    [Tooltip("각 페이지: 이미지 1장 + 여러 텍스트. Inspector에서 등록한 순서대로 표시됩니다.")]
    public StoryPage[] storyPages;

    [Header("하단 텍스트 박스")]
    [Tooltip("스토리 텍스트를 표시할 UI Text. 지정하지 않으면 텍스트 표시 안 함.")]
    public Text storyText;

    [Header("씬 전환")]
    [Tooltip("마지막 페이지 마지막 텍스트 후 이동할 씬.")]
    [SceneName]
    [SerializeField] private string nextScene = "02_CharacterSelectionLevel";

    [Header("입력")]
    [Tooltip("넘길 때 받을 키 (Space, Return 등)")]
    public KeyCode advanceKey = KeyCode.Space;

    [Header("레거시 (storyPages 비어 있을 때만 사용)")]
    [Tooltip("storyPages가 비어 있으면 이 배열로 이미지만 순차 표시")]
    public Sprite[] storyImages;

    private Image _image;
    private int _pageIndex;
    private int _textIndex;
    private bool _useLegacyMode;

    private void Awake()
    {
        _image = GetComponent<Image>();
        if (_image != null)
            _image.preserveAspect = true;
    }

    private void Start()
    {
        _useLegacyMode = storyPages == null || storyPages.Length == 0;

        if (_useLegacyMode)
        {
            if (storyImages == null || storyImages.Length == 0)
            {
                Debug.LogWarning("[StorySequenceController] storyPages와 storyImages가 모두 비어 있습니다. nextScene으로 이동합니다.");
                LoadNextScene();
                return;
            }
            _pageIndex = 0;
            ShowLegacyImage(0);
            if (storyText != null) storyText.text = "";
            return;
        }

        _pageIndex = 0;
        _textIndex = 0;
        ShowPage(0, 0);
    }

    private void Update()
    {
        bool advance = GetKeyDown(advanceKey) ||
                       GetKeyDown(KeyCode.Return) ||
                       GetKeyDown(KeyCode.KeypadEnter);

        if (advance)
            OnAdvance();
    }

    /// <summary>
    /// 터치/클릭으로 넘기기 (버튼 등에서 호출)
    /// </summary>
    public void OnAdvance()
    {
        if (_useLegacyMode)
        {
            if (storyImages == null || storyImages.Length == 0) return;
            if (_pageIndex + 1 < storyImages.Length)
            {
                _pageIndex++;
                ShowLegacyImage(_pageIndex);
            }
            else
                LoadNextScene();
            return;
        }

        if (storyPages == null || storyPages.Length == 0) return;

        var page = storyPages[_pageIndex];
        var texts = page.texts;
        bool hasTexts = texts != null && texts.Length > 0;

        if (hasTexts && _textIndex + 1 < texts.Length)
        {
            _textIndex++;
            ShowText(texts[_textIndex]);
        }
        else if (_pageIndex + 1 < storyPages.Length)
        {
            _pageIndex++;
            _textIndex = 0;
            ShowPage(_pageIndex, 0);
        }
        else
        {
            LoadNextScene();
        }
    }

    private void ShowPage(int pageIdx, int textIdx)
    {
        _pageIndex = pageIdx;
        _textIndex = textIdx;

        var page = storyPages[pageIdx];
        if (_image != null && page.image != null)
        {
            _image.sprite = page.image;
            _image.enabled = true;
        }

        if (page.texts != null && page.texts.Length > 0 && textIdx < page.texts.Length)
            ShowText(page.texts[textIdx]);
        else if (storyText != null)
            storyText.text = "";
    }

    private void ShowText(string text)
    {
        if (storyText != null)
            storyText.text = text ?? "";
    }

    private void ShowLegacyImage(int index)
    {
        _pageIndex = index;
        if (_image != null && storyImages != null && index >= 0 && index < storyImages.Length && storyImages[index] != null)
        {
            _image.sprite = storyImages[index];
            _image.enabled = true;
        }
    }

    private void LoadNextScene()
    {
        if (!string.IsNullOrEmpty(nextScene))
            SceneManager.LoadScene(nextScene);
        else
            Debug.LogWarning("[StorySequenceController] nextScene이 지정되지 않았습니다.");
    }

    private static bool GetKeyDown(KeyCode kc)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;
        switch (kc)
        {
            case KeyCode.Space: return kb.spaceKey != null && kb.spaceKey.wasPressedThisFrame;
            case KeyCode.Return:
            case KeyCode.KeypadEnter: return kb.enterKey != null && kb.enterKey.wasPressedThisFrame;
            case KeyCode.E: return kb.eKey != null && kb.eKey.wasPressedThisFrame;
            case KeyCode.F: return kb.fKey != null && kb.fKey.wasPressedThisFrame;
            case KeyCode.Z: return kb.zKey != null && kb.zKey.wasPressedThisFrame;
            case KeyCode.X: return kb.xKey != null && kb.xKey.wasPressedThisFrame;
            case KeyCode.Escape: return kb.escapeKey != null && kb.escapeKey.wasPressedThisFrame;
            default: return false;
        }
#else
        return UnityEngine.Input.GetKeyDown(kc);
#endif
    }
}
