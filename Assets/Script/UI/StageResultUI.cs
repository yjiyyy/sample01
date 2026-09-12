using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 모든 전투 스테이지가 공유하는 승리/패배 결과 화면입니다.
/// 씬에 미리 배치하지 않아도 Resources 프리팹을 자동으로 생성해 사용합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class StageResultUI : MonoBehaviour
{
    private const string PrefabResourcePath = "UI/StageResult/StageResultOverlay";
    private const string BossArtResourcePath = "UI/StageResult/BossEagle";

    [Header("연출 시간 (실시간 기준)")]
    [SerializeField, Min(0f)] private float buttonDelay = 5f;
    [SerializeField, Min(0.01f)] private float bossEnterDuration = 0.55f;
    [SerializeField, Min(0.01f)] private float wordSlamDuration = 0.22f;
    [SerializeField, Min(0f)] private float wordInterval = 0.16f;

    [Header("에셋")]
    [SerializeField] private TMP_FontAsset displayFont;
    [SerializeField] private Sprite bossSprite;

    [Header("자동 생성 참조")]
    [SerializeField] private CanvasGroup screenGroup;
    [SerializeField] private CanvasGroup bossGroup;
    [SerializeField] private RectTransform bossRect;
    [SerializeField] private Image bossImage;
    [SerializeField] private TMP_Text[] wordTexts;
    [SerializeField] private CanvasGroup buttonGroup;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button confirmButton;

    private Coroutine presentationRoutine;
    private string retrySceneName;
    private bool sceneLoading;

    public static StageResultUI ShowResult(bool success)
    {
        StageResultUI instance = FindFirstObjectByType<StageResultUI>();
        if (instance == null)
        {
            GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
            if (prefab != null)
            {
                GameObject resultObject = Instantiate(prefab);
                instance = resultObject.GetComponent<StageResultUI>();
            }

            if (instance == null)
                instance = new GameObject(nameof(StageResultUI)).AddComponent<StageResultUI>();
        }

        instance.Present(success);
        return instance;
    }

    private void Awake()
    {
        if (screenGroup == null || wordTexts == null || wordTexts.Length < 3)
            RebuildVisuals(displayFont, bossSprite != null ? bossSprite : Resources.Load<Sprite>(BossArtResourcePath));

        EnsureEventSystem();
        WireButtons();
    }

    private void OnDestroy()
    {
        retryButton?.onClick.RemoveListener(RetryStage);
        confirmButton?.onClick.RemoveListener(ReturnToLobby);
    }

    public void Present(bool success)
    {
        retrySceneName = SceneManager.GetActiveScene().name;
        sceneLoading = false;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (presentationRoutine != null)
            StopCoroutine(presentationRoutine);

        presentationRoutine = StartCoroutine(PresentRoutine(success));
    }

    private IEnumerator PresentRoutine(bool success)
    {
        string[] words = success
            ? new[] { "YOU", "MADE EARTH", "GREAT AGAIN" }
            : new[] { "YOU", "ARE", "FIRED" };

        Color resultColor = success
            ? new Color(1f, 0.82f, 0.15f, 1f)
            : new Color(1f, 0.14f, 0.08f, 1f);

        screenGroup.alpha = 0f;
        screenGroup.interactable = true;
        screenGroup.blocksRaycasts = true;
        bossGroup.alpha = 0f;
        bossRect.localScale = Vector3.one * 0.82f;
        buttonGroup.alpha = 0f;
        buttonGroup.interactable = false;
        buttonGroup.blocksRaycasts = false;
        retryButton.gameObject.SetActive(!success);

        for (int i = 0; i < wordTexts.Length; i++)
        {
            wordTexts[i].text = words[i];
            wordTexts[i].color = i == 2 ? resultColor : Color.white;
            wordTexts[i].alpha = 0f;
            wordTexts[i].rectTransform.localScale = Vector3.one * 2.4f;
        }

        float startedAt = Time.realtimeSinceStartup;
        yield return AnimateScreenAndBoss();

        for (int i = 0; i < wordTexts.Length; i++)
        {
            yield return SlamWord(wordTexts[i]);
            if (wordInterval > 0f)
                yield return new WaitForSecondsRealtime(wordInterval);
        }

        float remaining = buttonDelay - (Time.realtimeSinceStartup - startedAt);
        if (remaining > 0f)
            yield return new WaitForSecondsRealtime(remaining);

        yield return RevealButtons();
        presentationRoutine = null;
    }

    private IEnumerator AnimateScreenAndBoss()
    {
        float elapsed = 0f;
        while (elapsed < bossEnterDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / bossEnterDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            screenGroup.alpha = Mathf.Clamp01(t * 2.5f);
            bossGroup.alpha = eased;
            bossRect.localScale = Vector3.one * Mathf.LerpUnclamped(0.82f, 1f, BackOut(eased));
            yield return null;
        }

        screenGroup.alpha = 1f;
        bossGroup.alpha = 1f;
        bossRect.localScale = Vector3.one;
    }

    private IEnumerator SlamWord(TMP_Text text)
    {
        float elapsed = 0f;
        RectTransform rect = text.rectTransform;
        while (elapsed < wordSlamDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / wordSlamDuration);
            text.alpha = Mathf.Clamp01(t * 3f);
            rect.localScale = Vector3.one * Mathf.LerpUnclamped(2.4f, 1f, BackOut(t));
            yield return null;
        }

        text.alpha = 1f;
        rect.localScale = Vector3.one;
    }

    private IEnumerator RevealButtons()
    {
        buttonGroup.interactable = true;
        buttonGroup.blocksRaycasts = true;
        float elapsed = 0f;
        const float duration = 0.25f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            buttonGroup.alpha = t;
            buttonGroup.transform.localScale = Vector3.one * Mathf.Lerp(0.86f, 1f, BackOut(t));
            yield return null;
        }

        buttonGroup.alpha = 1f;
        buttonGroup.transform.localScale = Vector3.one;
    }

    private void WireButtons()
    {
        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(RetryStage);
            retryButton.onClick.AddListener(RetryStage);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(ReturnToLobby);
            confirmButton.onClick.AddListener(ReturnToLobby);
        }
    }

    private void RetryStage()
    {
        LoadScene(retrySceneName);
    }

    private void ReturnToLobby()
    {
        LoadScene(SceneNames.Lobby);
    }

    private void LoadScene(string sceneName)
    {
        if (sceneLoading)
            return;

        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[StageResultUI] '{sceneName}' 씬을 불러올 수 없습니다. Build Profiles의 Scene List를 확인하세요.", this);
            return;
        }

        sceneLoading = true;
        buttonGroup.interactable = false;
        InputManager.SetPlayerDeathBlock(false);
        Time.timeScale = 1f;
        SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
    }

    private static float BackOut(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float x = t - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        eventSystem.AddComponent<InputSystemUIInputModule>();
#else
        eventSystem.AddComponent<StandaloneInputModule>();
#endif
    }

    /// <summary>에디터 프리팹 생성과 런타임 비상 생성을 함께 사용합니다.</summary>
    public void RebuildVisuals(TMP_FontAsset font, Sprite artSprite)
    {
        displayFont = font != null ? font : TMP_Settings.defaultFontAsset;
        bossSprite = artSprite;

        RemoveGeneratedChildren();
        Canvas canvas = GetOrAdd<Canvas>(gameObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = GetOrAdd<CanvasScaler>(gameObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        GetOrAdd<GraphicRaycaster>(gameObject);
        screenGroup = GetOrAdd<CanvasGroup>(gameObject);

        RectTransform dim = CreateRect("Dim", transform, Vector2.zero, Vector2.one);
        Image dimImage = dim.gameObject.AddComponent<Image>();
        dimImage.color = new Color(0.015f, 0.02f, 0.035f, 0.9f);

        RectTransform glow = CreateRect("BossGlow", transform, new Vector2(0f, 0f), new Vector2(0.72f, 1f));
        Image glowImage = glow.gameObject.AddComponent<Image>();
        glowImage.color = new Color(0.28f, 0.12f, 0.015f, 0.35f);

        bossRect = CreateRect("BossArt", transform, new Vector2(0.015f, 0.08f), new Vector2(0.70f, 0.97f));
        bossImage = bossRect.gameObject.AddComponent<Image>();
        bossImage.preserveAspect = true;
        bossImage.raycastTarget = false;
        bossImage.sprite = bossSprite;
        bossGroup = bossRect.gameObject.AddComponent<CanvasGroup>();

        RectTransform wordsRoot = CreateRect("Words", transform, new Vector2(0.48f, 0.18f), new Vector2(0.99f, 0.94f));
        wordTexts = new TMP_Text[3];
        wordTexts[0] = CreateWord("Word01", wordsRoot, new Vector2(0f, 0.67f), new Vector2(0.88f, 1f), 150f);
        wordTexts[1] = CreateWord("Word02", wordsRoot, new Vector2(0.10f, 0.36f), new Vector2(1f, 0.70f), 125f);
        wordTexts[2] = CreateWord("Word03", wordsRoot, new Vector2(0f, 0f), new Vector2(1f, 0.43f), 158f);

        RectTransform buttons = CreateRect("Buttons", transform, new Vector2(0.53f, 0.045f), new Vector2(0.97f, 0.16f));
        HorizontalLayoutGroup layout = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 28f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        buttonGroup = buttons.gameObject.AddComponent<CanvasGroup>();
        retryButton = CreateButton("RetryButton", buttons, "재도전", new Color(0.72f, 0.06f, 0.035f, 0.96f));
        confirmButton = CreateButton("ConfirmButton", buttons, "확인", new Color(0.09f, 0.20f, 0.34f, 0.96f));
        WireButtons();
    }

    private TMP_Text CreateWord(string name, Transform parent, Vector2 min, Vector2 max, float size)
    {
        RectTransform rect = CreateRect(name, parent, min, max);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = displayFont;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 42f;
        text.fontSizeMax = size;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        Shadow shadow = rect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = new Vector2(9f, -9f);
        return text;
    }

    private Button CreateButton(string name, Transform parent, string label, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        Button button = go.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        button.colors = colors;

        RectTransform labelRect = CreateRect("Label", go.transform, Vector2.zero, Vector2.one);
        labelRect.offsetMin = new Vector2(12f, 6f);
        labelRect.offsetMax = new Vector2(-12f, -6f);
        TextMeshProUGUI text = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = displayFont;
        text.text = label;
        text.fontSize = 48f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return button;
    }

    private void RemoveGeneratedChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

}
