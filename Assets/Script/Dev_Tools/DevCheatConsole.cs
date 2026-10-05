using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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
    private DevCheatMenuView menuView;
    private OptionsUI returnToOptions;
    private float nextPortraitSearch;
    public static bool DeveloperToolsAvailable => Application.isEditor || Debug.isDebugBuild;
    public bool IsBattle => StageManager.Active != null;

    private enum ChildMenu
    {
        None,
        Weapon,
        Upgrade
    }

    public bool IsOverlayOpen => overlayOpen;

    public static void EnsureOn(StageManager stage)
    {
        if (!DeveloperToolsAvailable) return;
        if (UnityEngine.Object.FindFirstObjectByType<DevCheatConsole>() != null)
            return;
        if (stage != null)
            stage.gameObject.AddComponent<DevCheatConsole>();
    }

    public static DevCheatConsole EnsureExists(Transform owner)
    {
        var existing = FindFirstObjectByType<DevCheatConsole>();
        if (existing != null) return existing;
        var root = new GameObject("DevCheatConsole");
        root.transform.SetParent(owner, false);
        return root.AddComponent<DevCheatConsole>();
    }

    public bool OpenFromOptions(OptionsUI options)
    {
        if (!CanUseTools() || options == null || overlayOpen || waitingForChildMenu != ChildMenu.None) return false;
        returnToOptions = options;
        pausedByThisMenu = !GameplayTime.IsGameplayPaused;
        if (pausedByThisMenu) GameplayTime.Pause();
        ShowMenu();
        return true;
    }

    public void DismissFromOptions(OptionsUI options)
    {
        if (returnToOptions == options) CloseInternal(false);
    }

    private bool CanUseTools() => DeveloperToolsAvailable && (Application.isEditor || enableInBuild);

    private void ShowMenu()
    {
        if (menuView == null) menuView = DevCheatMenuView.Create(this);
        overlayOpen = true;
        menuView.Show(IsBattle, returnToOptions != null);
        if (InputManager.Instance != null) InputManager.Instance.SetOverlayInputBlocked(true);
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
        if (!CanUseTools()) return;
        if (overlayOpen || waitingForChildMenu != ChildMenu.None)
            return;
        if (GameplayTime.IsGameplayPaused)
            return;

        GameplayTime.Pause();
        pausedByThisMenu = true;
        returnToOptions = null;
        ShowMenu();
    }

    public void CloseOverlay()
    {
        CloseInternal(true);
    }

    private void CloseInternal(bool restoreOptions)
    {
        var options = returnToOptions;
        returnToOptions = null;
        overlayOpen = false;
        waitingForChildMenu = ChildMenu.None;
        if (menuView != null) menuView.gameObject.SetActive(false);
        if (weaponSwitcher != null && weaponSwitcher.IsOverlayOpen) weaponSwitcher.CloseOverlay();
        if (upgradeSwitcher != null && upgradeSwitcher.IsOverlayOpen) upgradeSwitcher.CloseOverlay();

        if (pausedByThisMenu)
        {
            pausedByThisMenu = false;
            GameplayTime.Resume();
        }
        if (GameplayTime.IsGameplayPaused && InputManager.Instance != null)
            InputManager.Instance.SetOverlayInputBlocked(true);
        if (restoreOptions && options != null) options.Show();
    }

    private void Awake()
    {
        if (!CanUseTools())
        {
            enabled = false;
            return;
        }
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        TryBindPortraitButton();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (portraitButton != null)
            portraitButton.onClick.RemoveListener(ToggleOverlay);
        CloseInternal(false);
    }

    private void OnDisable() => CloseInternal(false);
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CloseInternal(false);
        nextPortraitSearch = 0f;
    }

    private void Update()
    {
        if (IsBattle && portraitButton == null && Time.unscaledTime >= nextPortraitSearch)
        {
            nextPortraitSearch = Time.unscaledTime + 1f;
            TryBindPortraitButton();
        }

        if (waitingForChildMenu == ChildMenu.Weapon)
        {
            if (weaponSwitcher == null || !weaponSwitcher.IsOverlayOpen)
            {
                waitingForChildMenu = ChildMenu.None;
                ShowMenu();
            }
            return;
        }

        if (waitingForChildMenu == ChildMenu.Upgrade)
        {
            if (upgradeSwitcher == null || !upgradeSwitcher.IsOverlayOpen)
            {
                waitingForChildMenu = ChildMenu.None;
                ShowMenu();
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

    public void AddMoney(int amount)
    {
        PlayerResources resources = ResolveResources();
        if (CanUseTools() && resources != null && amount > 0)
            resources.AddMoney(Mathf.Min(amount, int.MaxValue - resources.Money));
    }

    public void AddGem(int amount)
    {
        PlayerResources resources = ResolveResources();
        if (CanUseTools() && resources != null && amount > 0)
            resources.AddGem(Mathf.Min(amount, int.MaxValue - resources.Gem));
    }

    public void ResetResources()
    {
        PlayerResources resources = ResolveResources();
        if (CanUseTools() && resources != null)
            resources.SetAllToZero();
    }

    public static PlayerResources ResolveResources()
    {
        return PlayerResources.Instance != null
            ? PlayerResources.Instance
            : UnityEngine.Object.FindFirstObjectByType<PlayerResources>();
    }

    public void OpenShop()
    {
        InGameShopOpener opener = StageManager.Active != null
            ? StageManager.Active.GetComponent<InGameShopOpener>()
            : UnityEngine.Object.FindFirstObjectByType<InGameShopOpener>();

        CloseForGameplayAction();
        opener?.OpenShop();
    }

    public void DropShopTicket()
    {
        InGameShopTrigger trigger = StageManager.Active != null
            ? StageManager.Active.GetComponent<InGameShopTrigger>()
            : UnityEngine.Object.FindFirstObjectByType<InGameShopTrigger>();

        CloseForGameplayAction();
        if (trigger == null)
        {
            Debug.LogWarning("[DevCheatConsole] InGameShopTrigger를 찾을 수 없습니다.");
            return;
        }

        trigger.SpawnTicketNearPlayer();
    }

    public void OpenWeaponMenu()
    {
        if (weaponSwitcher == null)
            weaponSwitcher = UnityEngine.Object.FindFirstObjectByType<DevWeaponSwitcher>();
        if (weaponSwitcher == null)
        {
            Debug.LogWarning("[DevCheatConsole] DevWeaponSwitcher를 찾을 수 없습니다.");
            return;
        }

        overlayOpen = false;
        if (menuView != null) menuView.gameObject.SetActive(false);
        waitingForChildMenu = ChildMenu.Weapon;
        weaponSwitcher.OpenOverlay();
    }

    public void OpenUpgradeMenu()
    {
        if (upgradeSwitcher == null)
            upgradeSwitcher = UnityEngine.Object.FindFirstObjectByType<DevUpgradeSwitcher>();
        if (upgradeSwitcher == null)
        {
            Debug.LogWarning("[DevCheatConsole] DevUpgradeSwitcher를 찾을 수 없습니다.");
            return;
        }

        overlayOpen = false;
        if (menuView != null) menuView.gameObject.SetActive(false);
        waitingForChildMenu = ChildMenu.Upgrade;
        upgradeSwitcher.OpenOverlay();
    }

    private void CloseForGameplayAction()
    {
        var options = returnToOptions;
        CloseInternal(false);
        if (options != null) options.Hide();
    }
}