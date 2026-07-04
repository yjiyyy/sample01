using UnityEngine;

/// <summary>
/// 몬스터 사망·아이템 박스 등에서 공통으로 쓰는 아이템 방출 로직.
/// </summary>
public static class ItemDropSpawner
{
    /// <summary>
    /// 몬스터 드랍용. rollCount번 확률 체크하며, 한 번에 여러 entry가 동시에 드랍될 수 있습니다.
    /// </summary>
    public static void DropItems(
        Vector3 origin,
        int totalDropCountMin,
        int totalDropCountMax,
        ItemDropEntry[] dropEntries,
        LayerMask dropGroundLayerMask,
        float heightOffset = 0.3f)
    {
        DropItemsInternal(
            origin,
            totalDropCountMin,
            totalDropCountMax,
            dropEntries,
            dropGroundLayerMask,
            heightOffset,
            oneItemPerRoll: false);
    }

    /// <summary>
    /// 아이템 박스용. min~max 사이 개수를 정한 뒤, 그만큼 아이템을 1개씩 방출합니다.
    /// </summary>
    public static void DropItemsForItemBox(
        Vector3 origin,
        int totalDropCountMin,
        int totalDropCountMax,
        ItemDropEntry[] dropEntries,
        LayerMask dropGroundLayerMask,
        float heightOffset = 0.3f)
    {
        DropItemsInternal(
            origin,
            totalDropCountMin,
            totalDropCountMax,
            dropEntries,
            dropGroundLayerMask,
            heightOffset,
            oneItemPerRoll: true);
    }

    private static void DropItemsInternal(
        Vector3 origin,
        int totalDropCountMin,
        int totalDropCountMax,
        ItemDropEntry[] dropEntries,
        LayerMask dropGroundLayerMask,
        float heightOffset,
        bool oneItemPerRoll)
    {
        if (dropEntries == null || dropEntries.Length == 0)
            return;

        totalDropCountMin = Mathf.Max(0, totalDropCountMin);
        totalDropCountMax = Mathf.Max(totalDropCountMin, totalDropCountMax);

        int rollCount = Random.Range(totalDropCountMin, totalDropCountMax + 1);
        if (rollCount <= 0)
            return;

        Vector3 dropPos = origin + Vector3.up * heightOffset;

        if (oneItemPerRoll)
        {
            for (int i = 0; i < rollCount; i++)
            {
                if (TrySpawnOneFromEntries(dropEntries, dropPos, dropGroundLayerMask, heightOffset))
                    continue;

                TrySpawnFirstAvailableEntry(dropEntries, dropPos, dropGroundLayerMask, heightOffset);
            }

            return;
        }

        for (int slot = 0; slot < rollCount; slot++)
        {
            foreach (var entry in dropEntries)
            {
                if (entry == null || entry.itemPrefab == null)
                    continue;
                if (entry.dropChance <= 0f || Random.value > entry.dropChance)
                    continue;

                SpawnEntry(entry, dropPos, dropGroundLayerMask, heightOffset);
            }
        }
    }

    private static bool TrySpawnOneFromEntries(
        ItemDropEntry[] dropEntries,
        Vector3 dropPos,
        LayerMask dropGroundLayerMask,
        float heightOffset)
    {
        if (dropEntries == null || dropEntries.Length == 0)
            return false;

        int startIndex = Random.Range(0, dropEntries.Length);
        for (int i = 0; i < dropEntries.Length; i++)
        {
            var entry = dropEntries[(startIndex + i) % dropEntries.Length];
            if (entry == null || entry.itemPrefab == null)
                continue;
            if (entry.dropChance <= 0f || Random.value > entry.dropChance)
                continue;

            SpawnEntry(entry, dropPos, dropGroundLayerMask, heightOffset);
            return true;
        }

        return false;
    }

    private static bool TrySpawnFirstAvailableEntry(
        ItemDropEntry[] dropEntries,
        Vector3 dropPos,
        LayerMask dropGroundLayerMask,
        float heightOffset)
    {
        if (dropEntries == null)
            return false;

        for (int i = 0; i < dropEntries.Length; i++)
        {
            var entry = dropEntries[i];
            if (entry == null || entry.itemPrefab == null)
                continue;

            SpawnEntry(entry, dropPos, dropGroundLayerMask, heightOffset);
            return true;
        }

        return false;
    }

    private static void SpawnEntry(
        ItemDropEntry entry,
        Vector3 dropPos,
        LayerMask dropGroundLayerMask,
        float heightOffset)
    {
        Vector3 offset = new Vector3(Random.Range(-0.2f, 0.2f), 0f, Random.Range(-0.2f, 0.2f));
        GameObject go = Object.Instantiate(entry.itemPrefab, dropPos + offset, Quaternion.identity);

        var arc = go.AddComponent<ItemDropArc>();
        float up = Random.Range(5.5f, 8f);
        float outMag = Random.Range(0.8f, 1.5f);
        Vector3 vel = Vector3.up * up
            + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized * outMag;
        arc.StartArc(vel, dropGroundLayerMask);
    }
}
