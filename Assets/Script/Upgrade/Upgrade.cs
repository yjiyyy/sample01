using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(UpgradeEffectRuntime))]
public class Upgrade : MonoBehaviour
{
    public const int SlotCount = 5;
    public const int MaxStackPerSlot = 5;

    [Header("획득 업그레이드 슬롯 (0~4 순서대로 UI에 표시)")]
    [SerializeField] private UpgradeEffectSO[] slots = new UpgradeEffectSO[SlotCount];

    [Header("슬롯별 스택 (같은 칸에 겹친 장수, 1~5)")]
    [SerializeField] private int[] stackCounts = new int[SlotCount];

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

        if (GetComponent<PlayerCompanionUpgradeRuntime>() == null)
            gameObject.AddComponent<PlayerCompanionUpgradeRuntime>();

        if (GetComponent<PlayerCompanionCooldownModifiers>() == null)
            gameObject.AddComponent<PlayerCompanionCooldownModifiers>();

        if (GetComponent<PlayerReviveTicketRuntime>() == null)
            gameObject.AddComponent<PlayerReviveTicketRuntime>();

        if (GetComponent<PlayerBarrierUpgradeRuntime>() == null)
            gameObject.AddComponent<PlayerBarrierUpgradeRuntime>();

        if (GetComponent<PlayerGodShieldUpgradeRuntime>() == null)
            gameObject.AddComponent<PlayerGodShieldUpgradeRuntime>();

        if (GetComponent<PlayerOverdriveUpgradeRuntime>() == null)
            gameObject.AddComponent<PlayerOverdriveUpgradeRuntime>();

        NormalizeStacks();
    }

    public UpgradeEffectSO GetSlot(int index)
    {
        if (index < 0 || index >= SlotCount)
            return null;

        return slots[index];
    }

    /// <summary>빈 칸은 0, 장착 중이면 1~<see cref="MaxStackPerSlot"/>.</summary>
    public int GetStackCount(int index)
    {
        if (index < 0 || index >= SlotCount)
            return 0;
        if (slots[index] == null)
            return 0;

        int stack = stackCounts != null && index < stackCounts.Length ? stackCounts[index] : 1;
        return Mathf.Clamp(stack, 1, MaxStackPerSlot);
    }

    /// <summary>
    /// HUD/카드에 그릴 "+N". 1장이면 0(숨김), 2장이면 1 … 최대 스택 5면 4.
    /// 천사 시리즈는 같은 칸에 안 겹치므로 항상 0.
    /// </summary>
    public int GetDuplicationDisplay(int index)
    {
        UpgradeEffectSO so = GetSlot(index);
        if (so == null || IsAngelSeries(so))
            return 0;

        int stack = GetStackCount(index);
        return stack >= 2 ? stack - 1 : 0;
    }

    /// <summary>비-천사: 이미 장착된 같은 SO의 같은 칸 스택. 없으면 0. 천사는 항상 0(칸마다 1장).</summary>
    public int GetSameSlotStack(UpgradeEffectSO upgrade)
    {
        if (upgrade == null || IsAngelSeries(upgrade))
            return 0;

        int index = FindSlotIndex(upgrade);
        return index >= 0 ? GetStackCount(index) : 0;
    }

    /// <summary>상점 카드 Duplication용. 천사는 0, 이미 설치됐으면 스택과 무관하게 +1.</summary>
    public int GetCardDuplicationDisplay(UpgradeEffectSO upgrade)
    {
        if (upgrade == null || IsAngelSeries(upgrade))
            return 0;

        return FindSlotIndex(upgrade) >= 0 ? 1 : 0;
    }

    /// <summary>일반 업그레이드가 같은 칸에 이미 최대 스택이면 상점에 안 넣습니다.</summary>
    public bool IsExcludedFromShop(UpgradeEffectSO upgrade)
    {
        if (upgrade == null)
            return true;
        if (IsAngelSeries(upgrade))
            return false;
        return GetSameSlotStack(upgrade) >= MaxStackPerSlot;
    }

    public int FindSlotIndex(UpgradeEffectSO upgrade)
    {
        if (upgrade == null)
            return -1;

        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i] == upgrade)
                return i;
        }

        return -1;
    }

    public int FindFirstEmptySlotIndex()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i] == null)
                return i;
        }

        return -1;
    }

    public bool HasEmptySlot() => FindFirstEmptySlotIndex() >= 0;

    /// <summary>같은 칸에 더 쌓을 수 있으면 true (천사 제외).</summary>
    public bool CanStackInPlace(UpgradeEffectSO upgrade)
    {
        if (upgrade == null || IsAngelSeries(upgrade))
            return false;

        int index = FindSlotIndex(upgrade);
        if (index < 0)
            return false;

        return GetStackCount(index) < MaxStackPerSlot;
    }

    /// <summary>빈 칸이 필요하거나(신규/천사), 같은 칸 스택이 가능한지.</summary>
    public bool NeedsEmptyOrReplaceSlot(UpgradeEffectSO upgrade)
    {
        if (upgrade == null)
            return true;
        if (CanStackInPlace(upgrade))
            return false;
        return !HasEmptySlot();
    }

    public bool TrySetSlot(int index, UpgradeEffectSO upgrade, int stackCount = 1)
    {
        if (index < 0 || index >= SlotCount)
            return false;

        EnsureStackArray();
        slots[index] = upgrade;
        if (upgrade == null)
            stackCounts[index] = 0;
        else if (IsAngelSeries(upgrade))
            stackCounts[index] = 1;
        else
            stackCounts[index] = Mathf.Clamp(stackCount, 1, MaxStackPerSlot);

        OnSlotsChanged?.Invoke();
        return true;
    }

    public bool TryClearSlot(int index)
    {
        return TrySetSlot(index, null, 0);
    }

    public bool TryAddStackAt(int index)
    {
        if (index < 0 || index >= SlotCount)
            return false;
        if (slots[index] == null || IsAngelSeries(slots[index]))
            return false;

        EnsureStackArray();
        int next = GetStackCount(index) + 1;
        if (next > MaxStackPerSlot)
            return false;

        stackCounts[index] = next;
        OnSlotsChanged?.Invoke();
        return true;
    }

    /// <summary>스택 1 감소. 1 이하면 칸을 비웁니다.</summary>
    public bool TryConsumeOneStack(int index)
    {
        if (index < 0 || index >= SlotCount)
            return false;
        if (slots[index] == null)
            return false;

        EnsureStackArray();
        int stack = GetStackCount(index);
        if (stack <= 1)
            return TryClearSlot(index);

        stackCounts[index] = stack - 1;
        OnSlotsChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 구매 장착. 같은 칸 스택 가능하면 쌓고, 아니면 빈 칸(1번부터).
    /// 빈 칸이 없고 스택도 불가면 false (버리기 선택 필요).
    /// </summary>
    public bool TryInstallFromShop(UpgradeEffectSO upgrade)
    {
        if (upgrade == null)
            return false;

        if (CanStackInPlace(upgrade))
        {
            int index = FindSlotIndex(upgrade);
            return TryAddStackAt(index);
        }

        int empty = FindFirstEmptySlotIndex();
        if (empty < 0)
            return false;

        return TrySetSlot(empty, upgrade, 1);
    }

    /// <summary>선택한 칸을 비우고 새 업그레이드를 1장 넣습니다.</summary>
    public bool TryReplaceSlotFromShop(int index, UpgradeEffectSO upgrade)
    {
        if (upgrade == null || index < 0 || index >= SlotCount)
            return false;

        return TrySetSlot(index, upgrade, 1);
    }

    public void ClearAllSlots()
    {
        EnsureStackArray();
        for (int i = 0; i < SlotCount; i++)
        {
            slots[i] = null;
            stackCounts[i] = 0;
        }

        OnSlotsChanged?.Invoke();
    }

    public void CopySlotsTo(UpgradeEffectSO[] effectsOut, int[] stacksOut)
    {
        if (effectsOut == null)
            return;

        EnsureStackArray();
        for (int i = 0; i < SlotCount; i++)
        {
            if (i < effectsOut.Length)
                effectsOut[i] = slots[i];
            if (stacksOut != null && i < stacksOut.Length)
                stacksOut[i] = GetStackCount(i);
        }
    }

    public void ApplySlotSnapshot(UpgradeEffectSO[] effects, int[] stacks)
    {
        EnsureStackArray();
        for (int i = 0; i < SlotCount; i++)
        {
            UpgradeEffectSO so = effects != null && i < effects.Length ? effects[i] : null;
            int stack = stacks != null && i < stacks.Length ? stacks[i] : 1;
            slots[i] = so;
            stackCounts[i] = so == null ? 0 : (IsAngelSeries(so) ? 1 : Mathf.Clamp(stack, 1, MaxStackPerSlot));
        }

        OnSlotsChanged?.Invoke();
    }

    public static bool IsAngelSeries(UpgradeEffectSO upgrade)
    {
        if (upgrade == null)
            return false;

        if (!string.IsNullOrEmpty(upgrade.id) &&
            upgrade.id.StartsWith("05_", StringComparison.Ordinal))
            return true;

        string typeName = upgrade.GetType().Name;
        return typeName.StartsWith("Upgrade_05_", StringComparison.Ordinal);
    }

    private void EnsureStackArray()
    {
        if (stackCounts == null || stackCounts.Length != SlotCount)
            Array.Resize(ref stackCounts, SlotCount);
    }

    private void NormalizeStacks()
    {
        EnsureStackArray();
        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i] == null)
            {
                stackCounts[i] = 0;
                continue;
            }

            if (IsAngelSeries(slots[i]))
                stackCounts[i] = 1;
            else if (stackCounts[i] < 1)
                stackCounts[i] = 1;
            else if (stackCounts[i] > MaxStackPerSlot)
                stackCounts[i] = MaxStackPerSlot;
        }
    }

    private void OnValidate()
    {
        // 배열 크기가 바뀌더라도 항상 5칸을 유지합니다.
        if (slots == null || slots.Length != SlotCount)
            Array.Resize(ref slots, SlotCount);

        EnsureStackArray();
        NormalizeStacks();
    }
}
