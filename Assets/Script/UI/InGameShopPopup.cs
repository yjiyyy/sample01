using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인게임 상점 팝업. 카드 3장 선택 + 구매/리롤/닫기 UI를 담당합니다.
/// 카탈로그가 있으면 등록된 업그레이드 중 3장을 보여 줍니다.
/// 에디터(대기)에서는 목록 맨 위 3장, 플레이 중에는 무작위입니다.
/// 실제 구매·장착은 <see cref="InGameShopOpener"/>가 처리합니다.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class InGameShopPopup : MonoBehaviour
{
    public const int CardCount = 3;
    public const string DefaultCatalogPath = "Assets/Data/Shop/InGameShopCatalog.asset";

    [Serializable]
    public class PreviewCard
    {
        public string itemName = "아이템";
        [TextArea] public string description = "설명";
        public Sprite icon;
        public int price = 10;
        public bool useGem;
    }

    [Header("카드 3장 (가로)")]
    [SerializeField] private InGameShopCardView[] cards = new InGameShopCardView[CardCount];

    [Header("버튼")]
    [SerializeField] private Button purchaseButton;
    [SerializeField] private Button rerollButton;
    [SerializeField] private Button closeButton;

    [Header("상점 목록")]
    [Tooltip("비어 있으면 기본 경로의 InGameShopCatalog를 찾습니다.")]
    [SerializeField] private InGameShopCatalogSO catalog;

    [Tooltip("켜면 닫기 시 팝업을 숨깁니다. 디자인 씬에서는 꺼 두는 것이 편합니다.")]
    [SerializeField] private bool hideOnClose;

    [Header("카탈로그가 없을 때만 쓰는 가짜 미리보기")]
    [SerializeField] private PreviewCard[] previewCards = new PreviewCard[CardCount];

    [SerializeField] private int selectedIndex = -1;

    private readonly InGameShopOffer[] currentOffers = new InGameShopOffer[CardCount];
    private int rerollCountThisVisit;
    private bool waitingForSlotReplace;
    private Upgrade playerUpgrade;

    public int SelectedIndex => selectedIndex;
    public InGameShopCatalogSO Catalog => catalog;
    public bool IsShown => isActiveAndEnabled;
    public bool IsWaitingForSlotReplace => waitingForSlotReplace;

    public event Action<int> OnPurchaseClicked;
    public event Action OnRerollClicked;
    public event Action OnCloseClicked;

    private void Awake()
    {
        WireButtons();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
            WireButtons();

        rerollCountThisVisit = 0;
        waitingForSlotReplace = false;
        RefreshCards();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying)
                return;
            RefreshCards();
        };
    }
#endif

    private void WireButtons()
    {
        if (cards != null)
        {
            for (int i = 0; i < cards.Length; i++)
            {
                int index = i;
                var card = cards[i];
                if (card == null)
                    continue;

                var button = card.Button != null ? card.Button : card.GetComponent<Button>();
                if (button == null)
                    continue;

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => Select(index));
            }
        }

        if (purchaseButton != null)
        {
            purchaseButton.onClick.RemoveAllListeners();
            purchaseButton.onClick.AddListener(HandlePurchase);
        }

        if (rerollButton != null)
        {
            rerollButton.onClick.RemoveAllListeners();
            rerollButton.onClick.AddListener(HandleReroll);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(HandleClose);
        }

        RefreshPurchaseButton();
    }

    public void Select(int index)
    {
        if (waitingForSlotReplace)
            return;

        if (cards == null || cards.Length == 0)
        {
            selectedIndex = -1;
            RefreshPurchaseButton();
            return;
        }

        bool valid = index >= 0 && index < cards.Length && cards[index] != null;
        selectedIndex = valid ? index : -1;
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] != null)
                cards[i].SetSelected(i == selectedIndex);
        }

        RefreshPurchaseButton();
    }

    public void ClearSelection()
    {
        Select(-1);
    }

    public void SetWaitingForSlotReplace(bool waiting)
    {
        waitingForSlotReplace = waiting;
        RefreshPurchaseButton();
        if (purchaseButton != null && waiting)
            purchaseButton.interactable = false;
    }

    /// <summary>
    /// 카드 내용을 다시 넣습니다. 에디터는 목록 위 3장, 플레이는 무작위입니다.
    /// </summary>
    public void RefreshCards()
    {
        EnsureCatalog();
        ResolvePlayerUpgrade();

        if (catalog != null)
        {
            if (Application.isPlaying)
                ApplyRolledOffers();
            else
                ApplyCatalogFirstOffers();
        }
        else
            ApplyPreviewFallback();

        ClearSelection();
    }

    public InGameShopOffer GetSelectedOffer()
    {
        if (selectedIndex < 0 || selectedIndex >= currentOffers.Length)
            return null;
        return currentOffers[selectedIndex];
    }

    private void ApplyCatalogFirstOffers()
    {
        if (catalog == null || cards == null)
            return;

        catalog.FillFirstOffers(currentOffers, playerUpgrade);
        ApplyCurrentOffersToCards();
    }

    private void ApplyRolledOffers()
    {
        if (catalog == null || cards == null)
            return;

        catalog.FillRandomOffers(currentOffers, playerUpgrade);
        ApplyCurrentOffersToCards();
    }

    private void ApplyCurrentOffersToCards()
    {
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null)
                continue;

            InGameShopOffer offer = i < currentOffers.Length ? currentOffers[i] : null;
            ApplyOffer(cards[i], offer, playerUpgrade);
        }
    }

    private static void ApplyOffer(InGameShopCardView card, InGameShopOffer offer, Upgrade upgrade)
    {
        if (offer == null || offer.upgrade == null)
        {
            card.Apply(string.Empty, string.Empty, null, 0, false);
            card.SetDuplication(0);
            return;
        }

        UpgradeEffectSO effect = offer.upgrade;
        card.Apply(
            effect.GetDisplayName(),
            effect.GetDescription(),
            effect.icon,
            offer.price,
            offer.currency == ShopCurrency.Gem,
            effect.cardFrame);

        int dup = upgrade != null ? upgrade.GetCardDuplicationDisplay(effect) : 0;
        card.SetDuplication(dup);
    }

    private void ApplyPreviewFallback()
    {
        if (previewCards == null || cards == null)
            return;

        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null)
                continue;

            PreviewCard preview = i < previewCards.Length ? previewCards[i] : null;
            if (preview == null)
                continue;

            currentOffers[i] = null;
            cards[i].Apply(
                preview.itemName,
                preview.description,
                preview.icon,
                preview.price,
                preview.useGem);
            cards[i].SetDuplication(0);
        }
    }

    private void HandlePurchase()
    {
        if (waitingForSlotReplace)
            return;
        if (selectedIndex < 0)
            return;

        InGameShopOffer offer = GetSelectedOffer();
        if (offer == null || offer.upgrade == null)
            return;

        OnPurchaseClicked?.Invoke(selectedIndex);
    }

    private void HandleReroll()
    {
        if (waitingForSlotReplace)
            return;

        OnRerollClicked?.Invoke();
        rerollCountThisVisit++;
        ApplyRolledOffers();
        ClearSelection();
        Debug.Log("[InGameShopPopup] Reroll (비용 없음)");
    }

    /// <summary>플레이 중 상점을 엽니다. 닫기 시 팝업을 숨깁니다.</summary>
    public void ShowForGameplay()
    {
        hideOnClose = true;
        waitingForSlotReplace = false;
        gameObject.SetActive(true);
    }

    /// <summary>플레이 중 상점을 숨깁니다. 일시정지는 호출하는 쪽에서 풉니다.</summary>
    public void HideForGameplay()
    {
        waitingForSlotReplace = false;
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    private void HandleClose()
    {
        waitingForSlotReplace = false;
        OnCloseClicked?.Invoke();
        if (hideOnClose && Application.isPlaying)
            gameObject.SetActive(false);
    }

    private void RefreshPurchaseButton()
    {
        if (purchaseButton == null)
            return;

        if (waitingForSlotReplace)
        {
            purchaseButton.interactable = false;
            return;
        }

        InGameShopOffer offer = GetSelectedOffer();
        bool hasOffer = selectedIndex >= 0 && offer != null && offer.upgrade != null;
        if (!hasOffer)
        {
            purchaseButton.interactable = false;
            return;
        }

        if (!Application.isPlaying)
        {
            purchaseButton.interactable = true;
            return;
        }

        PlayerResources resources = PlayerResources.Instance;
        bool canAfford = resources == null || resources.CanAfford(offer.currency, offer.price);
        purchaseButton.interactable = canAfford;
    }

    private void ResolvePlayerUpgrade()
    {
        if (!Application.isPlaying)
        {
            playerUpgrade = null;
            return;
        }

        if (playerUpgrade == null)
            playerUpgrade = UnityEngine.Object.FindFirstObjectByType<Upgrade>();
    }

    private void EnsureCatalog()
    {
        if (catalog != null)
            return;
#if UNITY_EDITOR
        catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<InGameShopCatalogSO>(DefaultCatalogPath);
#endif
    }
}
