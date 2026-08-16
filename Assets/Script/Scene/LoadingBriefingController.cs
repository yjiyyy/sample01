using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Loading 씬 브리핑: 컷마다 이미지 연출 + (선택) 한 문장 타이핑 후 N초 대기, 다음 컷/씬으로 진행합니다.
/// 홀드 중 타이핑 속도 2배. 컷당 문장은 1개(LocalizedString).
/// </summary>
public class LoadingBriefingController : MonoBehaviour
{
    public enum FadeColorType
    {
        Black = 0,
        White = 1
    }

    [System.Serializable]
    public class BriefingCut
    {
        [Tooltip("이 컷에서 보여줄 이미지. 비우면 현재 이미지 유지")]
        public Sprite sprite;

        [Tooltip("시작 구도 위치 (anchoredPosition)")]
        public Vector2 targetAnchoredPosition;

        [Tooltip("시작 구도 배율 (1 = 원본)")]
        [Min(0.1f)]
        public float targetScale = 1f;

        [Tooltip("화면에 보이는 동안 정속 드리프트 속도 (픽셀/초). 0이면 정지")]
        [Min(0f)]
        public float driftSpeed = 20f;

        [Tooltip("스케일 드리프트 세기. 0=없음, 1=빠르게 커짐, -1=빠르게 작아짐")]
        [Range(-1f, 1f)]
        public float driftScaleSpeed = 0f;

        [Tooltip("문장 타이핑이 끝난 뒤(문장 없으면 컷 시작 직후) 다음 컷으로 가기 전 대기 시간(초)")]
        [FormerlySerializedAs("holdDuration")]
        [Min(0f)]
        public float postTextHoldSeconds = 2f;

        [Tooltip("컷 시작 시 Fade In 시간")]
        [Min(0f)]
        public float fadeInDuration = 0.35f;

        [Tooltip("컷 종료 시 Fade Out 시간")]
        [Min(0f)]
        public float fadeOutDuration = 0.35f;

        [Tooltip("이 컷의 페이드 색 (씬 종료도 같은 Fade 사용)")]
        public FadeColorType fadeColor = FadeColorType.Black;

        [Tooltip("이 컷의 문장(한/영). 둘 다 비우면 이미지 연출만")]
        public LocalizedString line;
    }

    [Header("Scene")]
    [SceneName]
    [SerializeField] private string targetSceneName = "Stage00";

    [Header("UI References")]
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private RectTransform backgroundImageRect;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TMP_Text briefingText;
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private Image fadeImage;

    [Header("Playback")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private float minDisplaySeconds = 4f;
    [Tooltip("초당 타이핑 글자 수. 홀드 중에는 2배")]
    [Min(1f)]
    [SerializeField] private float typewriterCharsPerSecond = 28f;
    [SerializeField] private List<BriefingCut> cuts = new();

    private const float DriftScaleUnitsPerSecond = 0.35f;
    private const float MinDriftScale = 0.2f;
    private const float MaxDriftScale = 5f;
    private const float HoldTypewriterMultiplier = 2f;

    private Coroutine playRoutine;

    public IReadOnlyList<BriefingCut> Cuts => cuts;
    public int CutCount => cuts != null ? cuts.Count : 0;

    public RectTransform BackgroundImageRect => backgroundImageRect;
    public Image BackgroundImage => backgroundImage;
    public TMP_Text BriefingText => briefingText;

    private void Awake()
    {
        ResolveUiRefs();
        PrepareInitialFadeCover();
    }

    private void Start()
    {
        if (playOnStart)
            Play();
    }

    public void Play()
    {
        if (playRoutine != null)
            StopCoroutine(playRoutine);

        playRoutine = StartCoroutine(CoPlayAndLoad());
    }

    /// <summary>에디터: 선택한 컷의 이미지/구도/대사를 씬 UI에 적용합니다.</summary>
    public void ApplyCutToImage(int cutIndex)
    {
        ResolveUiRefs();

        if (!TryGetCut(cutIndex, out BriefingCut cut))
            return;

        if (backgroundImage == null)
        {
            Debug.LogError(
                "[LoadingBriefingController] Background Image를 찾을 수 없습니다. " +
                "Image_Background의 Image를 Background Image에 연결하거나, Background Image Rect가 Image를 가진 오브젝트인지 확인하세요.",
                this);
            return;
        }

        ApplyCutVisuals(cut, showFullText: true);
        SetFadeColor(cut.fadeColor);
        SetFadeAlpha(0f);
    }

    /// <summary>에디터: 현재 이미지 상태를 선택한 컷에 저장합니다.</summary>
    public void CaptureImageToCut(int cutIndex)
    {
        ResolveUiRefs();

        if (!TryGetCut(cutIndex, out BriefingCut cut))
            return;

        if (backgroundImageRect != null)
        {
            cut.targetAnchoredPosition = backgroundImageRect.anchoredPosition;
            cut.targetScale = backgroundImageRect.localScale.x;
        }

        if (backgroundImage != null && backgroundImage.sprite != null)
            cut.sprite = backgroundImage.sprite;

        if (briefingText != null)
        {
            // 에디터에서 보이는 문구를 현재 언어 칸에 저장
            GameLanguage lang = LanguageManager.Instance != null
                ? LanguageManager.Instance.CurrentLanguage
                : GameLanguage.Korean;

            if (lang == GameLanguage.English)
                cut.line.english = briefingText.text ?? string.Empty;
            else
                cut.line.korean = briefingText.text ?? string.Empty;
        }

        cuts[cutIndex] = cut;
    }

    private void ResolveUiRefs()
    {
        if (backgroundImage == null && backgroundImageRect != null)
            backgroundImage = backgroundImageRect.GetComponent<Image>();

        if (fadeImage == null && fadeCanvasGroup != null)
            fadeImage = fadeCanvasGroup.GetComponent<Image>();
    }

    private void PrepareInitialFadeCover()
    {
        FadeColorType color = FadeColorType.Black;
        if (cuts != null && cuts.Count > 0)
            color = cuts[0].fadeColor;

        SetFadeColor(color);
        SetFadeAlpha(1f);
    }

    private IEnumerator CoPlayAndLoad()
    {
        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogError("[LoadingBriefingController] targetSceneName이 비어 있습니다.", this);
            yield break;
        }

        ResolveUiRefs();

        if (rootPanel != null)
            rootPanel.SetActive(true);

        PrepareInitialFadeCover();
        yield return null;

        AsyncOperation loadOp = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
        if (loadOp == null)
        {
            Debug.LogError($"[LoadingBriefingController] '{targetSceneName}' 씬 로드에 실패했습니다.", this);
            yield break;
        }

        loadOp.allowSceneActivation = false;

        float shownTime = 0f;

        if (cuts == null || cuts.Count == 0)
        {
            Debug.LogWarning("[LoadingBriefingController] cuts가 비어 있습니다. 최소 시간 후 페이드 아웃하고 전환합니다.", this);
            SetFadeColor(FadeColorType.Black);
            SetFadeAlpha(1f);
            yield return CoFade(1f, 0f, 0.35f, accumulateShown: true, shownAccumulator: t => shownTime += t);

            while (shownTime < minDisplaySeconds || loadOp.progress < 0.9f)
            {
                shownTime += DeltaTime();
                yield return null;
            }

            yield return CoFade(0f, 1f, 0.35f, accumulateShown: false);
        }
        else
        {
            for (int i = 0; i < cuts.Count; i++)
            {
                yield return CoPlayCut(cuts[i], t => shownTime += t);
            }

            while (shownTime < minDisplaySeconds || loadOp.progress < 0.9f)
            {
                shownTime += DeltaTime();
                yield return null;
            }
        }

        SetFadeAlpha(1f);
        loadOp.allowSceneActivation = true;
        playRoutine = null;
    }

    private IEnumerator CoPlayCut(BriefingCut cut, System.Action<float> addShownTime)
    {
        SetFadeColor(cut.fadeColor);
        SetFadeAlpha(1f);
        ApplyCutVisuals(cut, showFullText: false);
        if (briefingText != null)
            briefingText.text = string.Empty;

        yield return null;

        Vector2 driftDir = Random.insideUnitCircle;
        if (driftDir.sqrMagnitude < 0.0001f)
            driftDir = Vector2.right;
        else
            driftDir.Normalize();

        // 1) Fade In
        yield return CoTimedSegment(
            Mathf.Max(0f, cut.fadeInDuration),
            cut,
            driftDir,
            addShownTime,
            (elapsed, duration) =>
            {
                float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
                SetFadeAlpha(1f - t);
            });

        SetFadeAlpha(0f);

        // 2) Typewriter (문장 있을 때)
        string fullLine = GetCutLine(cut);
        if (!string.IsNullOrEmpty(fullLine))
            yield return CoTypewriter(fullLine, cut, driftDir, addShownTime);
        else if (briefingText != null)
            briefingText.text = string.Empty;

        // 3) 여유 N초
        yield return CoTimedSegment(
            Mathf.Max(0f, cut.postTextHoldSeconds),
            cut,
            driftDir,
            addShownTime,
            null);

        // 4) Fade Out
        yield return CoTimedSegment(
            Mathf.Max(0f, cut.fadeOutDuration),
            cut,
            driftDir,
            addShownTime,
            (elapsed, duration) =>
            {
                float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
                SetFadeAlpha(t);
            });

        SetFadeAlpha(1f);
    }

    private IEnumerator CoTypewriter(
        string fullLine,
        BriefingCut cut,
        Vector2 driftDir,
        System.Action<float> addShownTime)
    {
        if (briefingText == null)
            yield break;

        float charsPerSecond = Mathf.Max(1f, typewriterCharsPerSecond);
        float visible = 0f;
        int length = fullLine.Length;

        while (visible < length)
        {
            float dt = Mathf.Min(DeltaTime(), 1f / 30f);
            addShownTime?.Invoke(dt);
            ApplyDrift(cut, driftDir, dt);

            float speed = charsPerSecond;
            if (IsHoldPressed())
                speed *= HoldTypewriterMultiplier;

            visible += speed * dt;
            int count = Mathf.Clamp(Mathf.FloorToInt(visible), 0, length);
            briefingText.text = fullLine.Substring(0, count);
            yield return null;
        }

        briefingText.text = fullLine;
    }

    private IEnumerator CoTimedSegment(
        float duration,
        BriefingCut cut,
        Vector2 driftDir,
        System.Action<float> addShownTime,
        System.Action<float, float> onUpdate)
    {
        if (duration <= 0f)
        {
            onUpdate?.Invoke(0f, 0f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float dt = Mathf.Min(DeltaTime(), 1f / 30f);
            elapsed += dt;
            addShownTime?.Invoke(dt);
            ApplyDrift(cut, driftDir, dt);
            onUpdate?.Invoke(elapsed, duration);
            yield return null;
        }

        onUpdate?.Invoke(duration, duration);
    }

    private void ApplyDrift(BriefingCut cut, Vector2 driftDir, float dt)
    {
        if (backgroundImageRect == null)
            return;

        if (cut.driftSpeed > 0f)
            backgroundImageRect.anchoredPosition += driftDir * (cut.driftSpeed * dt);

        if (Mathf.Abs(cut.driftScaleSpeed) > 0.0001f)
        {
            float scale = backgroundImageRect.localScale.x;
            scale += cut.driftScaleSpeed * DriftScaleUnitsPerSecond * dt;
            scale = Mathf.Clamp(scale, MinDriftScale, MaxDriftScale);
            backgroundImageRect.localScale = new Vector3(scale, scale, 1f);
        }
    }

    private static string GetCutLine(BriefingCut cut)
    {
        GameLanguage lang = LanguageManager.Instance != null
            ? LanguageManager.Instance.CurrentLanguage
            : GameLanguage.Korean;
        return cut.line.Get(lang);
    }

    private static bool IsHoldPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            return true;
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            return true;
        if (Pointer.current != null && Pointer.current.press.isPressed)
            return true;
        return false;
#else
        if (Input.GetMouseButton(0))
            return true;
        return Input.touchCount > 0;
#endif
    }

    private IEnumerator CoFade(
        float fromAlpha,
        float toAlpha,
        float duration,
        bool accumulateShown,
        System.Action<float> shownAccumulator = null)
    {
        if (fadeCanvasGroup == null)
        {
            Debug.LogWarning("[LoadingBriefingController] Fade Canvas Group이 비어 있어 페이드를 건너뜁니다.", this);
            yield break;
        }

        if (duration <= 0f)
        {
            SetFadeAlpha(toAlpha);
            yield break;
        }

        float elapsed = 0f;
        SetFadeAlpha(fromAlpha);

        while (elapsed < duration)
        {
            float dt = Mathf.Min(DeltaTime(), 1f / 30f);
            elapsed += dt;
            if (accumulateShown)
                shownAccumulator?.Invoke(dt);

            float t = Mathf.Clamp01(elapsed / duration);
            SetFadeAlpha(Mathf.Lerp(fromAlpha, toAlpha, t));
            yield return null;
        }

        SetFadeAlpha(toAlpha);
    }

    private void ApplyCutVisuals(BriefingCut cut, bool showFullText)
    {
        if (backgroundImage != null && cut.sprite != null)
            backgroundImage.sprite = cut.sprite;

        if (backgroundImageRect != null)
        {
            backgroundImageRect.anchoredPosition = cut.targetAnchoredPosition;
            float scale = Mathf.Max(0.1f, cut.targetScale);
            backgroundImageRect.localScale = new Vector3(scale, scale, 1f);
        }

        if (briefingText == null)
            return;

        if (showFullText)
            briefingText.text = GetCutLine(cut);
        else
            briefingText.text = string.Empty;
    }

    private bool TryGetCut(int cutIndex, out BriefingCut cut)
    {
        cut = default;
        if (cuts == null || cutIndex < 0 || cutIndex >= cuts.Count)
        {
            Debug.LogWarning($"[LoadingBriefingController] 잘못된 컷 인덱스: {cutIndex}", this);
            return false;
        }

        cut = cuts[cutIndex];
        return true;
    }

    private void SetFadeColor(FadeColorType type)
    {
        if (fadeImage == null)
            return;

        float rgb = type == FadeColorType.White ? 1f : 0f;
        fadeImage.color = new Color(rgb, rgb, rgb, 1f);
    }

    private void SetFadeAlpha(float alpha)
    {
        if (fadeCanvasGroup != null)
            fadeCanvasGroup.alpha = Mathf.Clamp01(alpha);
    }

    private float DeltaTime()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }
}
