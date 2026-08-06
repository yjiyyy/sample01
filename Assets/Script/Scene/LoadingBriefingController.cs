using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Loading 씬 브리핑: 컷마다 이미지/구도/페이드/드리프트 재생 후 다음 씬으로 전환합니다.
/// 컷 사이 블렌드 없이 Fade Out → 교체 → Fade In 으로 분리합니다.
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

        [Tooltip("이 컷이 유지되는 총 시간(초). Fade In/Out 시간이 이 안에 포함됩니다.")]
        [Min(0f)]
        public float holdDuration = 2f;

        [Tooltip("Hold 시작 구간에 포함되는 Fade In 시간")]
        [Min(0f)]
        public float fadeInDuration = 0.35f;

        [Tooltip("Hold 끝 구간에 포함되는 Fade Out 시간")]
        [Min(0f)]
        public float fadeOutDuration = 0.35f;

        [Tooltip("이 컷의 페이드 색 (씬 종료도 같은 Fade 사용)")]
        public FadeColorType fadeColor = FadeColorType.Black;

        [TextArea]
        public string line;
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
    [SerializeField] private List<BriefingCut> cuts = new();

    // driftScaleSpeed(-1~1) 1당 초당 스케일 변화량
    private const float DriftScaleUnitsPerSecond = 0.35f;
    private const float MinDriftScale = 0.2f;
    private const float MaxDriftScale = 5f;

    private Coroutine playRoutine;

    public IReadOnlyList<BriefingCut> Cuts => cuts;
    public int CutCount => cuts != null ? cuts.Count : 0;

    // 에디터 Undo용
    public RectTransform BackgroundImageRect => backgroundImageRect;
    public Image BackgroundImage => backgroundImage;
    public TMP_Text BriefingText => briefingText;

    private void Awake()
    {
        ResolveUiRefs();
        // 첫 프레임부터 화면을 가려 두고, 첫 컷 Fade In으로 시작하게 한다.
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

        ApplyCutVisuals(cut);
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
            cut.line = briefingText.text;

        cuts[cutIndex] = cut;
    }

    /// <summary>
    /// Inspector 비어 있어도 Rect/CanvasGroup에서 Image를 찾아 채웁니다.
    /// 에디터 미리보기에서도 Awake 없이 동작하게 합니다.
    /// </summary>
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

        // 첫 Fade In이 로딩 히치에 먹히지 않도록, 화면을 가린 뒤 한 프레임 대기하고 나서 로드를 시작한다.
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
                BriefingCut cut = cuts[i];

                SetFadeColor(cut.fadeColor);
                SetFadeAlpha(1f);
                ApplyCutVisuals(cut);

                // 구도/스프라이트 적용 후 한 프레임 대기
                yield return null;

                Vector2 driftDir = Random.insideUnitCircle;
                if (driftDir.sqrMagnitude < 0.0001f)
                    driftDir = Vector2.right;
                else
                    driftDir.Normalize();

                // Hold 시간 안에 Fade In/Out을 포함. 페이드 중에도 드리프트한다.
                float hold = Mathf.Max(0f, cut.holdDuration);
                float fadeIn = Mathf.Max(0f, cut.fadeInDuration);
                float fadeOut = Mathf.Max(0f, cut.fadeOutDuration);
                if (fadeIn + fadeOut > hold && hold > 0f)
                {
                    float scale = hold / (fadeIn + fadeOut);
                    fadeIn *= scale;
                    fadeOut *= scale;
                }

                float elapsed = 0f;
                while (elapsed < hold)
                {
                    float dt = Mathf.Min(DeltaTime(), 1f / 30f);
                    elapsed += dt;
                    shownTime += dt;

                    if (backgroundImageRect != null)
                    {
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

                    float alpha;
                    if (fadeIn > 0f && elapsed < fadeIn)
                    {
                        // Hold 초반: Fade In (가림 → 보임) + 드리프트
                        alpha = 1f - Mathf.Clamp01(elapsed / fadeIn);
                    }
                    else if (fadeOut > 0f && elapsed > hold - fadeOut)
                    {
                        // Hold 후반: Fade Out (보임 → 가림) + 드리프트
                        float outT = Mathf.Clamp01((elapsed - (hold - fadeOut)) / fadeOut);
                        alpha = outT;
                    }
                    else
                    {
                        alpha = 0f;
                    }

                    SetFadeAlpha(alpha);
                    yield return null;
                }

                SetFadeAlpha(1f);
            }

            while (shownTime < minDisplaySeconds || loadOp.progress < 0.9f)
            {
                shownTime += DeltaTime();
                yield return null;
            }
        }

        // 마지막 컷 Fade Out 상태(가려진 상태)로 씬 전환
        SetFadeAlpha(1f);
        loadOp.allowSceneActivation = true;
        playRoutine = null;
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
            // 로딩 히치로 dt가 커져도 페이드가 한 프레임에 끝나지 않게 제한
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

    private void ApplyCutVisuals(BriefingCut cut)
    {
        if (backgroundImage != null && cut.sprite != null)
            backgroundImage.sprite = cut.sprite;

        if (backgroundImageRect != null)
        {
            backgroundImageRect.anchoredPosition = cut.targetAnchoredPosition;
            float scale = Mathf.Max(0.1f, cut.targetScale);
            backgroundImageRect.localScale = new Vector3(scale, scale, 1f);
        }

        if (briefingText != null)
            briefingText.text = cut.line ?? string.Empty;
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

        // 페이드 강도는 CanvasGroup.alpha만 사용한다.
        // Image.color.a가 낮으면 페이드가 거의 안 보이는 것처럼 나온다.
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
