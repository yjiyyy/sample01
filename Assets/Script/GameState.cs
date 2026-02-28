using UnityEngine;

/// <summary>
/// 씬 전환 후에도 유지되는 게임 상태.
/// DontDestroyOnLoad 싱글톤으로, 선택한 캐릭터 등을 저장합니다.
/// </summary>
public class GameState : MonoBehaviour
{
    public static GameState Instance { get; private set; }

    /// <summary>
    /// 선택된 캐릭터 데이터. CharacterSelect에서 설정, Lobby에서 사용합니다.
    /// </summary>
    public CharacterDataSO SelectedCharacter { get; set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
