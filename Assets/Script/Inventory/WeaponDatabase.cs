using System.Collections.Generic;
using UnityEngine;

public class WeaponDatabase : MonoBehaviour
{
    public static WeaponDatabase Instance { get; private set; }

    // id -> WeaponDataSO 매핑
    private Dictionary<string, WeaponDataSO> db = new Dictionary<string, WeaponDataSO>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        LoadAllFromResources();
    }

    // Resources/WeaponSO 폴더에서 자동 로드
    private void LoadAllFromResources()
    {
        db.Clear();
        var all = Resources.LoadAll<WeaponDataSO>("WeaponSO");
        foreach (var so in all)
        {
            if (string.IsNullOrEmpty(so.id))
            {
                Debug.LogWarning($"WeaponDataSO '{so.name}' has empty id. Skipped.");
                continue;
            }
            if (db.ContainsKey(so.id))
            {
                Debug.LogWarning($"Duplicate WeaponData id '{so.id}' found. Overwriting.");
            }
            db[so.id] = so;
        }
    }

    public WeaponDataSO Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (db.TryGetValue(id, out var so)) return so;
        return null;
    }

    public IEnumerable<WeaponDataSO> GetAll()
    {
        return db.Values;
    }
}