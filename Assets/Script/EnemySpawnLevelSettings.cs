using System;
using UnityEngine;

/// <summary>
/// 레벨별 스폰 풀 항목. weight는 상대 가중치(합 100 필수 아님).
/// </summary>
[Serializable]
public class EnemySpawnEntry
{
    [Tooltip("스폰할 적 종류(EnemyConfig). 외형은 SO의 Appearance Pool에서 랜덤 조합됩니다.")]
    public EnemyConfig config;

    [Min(0f)]
    public float weight = 1f;
}

/// <summary>
/// StageManager 레벨(0=표시 Lv.1) 1칸에 대응하는 스폰 설정.
/// </summary>
[Serializable]
public class EnemySpawnLevelSettings
{
    [Tooltip("0이면 무제한. 이 레벨에서 동시에 살아 있을 수 있는 수(이 레벨에서 스폰한 몬스터만).")]
    public int maxConcurrentAlive = 0;

    [Min(0.01f)]
    public float spawnInterval = 2f;

    public EnemySpawnEntry[] enemies;

    public bool TryPickConfig(out EnemyConfig config)
    {
        config = null;
        if (enemies == null || enemies.Length == 0)
            return false;

        float totalWeight = 0f;
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemySpawnEntry entry = enemies[i];
            if (entry == null || entry.config == null || entry.weight <= 0f)
                continue;

            totalWeight += entry.weight;
        }

        if (totalWeight <= 0f)
            return false;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float accumulated = 0f;
        EnemyConfig fallback = null;

        for (int i = 0; i < enemies.Length; i++)
        {
            EnemySpawnEntry entry = enemies[i];
            if (entry == null || entry.config == null || entry.weight <= 0f)
                continue;

            fallback = entry.config;
            accumulated += entry.weight;
            if (roll <= accumulated)
            {
                config = entry.config;
                return true;
            }
        }

        config = fallback;
        return config != null;
    }
}
