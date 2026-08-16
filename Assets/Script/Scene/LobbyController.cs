using UnityEngine;

/// <summary>
/// 로비 씬. 이전 씬에서 고른 캐릭터를 스폰합니다.
/// 고른 캐릭터가 없으면 Inspector의 테스트용 캐릭터를 사용합니다.
/// </summary>
public class LobbyController : MonoBehaviour
{
    [Header("캐릭터 스폰")]
    [Tooltip("캐릭터가 스폰될 위치. 비워두면 'CharacterSpawnPoint' 이름으로 찾습니다.")]
    [SerializeField] private Transform characterSpawnPoint;

    [Header("테스트용 (이전 씬 데이터가 없을 때)")]
    [Tooltip("캐릭터 선택을 거치지 않고 로비만 켰을 때 사용할 캐릭터 데이터.")]
    [SerializeField] private CharacterDataSO fallbackCharacter;

    [Tooltip("fallbackCharacter가 없거나 modelPrefab이 비어 있을 때 직접 스폰할 모델.")]
    [SerializeField] private GameObject fallbackModelPrefab;

    private GameObject _spawnedCharacter;

    private void Start()
    {
        EnsureGameState();
        ResolveSpawnPoint();
        SpawnSelectedCharacter();

        var menu = FindFirstObjectByType<LobbyMenuUI>();
        if (menu != null)
            menu.BindResources();
    }

    private void EnsureGameState()
    {
        if (GameState.Instance == null)
        {
            var go = new GameObject("GameState");
            go.AddComponent<GameState>();
            Debug.Log("[LobbyController] GameState가 없어 생성했습니다. 테스트용 캐릭터를 사용할 수 있습니다.");
        }
    }

    private void ResolveSpawnPoint()
    {
        if (characterSpawnPoint != null) return;

        var spawnGO = GameObject.Find("CharacterSpawnPoint");
        if (spawnGO != null)
        {
            characterSpawnPoint = spawnGO.transform;
        }
        else
        {
            var go = new GameObject("CharacterSpawnPoint");
            go.transform.position = new Vector3(-1.35f, 0f, -3.2f);
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            characterSpawnPoint = go.transform;
            Debug.Log("[LobbyController] CharacterSpawnPoint를 자동 생성했습니다. 위치를 조정하세요.");
        }
    }

    private void SpawnSelectedCharacter()
    {
        if (characterSpawnPoint == null) return;

        var data = GameState.Instance != null ? GameState.Instance.SelectedCharacter : null;
        bool usedFallback = false;
        if (data == null)
        {
            data = fallbackCharacter;
            usedFallback = data != null;
        }

        GameObject prefab = data != null ? data.modelPrefab : null;
        if (prefab == null)
        {
            prefab = fallbackModelPrefab;
            usedFallback = prefab != null;
        }

        if (prefab == null)
        {
            Debug.LogWarning("[LobbyController] 스폰할 캐릭터가 없습니다. 캐릭터 선택 씬에서 고르거나, Inspector에 테스트용 캐릭터를 넣어 주세요.");
            return;
        }

        if (_spawnedCharacter != null)
            Destroy(_spawnedCharacter);

        _spawnedCharacter = Instantiate(prefab, characterSpawnPoint.position, characterSpawnPoint.rotation);
        _spawnedCharacter.transform.SetParent(characterSpawnPoint);

        var displayName = data != null && !string.IsNullOrEmpty(data.displayName) ? data.displayName : prefab.name;
        _spawnedCharacter.name = $"Player_{displayName}";

        DisableGameplayInput(_spawnedCharacter);

        if (usedFallback)
            Debug.Log($"[LobbyController] 이전 씬 데이터가 없어 테스트용 캐릭터를 배치했습니다: {displayName}");
    }

    /// <summary>
    /// 로비에서는 전시만 하므로 이동·전투 입력을 끕니다.
    /// </summary>
    private static void DisableGameplayInput(GameObject model)
    {
        if (model == null) return;

        var pm = model.GetComponentInChildren<PlayerMovement>();
        if (pm != null) pm.enabled = false;

        var pwc = model.GetComponentInChildren<PlayerWeaponController>();
        if (pwc != null) pwc.enabled = false;

        var rb = model.GetComponentInChildren<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
    }
}
