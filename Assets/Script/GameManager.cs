using System.Linq;
using UnityEngine;

// GameManager는 UI의 위치(anchoredPosition 등)를 건드리지 않습니다(C2).
// 대신 hudCanvas 아래에 미리 배치한 HPUIControllerBase들을 찾아서 PlayerHealth를 할당합니다.
// 플레이어가 런타임에 추가되면 RegisterPlayerHealth를 호출하여 빈 슬롯에 할당해 줍니다.

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("플레이어 관련")]
    // 기존 playerTransform은 유지(호환성). 여러 플레이어를 지원하려면 PlayerHealth 컴포넌트들을 사용합니다.
    public Transform playerTransform;

    [Tooltip("HUD로 사용할 Canvas (예: 가상패드가 있는 Canvas). " +
             "HUD 아래에 미리 배치한 HP UI(HPUIControllerBase)를 넣어두세요.")]
    public Canvas hudCanvas;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 씬에 미리 배치한 HP UI와 현재 씬의 PlayerHealth들을 연결
        AssignExistingPlayerUIs();
    }

    // 씬에 배치된 HPUIControllerBase들을 찾아 현재 씬의 PlayerHealth들과 매칭해서 초기 연결을 수행합니다.
    // 매칭 기준: 발견 순서(필요시 이 부분을 이름 기반 매칭 등으로 변경 가능)
    public void AssignExistingPlayerUIs()
    {
        if (hudCanvas == null)
        {
            Debug.LogWarning("[GameManager] hudCanvas가 설정되지 않았습니다. 씬에 배치한 HP UI를 자동으로 연결할 수 없습니다.");
            return;
        }

        // HUD 아래에 있는 HPUIControllerBase 컴포넌트들(비활성 오브젝트 포함)을 가져옵니다.
        var uiControllers = hudCanvas.GetComponentsInChildren<HPUIControllerBase>(true);

        // 씬에 존재하는 PlayerHealth들을 찾아서 연결합니다.
        var players = FindObjectsOfType<PlayerHealth>();

        int count = Mathf.Min(uiControllers.Length, players.Length);
        for (int i = 0; i < count; i++)
        {
            if (uiControllers[i] != null && players[i] != null)
            {
                uiControllers[i].Initialize(players[i]);
            }
        }

        if (players.Length > uiControllers.Length)
        {
            Debug.LogWarning($"[GameManager] 플레이어({players.Length}) 수가 HP UI({uiControllers.Length}) 슬롯보다 많습니다. 남은 플레이어는 수동 또는 동적 생성으로 처리하세요.");
        }
        else if (uiControllers.Length > players.Length)
        {
            Debug.Log($"[GameManager] HP UI 슬롯({uiControllers.Length})이 플레이어({players.Length})보다 많습니다. 남은 UI는 비어있습니다.");
        }
    }

    // 런타임에 플레이어가 추가될 때 호출하세요. 빈 UI 슬롯이 있으면 그 슬롯에 할당합니다.
    // 반환값: 할당 성공 여부
    public bool RegisterPlayerHealth(PlayerHealth playerHealth)
    {
        if (playerHealth == null)
            return false;

        if (hudCanvas == null)
        {
            Debug.LogWarning("[GameManager] hudCanvas가 설정되지 않아 HP UI를 자동 연결할 수 없습니다.");
            return false;
        }

        var uiControllers = hudCanvas.GetComponentsInChildren<HPUIControllerBase>(true);
        // 첫 번째로 health가 null인(아직 할당되지 않은) UI를 찾아 초기화
        foreach (var ui in uiControllers)
        {
            // 내부 필드가 protected, 그래서 체크는 reflection 없이 public 'health'를 사용 (public으로 남아있음)
            if (ui != null && ui.health == null)
            {
                ui.Initialize(playerHealth);
                return true;
            }
        }

        Debug.LogWarning("[GameManager] 빈 HP UI 슬롯이 없습니다. 필요하면 HP UI 프리팹을 동적으로 생성하도록 변경하세요.");
        return false;
    }
}