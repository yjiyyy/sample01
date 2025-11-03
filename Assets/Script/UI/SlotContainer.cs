using System.Collections.Generic;
using UnityEngine;

// Slot 프리팹을 풀링하여 컨테이너에 맞게 갱신한다.
public class SlotContainer : MonoBehaviour
{
    [Tooltip("SlotView 프리팹 (간단한 UI 프리팹)")]
    public SlotView slotPrefab;

    [Tooltip("슬롯들이 위치할 부모 Transform")]
    public Transform contentParent;

    // 미리 생성해 두는 초기 풀 사이즈 (옵션)
    public int initialPoolSize = 4;

    private List<SlotView> pool = new List<SlotView>();

    private void Awake()
    {
        if (slotPrefab == null) return;
        for (int i = 0; i < initialPoolSize; i++)
        {
            var s = Instantiate(slotPrefab, contentParent);
            s.gameObject.SetActive(false);
            pool.Add(s);
        }
    }

    private SlotView GetSlot()
    {
        foreach (var s in pool)
        {
            if (!s.gameObject.activeSelf) { s.gameObject.SetActive(true); return s; }
        }
        var ns = Instantiate(slotPrefab, contentParent);
        pool.Add(ns);
        return ns;
    }

    // 주어진 데이터 리스트로 화면 갱신
    public void Refresh(List<InventoryEntry> entries)
    {
        // 비활성화된 슬롯도 포함하여 재활용
        // 1) 필요한 슬롯 수를 활성화
        int idx = 0;
        if (entries != null)
        {
            foreach (var e in entries)
            {
                var slot = GetSlot();
                var data = WeaponDatabase.Instance?.Get(e.id);
                slot.SetData(data, e.count);
                idx++;
            }
        }

        // 2) 남아있는 활성 슬롯 비활성화
        for (int i = idx; i < pool.Count; i++)
        {
            pool[i].Clear();
            pool[i].gameObject.SetActive(false);
        }
    }

    // 단일 장착 표시용: 하나의 InventoryEntry로 갱신
    public void RefreshSingle(InventoryEntry entry)
    {
        // ensure at least one slot visible
        foreach (var s in pool) { s.Clear(); s.gameObject.SetActive(false); }

        var slot = GetSlot();
        var data = WeaponDatabase.Instance?.Get(entry?.id);
        slot.SetData(data, entry?.count ?? 0);

        // deactive extras
        for (int i = 1; i < pool.Count; i++)
        {
            pool[i].gameObject.SetActive(false);
        }
    }

    // clear all
    public void ClearAll()
    {
        foreach (var s in pool)
        {
            s.Clear();
            s.gameObject.SetActive(false);
        }
    }
}