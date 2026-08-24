using System.Reflection;
using UnityEngine;

/// <summary>
/// 06_04 오버드라이브 업그레이드(SO) 합산형 타이머.
/// 슬롯에 장착된 오버드라이브의 durationSeconds 합이 하나의 풀로 깎이며,
/// 0이 되면 해당 슬롯을 한꺼번에 비웁니다.
/// SO 타입 이름은 <c>Upgrade_06_04_Overdrive</c> — 컴파일 순서/누락 이슈를 피하려고
/// <see cref="UpgradeEffectSO"/>와 리플렉션만 사용합니다(필드명은 SO와 동일해야 함).
/// </summary>
[DisallowMultipleComponent]
public class PlayerOverdriveUpgradeRuntime : MonoBehaviour
{
    /// <summary>SO 클래스 타입 이름 (에셋 스크립트 <c>Upgrade_06_04_Overdrive</c>와 동일해야 함).</summary>
    private const string OverdriveEffectTypeName = "Upgrade_06_04_Overdrive";

    [SerializeField] private Upgrade upgrade;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private UpgradeHUD upgradeHud;

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog;
    [SerializeField] private bool verboseOverdriveSlotFx;

    private float remainingSeconds;
    private float lastComputedPoolTotal;

    /// <summary>
    /// <see cref="PlayerGodShieldUpgradeRuntime"/>와 동일한 이유(연속 슬롯 비우기)로 사용합니다.
    /// </summary>
    private bool suppressSlotSync;

    private GameObject _activeFxInstance;
    private FullBodySilhouetteGhost _silhouetteGhost;

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
        ClearOverdrivePresentation();
    }

    private void OnDestroy()
    {
        ClearOverdrivePresentation();
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

    /// <summary>오버드라이브가 켜져 스태미나 소모가 막혀야 하는지.</summary>
    public bool IsOverdriveActive => remainingSeconds > 0f;

    public void SyncFromSlots()
    {
        if (suppressSlotSync)
            return;

        if (upgrade == null)
            return;

        float newPool = ComputeOverdrivePoolSum(upgrade);
        float delta = newPool - lastComputedPoolTotal;

        bool wasActive = remainingSeconds > 0f;
        remainingSeconds = Mathf.Max(0f, remainingSeconds + delta);
        lastComputedPoolTotal = newPool;

        if (newPool <= 0f)
        {
            remainingSeconds = 0f;
            lastComputedPoolTotal = 0f;
            if (wasActive)
                ClearOverdrivePresentation();
            return;
        }

        if (!wasActive && remainingSeconds > 0f)
            OnEffectBeganFresh();

        else if (wasActive && remainingSeconds > 0f && delta > 0f)
            RefreshOverdrivePresentation();
    }

    private static float ComputeOverdrivePoolSum(Upgrade u)
    {
        if (u == null)
            return 0f;

        float sum = 0f;
        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            UpgradeEffectSO slot = u.GetSlot(i);
            if (IsOverdriveEffect(slot))
                sum += ReadPublicFloat(slot, "durationSeconds") * u.GetStackCount(i);
        }

        return sum;
    }

    private static bool IsOverdriveEffect(UpgradeEffectSO so)
    {
        return so != null && so.GetType().Name == OverdriveEffectTypeName;
    }

    private static float ReadPublicFloat(UpgradeEffectSO so, string fieldName)
    {
        FieldInfo f = so.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        if (f == null || f.FieldType != typeof(float))
            return 0f;
        return Mathf.Max(0f, (float)f.GetValue(so));
    }

    private static GameObject ReadPublicGameObject(UpgradeEffectSO so, string fieldName)
    {
        FieldInfo f = so.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        if (f == null || f.FieldType != typeof(GameObject))
            return null;
        return f.GetValue(so) as GameObject;
    }

    private void OnEffectBeganFresh()
    {
        RefreshOverdrivePresentation();
    }

    private void RefreshOverdrivePresentation()
    {
        SpawnActiveFx();
        EnsureSilhouetteGhost();
    }

    private void OnTimerExpired()
    {
        remainingSeconds = 0f;
        lastComputedPoolTotal = 0f;

        ClearOverdrivePresentation();

        suppressSlotSync = true;
        try
        {
            PlayAllSlotConsumeFxThenClearOverdriveSlots();
        }
        finally
        {
            suppressSlotSync = false;
        }

        SyncFromSlots();
    }

    private void PlayAllSlotConsumeFxThenClearOverdriveSlots()
    {
        if (upgrade == null)
            return;

        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            UpgradeEffectSO so = upgrade.GetSlot(i);
            if (IsOverdriveEffect(so))
            {
                PlayOverdriveSlotConsumeFx(so, i);
                upgrade.TryClearSlot(i);
            }
        }
    }

    private void SpawnActiveFx()
    {
        ClearFxInstance();
        if (upgrade == null)
            return;

        GameObject prefab = null;
        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            UpgradeEffectSO slot = upgrade.GetSlot(i);
            if (IsOverdriveEffect(slot))
            {
                prefab = ReadPublicGameObject(slot, "activeFxPrefab");
                if (prefab != null)
                    break;
            }
        }

        if (prefab == null)
            return;

        EnsurePlayerHealth();
        Transform parent = playerHealth != null ? playerHealth.transform : transform.root;
        _activeFxInstance = Instantiate(prefab, parent.position, parent.rotation, parent);
    }

    private void ClearOverdrivePresentation()
    {
        ClearFxInstance();
        ClearSilhouetteGhost();
    }

    private void ClearFxInstance()
    {
        if (_activeFxInstance == null)
            return;

        Destroy(_activeFxInstance);
        _activeFxInstance = null;
    }

    private void EnsureSilhouetteGhost()
    {
        SilhouetteGhostProfile ghostProfile = FindFirstOverdriveSilhouetteProfile();
        if (ghostProfile == null || ghostProfile.ghostMaterial == null)
        {
            ClearSilhouetteGhost();
            return;
        }

        Transform host = ResolveSilhouetteHostTransform();
        if (host == null)
        {
            if (enableDebugLog)
                Debug.LogWarning("[PlayerOverdriveUpgradeRuntime] 잔상 호스트 Transform을 찾지 못했습니다.");
            return;
        }

        if (_silhouetteGhost != null && _silhouetteGhost.transform != host)
            ClearSilhouetteGhost();

        if (_silhouetteGhost == null)
        {
            _silhouetteGhost = host.GetComponent<FullBodySilhouetteGhost>();
            if (_silhouetteGhost == null)
                _silhouetteGhost = host.gameObject.AddComponent<FullBodySilhouetteGhost>();
        }

        _silhouetteGhost.ConfigureForRuntime(ghostProfile, host);
    }

    private void ClearSilhouetteGhost()
    {
        if (_silhouetteGhost == null)
            return;

        Destroy(_silhouetteGhost);
        _silhouetteGhost = null;
    }

    private SilhouetteGhostProfile FindFirstOverdriveSilhouetteProfile()
    {
        if (upgrade == null)
            return null;

        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            UpgradeEffectSO slot = upgrade.GetSlot(i);
            if (slot is Upgrade_06_04_Overdrive od && od.silhouetteGhostProfile != null)
                return od.silhouetteGhostProfile;
        }

        return null;
    }

    private Transform ResolveSilhouetteHostTransform()
    {
        EnsurePlayerHealth();
        Transform searchRoot = playerHealth != null ? playerHealth.transform : transform.root;
        if (searchRoot == null)
            return null;

        SkinnedMeshRenderer smr = searchRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
        return smr != null ? smr.transform : searchRoot;
    }

    private void EnsureUpgradeHud()
    {
        if (upgrade == null)
            return;

        UpgradeHUD chosen = UpgradeHUD.ResolveAndBindHud(upgrade, verboseOverdriveSlotFx);
        if (chosen == null)
        {
            if (enableDebugLog)
                Debug.LogWarning("[PlayerOverdriveUpgradeRuntime] 씬에 UpgradeHUD가 없습니다.");
            return;
        }

        upgradeHud = chosen;

        if (enableDebugLog && chosen.CountAssignedSlotImages() == 0)
        {
            Debug.LogWarning(
                $"[PlayerOverdriveUpgradeRuntime] 선택된 UpgradeHUD('{chosen.name}')에 슬롯 Image가 없습니다.");
        }
    }

    private bool PlayOverdriveSlotConsumeFx(UpgradeEffectSO overdriveSo, int slotIndex)
    {
        if (overdriveSo == null || !IsOverdriveEffect(overdriveSo))
            return false;

        GameObject slotPrefab = ReadPublicGameObject(overdriveSo, "slotConsumeFxPrefab");
        if (slotPrefab == null)
            return false;

        float autoDestroy = ReadPublicFloat(overdriveSo, "slotFxAutoDestroySeconds");

        EnsureUpgradeHud();
        if (upgradeHud == null)
            return false;

        bool ok = upgradeHud.TryPlaySlotFx(
            slotIndex,
            slotPrefab,
            autoDestroy,
            verboseOverdriveSlotFx);

        if (!ok && enableDebugLog)
        {
            Debug.LogWarning(
                $"[PlayerOverdriveUpgradeRuntime] TryPlaySlotFx 실패 — HUD:'{upgradeHud.name}', slotIndex:{slotIndex}");
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
