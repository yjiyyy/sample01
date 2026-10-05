using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상점 아이템 칸 하나. 위 이름, 가운데 아이콘, 아래 가격입니다.
/// </summary>
public class LobbyShopCellUI : MonoBehaviour
{
    [SerializeField] private int itemIndex;
    [SerializeField] private Image cardBackground;
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI nameLabel;
    [SerializeField] private TextMeshProUGUI priceLabel;
    [SerializeField] private Image currencyIcon;
    [SerializeField] private Button selectButton;

    private static readonly Color CardNormal = new Color(0.10f, 0.10f, 0.12f, 0.34f);
    private static readonly Color CardSelected = new Color(0.16f, 0.17f, 0.20f, 0.62f);

    public int ItemIndex => itemIndex;

    public void Setup(
        int index,
        Image card,
        Image iconImage,
        TextMeshProUGUI name,
        TextMeshProUGUI price,
        Image currency,
        Button select)
    {
        itemIndex = index;
        cardBackground = card;
        icon = iconImage;
        nameLabel = name;
        priceLabel = price;
        currencyIcon = currency;
        selectButton = select;

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() =>
            {
                var panel = GetComponentInParent<LobbyShopPanel>(true);
                panel?.SelectItem(itemIndex);
            });
        }
    }

    public void Refresh(LobbyShopItem item, bool selected)
    {
        if (item == null)
            return;

        GameLanguage lang = LanguageManager.Instance != null
            ? LanguageManager.Instance.CurrentLanguage
            : GameLanguage.Korean;

        if (nameLabel != null)
        {
            nameLabel.text = item.DisplayName(lang);
            nameLabel.color = selected ? Color.white : new Color(1f, 1f, 1f, 0.86f);
        }

        if (icon != null)
        {
            icon.sprite = item.DisplayIcon;
            icon.enabled = icon.sprite != null;
            icon.color = Color.white;
        }

        bool owned = AccountInventory.Owns(item.weapon);
        var panel = GetComponentInParent<LobbyShopPanel>();
        if (priceLabel != null)
        {
            priceLabel.text = owned ? LobbyShopPanel.Text("보유 중", "Owned") : item.price.ToString("N0");
            priceLabel.color = owned ? new Color(.45f,.86f,.82f) : new Color(1,.83f,.22f);
        }
        var weight = transform.Find("Weight")?.GetComponent<TextMeshProUGUI>();
        if (weight != null) weight.text = item.weapon != null ? item.weapon.weight.ToString("0.##") : "-";
        var warning = transform.Find("EquipStatus")?.GetComponent<TextMeshProUGUI>();
        if (warning != null) warning.text = Application.isPlaying && panel != null && !panel.CanEquip(item.weapon) ? LobbyShopPanel.Text("장착 불가", "Cannot equip") : "";
        var outline = GetComponent<Outline>(); if (outline != null) outline.enabled = selected;

        if (currencyIcon != null)
        {
            currencyIcon.enabled = !owned;
            var catalog = LobbyShopCatalogSO.Load();
            Sprite sprite = item.currency == ShopCurrency.Gem ? catalog?.gemIcon : catalog?.coinIcon;
            if (sprite != null)
            {
                currencyIcon.sprite = sprite;
                currencyIcon.color = Color.white;
            }
            else
            {
                currencyIcon.color = item.currency == ShopCurrency.Gem
                    ? new Color(0.45f, 0.72f, 1f, 1f)
                    : new Color(0.96f, 0.76f, 0.22f, 1f);
            }
        }

        if (cardBackground != null)
            cardBackground.color = selected ? new Color(1,.83f,.22f) : new Color(.19f,.20f,.22f);
    }

    private static Sprite PlaceholderIcon(int index)
    {
        string[] fallbacks = { "Icon_HP", "Icon_STA", "Icon_SPD", "Icon_STR" };
        return LobbyStatUpgradeCatalogSO.LoadSprite(fallbacks[Mathf.Abs(index) % fallbacks.Length]);
    }
}
