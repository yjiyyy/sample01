using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUpgradePanel : MonoBehaviour
{
    public const string ObjectName = "UpgradePanel";
    public const int LayoutVersion = 6;
    [SerializeField] private LobbyStatUpgradeCatalogSO catalog;
    [SerializeField] private GameObject rightMenuToHide;
    [SerializeField] private Button backButton, upgradeButton;
    [SerializeField] private Image npcPortrait, actionCoin;
    [SerializeField] private LocalizedText npcLineText;
    [SerializeField] private TextMeshProUGUI titleText, legendText, actionPrice, statusText;
    [SerializeField] private LobbyUpgradeRowUI[] rows = new LobbyUpgradeRowUI[0];
    [SerializeField] private int builtLayoutVersion;
    private LobbyStatId selected = LobbyStatId.HP;
    private LobbyCharacterUpgradeSO profile, builtProfile;
    private PlayerResources wallet;
    private bool building;
    public LobbyStatUpgradeCatalogSO Catalog => catalog != null ? catalog : (catalog = LobbyStatUpgradeCatalogSO.Load());
    public LobbyStatUpgradeEntry[] Entries => profile != null ? profile.stats : System.Array.Empty<LobbyStatUpgradeEntry>();

    public static LobbyUpgradePanel EnsureOnLobbyCanvas()
    {
        var existing = FindFirstObjectByType<LobbyUpgradePanel>(FindObjectsInactive.Include);
        if (existing != null) { existing.EnsureBuilt(); return existing; }
        var canvas = GameObject.Find("LobbyCanvas");
        return canvas != null ? LobbyUpgradePanelBuilder.Build(canvas.GetComponent<RectTransform>()) : null;
    }

    private void Awake() { EnsureBuilt(); WireButtons(); }
    private void OnEnable()
    {
        LanguageManager.LanguageChanged += RefreshAll;
        LobbyStatUpgradeState.Changed += RefreshAll;
        EnsureBuilt(); BindWallet(); RefreshAll();
    }
    private void OnDisable()
    {
        LanguageManager.LanguageChanged -= RefreshAll;
        LobbyStatUpgradeState.Changed -= RefreshAll;
        if (wallet != null) wallet.OnResourcesChanged -= OnMoneyChanged;
        wallet = null;
    }

    // 오브젝트 교체만 확인하며, 매 프레임 씬 탐색이나 UI 문자열 생성을 하지 않습니다.
    private void Update()
    {
        if (wallet != PlayerResources.Instance) { BindWallet(); RefreshAll(); }
        var active = GameState.Instance != null ? GameState.Instance.SelectedCharacter : null;
        if (active != null && Catalog != null && profile != Catalog.GetCharacter(active))
        { EnsureBuilt(); RefreshAll(); }
    }

    public void Show()
    {
        EnsureBuilt();
        var shop = FindFirstObjectByType<LobbyShopPanel>(FindObjectsInactive.Include);
        if (shop != null) shop.HideForSwitch();
        if (profile != null && profile.GetEntry(LobbyStatId.HP) != null) selected = LobbyStatId.HP;
        else if (rows.Length > 0) selected = rows[0].StatId;
        gameObject.SetActive(true);
        if (rightMenuToHide != null) rightMenuToHide.SetActive(false);
        var blend = LobbyUpgradeCameraBlend.Ensure();
        if (blend != null) blend.GoUpgrade(!Application.isPlaying);
        BindWallet(); SetLine(Catalog != null ? Catalog.idleLine : default); RefreshAll();
    }
    public void Hide()
    {
        if (rightMenuToHide != null) rightMenuToHide.SetActive(true);
        var blend = LobbyUpgradeCameraBlend.Ensure();
        if (blend != null) blend.GoRest(!Application.isPlaying);
        gameObject.SetActive(false);
    }
    public void HideForSwitch() => gameObject.SetActive(false);
    public void SelectStat(LobbyStatId id)
    {
        var entry = profile != null ? profile.GetEntry(id) : null;
        if (entry == null) return;
        selected = id; SetLine(entry.selectLine); RefreshAll();
    }
    public void TryUpgradeSelected() => TryUpgrade(selected);
    public void TryUpgrade(LobbyStatId id)
    {
        if (!Application.isPlaying || profile == null || Catalog == null) return;
        var entry = profile.GetEntry(id);
        if (entry == null) return;
        int level = LobbyStatUpgradeState.GetLevel(profile, id);
        if (!LobbyStatUpgradeApplier.CanUpgrade(entry, profile.Config, level))
        { SetLine(catalog.maxedLine); RefreshAll(); return; }
        BindWallet();
        bool bought = LobbyStatUpgradeState.TryPurchase(profile, id, wallet);
        selected = id;
        SetLine(bought ? catalog.upgradedLine : catalog.cannotAffordLine);
        RefreshAll();
    }

    public void EnsureBuilt(bool force = false)
    {
        if (building || Catalog == null) return;
        var character = GameState.Instance != null ? GameState.Instance.SelectedCharacter : null;
        if (character == null)
        {
            var lobby = FindFirstObjectByType<LobbyController>();
            if (lobby != null) character = lobby.PreviewCharacter;
        }
        profile = Catalog.GetCharacter(character);
        bool sameRows = rows != null;
        int expected = 0;
        var seen = new System.Collections.Generic.HashSet<LobbyStatId>();
        foreach (var entry in Entries)
        {
            if (entry == null || !seen.Add(entry.id)) continue;
            if (rows == null || expected >= rows.Length || rows[expected] == null || rows[expected].StatId != entry.id) sameRows = false;
            expected++;
        }
        sameRows &= rows != null && expected == rows.Length;
        if (!force && sameRows && builtLayoutVersion == LayoutVersion && builtProfile == profile)
        { WireButtons(); return; }
        building = true;
        try
        {
            LobbyUpgradePanelBuilder.RebuildInto(this);
            builtProfile = profile;
            if (profile == null || profile.GetEntry(selected) == null)
                selected = rows.Length > 0 ? rows[0].StatId : LobbyStatId.HP;
        }
        finally { building = false; }
        WireButtons(); RefreshAll();
    }

    public void AssignBuiltRefs(LobbyStatUpgradeCatalogSO data, GameObject menu, Button back, Button upgrade,
        Image portrait, LocalizedText line, LobbyUpgradeRowUI[] newRows, TextMeshProUGUI title,
        TextMeshProUGUI legend, TextMeshProUGUI price, Image coin, TextMeshProUGUI status)
    {
        catalog = data; rightMenuToHide = menu; backButton = back; upgradeButton = upgrade;
        npcPortrait = portrait; npcLineText = line; rows = newRows; titleText = title;
        legendText = legend; actionPrice = price; actionCoin = coin; statusText = status;
        builtLayoutVersion = LayoutVersion;
    }
    private void WireButtons()
    {
        if (backButton != null) { backButton.onClick.RemoveListener(Hide); backButton.onClick.AddListener(Hide); }
        if (upgradeButton != null) { upgradeButton.onClick.RemoveListener(TryUpgradeSelected); upgradeButton.onClick.AddListener(TryUpgradeSelected); }
    }
    private void BindWallet()
    {
        if (wallet != null) wallet.OnResourcesChanged -= OnMoneyChanged;
        wallet = Application.isPlaying ? PlayerResources.Instance : null;
        if (wallet != null && isActiveAndEnabled) wallet.OnResourcesChanged += OnMoneyChanged;
    }
    private void OnMoneyChanged(int money, int gem) => RefreshAll();
    public void RefreshAll()
    {
        if (Catalog == null || building) return;
        bool ko = LanguageManager.Instance == null || LanguageManager.Instance.CurrentLanguage == GameLanguage.Korean;
        if (titleText != null) titleText.text = profile != null && profile.character != null
            ? profile.character.displayName + (ko ? " · 능력 강화" : " · UPGRADES") : (ko ? "캐릭터 설정 필요" : "Character not configured");
        if (legendText != null) legendText.text = ko
            ? "<color=#F2F0E6>■ 기본 능력</color>     <color=#73D9D4>■ 강화 증가</color>"
            : "<color=#F2F0E6>■ Base stats</color>     <color=#73D9D4>■ Purchased growth</color>";
        if (npcPortrait != null) npcPortrait.sprite = catalog.npcPortrait;
        SetButtonText(backButton, catalog.backLabel);
        foreach (var row in rows)
            if (row != null) row.Refresh(profile != null ? profile.GetEntry(row.StatId) : null, profile, row.StatId == selected);
        RefreshAction(ko);
    }
    private void RefreshAction(bool ko)
    {
        if (upgradeButton == null) return;
        var entry = profile != null ? profile.GetEntry(selected) : null;
        int level = LobbyStatUpgradeState.GetLevel(profile, selected);
        bool valid = entry != null && profile.Config != null && !string.IsNullOrEmpty(profile.CharacterId);
        bool maxed = valid && !LobbyStatUpgradeApplier.CanUpgrade(entry, profile.Config, level);
        int cost = LobbyStatUpgradeApplier.GetNextCost(entry, level);
        bool afford = wallet != null && wallet.CanAfford(ShopCurrency.Money, cost);
        upgradeButton.interactable = Application.isPlaying && valid && !maxed && afford;
        string nameKo = entry != null ? entry.displayName.korean : "";
        string nameEn = entry != null ? entry.displayName.english : "";
        SetButtonText(upgradeButton, new LocalizedString {
            korean = nameKo + (maxed ? " 강화 완료" : " 업그레이드"),
            english = nameEn + (maxed ? " MAX" : " Upgrade") });
        if (actionPrice != null) actionPrice.text = maxed ? "MAX" : cost.ToString("N0");
        if (actionCoin != null) actionCoin.enabled = valid && !maxed;
        if (statusText != null) statusText.text = !valid ? (ko ? "캐릭터의 업그레이드 SO를 연결하세요." : "Assign character upgrade settings.")
            : maxed ? (ko ? "강화 완료" : "Fully upgraded")
            : Application.isPlaying && !afford ? (ko ? "골드 부족" : "Not enough gold") : "";
    }
    private static void SetButtonText(Button button, LocalizedString text)
    {
        if (button != null) button.GetComponentInChildren<LocalizedText>(true)?.SetTexts(text.korean, text.english);
    }
    private void SetLine(LocalizedString text)
    {
        if (npcLineText != null) npcLineText.SetTexts(text.korean, text.english);
    }
}
