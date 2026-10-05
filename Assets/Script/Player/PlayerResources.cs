using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 플레이어 Money / Gem 보유량. 픽업 자석 반경은 이후 업그레이드에서 조정 가능.
/// </summary>
[DisallowMultipleComponent]
public class PlayerResources : MonoBehaviour
{
    public static PlayerResources Instance { get; private set; }

    [Header("픽업 (거리 기반)")]
    [Tooltip("이 거리(m) 안에 들어온 드랍 아이템이 플레이어 쪽으로 끌려옵니다. 업그레이드 시 이 값을 올리면 됩니다.")]
    [SerializeField] private float pickupMagnetRadius = 3f;

    [Header("보유량 (런타임)")]
    [SerializeField] private int money;
    [FormerlySerializedAs("jam")]
    [SerializeField] private int gem;

    /// <summary>자석에 걸리기 시작하는 거리 (미터).</summary>
    public float PickupMagnetRadius => pickupMagnetRadius;

    public int Money => Application.isPlaying ? AccountSession.Money : money;
    public int Gem => Application.isPlaying ? AccountSession.Gem : gem;
    private static readonly List<PlayerResources> activeWallets = new();

    /// <summary>Money 또는 Gem이 바뀔 때 (money, gem).</summary>
    public event Action<int, int> OnResourcesChanged;

    /// <summary>돈 또는 젬을 획득했을 때 (종류, 획득량). 차감은 호출하지 않습니다.</summary>
    public event Action<ShopCurrency, int> OnResourceGained;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void BeginSession()
    {
        Instance = null;
        activeWallets.Clear();
    }

    private void OnEnable()
    {
        activeWallets.Add(this);
        if (Instance == null) Instance = this;
        AccountSession.ResourcesChanged += OnAccountResourcesChanged;
        OnAccountResourcesChanged(AccountSession.Money, AccountSession.Gem);
    }

    private void OnDisable()
    {
        AccountSession.ResourcesChanged -= OnAccountResourcesChanged;
        activeWallets.Remove(this);
        if (Instance == this) Instance = activeWallets.Count > 0 ? activeWallets[0] : null;
    }

    private void OnAccountResourcesChanged(int currentMoney, int currentGem)
    {
        money = currentMoney;
        gem = currentGem;
        OnResourcesChanged?.Invoke(money, gem);
    }

    /// <summary>업그레이드·버프 등에서 자석 거리 조절.</summary>
    public void SetPickupMagnetRadius(float radius)
    {
        pickupMagnetRadius = Mathf.Max(0.1f, radius);
    }

    public void AddMoney(int amount)
    {
        int gained = AccountSession.Add(ShopCurrency.Money, amount);
        if (gained > 0) OnResourceGained?.Invoke(ShopCurrency.Money, gained);
    }

    public void AddGem(int amount)
    {
        int gained = AccountSession.Add(ShopCurrency.Gem, amount);
        if (gained > 0) OnResourceGained?.Invoke(ShopCurrency.Gem, gained);
    }

    public bool CanAfford(ShopCurrency currency, int amount)
    {
        return AccountSession.CanAfford(currency, amount);
    }

    /// <summary>보유량이 부족하면 false. 성공 시 차감합니다.</summary>
    public bool TrySpend(ShopCurrency currency, int amount)
    {
        return AccountSession.TrySpend(currency, amount);
    }

    /// <summary>개발자 치트 메뉴에서 돈과 젬을 한 번에 초기화합니다.</summary>
    public void SetAllToZero()
    {
        AccountSession.ClearResources();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        pickupMagnetRadius = Mathf.Max(0.1f, pickupMagnetRadius);
        money = Mathf.Max(0, money);
        gem = Mathf.Max(0, gem);
    }
#endif
}
