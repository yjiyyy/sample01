using System.Collections;
using UnityEngine;

/// <summary>
/// <see cref="Upgrade_06_02_Barrier"/> 슬롯별 런타임 베리어 풀을 관리합니다.
/// 피해는 <see cref="PlayerHealth"/>에서 슬롯 인덱스 작은 쪽부터 흡수합니다.
/// </summary>
[DisallowMultipleComponent]
public class PlayerBarrierUpgradeRuntime : MonoBehaviour
{
    [SerializeField] private Upgrade upgrade;
    [SerializeField] private UpgradeHUD upgradeHud;

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog;
    [Tooltip("베리어 슬롯 소모 FX / HUD 탐색 단계 로그")]
    [SerializeField] private bool verboseBarrierSlotFx;

    private readonly float[] currentBarrier = new float[Upgrade.SlotCount];
    private readonly UpgradeEffectSO[] boundEffect = new UpgradeEffectSO[Upgrade.SlotCount];
    private readonly Coroutine[] pendingBarrierSlotClear = new Coroutine[Upgrade.SlotCount];

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
        for (int i = 0; i < Upgrade.SlotCount; i++)
            StopPendingBarrierSlotClear(i);

        UnbindUpgrade();
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

    /// <summary>슬롯 변경 시: 비-베리어·해제 시 해당 칸 0, 새로 장착 시 SO 수치로 풀 충전.</summary>
    public void SyncFromSlots()
    {
        if (upgrade == null)
        {
            ClearAllPoolsLocal();
            return;
        }

        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            UpgradeEffectSO slot = upgrade.GetSlot(i);
            if (slot is Upgrade_06_02_Barrier barrierSo)
            {
                if (boundEffect[i] != slot)
                {
                    StopPendingBarrierSlotClear(i);
                    boundEffect[i] = slot;
                    currentBarrier[i] = Mathf.Max(0f, barrierSo.barrierMaxPoints);
                }
                else
                {
                    float cap = Mathf.Max(0f, barrierSo.barrierMaxPoints);
                    if (currentBarrier[i] > cap)
                        currentBarrier[i] = cap;
                }
            }
            else
            {
                StopPendingBarrierSlotClear(i);
                boundEffect[i] = null;
                currentBarrier[i] = 0f;
            }
        }
    }

    private void ClearAllPoolsLocal()
    {
        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            StopPendingBarrierSlotClear(i);
            boundEffect[i] = null;
            currentBarrier[i] = 0f;
        }
    }

    public float GetBarrierTotalCurrent()
    {
        float sum = 0f;
        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            if (upgrade != null && upgrade.GetSlot(i) is Upgrade_06_02_Barrier)
                sum += Mathf.Max(0f, currentBarrier[i]);
        }

        return sum;
    }

    public float GetBarrierTotalMax()
    {
        if (upgrade == null)
            return 0f;

        float sum = 0f;
        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            if (upgrade.GetSlot(i) is Upgrade_06_02_Barrier b)
                sum += Mathf.Max(0f, b.barrierMaxPoints);
        }

        return sum;
    }

    /// <summary>베리어로 흡수한 뒤 HP에 적용할 남은 피해량을 반환합니다.</summary>
    public float AbsorbDamageBeforeHp(float amount)
    {
        if (amount <= 0f || upgrade == null)
            return amount;

        float remaining = amount;
        for (int i = 0; i < Upgrade.SlotCount && remaining > 0f; i++)
        {
            if (upgrade.GetSlot(i) is not Upgrade_06_02_Barrier)
                continue;

            if (currentBarrier[i] <= 0f)
                continue;

            float chunk = Mathf.Min(currentBarrier[i], remaining);
            currentBarrier[i] -= chunk;
            remaining -= chunk;

            if (chunk > 0f)
            {
                Upgrade_06_02_Barrier hitSo = upgrade.GetSlot(i) as Upgrade_06_02_Barrier;
                if (hitSo != null && hitSo.barrierGaugeHitSlotFxPrefab != null)
                    PlayBarrierGaugeHitSlotFx(hitSo, i);
            }

            if (currentBarrier[i] <= 0f)
            {
                currentBarrier[i] = 0f;
                Upgrade_06_02_Barrier depletedSo = upgrade.GetSlot(i) as Upgrade_06_02_Barrier;
                if (depletedSo != null && depletedSo.slotConsumeFxPrefab != null)
                    PlayBarrierSlotConsumeFx(depletedSo, i);

                float clearDelay = depletedSo != null ? Mathf.Max(0f, depletedSo.slotClearDelaySeconds) : 0f;
                StopPendingBarrierSlotClear(i);
                if (clearDelay > 0f && depletedSo != null)
                    pendingBarrierSlotClear[i] = StartCoroutine(CoClearBarrierSlotAfterDelay(i, depletedSo, clearDelay));
                else
                    upgrade.TryClearSlot(i);
            }
        }

        return remaining;
    }

    private void EnsureUpgradeHud()
    {
        if (upgrade == null)
            return;

        UpgradeHUD chosen = UpgradeHUD.ResolveAndBindHud(upgrade, verboseBarrierSlotFx);
        if (chosen == null)
        {
            if (enableDebugLog)
                Debug.LogWarning("[PlayerBarrierUpgradeRuntime] 씬에 UpgradeHUD가 없습니다.");
            return;
        }

        upgradeHud = chosen;

        if (enableDebugLog && chosen.CountAssignedSlotImages() == 0)
        {
            Debug.LogWarning(
                $"[PlayerBarrierUpgradeRuntime] 선택된 UpgradeHUD('{chosen.name}')에 슬롯 Image가 없습니다. Slot Consume FX가 재생되지 않을 수 있습니다.");
        }
    }

    private bool PlayBarrierSlotConsumeFx(Upgrade_06_02_Barrier barrierSo, int slotIndex)
    {
        if (barrierSo == null || barrierSo.slotConsumeFxPrefab == null)
            return false;

        EnsureUpgradeHud();
        if (upgradeHud == null)
            return false;

        bool ok = upgradeHud.TryPlaySlotFx(
            slotIndex,
            barrierSo.slotConsumeFxPrefab,
            barrierSo.slotFxAutoDestroySeconds,
            verboseBarrierSlotFx);

        if (!ok && enableDebugLog)
        {
            Debug.LogWarning(
                $"[PlayerBarrierUpgradeRuntime] TryPlaySlotFx 실패 — HUD:'{upgradeHud.name}', slotIndex:{slotIndex}, " +
                $"슬롯 Image 수:{upgradeHud.CountAssignedSlotImages()}, 프리팹:'{barrierSo.slotConsumeFxPrefab.name}'");
        }
        else if (ok && verboseBarrierSlotFx)
        {
            Debug.Log($"[BarrierSlotFx] TryPlaySlotFx 성공 — slot:{slotIndex}, prefab:'{barrierSo.slotConsumeFxPrefab.name}'");
        }

        return ok;
    }

    private bool PlayBarrierGaugeHitSlotFx(Upgrade_06_02_Barrier barrierSo, int slotIndex)
    {
        if (barrierSo == null || barrierSo.barrierGaugeHitSlotFxPrefab == null)
            return false;

        EnsureUpgradeHud();
        if (upgradeHud == null)
            return false;

        bool ok = upgradeHud.TryPlaySlotFx(
            slotIndex,
            barrierSo.barrierGaugeHitSlotFxPrefab,
            barrierSo.barrierGaugeHitSlotFxAutoDestroySeconds,
            verboseBarrierSlotFx);

        if (!ok && enableDebugLog)
        {
            Debug.LogWarning(
                $"[PlayerBarrierUpgradeRuntime] 베리어 피격 TryPlaySlotFx 실패 — HUD:'{upgradeHud.name}', slotIndex:{slotIndex}, " +
                $"슬롯 Image 수:{upgradeHud.CountAssignedSlotImages()}, 프리팹:'{barrierSo.barrierGaugeHitSlotFxPrefab.name}'");
        }
        else if (ok && verboseBarrierSlotFx)
        {
            Debug.Log($"[BarrierGaugeHitFx] TryPlaySlotFx 성공 — slot:{slotIndex}, prefab:'{barrierSo.barrierGaugeHitSlotFxPrefab.name}'");
        }

        return ok;
    }

    private void StopPendingBarrierSlotClear(int index)
    {
        if (index < 0 || index >= Upgrade.SlotCount)
            return;

        if (pendingBarrierSlotClear[index] == null)
            return;

        StopCoroutine(pendingBarrierSlotClear[index]);
        pendingBarrierSlotClear[index] = null;
    }

    private IEnumerator CoClearBarrierSlotAfterDelay(int slotIndex, Upgrade_06_02_Barrier expectedSo, float delaySeconds)
    {
        if (delaySeconds > 0f)
            yield return new WaitForSeconds(delaySeconds);

        pendingBarrierSlotClear[slotIndex] = null;

        if (upgrade == null || expectedSo == null)
            yield break;

        if (upgrade.GetSlot(slotIndex) != expectedSo)
            yield break;

        if (currentBarrier[slotIndex] > 0f)
            yield break;

        upgrade.TryClearSlot(slotIndex);
    }
}
