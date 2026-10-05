using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>계정 공용 보유 무기와 캐릭터별 로비 장착 설정. 디스크에는 저장하지 않습니다.</summary>
public static class AccountInventory
{
    private static readonly List<WeaponDataSO> weapons = new();
    private static readonly Dictionary<CharacterDataSO, PlayerReviveWeaponSnapshot> loadouts = new();
    private static bool purchasing;
    public static IReadOnlyList<WeaponDataSO> Weapons => weapons;
    public static event Action Changed;
    public static int Capacity => Mathf.Max(1, LobbyShopCatalogSO.Load() != null ? LobbyShopCatalogSO.Load().inventoryCapacity : 10);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        weapons.Clear(); loadouts.Clear(); purchasing = false; Changed = null;
    }

    public static bool Owns(WeaponDataSO weapon) => weapon != null && weapons.Exists(w => w != null && w.id == weapon.id);

    public static bool TryBuy(LobbyShopItem item)
    {
        if (!Application.isPlaying || purchasing || item == null || !item.IsValid || Owns(item.weapon) ||
            weapons.Count >= Capacity || !AccountSession.CanAfford(item.currency, item.price)) return false;
        purchasing = true;
        try
        {
            // 재화 변경 알림에서도 구매 완료 상태를 볼 수 있도록 먼저 보유 목록에 반영합니다.
            weapons.Add(item.weapon);
            if (!AccountSession.TrySpend(item.currency, item.price)) { weapons.Remove(item.weapon); return false; }
            Changed?.Invoke();
            return true;
        }
        finally { purchasing = false; }
    }

    public static void RememberLoadout(CharacterDataSO character, PlayerEquipmentController equipment)
    {
        if (character == null || equipment == null) return;
        // 전투 중 탄약이나 임시 효과는 로비 장착 기록에 저장하지 않습니다.
        loadouts[character] = new PlayerReviveWeaponSnapshot {
            slot0 = equipment.GetSlot(0), slot1 = equipment.GetSlot(1), activeSlotIndex = equipment.ActiveSlotIndex
        };
        Changed?.Invoke();
    }

    public static void ApplyLoadout(CharacterDataSO character, GameObject root)
    {
        if (!Application.isPlaying || character == null || root == null || !loadouts.TryGetValue(character, out var slots)) return;
        var equipment = root.GetComponentInChildren<PlayerEquipmentController>(true);
        if (equipment != null) equipment.ApplyReviveWeaponSnapshot(slots, root.transform);
    }
}
