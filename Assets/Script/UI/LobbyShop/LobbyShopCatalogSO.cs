using System;
using UnityEngine;

/// <summary>
/// 로비 상점 칸 하나. 이름·설명·아이콘·가격을 여기서 고칩니다.
/// NPC 말풍선은 description을 그대로 보여 줍니다.
/// </summary>
[Serializable]
public class LobbyShopItem
{
    public WeaponDataSO weapon;
    public bool forSale = true;
    public bool IsValid => forSale && weapon != null && weapon.weaponPrefab != null && !string.IsNullOrWhiteSpace(weapon.id) && price >= 0;
    public Sprite DisplayIcon => weapon != null ? weapon.icon : icon;
    public string DisplayName(GameLanguage language)
    {
        string label = displayName.Get(language);
        return !string.IsNullOrEmpty(label) ? label : weapon != null ? weapon.weaponName : "";
    }
    public LocalizedString displayName;
    [Tooltip("이 칸을 고르면 NPC 말풍선에 들어가는 설명입니다.")]
    public LocalizedString description;
    [HideInInspector] public Sprite icon;
    [Min(0)]
    public int price = 50;
    public ShopCurrency currency = ShopCurrency.Money;
}

/// <summary>
/// 상점 큰 분류 하나. 탭 이름과 그 안의 아이템 목록입니다.
/// 목록에 줄을 넣으면 화면에 칸이 생깁니다.
/// </summary>
[Serializable]
public class LobbyShopCategory
{
    public LocalizedString displayName;
    public LobbyShopItem[] items = Array.Empty<LobbyShopItem>();
}

/// <summary>
/// 로비 상점 분류·아이템·NPC 문구. 한/영은 이 에셋만 고치면 됩니다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Lobby/Shop Catalog", fileName = "LobbyShopCatalog")]
public class LobbyShopCatalogSO : ScriptableObject
{
    [Min(1)] public int inventoryCapacity = 10;
    public Sprite coinIcon;
    public Sprite gemIcon;
    public LobbyShopCategorySO[] categoryAssets = Array.Empty<LobbyShopCategorySO>();
    public LobbyShopCategory[] Categories => categoryAssets != null && categoryAssets.Length > 0
        ? Array.ConvertAll(categoryAssets, x => x != null ? x.category : null) : categories;
    public const string ResourcesPath = "UI/LobbyShopCatalog";

    [Header("NPC")]
    public Sprite npcPortrait;
    public LocalizedString npcName = new LocalizedString
    {
        korean = "점원",
        english = "Clerk"
    };
    public LocalizedString idleLine = new LocalizedString
    {
        korean = "오늘은 뭐가 필요해?",
        english = "What do you need today?"
    };

    [Header("버튼")]
    public LocalizedString backLabel = new LocalizedString
    {
        korean = "돌아가기",
        english = "Back"
    };
    public LocalizedString buyLabel = new LocalizedString
    {
        korean = "구매",
        english = "Buy"
    };

    [Header("분류 (순서대로 탭에 표시)")]
    [HideInInspector] public LobbyShopCategory[] categories = CreateDefaultCategories();

    public static LobbyShopCatalogSO Load()
    {
        var settings = Resources.Load<LobbyShopSettingsSO>("UI/LobbyShopSettings");
        return settings != null && settings.catalog != null ? settings.catalog : Resources.Load<LobbyShopCatalogSO>(ResourcesPath);
    }

    public LobbyShopCategory GetCategory(int index)
    {
        var list = Categories;
        if (list == null || index < 0 || index >= list.Length)
            return null;
        return list[index];
    }

    public LobbyShopItem GetItem(int categoryIndex, int itemIndex)
    {
        var category = GetCategory(categoryIndex);
        if (category == null || category.items == null)
            return null;
        if (itemIndex < 0 || itemIndex >= category.items.Length)
            return null;
        return category.items[itemIndex];
    }

    public static LobbyShopCategory[] CreateDefaultCategories()
    {
        return new[]
        {
            new LobbyShopCategory
            {
                displayName = Loc("소모품", "Consumables"),
                items = new[]
                {
                    Item("회복약", "Potion", "전투 중 체력을 회복한다.", "Restores health in battle.", 50),
                    Item("부활권", "Revive", "쓰러져도 한 번 더 일어난다.", "Get back up once after falling.", 200),
                    Item("자석", "Magnet", "떨어진 재화를 더 잘 끌어온다.", "Pulls dropped coins in from farther away.", 80),
                    Item("상자", "Chest", "안에서 무엇이 나올지 모른다.", "Open it. You will not know what comes out.", 120),
                    Item("붕대", "Bandage", "상처를 빠르게 감싼다.", "Wraps a wound in a hurry.", 30),
                    Item("열쇠", "Key", "잠긴 상자를 열 때 쓴다.", "Opens a locked chest.", 90),
                    Item("폭탄", "Bomb", "한 번에 주변을 밀어낸다.", "Pushes everything nearby at once.", 70),
                    Item("신발", "Boots", "조금 더 가볍게 달린다.", "Lets you run a little lighter.", 110),
                    Item("별", "Star", "잠시 반짝이며 힘을 준다.", "A short spark of extra power.", 150),
                    Item("망치", "Hammer", "단단한 것을 두들겨 연다.", "Breaks what will not open.", 60),
                    Item("방패", "Shield", "한 대를 막아 준다.", "Blocks one hit for you.", 90),
                    Item("시계", "Clock", "조금 더 오래 버틴다.", "Gives you a little more time.", 200),
                    Item("장갑", "Gloves", "물건을 더 꽉 잡는다.", "Helps you hold on tighter.", 40),
                    Item("부적", "Charm", "운이 조금 좋아진다.", "A small nudge of luck.", 130),
                    Item("깃발", "Flag", "멀리서도 위치를 알린다.", "Marks your spot from far away.", 75),
                    Item("깃털", "Feather", "착지가 한결 부드러워진다.", "Makes landings a bit softer.", 55)
                }
            },
            new LobbyShopCategory
            {
                displayName = Loc("재화", "Currency"),
                items = new[]
                {
                    Item("골드팩", "Gold Pack", "작은 골드 주머니다.", "A small pouch of gold.", 100, ShopCurrency.Gem),
                    Item("젬팩", "Gem Pack", "젬이 조금 들어 있다.", "A few gems inside.", 300, ShopCurrency.Money),
                    Item("큰 골드", "Big Gold", "골드가 많이 들어 있다.", "A large pile of gold.", 500, ShopCurrency.Gem)
                }
            },
            new LobbyShopCategory
            {
                displayName = Loc("스페셜", "Special"),
                items = new[]
                {
                    Item("이벤트 상자", "Event Chest", "지금만 살 수 있는 상자다.", "A chest you can buy only now.", 250),
                    Item("점원의 선물", "Clerk Gift", "점원이 아끼는 물건이다.", "The clerk's favorite item.", 180)
                }
            }
        };
    }

    private static LocalizedString Loc(string korean, string english)
    {
        return new LocalizedString { korean = korean, english = english };
    }

    private static LobbyShopItem Item(
        string nameKr,
        string nameEn,
        string descKr,
        string descEn,
        int price,
        ShopCurrency currency = ShopCurrency.Money)
    {
        return new LobbyShopItem
        {
            displayName = Loc(nameKr, nameEn),
            description = Loc(descKr, descEn),
            price = price,
            currency = currency
        };
    }
}
