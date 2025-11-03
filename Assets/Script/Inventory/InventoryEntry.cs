[System.Serializable]
public class InventoryEntry
{
    public string id; // WeaponDataSO.id
    public int count; // 소비형이면 개수(스택), 장착무기는 보통 1

    public InventoryEntry(string id, int count)
    {
        this.id = id;
        this.count = count;
    }
}