using UnityEngine;

/// <summary>
/// <see cref="Upgrade_06_03_GodShield"/> 합산형 타이머.
/// 슬롯에 장착된 모든 God Shield의 durationSeconds 합이 하나의 남은 시간 풀로 깎이며,
/// 0이 되면 God Shield가 들어간 슬롯을 한꺼번에 비웁니다. (기존 FIFO 큐 방식 아님)
/// </summary>
[DisallowMultipleComponent]
public class PlayerGodShieldUpgradeRuntime : MonoBehaviour
{
    [SerializeField] private Upgrade upgrade;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private UpgradeHUD upgradeHud;

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog;
    [SerializeField] private bool verboseGodShieldSlotFx;

    /// <summary>남은 지속 시간(초). 슬롯 합산 풀.</summary>
    private float remainingSeconds;

    /// <summary>직전 Sync에서 계산한 슬롯별 기여 시간 합계(변화분 = 델타).</summary>
    private float lastComputedPoolTotal;

    /// <summary>
    /// 타이머 만료로 슬롯을 연속 비울 때 <see cref="SyncFromSlots"/>가 끼어들면
    /// <c>lastComputedPoolTotal == 0</c>인 채로 남은 슬롯 합이 델타로 더해져 버리는 것을 막습니다.
    /// </summary>
    private bool suppressSlotSync;

    private GameObject _activeFxInstance;

    private void Awake()
    {
        if (upgrade == null)
            upgrade = GetComponent<Upgrade>();
        if (upgrade == null)
            upgrade = GetComponentInChildren<Upgrade>(true);
        if (upgrade == null)
            upgrade = GetComponentInParent<Upgrade>();
    }

    private void OnEnable()
    {
        BindUpgrade();
        SyncFromSlots();
    }

    private void OnDisable()
    {
        UnbindUpgrade();
    }

    private void OnDestroy()
    {
        ClearFxInstance();
    }

    private void Update()
    {
        if (remainingSeconds <= 0f)
            return;

        float dt = GameplayTime.DeltaTime;
        if (dt <= 0f)
            return;

        remainingSeconds -= dt;
        if (remainingSeconds <= 0f)
            OnTimerExpired();
    }

    private void BindUpgrade()
    {
        if (upgrade == null)
            return;

        upgrade.OnSlotsChanged -= SyncFromSlots;
        upgrade.OnSlotsChanged += SyncFromSlots;
    }

    private void UnbindUpgrade()
    {
        if (upgrade == null)
            return;

        upgrade.OnSlotsChanged -= SyncFromSlots;
    }

    /// <summary>무적 효과가 이 플레이어에게 적용 중인지.</summary>
    public bool IsProtectionActive => remainingSeconds > 0f;

    public void SyncFromSlots()
    {
        if (suppressSlotSync)
            return;

        if (upgrade == null)
            return;

        float newPool = ComputeGodShieldPoolTotal();
        float delta = newPool - lastComputedPoolTotal;

        bool wasActive = remainingSeconds > 0f;
        remainingSeconds = Mathf.Max(0f, remainingSeconds + delta);
        lastComputedPoolTotal = newPool;

        if (newPool <= 0f)
        {
            remainingSeconds = 0f;
            lastComputedPoolTotal = 0f;
            if (wasActive)
                ClearFxInstance();
            return;
        }

        if (!wasActive && remainingSeconds > 0f)
            OnEffectBeganFresh();

        else if (wasActive && remainingSeconds > 0f && delta > 0f)
            RefreshActiveFxPrefab();
    }

    private static float ComputeGodShieldPoolSum(Upgrade u)
    {
        if (u == null)
            return 0f;

        float sum = 0f;
        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            UpgradeEffectSO slot = u.GetSlot(i);
            if (slot is Upgrade_06_03_GodShield gs)
                sum += Mathf.Max(0f, gs.durationSeconds);
        }

        return sum;
    }

    private float ComputeGodShieldPoolTotal() => ComputeGodShieldPoolSum(upgrade);

    private void OnEffectBeganFresh()
    {
        ClearPoisonForGodShield();
        SpawnActiveFx();
    }

    private void RefreshActiveFxPrefab()
    {
        SpawnActiveFx();
    }

    private void OnTimerExpired()
    {
        remainingSeconds = 0f;
        lastComputedPoolTotal = 0f;

        ClearFxInstance();

        suppressSlotSync = true;
        try
        {
            PlayAllSlotConsumeFxThenClearGodShieldSlots();
        }
        finally
        {
            suppressSlotSync = false;
        }

        SyncFromSlots();
        ClearPoisonForGodShield();
    }

    private void PlayAllSlotConsumeFxThenClearGodShieldSlots()
    {
        if (upgrade == null)
            return;

        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            UpgradeEffectSO so = upgrade.GetSlot(i);
            if (so is Upgrade_06_03_GodShield gs)
            {
                PlayGodShieldSlotConsumeFx(gs, i);
                upgrade.TryClearSlot(i);
            }
        }
    }

    private void ClearPoisonForGodShield()
    {
        EnsurePlayerHealth();
        if (playerHealth == null)
            return;

        var poison = playerHealth.GetComponent<PlayerPoisonDebuffRuntime>() ??
                     playerHealth.GetComponentInChildren<PlayerPoisonDebuffRuntime>(true);
        poison?.ClearPoisonState();
    }

    private void SpawnActiveFx()
    {
        ClearFxInstance();
        if (upgrade == null)
            return;

        GameObject prefab = null;
        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            if (upgrade.GetSlot(i) is Upgrade_06_03_GodShield gs && gs.activeFxPrefab != null)
            {
                prefab = gs.activeFxPrefab;
                break;
            }
        }

        if (prefab == null)
            return;

        EnsurePlayerHealth();
        Transform parent = playerHealth != null ? playerHealth.transform : transform.root;
        _activeFxInstance = Instantiate(prefab, parent.position, parent.rotation, parent);
    }

    private void ClearFxInstance()
    {
        if (_activeFxInstance == null)
            return;

        Destroy(_activeFxInstance);
        _activeFxInstance = null;
    }

    private void EnsureUpgradeHud()
    {
        if (upgrade == null)
            return;

        UpgradeHUD chosen = UpgradeHUD.ResolveAndBindHud(upgrade, verboseGodShieldSlotFx);
        if (chosen == null)
        {
            if (enableDebugLog)
                Debug.LogWarning("[PlayerGodShieldUpgradeRuntime] 씬에 UpgradeHUD가 없습니다.");
            return;
        }

        upgradeHud = chosen;

        if (enableDebugLog && chosen.CountAssignedSlotImages() == 0)
        {
            Debug.LogWarning(
                $"[PlayerGodShieldUpgradeRuntime] 선택된 UpgradeHUD('{chosen.name}')에 슬롯 Image가 없습니다.");
        }
    }

    private bool PlayGodShieldSlotConsumeFx(Upgrade_06_03_GodShield shieldSo, int slotIndex)
    {
        if (shieldSo == null || shieldSo.slotConsumeFxPrefab == null)
            return false;

        EnsureUpgradeHud();
        if (upgradeHud == null)
            return false;

        bool ok = upgradeHud.TryPlaySlotFx(
            slotIndex,
            shieldSo.slotConsumeFxPrefab,
            shieldSo.slotFxAutoDestroySeconds,
            verboseGodShieldSlotFx);

        if (!ok && enableDebugLog)
        {
            Debug.LogWarning(
                $"[PlayerGodShieldUpgradeRuntime] TryPlaySlotFx 실패 — HUD:'{upgradeHud.name}', slotIndex:{slotIndex}");
        }

        return ok;
    }

    private void EnsurePlayerHealth()
    {
        if (playerHealth != null)
            return;

        playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = GetComponentInChildren<PlayerHealth>(true);
        if (playerHealth == null)
            playerHealth = GetComponentInParent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = transform.root.GetComponentInChildren<PlayerHealth>(true);
    }
}
