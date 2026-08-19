using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 상단 등에 돈/젬 아이콘과 숫자 표시. <see cref="PlayerResources"/> 이벤트로 갱신.
/// </summary>
/// <remarks>
/// 아이콘 위치·크기는 Hierarchy의 Icon_Coin / Icon_Gem에서 직접 조정한 뒤 씬을 저장하세요.
/// 씬에 UI 자동 생성: <b>GameObject → UI → Resource HUD (Money/Gem)</b> 또는
/// <b>Tools → UI → Create Resource HUD (Money/Gem)</b>
/// </remarks>
[ExecuteAlways]
[DisallowMultipleComponent]
public class ResourceHUD : MonoBehaviour
{
    private const float DefaultIconSize = 48f;

    [Header("UI (숫자)")]
    [SerializeField] private Text moneyText;
    [FormerlySerializedAs("jamText")]
    [SerializeField] private Text gemText;

    [Header("아이콘")]
    [Tooltip("비어 있으면 MoneyText 아래에 Icon_Coin을 만듭니다. 위치·크기는 이 오브젝트를 골라 조정하세요.")]
    [SerializeField] private Image moneyIcon;
    [Tooltip("비어 있으면 GemText 아래에 Icon_Gem을 만듭니다. 위치·크기는 이 오브젝트를 골라 조정하세요.")]
    [SerializeField] private Image gemIcon;

    [Tooltip("비우면 씬에서 PlayerResources 검색")]
    [SerializeField] private PlayerResources resources;

    private void OnEnable()
    {
        EnsureIcons();
        RefreshDisplay();
    }

    private void OnDisable()
    {
        if (Application.isPlaying && resources != null)
            resources.OnResourcesChanged -= OnResourcesChanged;
    }

    private void Start()
    {
        if (!Application.isPlaying)
            return;

        if (resources == null)
            resources = PlayerResources.Instance != null
                ? PlayerResources.Instance
                : Object.FindFirstObjectByType<PlayerResources>();

        if (resources != null)
        {
            resources.OnResourcesChanged -= OnResourcesChanged;
            resources.OnResourcesChanged += OnResourcesChanged;
            Refresh(resources.Money, resources.Gem);
        }
        else
            RefreshDisplay();
    }

    private void OnResourcesChanged(int money, int gem)
    {
        Refresh(money, gem);
    }

    /// <summary>
    /// 에디터에서는 0, 플레이 중에는 현재 보유량으로 숫자만 바꿉니다. 아이콘 위치는 건드리지 않습니다.
    /// </summary>
    private void RefreshDisplay()
    {
        int m = 0;
        int g = 0;

        if (Application.isPlaying)
        {
            var res = resources != null
                ? resources
                : (PlayerResources.Instance != null
                    ? PlayerResources.Instance
                    : Object.FindFirstObjectByType<PlayerResources>());
            if (res != null)
            {
                m = res.Money;
                g = res.Gem;
            }
        }

        Refresh(m, g);
    }

    public void EnsureIcons()
    {
        if (moneyIcon == null && moneyText != null)
            moneyIcon = HudResourceIcons.GetOrCreateIcon(
                moneyText, HudResourceIcons.Coin, HudResourceIcons.CoinChildName, DefaultIconSize);

        if (gemIcon == null && gemText != null)
            gemIcon = HudResourceIcons.GetOrCreateIcon(
                gemText, HudResourceIcons.Gem, HudResourceIcons.GemChildName, DefaultIconSize);
    }

    private void Refresh(int money, int gem)
    {
        if (moneyText != null)
            moneyText.text = money.ToString();
        if (gemText != null)
            gemText.text = gem.ToString();
    }
}
