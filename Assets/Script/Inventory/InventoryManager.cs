using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 간단한 인벤토리 매니저: 소지(특수소모품) 관리 및 장착 상태 관리
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    // 소비형(특수무기) 목록: id -> count
    private Dictionary<string, int> consumables = new Dictionary<string, int>();

    // 현재 장착된 무기 id (빈 문자열 또는 null이면 없음)
    private string equippedWeaponId = null;

    // 이벤트: 인벤토리 또는 장착 상태가 바뀔 때 발행
    public event Action OnInventoryChanged;
    public event Action<string> OnEquipChanged; // 새로 장착된 id (null이면 해제)

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    // 소비형 추가(예: 수류탄 3개)
    public void AddConsumable(string id, int amount)
    {
        if (string.IsNullOrEmpty(id) || amount <= 0) return;
        if (!consumables.ContainsKey(id)) consumables[id] = 0;
        consumables[id] += amount;
        OnInventoryChanged?.Invoke();
    }

    // 소비형 사용(실제로 소모)
    // 반환값: 사용 성공 시 true(개수가 1 이상이어야 함)
    public bool UseConsumable(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (!consumables.TryGetValue(id, out var cnt) || cnt <= 0) return false;
        consumables[id] = cnt - 1;
        if (consumables[id] <= 0) consumables.Remove(id);
        OnInventoryChanged?.Invoke();
        return true;
    }

    // 전체 소비형 목록 반환 (복사본)
    public List<InventoryEntry> GetAllConsumables()
    {
        return consumables.Select(kv => new InventoryEntry(kv.Key, kv.Value)).ToList();
    }

    // 장착
    public void Equip(string id)
    {
        equippedWeaponId = id;
        OnEquipChanged?.Invoke(equippedWeaponId);
        OnInventoryChanged?.Invoke();
    }

    public void Unequip()
    {
        equippedWeaponId = null;
        OnEquipChanged?.Invoke(equippedWeaponId);
        OnInventoryChanged?.Invoke();
    }

    public string GetEquippedWeaponId()
    {
        return equippedWeaponId;
    }

    // 디버그용 빠른 초기화 (옵션)
    [ContextMenu("AddTestConsumables")]
    public void AddTestConsumables()
    {
        AddConsumable("grenade", 3);
        AddConsumable("smoke", 2);
    }
}