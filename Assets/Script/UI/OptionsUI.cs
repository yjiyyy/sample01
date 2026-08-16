using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 옵션 패널. LanguageManager와 같은 루트에 두면 씬이 바뀌어도 유지됩니다.
/// 여러 씬에서 OptionsUI.Instance.Show() / Hide()로 열고 닫습니다.
/// </summary>
public class OptionsUI : MonoBehaviour
{
    public static OptionsUI Instance { get; private set; }

    [Header("패널")]
    [Tooltip("옵션 창 루트(보통 자식 Panel). 비우면 이 오브젝트를 사용합니다. 스크립트는 부모에 두는 것을 권장합니다.")]
    [SerializeField] private GameObject panelRoot;

    [Header("버튼 (Inspector에서 연결)")]
    [SerializeField] private Button koreanButton;
    [SerializeField] private Button englishButton;
    [SerializeField] private Button backButton;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // LanguageManager가 같은 오브젝트/루트에 있으면 그쪽 DontDestroyOnLoad로 함께 유지됩니다.
        // 단독 배치일 때만 여기서 유지합니다.
        if (GetComponent<LanguageManager>() == null && transform.root.GetComponent<LanguageManager>() == null)
            DontDestroyOnLoad(transform.root.gameObject);

        if (panelRoot == null)
            panelRoot = gameObject;

        WireButtons();
        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void WireButtons()
    {
        if (koreanButton != null)
        {
            koreanButton.onClick.RemoveAllListeners();
            koreanButton.onClick.AddListener(OnClickKorean);
        }

        if (englishButton != null)
        {
            englishButton.onClick.RemoveAllListeners();
            englishButton.onClick.AddListener(OnClickEnglish);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(Hide);
        }
    }

    public void Show()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);
    }

    public void Hide()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    public void Toggle()
    {
        if (panelRoot == null)
            return;

        if (panelRoot.activeSelf)
            Hide();
        else
            Show();
    }

    private void OnClickKorean()
    {
        if (LanguageManager.Instance != null)
            LanguageManager.Instance.SetKorean();
        else
            Debug.LogWarning("[OptionsUI] LanguageManager가 없습니다. PersistentSystems에 LanguageManager를 붙이세요.");
    }

    private void OnClickEnglish()
    {
        if (LanguageManager.Instance != null)
            LanguageManager.Instance.SetEnglish();
        else
            Debug.LogWarning("[OptionsUI] LanguageManager가 없습니다. PersistentSystems에 LanguageManager를 붙이세요.");
    }
}
