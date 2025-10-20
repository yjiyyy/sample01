using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WeaponRegistry", menuName = "Dev/Weapon Registry")]
public class WeaponRegistrySO : ScriptableObject
{
    [System.Serializable]
    public class WeaponEntry
    {
        public string id;           // 기본: prefab.name
        public string displayName;  // 기본: prefab.name
        public GameObject prefab;   // WeaponBehavior 포함 프리팹
    }

    [SerializeField] public List<WeaponEntry> entries = new List<WeaponEntry>();

    public IReadOnlyList<WeaponEntry> GetSortedEntries()
    {
        // displayName 오름차순 정렬된 복사본 반환
        var copy = new List<WeaponEntry>(entries.Count);
        copy.AddRange(entries);
        copy.Sort((a, b) =>
        {
            string sa = a != null ? a.displayName : "";
            string sb = b != null ? b.displayName : "";
            return string.Compare(sa, sb, System.StringComparison.OrdinalIgnoreCase);
        });
        return copy;
    }
}