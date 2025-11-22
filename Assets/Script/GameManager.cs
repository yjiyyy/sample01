using System.Linq;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("플레이어 관련")]
    public Transform playerTransform;
    public Canvas hudCanvas;

    [Header("디버그 설정")]
    [Tooltip("디버그 로그 출력 여부. 모바일 빌드에서는 자동으로 꺼집니다.")]
    public bool isDebugMode = false; // 기본 false

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

#if !UNITY_EDITOR
            // 에디터가 아닌 빌드에서는 자동 OFF (혹시 Inspector에서 켜놨더라도 비활성화)
            isDebugMode = false;
#endif
            InitializeFrameRate();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        AssignExistingPlayerUIs();
    }

    private void InitializeFrameRate()
    {
        // 모바일 환경 목표: VSync OFF + 60fps (Unity6에서도 동일 개념)
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

        if (isDebugMode)
        {
            Debug.Log("[GameManager] Frame rate initialized: VSync OFF, Target 60fps");
        }
    }

    public void AssignExistingPlayerUIs()
    {
        if (hudCanvas == null)
        {
            // 설정 누락 경고는 항상 출력
            Debug.LogWarning("[GameManager] hudCanvas가 설정되지 않았습니다. HP UI 자동 연결 불가.");
            return;
        }

        var uiControllers = hudCanvas.GetComponentsInChildren<HPUIControllerBase>(true);
        var players = UnityEngine.Object.FindObjectsByType<PlayerHealth>(UnityEngine.FindObjectsSortMode.InstanceID);

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
            // 슬롯 부족은 경고 (항상 알려주는 것이 운영에 유리)
            Debug.LogWarning($"[GameManager] 플레이어({players.Length}) 수가 HP UI 슬롯({uiControllers.Length})보다 많습니다. 남은 플레이어는 수동/동적 처리 필요.");
        }
        else if (uiControllers.Length > players.Length && isDebugMode)
        {
            // 남는 슬롯 정보는 디버그 모드일 때만
            Debug.Log($"[GameManager] UI 슬롯({uiControllers.Length})이 플레이어({players.Length})보다 많습니다. 일부 슬롯은 비어 있음.");
        }
    }

    public bool RegisterPlayerHealth(PlayerHealth playerHealth)
    {
        if (playerHealth == null) return false;

        if (hudCanvas == null)
        {
            Debug.LogWarning("[GameManager] hudCanvas 미설정: HP UI 자동 연결 불가.");
            return false;
        }

        var uiControllers = hudCanvas.GetComponentsInChildren<HPUIControllerBase>(true);
        foreach (var ui in uiControllers)
        {
            if (ui != null && ui.health == null)
            {
                ui.Initialize(playerHealth);
                if (isDebugMode)
                {
                    Debug.Log($"[GameManager] 새 플레이어 Health를 빈 UI 슬롯에 할당: {playerHealth.name}");
                }
                return true;
            }
        }

        Debug.LogWarning("[GameManager] 빈 HP UI 슬롯이 없습니다. 필요 시 동적 생성 로직 추가하세요.");
        return false;
    }
}