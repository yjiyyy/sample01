using UnityEngine;

/// <summary>
/// 로비 씬. 캐릭터 선택 화면에서 선택한 캐릭터를 스폰 포인트에 생성합니다.
/// Inspector에서 CharacterSpawnPoint를 지정하세요. (비워두면 'CharacterSpawnPoint' 이름으로 찾습니다)
/// </summary>
public class LobbyController : MonoBehaviour
{
    [Header("캐릭터 스폰")]
    [Tooltip("캐릭터가 스폰될 위치. 비워두면 'CharacterSpawnPoint' 이름으로 찾습니다.")]
    [SerializeField] private Transform characterSpawnPoint;

    private GameObject _spawnedCharacter;

    private void Start()
    {
        EnsureGameState();
        ResolveSpawnPoint();
        SpawnSelectedCharacter();
    }

    private void EnsureGameState()
    {
        if (GameState.Instance == null)
        {
            var go = new GameObject("GameState");
            go.AddComponent<GameState>();
            Debug.Log("[LobbyController] GameState가 없어 생성했습니다. 캐릭터 선택을 거치지 않고 진입한 경우 SelectedCharacter가 비어 있을 수 있습니다.");
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
            go.transform.position = new Vector3(0, 0, 0);
            characterSpawnPoint = go.transform;
            Debug.Log("[LobbyController] CharacterSpawnPoint를 자동 생성했습니다. 위치를 조정하세요.");
        }
    }

    private void SpawnSelectedCharacter()
    {
        if (characterSpawnPoint == null) return;

        var data = GameState.Instance?.SelectedCharacter;
        if (data == null)
        {
            Debug.LogWarning("[LobbyController] 선택된 캐릭터가 없습니다. Character Selection 씬에서 캐릭터를 선택한 후 로비로 진입하세요.");
            return;
        }

        var prefab = data.modelPrefab;
        if (prefab == null)
        {
            Debug.LogWarning($"[LobbyController] {data.name}에 modelPrefab이 지정되지 않았습니다.");
            return;
        }

        if (_spawnedCharacter != null)
            Destroy(_spawnedCharacter);

        _spawnedCharacter = Instantiate(prefab, characterSpawnPoint.position, characterSpawnPoint.rotation);
        _spawnedCharacter.transform.SetParent(characterSpawnPoint);
        var displayName = !string.IsNullOrEmpty(data.displayName) ? data.displayName : data.name;
        _spawnedCharacter.name = $"Player_{displayName}";

        // 로비에서는 이동·전투 입력 비활성화
        var pm = _spawnedCharacter.GetComponentInChildren<PlayerMovement>();
        if (pm != null) pm.enabled = false;
        var pwc = _spawnedCharacter.GetComponentInChildren<PlayerWeaponController>();
        if (pwc != null) pwc.enabled = false;
    }
}
