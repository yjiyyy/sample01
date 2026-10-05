using System;
using UnityEngine;

/// <summary>실행 중 계정 상태에 캐릭터별 구매 기록을 반영합니다. 디스크에는 저장하지 않습니다.</summary>
public static class LobbyStatUpgradeState
{
    public static event Action Changed;
    private static bool _purchasing;

    public static int GetLevel(LobbyCharacterUpgradeSO profile, LobbyStatId id) =>
        Application.isPlaying && IsValid(profile) ? AccountSession.GetUpgradeLevel(profile.CharacterId, id) : 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void BeginSession()
    {
        _purchasing = false;
        Changed = null;
    }

    public static bool TryPurchase(LobbyCharacterUpgradeSO profile, LobbyStatId id, PlayerResources wallet)
    {
        if (!Application.isPlaying || _purchasing || !IsValid(profile) || profile.Config == null || wallet == null)
            return false;
        var entry = profile.GetEntry(id);
        int level = GetLevel(profile, id);
        if (entry == null || !LobbyStatUpgradeApplier.CanUpgrade(entry, profile.Config, level)) return false;
        int cost = LobbyStatUpgradeApplier.GetNextCost(entry, level);
        if (!wallet.CanAfford(ShopCurrency.Money, cost)) return false;

        // 지갑 변경 알림에서 구매가 다시 호출되어도 중복 결제하지 않습니다.
        _purchasing = true;
        try
        {
            if (!wallet.TrySpend(ShopCurrency.Money, cost)) return false;
            AccountSession.SetUpgradeLevel(profile.CharacterId, id, level + 1);
            Changed?.Invoke();
            return true;
        }
        finally { _purchasing = false; }
    }

    private static bool IsValid(LobbyCharacterUpgradeSO profile) =>
        profile != null && !string.IsNullOrEmpty(profile.CharacterId);
}
