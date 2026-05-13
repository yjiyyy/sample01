using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerReviveTicketRuntime : MonoBehaviour
{
    [SerializeField] private Upgrade upgrade;
    [SerializeField] private UpgradeHUD upgradeHud;
    [SerializeField] private bool enableDebugLog = true;

    [Header("Debug — 슬롯 소모 FX")]
    [Tooltip("부활 티켓 사용 시 UpgradeHUD / FX_Slot / Instantiate 단계 로그를 콘솔에 남깁니다. 원인 파악 후 끄세요.")]
    [SerializeField] private bool verboseReviveSlotFx = true;

    private bool revivePending;

    private void Awake()
    {
        if (upgrade == null)
            upgrade = GetComponent<Upgrade>();
        if (upgrade == null)
            upgrade = GetComponentInChildren<Upgrade>(true);
        if (upgrade == null)
            upgrade = GetComponentInParent<Upgrade>();
    }

    public bool TryHandleDeath(PlayerHealth playerHealth, Vector3 hitDir, WeaponDataSO weapon, float impactScale)
    {
        if (playerHealth == null || revivePending)
            return revivePending;

        if (upgrade == null)
            return false;

        int slotIndex = -1;
        Upgrade_06_01_ReviveTicket ticket = null;
        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            if (upgrade.GetSlot(i) is not Upgrade_06_01_ReviveTicket found)
                continue;

            slotIndex = i;
            ticket = found;
            break; // 앞 슬롯부터 소모
        }

        if (slotIndex < 0 || ticket == null)
            return false;

        if (verboseReviveSlotFx)
        {
            Debug.Log(
                $"[ReviveSlotFx] TryHandleDeath 시작 — ticketSO:'{(ticket != null ? ticket.name : "?")}', " +
                $"slotConsumeFx:'{(ticket.slotConsumeFxPrefab != null ? ticket.slotConsumeFxPrefab.name : "NULL")}', " +
                $"slotIndex:{slotIndex}, upgrade:'{(upgrade != null ? upgrade.name : "?")}'");
        }

        revivePending = true;
        Transform corpseRoot = playerHealth.transform.root;
        Vector3 deathPos = corpseRoot.position;

        if (InputManager.Instance != null)
        {
            InputManager.SetPlayerDeathBlock(true);
            InputManager.Instance.ClearPlayerInput();
        }

        EnsureUpgradeHud();
        if (!PlaySlotConsumeFx(ticket, slotIndex))
        {
            if (enableDebugLog)
                Debug.LogWarning("[PlayerReviveTicketRuntime] 슬롯 FX 재생 실패: UpgradeHUD/슬롯 참조를 확인하세요.");
        }
        SpawnWorldFx(ticket.reviveCastFxPrefab, deathPos, ticket.worldFxAutoDestroySeconds);

        // 리스폰 시 복원용 슬롯 스냅샷(티켓은 이미 소모된 것으로 반영)
        UpgradeEffectSO[] preservedSlots = CaptureCurrentSlots();
        if (slotIndex >= 0 && slotIndex < preservedSlots.Length)
            preservedSlots[slotIndex] = null;

        var spawnManager = Object.FindFirstObjectByType<SpawnManager>();
        if (spawnManager == null)
        {
            Debug.LogError("[PlayerReviveTicketRuntime] SpawnManager를 찾지 못해 부활을 진행할 수 없습니다.");
            revivePending = false;
            return false;
        }

        // 시체는 남겨두고, 리스폰 시점에 제거합니다.
        playerHealth.EnterReviveWaitingState();
        spawnManager.ScheduleRevive(ticket, deathPos, preservedSlots, corpseRoot);
        StartCoroutine(ClearSlotAfterDelay(slotIndex, Mathf.Max(0f, ticket.respawnDelaySeconds)));

        if (enableDebugLog)
            Debug.Log($"[PlayerReviveTicketRuntime] Revive Ticket 소모 완료. slot:{slotIndex}, delay:{ticket.respawnDelaySeconds:F2}s");

        return true;
    }

    private UpgradeEffectSO[] CaptureCurrentSlots()
    {
        var slots = new UpgradeEffectSO[Upgrade.SlotCount];
        if (upgrade == null)
            return slots;

        for (int i = 0; i < Upgrade.SlotCount; i++)
            slots[i] = upgrade.GetSlot(i);

        return slots;
    }

    private IEnumerator ClearSlotAfterDelay(int slotIndex, float delay)
    {
        if (upgrade == null)
            yield break;

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        upgrade.TryClearSlot(slotIndex);
    }

    private void EnsureUpgradeHud()
    {
        if (upgrade == null)
            return;

        UpgradeHUD[] allHuds = Object.FindObjectsByType<UpgradeHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (allHuds == null || allHuds.Length == 0)
        {
            if (enableDebugLog)
                Debug.LogWarning("[PlayerReviveTicketRuntime] 씬에 UpgradeHUD가 없습니다.");
            return;
        }

        if (verboseReviveSlotFx)
            Debug.Log($"[ReviveSlotFx] UpgradeHUD 검색 — 총 {allHuds.Length}개");

        // 동일 Upgrade에 묶인 UpgradeHUD가 2개 이상이면 DataSource만으로는 구분이 안 됩니다.
        // 슬롯 Image가 실제로 연결된 HUD(UpgradeHUDRoot 등)를 우선합니다.
        // (예전 Screen Space Camera용 Canvas_UpgradeFX 아래 HUD는 사용하지 않을 때가 많아 제외합니다.)
        UpgradeHUD chosen = null;
        int bestRank = int.MinValue;
        for (int i = 0; i < allHuds.Length; i++)
        {
            UpgradeHUD hud = allHuds[i];
            if (hud == null)
                continue;

            if (IsUnderDeprecatedFxCanvas(hud.transform))
            {
                if (verboseReviveSlotFx)
                    Debug.Log($"[ReviveSlotFx] HUD 후보 제외(Canvas_UpgradeFX 하위): '{hud.name}' path:{BuildTransformPath(hud.transform)}");
                continue;
            }

            int slotFilled = hud.CountAssignedSlotImages();
            bool linked = hud.DataSource == upgrade;
            int rank = slotFilled * 1000;
            if (linked)
                rank += 100;
            if (hud.gameObject.activeInHierarchy)
                rank += 10;
            string goName = hud.gameObject.name;
            if (goName.IndexOf("UpgradeHUDRoot", System.StringComparison.OrdinalIgnoreCase) >= 0)
                rank += 50;

            if (verboseReviveSlotFx)
            {
                Debug.Log(
                    $"[ReviveSlotFx] HUD 후보 — name:'{hud.name}', path:{BuildTransformPath(hud.transform)}, " +
                    $"slotsFilled:{slotFilled}, linked:{linked}, active:{hud.gameObject.activeInHierarchy}, rank:{rank}");
            }

            if (rank > bestRank)
            {
                bestRank = rank;
                chosen = hud;
            }
        }

        if (chosen == null)
        {
            for (int i = 0; i < allHuds.Length; i++)
            {
                if (allHuds[i] != null && !IsUnderDeprecatedFxCanvas(allHuds[i].transform))
                {
                    chosen = allHuds[i];
                    break;
                }
            }
        }

        if (chosen == null)
            chosen = allHuds[0];

        chosen.EnsureDataSource(upgrade);
        upgradeHud = chosen;

        if (verboseReviveSlotFx)
        {
            Debug.Log(
                $"[ReviveSlotFx] HUD 선택 완료 — '{chosen.name}', path:{BuildTransformPath(chosen.transform)}, " +
                $"slotsFilled:{chosen.CountAssignedSlotImages()}, DataSource:'{(chosen.DataSource != null ? chosen.DataSource.name : "null")}'");
        }

        if (enableDebugLog && chosen.CountAssignedSlotImages() == 0)
            Debug.LogWarning($"[PlayerReviveTicketRuntime] 선택된 UpgradeHUD('{chosen.name}')에 슬롯 Image가 없습니다. Slot Consume FX가 재생되지 않을 수 있습니다.");
    }

    private static string BuildTransformPath(Transform t)
    {
        if (t == null)
            return "(null)";

        System.Text.StringBuilder sb = new System.Text.StringBuilder(128);
        Transform walk = t;
        while (walk != null)
        {
            if (sb.Length > 0)
                sb.Insert(0, '/');
            sb.Insert(0, walk.name);
            walk = walk.parent;
        }

        return sb.ToString();
    }

    /// <summary>사용하지 않는 FX 전용 캔버스(삭제 예정) 아래인지.</summary>
    private static bool IsUnderDeprecatedFxCanvas(Transform t)
    {
        const string deprecatedCanvas = "Canvas_UpgradeFX";
        while (t != null)
        {
            if (t.name.IndexOf(deprecatedCanvas, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            t = t.parent;
        }

        return false;
    }

    private bool PlaySlotConsumeFx(Upgrade_06_01_ReviveTicket ticket, int slotIndex)
    {
        if (ticket == null || ticket.slotConsumeFxPrefab == null || upgradeHud == null)
            return false;

        bool ok = upgradeHud.TryPlaySlotFx(slotIndex, ticket.slotConsumeFxPrefab, ticket.slotFxAutoDestroySeconds, verboseReviveSlotFx);
        if (!ok && enableDebugLog)
        {
            Debug.LogWarning(
                $"[PlayerReviveTicketRuntime] TryPlaySlotFx 실패 — HUD:'{upgradeHud.name}', slotIndex:{slotIndex}, " +
                $"슬롯 Image 수:{upgradeHud.CountAssignedSlotImages()}, 프리팹:'{ticket.slotConsumeFxPrefab.name}'");
        }
        else if (ok && verboseReviveSlotFx)
        {
            Debug.Log($"[ReviveSlotFx] TryPlaySlotFx 성공 — slot:{slotIndex}, prefab:'{ticket.slotConsumeFxPrefab.name}'");
        }

        return ok;
    }

    private void SpawnWorldFx(GameObject fxPrefab, Vector3 pos, float autoDestroySeconds)
    {
        if (fxPrefab == null)
            return;

        GameObject fx = Instantiate(fxPrefab, pos, Quaternion.identity);
        if (autoDestroySeconds > 0f)
            Destroy(fx, autoDestroySeconds);
    }
}
