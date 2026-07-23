using UnityEngine;

/// <summary>
/// EnemyConfig SO로 적을 스폰합니다.
/// 바디는 풀에서 랜덤, 스탯·공격은 SO 고정, Head/Hair/피부는 풀이 있으면 랜덤 적용합니다.
/// </summary>
public static class EnemyConfigSpawner
{
    /// <summary>
    /// bodyPrefab을 Instantiate한 뒤 Config·외형을 적용합니다.
    /// bodyPrefab은 보통 config.TryPickBodyPrefab으로 미리 고른 값을 넣습니다 (위치 검사와 동일 바디 보장).
    /// </summary>
    public static GameObject Spawn(EnemyConfig config, GameObject bodyPrefab, Vector3 position, Quaternion rotation)
    {
        if (config == null)
        {
            Debug.LogWarning("[EnemyConfigSpawner] EnemyConfig가 null입니다.");
            return null;
        }

        if (bodyPrefab == null)
        {
            Debug.LogWarning($"[EnemyConfigSpawner] '{config.name}' 바디 프리팹이 null입니다. Appearance Pool의 Body Prefabs를 확인하세요.");
            return null;
        }

        GameObject enemy = Object.Instantiate(bodyPrefab, position, rotation);
        ApplyConfigAndAppearance(config, enemy);
        return enemy;
    }

    /// <summary>Config를 Facade에 넣고, 외형 풀을 BodyPartSlots에 적용합니다.</summary>
    public static void ApplyConfigAndAppearance(EnemyConfig config, GameObject enemyInstance)
    {
        if (config == null || enemyInstance == null)
            return;

        EnemyFacade facade = enemyInstance.GetComponent<EnemyFacade>();
        if (facade == null)
            facade = enemyInstance.GetComponentInChildren<EnemyFacade>(true);

        if (facade != null)
        {
            facade.config = config;
            // Instantiate 직후 Awake가 이미 돌았을 수 있으므로, 새 Config로 다시 동기화
            if (facade.autoSync)
                facade.ApplyToComponents();
        }
        else
        {
            Debug.LogWarning(
                $"[EnemyConfigSpawner] '{enemyInstance.name}'에 EnemyFacade가 없습니다. Config를 적용할 수 없습니다.",
                enemyInstance);
        }

        config.ApplyRandomAppearance(enemyInstance);
    }
}
