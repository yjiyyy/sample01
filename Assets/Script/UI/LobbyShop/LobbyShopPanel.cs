using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyShopPanel : MonoBehaviour
{
    public const string ObjectName = "ShopPage";
    public const int LayoutVersion = 8;
    [SerializeField] private LobbyShopCatalogSO catalog;
    [SerializeField] private GameObject rightMenuToHide;
    [SerializeField] private Button backButton, buyButton;
    [SerializeField] private Image npcPortrait;
    [SerializeField] private LocalizedText npcLineText;
    [SerializeField] private RectTransform tabRow, itemContent;
    [SerializeField] private LobbyShopGridFitter gridFitter;
    [SerializeField] private Button[] tabButtons;
    [SerializeField] private int builtLayoutVersion;
    private LobbyShopCellUI[] cells = Array.Empty<LobbyShopCellUI>();
    private LobbyShopItem[] items = Array.Empty<LobbyShopItem>();
    private TMP_FontAsset font;
    private int categoryIndex, selectedIndex = -1;
    private bool built, building, inventoryMode, previewing;
    private PlayerEquipmentController equipment;
    private TextMeshProUGUI status, characterLabel, notice;
    private Button slotButton;
    private Image buyCoin;
    private TextMeshProUGUI buyPrice;
    public LobbyShopCatalogSO Catalog => catalog != null ? catalog : (catalog = LobbyShopCatalogSO.Load());
    public bool InventoryMode => inventoryMode;
    public LobbyShopItem SelectedItem => selectedIndex >= 0 && selectedIndex < items.Length ? items[selectedIndex] : null;
    public static string Text(string ko, string en) => LanguageManager.Instance != null && LanguageManager.Instance.CurrentLanguage != GameLanguage.Korean ? en : ko;

    public static LobbyShopPanel EnsureOnLobbyCanvas()
    {
        var panel = FindFirstObjectByType<LobbyShopPanel>(FindObjectsInactive.Include);
        if (panel != null) { panel.EnsureBuilt(); return panel; }
        var canvas = GameObject.Find("LobbyCanvas");
        return canvas != null ? LobbyShopPanelBuilder.Build(canvas.GetComponent<RectTransform>()) : null;
    }
    private void Awake() => EnsureBuilt();
    private void OnEnable()
    {
        AccountSession.ResourcesChanged += MoneyChanged;
        AccountInventory.Changed += Refresh;
        LanguageManager.LanguageChanged += Refresh;
    }
    private void OnDisable()
    {
        RestorePreview();
        AccountSession.ResourcesChanged -= MoneyChanged;
        AccountInventory.Changed -= Refresh;
        LanguageManager.LanguageChanged -= Refresh;
    }
    private void MoneyChanged(int money, int gem) => Refresh();
    public void Show() => Open(false);
    public void ShowInventory() => Open(true);
    private void Open(bool inventory)
    {
        RestorePreview(); inventoryMode = inventory;
        EnsureBuilt();
        var upgrade = FindFirstObjectByType<LobbyUpgradePanel>(FindObjectsInactive.Include);
        if (upgrade != null) upgrade.HideForSwitch();
        gameObject.SetActive(true);
        if (rightMenuToHide != null) rightMenuToHide.SetActive(false);
        var spawn = GameObject.Find("CharacterSpawnPoint");
        equipment = spawn != null ? spawn.GetComponentInChildren<PlayerEquipmentController>() : null;
        LobbyUpgradeCameraBlend.Ensure()?.GoShop(!Application.isPlaying);
        SelectCategory(0);
    }
    public void Hide()
    {
        RestorePreview();
        if (rightMenuToHide != null) rightMenuToHide.SetActive(true);
        LobbyUpgradeCameraBlend.Ensure()?.GoRest(!Application.isPlaying);
        gameObject.SetActive(false);
    }
    public void HideForSwitch() { RestorePreview(); gameObject.SetActive(false); }
    private void RestorePreview()
    {
        if (previewing && equipment != null && Application.isPlaying) equipment.EquipActive(equipment.transform);
        previewing = false;
    }
    public void SelectCategory(int index)
    {
        RestorePreview(); categoryIndex = index;
        if (inventoryMode)
            items = index == 0 ? AccountInventory.Weapons.Select(w => Catalog.Categories.Where(c=>c!=null).SelectMany(c=>c.items).FirstOrDefault(i=>i!=null && i.weapon==w) ?? new LobbyShopItem { weapon = w }).ToArray() : Array.Empty<LobbyShopItem>();
        else items = (Catalog?.GetCategory(index)?.items ?? Array.Empty<LobbyShopItem>()).Where(i => i != null && i.forSale).ToArray();
        for (int i = itemContent.childCount - 1; i >= 0; i--)
        {
            var child = itemContent.GetChild(i).gameObject; child.SetActive(false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
        cells = new LobbyShopCellUI[items.Length];
        for (int i=0; i<items.Length; i++) cells[i] = LobbyShopPanelBuilder.CreateCell(itemContent, font, i);
        Canvas.ForceUpdateCanvases(); gridFitter.ForceApply();
        var scroll = itemContent.GetComponentInParent<ScrollRect>();
        if (scroll != null) { scroll.StopMovement(); scroll.verticalNormalizedPosition = 1; }
        SelectItem(items.Length > 0 ? 0 : -1);
    }
    public void SelectItem(int index)
    {
        RestorePreview(); selectedIndex = index;
        var item = SelectedItem;
        if (Application.isPlaying && equipment != null && item?.weapon != null && item.weapon.weaponPrefab != null)
        {
            // 표시 무기만 바꾸고 실제 슬롯과 보유 기록은 유지합니다.
            equipment.EquipByData(item.weapon, equipment.transform, false);
            previewing = true;
        }
        if (npcLineText != null && Catalog != null)
        {
            var line = item != null && !string.IsNullOrEmpty(item.description.korean) ? item.description : Catalog.idleLine;
            npcLineText.SetTexts(line.korean, line.english);
        }
        Refresh();
    }
    public bool CanEquip(WeaponDataSO weapon)
    {
        return equipment != null && weapon != null && weapon.weaponPrefab != null &&
            (equipment.IsUnarmed(weapon) || !equipment.IsSameWeapon(weapon, equipment.InactiveWeaponData)) &&
            equipment.CanAssignToActiveSlotByStrength(weapon, out _, out _);
    }
    public void PurchaseSelected()
    {
        var item = SelectedItem;
        if (item?.weapon == null) return;
        bool success = inventoryMode ? AccountInventory.Owns(item.weapon) : AccountInventory.TryBuy(item);
        if (!success) { Refresh(); return; }
        RestorePreview();
        bool equipped = CanEquip(item.weapon) && equipment.TryAssignToActiveSlot(item.weapon, equipment.transform);
        if (equipped) AccountInventory.RememberLoadout(AccountSession.SelectedCharacter, equipment);
        Refresh();
        notice.text = equipped ? Text("장착했습니다", "Equipped") : Text("인벤토리에 보관했습니다", "Stored in inventory");
    }
    public void SwitchSlot()
    {
        RestorePreview();
        if (equipment != null) { equipment.SwitchActiveSlot(equipment.transform); AccountInventory.RememberLoadout(AccountSession.SelectedCharacter, equipment); }
        SelectItem(selectedIndex);
    }
    public void EnsureBuilt()
    {
        if (building || (built && builtLayoutVersion == LayoutVersion)) return;
        building = true;
        try { LobbyShopPanelBuilder.RebuildInto(this); built = true; builtLayoutVersion = LayoutVersion; }
        finally { building = false; }
    }
    public void AssignBuiltRefs(LobbyShopCatalogSO data, GameObject menu, Button back, Button buy, Image portrait,
        LocalizedText line, RectTransform tabs, RectTransform content, LobbyShopGridFitter fitter, Button[] buttons, TMP_FontAsset uiFont)
    {
        catalog=data; rightMenuToHide=menu; backButton=back; buyButton=buy; npcPortrait=portrait;
        npcLineText=line; tabRow=tabs; itemContent=content; gridFitter=fitter; tabButtons=buttons; font=uiFont;
        back.onClick.RemoveAllListeners(); back.onClick.AddListener(Hide);
        buy.onClick.RemoveAllListeners(); buy.onClick.AddListener(PurchaseSelected);
        buy.GetComponent<RectTransform>().sizeDelta = new Vector2(350,80);
        LobbyPanelChrome.ApplyRounded(buy.GetComponent<Image>(), new Color(1,.83f,.22f));
        buy.GetComponentInChildren<TextMeshProUGUI>().fontSize=36;
        var buyText=buy.GetComponentInChildren<TextMeshProUGUI>();
        LobbyShopPanelBuilder.Place(buyText.rectTransform,.04f,0,.44f,1);
        buyCoin=LobbyPanelChrome.CreateUi("BuyCoin",buy.transform).AddComponent<Image>();
        buyCoin.preserveAspect=true; buyCoin.raycastTarget=false;
        LobbyShopPanelBuilder.Place(buyCoin.rectTransform,.44f,.18f,.59f,.82f);
        buyPrice=LobbyShopPanelBuilder.Label("BuyPrice",buy.transform,34);
        buyPrice.color=new Color(.12f,.12f,.14f); buyPrice.alignment=TextAlignmentOptions.MidlineLeft;
        LobbyShopPanelBuilder.Place(buyPrice.rectTransform,.61f,.1f,.98f,.9f);
        status=LobbyShopPanelBuilder.Label("AccountInfo", transform, 25);
        LobbyShopPanelBuilder.Place(status.rectTransform,.025f,.125f,.265f,.27f);
        status.alignment=TextAlignmentOptions.MidlineLeft;
        var bg=LobbyPanelChrome.CreateUi("AccountCard",transform).AddComponent<Image>();
        LobbyShopPanelBuilder.Place(bg.rectTransform,.02f,.12f,.27f,.275f);
        LobbyPanelChrome.ApplyRounded(bg,new Color(.1f,.11f,.13f,.8f)); bg.raycastTarget=false; bg.transform.SetSiblingIndex(0);
        characterLabel=LobbyShopPanelBuilder.Label("PreviewLabel",transform,30);
        LobbyShopPanelBuilder.Place(characterLabel.rectTransform,.025f,.825f,.275f,.91f);
        notice=LobbyShopPanelBuilder.Label("PurchaseNotice",transform,23);
        LobbyShopPanelBuilder.Place(notice.rectTransform,.79f,.115f,.98f,.18f);
        notice.alignment=TextAlignmentOptions.BottomRight;
        slotButton=LobbyPanelChrome.CreateActionButton(transform,font,new LocalizedString {korean="슬롯 변경",english="Switch slot"},"SlotButton");
        LobbyShopPanelBuilder.Place(slotButton.GetComponent<RectTransform>(),.025f,.28f,.26f,.325f);
        slotButton.GetComponentInChildren<TextMeshProUGUI>().fontSize=22;
        slotButton.onClick.AddListener(SwitchSlot);
        SelectCategory(0);
    }
    private void Refresh()
    {
        if (buyButton == null) return;
        var item=SelectedItem; bool owned=item?.weapon!=null && AccountInventory.Owns(item.weapon);
        bool full=AccountInventory.Weapons.Count>=AccountInventory.Capacity;
        bool afford=item!=null && AccountSession.CanAfford(item.currency,item.price);
        bool valid=item!=null && (inventoryMode ? item.weapon!=null && item.weapon.weaponPrefab!=null : item.IsValid);
        string reason=items.Length==0 ? Text("등록된 아이템이 없습니다", "No items") : !valid ? Text("판매 설정 확인 필요","Unavailable") :
            inventoryMode ? (CanEquip(item.weapon)?"":Text("장착 불가", "Cannot equip")) : owned ? Text("보유 중", "Owned") :
            full ? Text("인벤토리가 가득 찼습니다", "Inventory full") : !afford ? Text("재화 부족", "Insufficient funds") : "";
        buyButton.interactable=Application.isPlaying && valid && (inventoryMode ? owned && CanEquip(item.weapon) : !owned && !full && afford);
        var loc=buyButton.GetComponentInChildren<LocalizedText>();
        bool showPrice=!inventoryMode && valid && !owned;
        loc.SetTexts(inventoryMode?"장착":owned?"보유 중":"구매",inventoryMode?"Equip":owned?"Owned":"Buy");
        LobbyShopPanelBuilder.Place(loc.GetComponent<RectTransform>(),.04f,0,showPrice ? .44f : .96f,1);
        buyPrice.text=showPrice ? item.price.ToString("N0") : "";
        buyCoin.enabled=showPrice; buyCoin.sprite=item?.currency==ShopCurrency.Gem ? Catalog.gemIcon : Catalog.coinIcon;
        notice.text=reason;
        var stats=equipment!=null ? equipment.GetComponent<PlayerStats>() : null;
        status.text=(CanEquip(item?.weapon)?"<color=#72DAD2>"+Text("장착 가능","Can equip")+"</color>":Text("장착 불가","Cannot equip"))+
            "\n"+Text("현재 근력  ","Strength  ")+(stats!=null?stats.strength.ToString("0.##"):"-")+"\n"+
            Text("인벤토리  ","Inventory  ")+AccountInventory.Weapons.Count+" / "+AccountInventory.Capacity;
        characterLabel.text=(AccountSession.SelectedCharacter!=null?AccountSession.SelectedCharacter.displayName:"")+"\n<size=22>"+Text("장착 미리보기","Equipment preview")+"</size>";
        slotButton.gameObject.SetActive(inventoryMode && equipment!=null);
        if (slotButton.gameObject.activeSelf) slotButton.GetComponentInChildren<LocalizedText>().SetTexts("슬롯 "+(equipment.ActiveSlotIndex+1)+" · 변경","Slot "+(equipment.ActiveSlotIndex+1)+" · Switch");
        for(int i=0;i<cells.Length;i++) cells[i].Refresh(items[i],i==selectedIndex);
        for(int i=0;i<tabButtons.Length;i++)
        {
            tabButtons[i].GetComponentInChildren<TextMeshProUGUI>().text=inventoryMode && i==0?Text("보유 무기","Owned"):Catalog.GetCategory(i)?.displayName.Get(LanguageManager.Instance!=null?LanguageManager.Instance.CurrentLanguage:GameLanguage.Korean);
            tabButtons[i].GetComponentInChildren<TextMeshProUGUI>().color=i==categoryIndex?new Color(1,.83f,.22f):Color.white;
        }
    }
}
