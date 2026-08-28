using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 화면 중앙 안내 토스트. 비주얼은 Prefab(UI_PlayerToast)에서 편집합니다.
/// </summary>
public class PlayerToastUI : MonoBehaviour
{
    public const string PrefabEditorPath = "Assets/Arts/UI/Popup/UI_PlayerToast.prefab";
    public const string ResourcesAssetPath = "UI/PlayerToastRefs";

    public static PlayerToastUI Instance { get; private set; }

    [Header("References (Prefab에서 연결)")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI label;

    [Header("Timing")]
    [SerializeField] private float displaySeconds = 2.2f;
    [SerializeField] private float fadeSeconds = 0.35f;

    [Header("Messages")]
    [Tooltip("근력 부족 시 문구. {0}=무게 합, {1}=근력(STR)")]
    [SerializeField] private LocalizedString insufficientStrengthMessage = new LocalizedString
    {
        korean = "근력이 부족합니다. (무게 {0} / 근력 {1})",
        english = "Not enough Strength. (Weight {0} / STR {1})"
    };

    private Coroutine hideRoutine;

    public static void Show(string korean, string english = null)
    {
        if (!EnsureInstance())
            return;

        string text = ResolveText(korean, english);
        if (string.IsNullOrEmpty(text))
            return;

        Instance.ShowInternal(text);
    }

    public static void ShowLocalized(LocalizedString message)
    {
        Show(message.korean, message.english);
    }

    /// <summary>근력 부족 토스트. Prefab Inspector의 Insufficient Strength Message를 사용합니다.</summary>
    public static void ShowInsufficientStrength(float totalWeight, float strength)
    {
        if (!EnsureInstance())
            return;

        Instance.ShowInsufficientStrengthInternal(totalWeight, strength);
    }

    private void ShowInsufficientStrengthInternal(float totalWeight, float strength)
    {
        string weightText = FormatStat(totalWeight);
        string strengthText = FormatStat(strength);

        string koreanTemplate = string.IsNullOrEmpty(insufficientStrengthMessage.korean)
            ? "근력이 부족합니다. (무게 {0} / 근력 {1})"
            : insufficientStrengthMessage.korean;
        string englishTemplate = string.IsNullOrEmpty(insufficientStrengthMessage.english)
            ? "Not enough Strength. (Weight {0} / STR {1})"
            : insufficientStrengthMessage.english;

        string text = ResolveText(
            string.Format(koreanTemplate, weightText, strengthText),
            string.Format(englishTemplate, weightText, strengthText));

        if (!string.IsNullOrEmpty(text))
            ShowInternal(text);
    }

    private static string FormatStat(float value)
    {
        if (Mathf.Approximately(value, Mathf.Round(value)))
            return Mathf.RoundToInt(value).ToString();
        return value.ToString("0.#");
    }

    private static string ResolveText(string korean, string english)
    {
        GameLanguage lang = LanguageManager.Instance != null
            ? LanguageManager.Instance.CurrentLanguage
            : GameLanguage.Korean;

        if (lang == GameLanguage.English)
        {
            if (!string.IsNullOrEmpty(english))
                return english;
            return korean ?? string.Empty;
        }

        if (!string.IsNullOrEmpty(korean))
            return korean;
        return english ?? string.Empty;
    }

    /// <summary>씬에 있으면 그걸 쓰고, 없으면 Prefab을 생성합니다.</summary>
    private static bool EnsureInstance()
    {
        if (Instance != null)
            return true;

        var existing = Object.FindFirstObjectByType<PlayerToastUI>();
        if (existing != null)
        {
            Instance = existing;
            return true;
        }

        GameObject prefab = LoadPrefab();
        if (prefab == null)
        {
            Debug.LogWarning(
                "[PlayerToastUI] UI_PlayerToast 프리팹을 찾지 못했습니다. " +
                "Tools → UI → Create Player Toast Prefab 을 실행하세요.");
            return false;
        }

        var root = new GameObject("PlayerToastRoot");
        Object.DontDestroyOnLoad(root);
        var instance = Object.Instantiate(prefab, root.transform, false);
        instance.name = "UI_PlayerToast";
        instance.SetActive(true);

        Instance = instance.GetComponent<PlayerToastUI>();
        if (Instance == null)
        {
            Debug.LogWarning("[PlayerToastUI] 프리팹에 PlayerToastUI 컴포넌트가 없습니다.");
            Object.Destroy(root);
            return false;
        }

        Instance.HideImmediate();
        return true;
    }

    private static GameObject LoadPrefab()
    {
        var refs = Resources.Load<PlayerToastRefs>(ResourcesAssetPath);
        if (refs != null && refs.toastPrefab != null)
            return refs.toastPrefab;

#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabEditorPath);
#else
        return null;
#endif
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (canvasGroup == null)
            canvasGroup = GetComponentInChildren<CanvasGroup>(true);
        if (label == null)
            label = GetComponentInChildren<TextMeshProUGUI>(true);

        HideImmediate();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void ShowInternal(string text)
    {
        if (label != null)
            label.text = text;

        if (hideRoutine != null)
            StopCoroutine(hideRoutine);
        hideRoutine = StartCoroutine(ShowRoutine());
    }

    private void HideImmediate()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    private IEnumerator ShowRoutine()
    {
        if (canvasGroup == null)
            yield break;

        canvasGroup.alpha = 1f;
        float hold = Mathf.Max(0.1f, displaySeconds);
        yield return new WaitForSecondsRealtime(hold);

        float fade = Mathf.Max(0.05f, fadeSeconds);
        float t = 0f;
        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, t / fade);
            yield return null;
        }

        HideImmediate();
        hideRoutine = null;
    }
}
