using UnityEngine;

/// <summary>
/// 개발용 치트 콘솔 (현재 1번 치트만 지원)
/// - 자체 토글 키로 열기/닫기
/// - 열려 있을 때 숫자 1 입력 시 플레이어 HP 50 감소 후 자동 닫기
/// </summary>
public class DevCheatConsole : MonoBehaviour
{
    [Header("빌드에서 활성화 여부")]
    public bool enableInBuild = true;

    [Header("치트 오버레이 키")]
    [Tooltip("원하는 키로 치트 오버레이 열기/닫기")]
    public KeyCode toggleKey = KeyCode.F2;

    [Header("대상 플레이어")]
    public PlayerHealth targetPlayerHealth;
    public PlayerEvadeController targetPlayerEvade;

    [Header("표시 옵션")]
    [Range(0.2f, 1f)] public float overlayWidthPercent = 0.55f;
    [Range(0.2f, 1f)] public float overlayHeightPercent = 0.32f;
    [Range(0f, 0.5f)] public float overlayTopMarginPercent = 0.08f;

    private bool overlayOpen;
    private GUIStyle headerStyle;
    private GUIStyle bodyStyle;

    public bool IsOverlayOpen => overlayOpen;
    public void ToggleOverlay() => SetOverlayOpen(!overlayOpen);
    public void OpenOverlay() => SetOverlayOpen(true);
    public void CloseOverlay() => SetOverlayOpen(false);

    private void Awake()
    {
        if (!Application.isEditor && !enableInBuild)
        {
            enabled = false;
            return;
        }
    }

    private void OnDestroy()
    {
        if (InputManager.Instance != null)
            InputManager.Instance.OverlayInputBlocked = false;
    }

    private void SetOverlayOpen(bool open)
    {
        overlayOpen = open;
        if (InputManager.Instance != null)
            InputManager.Instance.OverlayInputBlocked = overlayOpen;
    }

    private void Update()
    {
        if (InputManager.Instance == null) return;

        if (InputManager.Instance.GetKeyDown(toggleKey))
        {
            SetOverlayOpen(!overlayOpen);
            return;
        }

        if (!overlayOpen) return;

        if (InputManager.Instance.GetKeyDown(KeyCode.Alpha1))
        {
            ExecuteCheatDamage50();
            SetOverlayOpen(false);
            return;
        }

        if (InputManager.Instance.GetKeyDown(KeyCode.Alpha2))
        {
            ExecuteCheatEvadeCost50();
            SetOverlayOpen(false);
        }
    }

    private void EnsureTargetPlayer()
    {
        if (targetPlayerHealth != null) return;

        if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
        {
            targetPlayerHealth = GameManager.Instance.playerTransform.GetComponent<PlayerHealth>();
            if (targetPlayerHealth != null) return;
        }

        targetPlayerHealth = FindFirstObjectByType<PlayerHealth>();
    }

    private void EnsureTargetEvade()
    {
        if (targetPlayerEvade != null) return;

        if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
        {
            targetPlayerEvade = GameManager.Instance.playerTransform.GetComponent<PlayerEvadeController>();
            if (targetPlayerEvade != null) return;
        }

        targetPlayerEvade = FindFirstObjectByType<PlayerEvadeController>();
    }

    private void ExecuteCheatDamage50()
    {
        EnsureTargetPlayer();
        if (targetPlayerHealth == null)
        {
            Debug.LogWarning("[DevCheatConsole] PlayerHealth를 찾을 수 없습니다.");
            return;
        }

        // 기본 피해 처리 경로 사용 (넉백/스턴 추가 호출 없음)
        targetPlayerHealth.ApplyDamage(50f);
        Debug.Log("[DevCheatConsole] Cheat #1 실행: Player HP -50");
    }

    private void ExecuteCheatEvadeCost50()
    {
        EnsureTargetEvade();
        if (targetPlayerEvade == null)
        {
            Debug.LogWarning("[DevCheatConsole] PlayerEvadeController를 찾을 수 없습니다.");
            return;
        }

        targetPlayerEvade.ConsumeGauge(50f);
        Debug.Log("[DevCheatConsole] Cheat #2 실행: Evade Gauge -50");
    }

    private void InitStylesIfNeeded()
    {
        if (headerStyle != null && bodyStyle != null) return;

        headerStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(10, 10, 8, 8)
        };

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            alignment = TextAnchor.UpperLeft,
            wordWrap = true,
            padding = new RectOffset(6, 6, 6, 6)
        };
    }

    private void OnGUI()
    {
        if (!overlayOpen) return;

        InitStylesIfNeeded();

        float width = Mathf.Clamp(Screen.width * overlayWidthPercent, 320f, Screen.width - 16f);
        float height = Mathf.Clamp(Screen.height * overlayHeightPercent, 160f, Screen.height - 16f);
        float left = Mathf.Round((Screen.width - width) * 0.5f);
        float top = Mathf.Round(Screen.height * overlayTopMarginPercent);
        Rect window = new Rect(left, top, width, height);

        GUILayout.BeginArea(window, GUI.skin.window);
        GUILayout.Label("Dev Cheat Console", headerStyle);
        GUILayout.Space(8);
        GUILayout.Label("숫자 키를 눌러 치트를 실행하세요.", bodyStyle);
        GUILayout.Label("1 : 현재 플레이어 HP -50 (기본 피격 경로)", bodyStyle);
        GUILayout.Label("2 : 스태미너(회피 게이지) -50", bodyStyle);
        GUILayout.Space(4);
        GUILayout.Label($"{toggleKey} : 치트 창 열기/닫기", bodyStyle);
        GUILayout.Space(2);
        GUILayout.Label("실행 후 치트 창은 자동으로 닫힙니다.", bodyStyle);
        GUILayout.EndArea();
    }
}
