using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 초상화를 눌러 여는 개발용 치트 메뉴.
/// 키보드 단축키는 사용하지 않으며 메뉴가 열려 있는 동안 게임을 일시정지합니다.
/// </summary>
public class DevCheatConsole : MonoBehaviour
{
    [Header("빌드에서 활성화 여부")]
    public bool enableInBuild = true;

    [Header("대상 플레이어")]
    public PlayerHealth targetPlayerHealth;
    public PlayerEvadeController targetPlayerEvade;

    [Header("표시 옵션")]
    [Range(0.2f, 1f)] public float overlayWidthPercent = 0.55f;
    [Range(0.2f, 1f)] public float overlayHeightPercent = 0.72f;
    [Range(0f, 0.5f)] public float overlayTopMarginPercent = 0.08f;

    private bool overlayOpen;
    private bool pausedByThisMenu;
    private Button portraitButton;
    private DevWeaponSwitcher weaponSwitcher;
    private DevUpgradeSwitcher upgradeSwitcher;
    private ChildMenu waitingForChildMenu;
    private GUIStyle headerStyle;
    private GUIStyle buttonStyle;

    private enum ChildMenu
    {
        None,
        Weapon,
        Upgrade
    }

    public bool IsOverlayOpen => overlayOpen;

    public static void EnsureOn(StageManager stage)
    {
        if (UnityEngine.Object.FindFirstObjectByType<DevCheatConsole>() != null)
            return;
        if (stage != null)
            stage.gameObject.AddComponent<DevCheatConsole>();
    }

    public void ToggleOverlay()
    {
        if (overlayOpen || waitingForChildMenu != ChildMenu.None)
            CloseOverlay();
        else
            OpenOverlay();
    }

    public void OpenOverlay()
    {
        if (overlayOpen || waitingForChildMenu != ChildMenu.None)
            return;
        if (GameplayTime.IsGameplayPaused)
            return;

        GameplayTime.Pause();
        pausedByThisMenu = true;
        overlayOpen = true;
    }

    public void CloseOverlay()
    {
        overlayOpen = false;
        waitingForChildMenu = ChildMenu.None;
        weaponSwitcher?.CloseOverlay();
        upgradeSwitcher?.CloseOverlay();

        if (pausedByThisMenu)
        {
            pausedByThisMenu = false;
            GameplayTime.Resume();
        }
    }

    private void Awake()
    {
        if (!Application.isEditor && !enableInBuild)
        {
            enabled = false;
            return;
        }
    }

    private void Start()
    {
        TryBindPortraitButton();
    }

    private void OnDestroy()
    {
        if (portraitButton != null)
            portraitButton.onClick.RemoveListener(ToggleOverlay);
        CloseOverlay();
    }

    private void Update()
    {
        if (portraitButton == null)
            TryBindPortraitButton();

        if (waitingForChildMenu == ChildMenu.Weapon)
        {
            if (weaponSwitcher == null || !weaponSwitcher.IsOverlayOpen)
            {
                waitingForChildMenu = ChildMenu.None;
                overlayOpen = true;
            }
            return;
        }

        if (waitingForChildMenu == ChildMenu.Upgrade)
        {
            if (upgradeSwitcher == null || !upgradeSwitcher.IsOverlayOpen)
            {
                waitingForChildMenu = ChildMenu.None;
                overlayOpen = true;
            }
        }
    }

    private void TryBindPortraitButton()
    {
        Image[] images = UnityEngine.Object.FindObjectsByType<Image>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || image.name != "Character")
                continue;
            if (!HasAncestorNamed(image.transform, "Player_HP"))
                continue;

            portraitButton = image.GetComponent<Button>();
            if (portraitButton == null)
                portraitButton = image.gameObject.AddComponent<Button>();

            portraitButton.targetGraphic = image;
            portraitButton.onClick.RemoveListener(ToggleOverlay);
            portraitButton.onClick.AddListener(ToggleOverlay);
            return;
        }
    }

    private static bool HasAncestorNamed(Transform transform, string objectName)
    {
        Transform current = transform;
        while (current != null)
        {
            if (current.name == objectName)
                return true;
            current = current.parent;
        }
        return false;
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

    public void ExecuteCheatDamage50()
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

    public void ExecuteCheatEvadeCost50()
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

    private void AddMoney100()
    {
        PlayerResources resources = ResolveResources();
        if (resources != null)
            resources.AddMoney(100);
    }

    private void AddGem100()
    {
        PlayerResources resources = ResolveResources();
        if (resources != null)
            resources.AddGem(100);
    }

    private void ResetResources()
    {
        PlayerResources resources = ResolveResources();
        if (resources != null)
            resources.SetAllToZero();
    }

    private static PlayerResources ResolveResources()
    {
        return PlayerResources.Instance != null
            ? PlayerResources.Instance
            : UnityEngine.Object.FindFirstObjectByType<PlayerResources>();
    }

    private void OpenShop()
    {
        InGameShopOpener opener = StageManager.Active != null
            ? StageManager.Active.GetComponent<InGameShopOpener>()
            : UnityEngine.Object.FindFirstObjectByType<InGameShopOpener>();

        CloseOverlay();
        opener?.OpenShop();
    }

    private void OpenWeaponMenu()
    {
        if (weaponSwitcher == null)
            weaponSwitcher = UnityEngine.Object.FindFirstObjectByType<DevWeaponSwitcher>();
        if (weaponSwitcher == null)
        {
            Debug.LogWarning("[DevCheatConsole] DevWeaponSwitcher를 찾을 수 없습니다.");
            return;
        }

        overlayOpen = false;
        waitingForChildMenu = ChildMenu.Weapon;
        weaponSwitcher.OpenOverlay();
    }

    private void OpenUpgradeMenu()
    {
        if (upgradeSwitcher == null)
            upgradeSwitcher = UnityEngine.Object.FindFirstObjectByType<DevUpgradeSwitcher>();
        if (upgradeSwitcher == null)
        {
            Debug.LogWarning("[DevCheatConsole] DevUpgradeSwitcher를 찾을 수 없습니다.");
            return;
        }

        overlayOpen = false;
        waitingForChildMenu = ChildMenu.Upgrade;
        upgradeSwitcher.OpenOverlay();
    }

    private void InitStylesIfNeeded()
    {
        if (headerStyle != null && buttonStyle != null)
            return;

        headerStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(10, 10, 8, 8)
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 20,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(8, 8, 8, 8)
        };
    }

    private void OnGUI()
    {
        if (!overlayOpen) return;

        InitStylesIfNeeded();

        float width = Mathf.Clamp(Screen.width * overlayWidthPercent, 340f, Screen.width - 16f);
        float height = Mathf.Clamp(Screen.height * overlayHeightPercent, 480f, Screen.height - 16f);
        float left = Mathf.Round((Screen.width - width) * 0.5f);
        float top = Mathf.Round(Screen.height * overlayTopMarginPercent);
        Rect window = new Rect(left, top, width, height);

        GUILayout.BeginArea(window, GUI.skin.window);
        GUILayout.Label("개발자 치트 메뉴", headerStyle);
        GUILayout.Space(8);

        const float buttonHeight = 44f;
        if (GUILayout.Button("상점 열기", buttonStyle, GUILayout.Height(buttonHeight)))
            OpenShop();
        if (GUILayout.Button("HP -50", buttonStyle, GUILayout.Height(buttonHeight)))
            ExecuteCheatDamage50();
        if (GUILayout.Button("회피 게이지 -50", buttonStyle, GUILayout.Height(buttonHeight)))
            ExecuteCheatEvadeCost50();
        if (GUILayout.Button("무기 선택", buttonStyle, GUILayout.Height(buttonHeight)))
            OpenWeaponMenu();
        if (GUILayout.Button("업그레이드 선택", buttonStyle, GUILayout.Height(buttonHeight)))
            OpenUpgradeMenu();
        if (GUILayout.Button("돈 +100", buttonStyle, GUILayout.Height(buttonHeight)))
            AddMoney100();
        if (GUILayout.Button("젬 +100", buttonStyle, GUILayout.Height(buttonHeight)))
            AddGem100();
        if (GUILayout.Button("돈·젬 전부 0", buttonStyle, GUILayout.Height(buttonHeight)))
            ResetResources();

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("닫기", buttonStyle, GUILayout.Height(52f)))
            CloseOverlay();
        GUILayout.EndArea();
    }
}
