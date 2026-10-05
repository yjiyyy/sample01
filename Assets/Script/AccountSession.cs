using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>실행 중에만 유지하는 계정 상태. 향후 세이브 시스템에서 읽고 복원할 대상입니다.</summary>
public static class AccountSession
{
    public static int Money { get; private set; }
    public static int Gem { get; private set; }
    public static CharacterDataSO SelectedCharacter { get; set; }
    public static event Action<int, int> ResourcesChanged;
    private static readonly Dictionary<string, Dictionary<LobbyStatId, int>> upgrades = new();

    // 도메인 리로드를 끈 에디터에서도 Play를 다시 시작하면 초기화합니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void BeginSession()
    {
        Money = Gem = 0;
        SelectedCharacter = null;
        upgrades.Clear();
        ResourcesChanged = null;
    }

    public static int Add(ShopCurrency currency, int amount)
    {
        int before = currency == ShopCurrency.Gem ? Gem : Money;
        int after = (int)Math.Max(0L, Math.Min(int.MaxValue, (long)before + amount));
        if (after == before) return 0;
        if (currency == ShopCurrency.Gem) Gem = after;
        else Money = after;
        ResourcesChanged?.Invoke(Money, Gem);
        return after - before;
    }

    public static bool CanAfford(ShopCurrency currency, int amount) =>
        amount <= 0 || (currency == ShopCurrency.Gem ? Gem : Money) >= amount;

    public static bool TrySpend(ShopCurrency currency, int amount)
    {
        if (amount <= 0) return true;
        if (!CanAfford(currency, amount)) return false;
        Add(currency, -amount);
        return true;
    }

    public static void ClearResources()
    {
        if (Money == 0 && Gem == 0) return;
        Money = Gem = 0;
        ResourcesChanged?.Invoke(Money, Gem);
    }

    public static int GetUpgradeLevel(string characterId, LobbyStatId stat) =>
        !string.IsNullOrEmpty(characterId) && upgrades.TryGetValue(characterId, out var levels) &&
        levels.TryGetValue(stat, out int level) ? level : 0;

    internal static void SetUpgradeLevel(string characterId, LobbyStatId stat, int level)
    {
        if (string.IsNullOrEmpty(characterId)) return;
        if (!upgrades.TryGetValue(characterId, out var levels))
            upgrades.Add(characterId, levels = new Dictionary<LobbyStatId, int>());
        levels[stat] = Math.Max(0, level);
    }
}
