using System.Collections;
using UnityEngine;

/// <summary>
/// 타이틀 인트로: 검은 화면 Fade In → 로고 확대 → 메뉴 On.
/// 위치·크기는 씬의 RectTransform에서, 시간·배율은 Inspector 필드로 조절합니다.
/// </summary>
public class TitleIntroController : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private RectTransform logoRect;
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private CanvasGroup fadeCanvasGroup;

    [Header("재생")]
    [SerializeField] private bool playOnStart = true;
    [Tooltip("체크하면 일시정지와 관계없이 같은 속도로 재생됩니다.")]
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Fade In (검은 화면이 걷힘)")]
    [Tooltip("씬이 켜진 뒤, 페이드를 시작하기 전 검은 화면을 유지하는 시간(초)")]
    [Min(0f)]
    [SerializeField] private float blackHoldDuration = 0.4f;

    [Tooltip("검은 화면이 투명해지는 시간(초)")]
    [Min(0f)]
    [SerializeField] private float fadeInDuration = 1.5f;

    [Header("로고 확대")]
    [Tooltip("페이드가 끝난 뒤, 로고 확대를 시작하기까지 기다리는 시간(초)")]
    [Min(0f)]
    [SerializeField] private float logoDelayAfterFade = 0.15f;

    [Tooltip("로고가 커지기 시작할 때의 크기. 0이면 안 보이다가 커집니다.")]
    [Min(0f)]
    [SerializeField] private float logoStartScale = 0f;

    [Tooltip("로고가 커진 뒤 멈출 크기. 보통 1입니다.")]
    [Min(0f)]
    [SerializeField] private float logoEndScale = 1f;

    [Tooltip("로고가 시작 크기에서 끝 크기까지 커지는 시간(초)")]
    [Min(0f)]
    [SerializeField] private float logoScaleDuration = 0.7f;

    [Tooltip("로고가 커지는 속도 곡선. 가로=진행(0~1), 세로=적용 비율(0~1)")]
    [SerializeField] private AnimationCurve logoScaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("메뉴")]
    [Tooltip("로고 확대가 끝난 뒤, 메뉴가 켜질 때까지 기다리는 시간(초). 0이면 로고가 끝나자마자 켭니다.")]
    [Min(0f)]
    [SerializeField] private float menuDelayAfterLogo = 0.4f;

    private Coroutine playRoutine;

    private void Awake()
    {
        PrepareInitialState();
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

        playRoutine = StartCoroutine(CoPlay());
    }

    private void PrepareInitialState()
    {
        SetLogoScale(logoStartScale);
        SetMenuVisible(false);
        SetFadeAlpha(1f);
        SetFadeBlocksRaycasts(true);
    }

    private IEnumerator CoPlay()
    {
        PrepareInitialState();
        yield return null;

        yield return CoWait(blackHoldDuration);
        yield return CoFade(1f, 0f, fadeInDuration);
        SetFadeBlocksRaycasts(false);

        yield return CoWait(logoDelayAfterFade);
        yield return CoScaleLogo(logoStartScale, logoEndScale, logoScaleDuration);

        yield return CoWait(menuDelayAfterLogo);
        SetMenuVisible(true);

        playRoutine = null;
    }

    private IEnumerator CoWait(float duration)
    {
        if (duration <= 0f)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += DeltaTime();
            yield return null;
        }
    }

    private IEnumerator CoFade(float fromAlpha, float toAlpha, float duration)
    {
        if (fadeCanvasGroup == null)
        {
            Debug.LogWarning("[TitleIntroController] Fade Canvas Group이 없어 페이드를 건너뜁니다.", this);
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
            elapsed += DeltaTime();
            float t = Mathf.Clamp01(elapsed / duration);
            SetFadeAlpha(Mathf.Lerp(fromAlpha, toAlpha, t));
            yield return null;
        }

        SetFadeAlpha(toAlpha);
    }

    private IEnumerator CoScaleLogo(float fromScale, float toScale, float duration)
    {
        if (logoRect == null)
        {
            Debug.LogWarning("[TitleIntroController] 로고 RectTransform이 없어 확대를 건너뜁니다.", this);
            yield break;
        }

        if (duration <= 0f)
        {
            SetLogoScale(toScale);
            yield break;
        }

        float elapsed = 0f;
        SetLogoScale(fromScale);

        while (elapsed < duration)
        {
            elapsed += DeltaTime();
            float t = Mathf.Clamp01(elapsed / duration);
            float curved = logoScaleCurve != null ? logoScaleCurve.Evaluate(t) : t;
            SetLogoScale(Mathf.LerpUnclamped(fromScale, toScale, curved));
            yield return null;
        }

        SetLogoScale(toScale);
    }

    private void SetLogoScale(float scale)
    {
        if (logoRect == null)
            return;

        logoRect.localScale = new Vector3(scale, scale, 1f);
    }

    private void SetMenuVisible(bool visible)
    {
        if (menuRoot != null)
            menuRoot.SetActive(visible);
    }

    private void SetFadeAlpha(float alpha)
    {
        if (fadeCanvasGroup != null)
            fadeCanvasGroup.alpha = Mathf.Clamp01(alpha);
    }

    private void SetFadeBlocksRaycasts(bool blocks)
    {
        if (fadeCanvasGroup == null)
            return;

        fadeCanvasGroup.blocksRaycasts = blocks;
        fadeCanvasGroup.interactable = blocks;
    }

    private float DeltaTime()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        return Mathf.Min(dt, 1f / 30f);
    }
}
