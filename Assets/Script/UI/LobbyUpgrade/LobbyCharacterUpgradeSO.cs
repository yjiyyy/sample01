using UnityEngine;

/// <summary>기본 능력은 CharacterDataSO에서, 강화 규칙은 이 에셋에서 읽습니다.</summary>
[CreateAssetMenu(menuName = "Game/Lobby/Character Upgrades", fileName = "Upgrade_Character")]
public class LobbyCharacterUpgradeSO : ScriptableObject
{
    [InspectorName("대상 캐릭터")] public CharacterDataSO character;
    [SerializeField, HideInInspector] private string characterId;
    [InspectorName("업그레이드 목록 (표시 순서)")]
    public LobbyStatUpgradeEntry[] stats = LobbyStatUpgradeCatalogSO.CreateDefaultStats();
    public string CharacterId => characterId;
    public PlayerConfig Config => character != null ? character.playerConfig : null;

    public LobbyStatUpgradeEntry GetEntry(LobbyStatId id)
    {
        if (stats != null)
            foreach (var entry in stats)
                if (entry != null && entry.id == id) return entry;
        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        characterId = character != null
            ? UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(character)) : "";
    }
#endif
}
