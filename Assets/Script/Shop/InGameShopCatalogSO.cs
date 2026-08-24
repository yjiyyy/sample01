using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상점 가격에 쓰는 재화. 돈 또는 젬입니다.
/// </summary>
public enum ShopCurrency
{
    Money,
    Gem
}

/// <summary>
/// 상점에 올리는 업그레이드 한 줄. 무엇을 얼마에 팔지가 여기 있습니다.
/// </summary>
[Serializable]
public class InGameShopOffer
{
    [Tooltip("상점에 등록할 업그레이드 SO")]
    public UpgradeEffectSO upgrade;

    [Tooltip("이 카드의 가격 종류")]
    public ShopCurrency currency = ShopCurrency.Money;

    [Tooltip("구매에 필요한 숫자")]
    [Min(0)]
    public int price = 10;

    public bool IsValid => upgrade != null && price >= 0;
}

/// <summary>
/// 인게임 상점에 나올 업그레이드 목록과 가격, 리롤 비용.
/// 카드 아이콘·이름·설명은 각 업그레이드 SO에 있고, 등록/가격만 이 파일에서 관리합니다.
/// </summary>
[CreateAssetMenu(fileName = "InGameShopCatalog", menuName = "Game/Shop/InGame Shop Catalog")]
public class InGameShopCatalogSO : ScriptableObject
{
    [Header("상점 등록 (이 리스트에서 3장을 뽑습니다)")]
    public List<InGameShopOffer> offers = new List<InGameShopOffer>();

    [Header("리롤")]
    [Tooltip("리롤에 쓰는 재화")]
    public ShopCurrency rerollCurrency = ShopCurrency.Money;

    [Tooltip("첫 리롤 비용")]
    [Min(0)]
    public int rerollCost = 5;

    [Tooltip("같은 상점 방문에서 리롤할 때마다 더해지는 값. 0이면 매번 같습니다.")]
    [Min(0)]
    public int rerollCostIncrease = 0;

    /// <summary>
    /// 이번 상점에서 rerollCount번째 리롤에 드는 비용 (0이 첫 리롤).
    /// </summary>
    public int GetRerollCost(int rerollCountThisVisit)
    {
        int extra = Mathf.Max(0, rerollCountThisVisit) * Mathf.Max(0, rerollCostIncrease);
        return Mathf.Max(0, rerollCost) + extra;
    }

    /// <summary>
    /// 목록 위에서부터 유효한 항목을 순서대로 넣습니다. 에디터 미리보기에 씁니다.
    /// </summary>
    public int FillFirstOffers(InGameShopOffer[] buffer, Upgrade playerUpgrade = null)
    {
        if (buffer == null || buffer.Length == 0)
            return 0;

        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = null;

        if (offers == null)
            return 0;

        int filled = 0;
        for (int i = 0; i < offers.Count && filled < buffer.Length; i++)
        {
            InGameShopOffer offer = offers[i];
            if (offer == null || !offer.IsValid)
                continue;
            if (playerUpgrade != null && playerUpgrade.IsExcludedFromShop(offer.upgrade))
                continue;
            buffer[filled] = offer;
            filled++;
        }

        return filled;
    }

    /// <summary>
    /// 등록된 항목 중 서로 다른 업그레이드를 무작위로 넣습니다. 넣은 개수를 반환합니다.
    /// 최대 스택인 일반 업그레이드는 playerUpgrade 기준으로 제외합니다.
    /// </summary>
    public int FillRandomOffers(InGameShopOffer[] buffer, Upgrade playerUpgrade = null)
    {
        if (buffer == null || buffer.Length == 0)
            return 0;

        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = null;

        var pool = new List<InGameShopOffer>(offers != null ? offers.Count : 0);
        var seen = new HashSet<UpgradeEffectSO>();
        if (offers != null)
        {
            for (int i = 0; i < offers.Count; i++)
            {
                InGameShopOffer offer = offers[i];
                if (offer == null || !offer.IsValid)
                    continue;
                if (playerUpgrade != null && playerUpgrade.IsExcludedFromShop(offer.upgrade))
                    continue;
                if (!seen.Add(offer.upgrade))
                    continue;
                pool.Add(offer);
            }
        }

        int take = Mathf.Min(buffer.Length, pool.Count);
        for (int i = 0; i < take; i++)
        {
            int j = i + UnityEngine.Random.Range(0, pool.Count - i);
            InGameShopOffer tmp = pool[i];
            pool[i] = pool[j];
            pool[j] = tmp;
            buffer[i] = pool[i];
        }

        return take;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        rerollCost = Mathf.Max(0, rerollCost);
        rerollCostIncrease = Mathf.Max(0, rerollCostIncrease);
        if (offers == null)
            return;

        for (int i = 0; i < offers.Count; i++)
        {
            if (offers[i] != null)
                offers[i].price = Mathf.Max(0, offers[i].price);
        }
    }
#endif
}
