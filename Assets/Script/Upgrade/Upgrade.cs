using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(UpgradeEffectRuntime))]
public class Upgrade : MonoBehaviour
{
    public const int SlotCount = 5;

    [Header("획득 업그레이드 슬롯 (0~4 순서대로 UI에 표시)")]
    [SerializeField] private UpgradeEffectSO[] slots = new UpgradeEffectSO[SlotCount];

    /// <summary>
    /// 슬롯이 변경되면 UI 갱신용으로 호출.
    /// </summary>
    public event Action OnSlotsChanged;

    public int Count => SlotCount;

    private void Awake()
    {
        // 기존 프리팹/씬에도 효과 런타임이 누락되지 않도록 안전하게 보장합니다.
        if (GetComponent<UpgradeEffectRuntime>() == null)
            gameObject.AddComponent<UpgradeEffectRuntime>();

        if (GetComponent<PlayerWeaponDamageModifiers>() == null)
            gameObject.AddComponent<PlayerWeaponDamageModifiers>();
    }

    public UpgradeEffectSO GetSlot(int index)
    {
        if (index < 0 || index >= SlotCount)
            return null;

        return slots[index];
    }

    public bool TrySetSlot(int index, UpgradeEffectSO upgrade)
    {
        if (index < 0 || index >= SlotCount)
            return false;

        slots[index] = upgrade;
        OnSlotsChanged?.Invoke();
        return true;
    }

    public bool TryClearSlot(int index)
    {
        return TrySetSlot(index, null);
    }

    public void ClearAllSlots()
    {
        for (int i = 0; i < SlotCount; i++)
            slots[i] = null;

        OnSlotsChanged?.Invoke();
    }

    private void OnValidate()
    {
        // 배열 크기가 바뀌더라도 항상 5칸을 유지합니다.
        if (slots == null || slots.Length != SlotCount)
        {
            Array.Resize(ref slots, SlotCount);
        }
    }
}
