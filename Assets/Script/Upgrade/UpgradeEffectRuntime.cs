using System.Collections;
using UnityEngine;

/// <summary>
/// Upgrade 슬롯을 감시하여 실제 업그레이드 효과를 적용합니다.
/// </summary>
[DisallowMultipleComponent]
public class UpgradeEffectRuntime : MonoBehaviour
{
    [Header("데이터 소스")]
    [SerializeField] private Upgrade upgrade;

    [Header("대상 체력 컴포넌트")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerStats playerStats;

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;

    private readonly Coroutine[] slotCoroutines = new Coroutine[Upgrade.SlotCount];

    private void OnEnable()
    {
        BindReferences();
        BindUpgrade();
        RefreshEffects();
    }

    private void OnDisable()
    {
        UnbindUpgrade();
        StopAllSlotEffects();
        ApplyMoveSpeedFromUpgrades(1f);
        ApplyStaminaRechargeFromUpgrades(1f);
        ApplyStaminaRechargeDelayReductionFromUpgrades(0f);
    }

    private void BindReferences()
    {
        if (upgrade == null)
            upgrade = GetComponent<Upgrade>();

        if (upgrade == null)
            upgrade = GetComponentInChildren<Upgrade>(true);

        if (upgrade == null)
            upgrade = GetComponentInParent<Upgrade>();

        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();

        if (playerHealth == null)
            playerHealth = GetComponentInChildren<PlayerHealth>(true);

        if (playerHealth == null)
            playerHealth = GetComponentInParent<PlayerHealth>();

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (playerMovement == null)
            playerMovement = GetComponentInChildren<PlayerMovement>(true);

        if (playerMovement == null)
            playerMovement = GetComponentInParent<PlayerMovement>();

        if (playerStats == null)
            playerStats = GetComponent<PlayerStats>();

        if (playerStats == null)
            playerStats = GetComponentInChildren<PlayerStats>(true);

        if (playerStats == null)
            playerStats = GetComponentInParent<PlayerStats>();

        // 가능한 한 같은 루트(같은 플레이어)의 컴포넌트를 우선 사용합니다.
        Transform root = transform.root;
        if (root != null)
        {
            if (upgrade == null)
                upgrade = root.GetComponentInChildren<Upgrade>(true);

            if (playerHealth == null)
                playerHealth = root.GetComponentInChildren<PlayerHealth>(true);

            if (playerMovement == null)
                playerMovement = root.GetComponentInChildren<PlayerMovement>(true);

            if (playerStats == null)
                playerStats = root.GetComponentInChildren<PlayerStats>(true);
        }

        // 마지막 fallback: 씬 전체 검색(동명이인/다중 플레이어 상황에서는 잘못 잡을 수 있어 권장하지 않음)
        if (upgrade == null)
            upgrade = Object.FindFirstObjectByType<Upgrade>();

        if (playerHealth == null)
            playerHealth = Object.FindFirstObjectByType<PlayerHealth>();

        if (playerMovement == null)
            playerMovement = Object.FindFirstObjectByType<PlayerMovement>();

        if (playerStats == null)
            playerStats = Object.FindFirstObjectByType<PlayerStats>();

        if (enableDebugLog)
        {
            Debug.Log($"[UpgradeEffectRuntime] BindReferences - upgrade:{(upgrade != null ? upgrade.name : "null")}, playerHealth:{(playerHealth != null ? playerHealth.name : "null")}, owner:{name}");
        }
    }

    private void BindUpgrade()
    {
        if (upgrade == null)
            return;

        upgrade.OnSlotsChanged -= RefreshEffects;
        upgrade.OnSlotsChanged += RefreshEffects;
    }

    private void UnbindUpgrade()
    {
        if (upgrade == null)
            return;

        upgrade.OnSlotsChanged -= RefreshEffects;
    }

    private void RefreshEffects()
    {
        StopAllSlotEffects();

        RebuildWeaponDamageModifiers();
        RebuildCompanionCooldownModifiers();

        Transform equipRoot = transform.root;
        if (equipRoot != null)
        {
            var equip = equipRoot.GetComponentInChildren<PlayerEquipmentController>(true);
            if (equip != null)
                equip.ApplyExtendedMagazineFromUpgrades();
        }

        RebuildMoveSpeedModifiers();
        RebuildStaminaRechargeModifiers();

        if (upgrade == null || playerHealth == null)
        {
            if (enableDebugLog)
                Debug.LogWarning($"[UpgradeEffectRuntime] RefreshEffects(HP 코루틴 스킵) - upgrade:{upgrade != null}, playerHealth:{playerHealth != null}, owner:{name}");
            if (upgrade == null)
                return;
        }

        int appliedCount = 0;
        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            UpgradeEffectSO slot = upgrade.GetSlot(i);
            if (slot == null)
                continue;

            if (slot is Upgrade_01_01_HPRegen hpRegen)
            {
                if (playerHealth == null)
                    continue;

                int stacks = upgrade.GetStackCount(i);
                slotCoroutines[i] = StartCoroutine(RunHpRegen(hpRegen, stacks));
                appliedCount++;

                if (enableDebugLog)
                {
                    Debug.Log($"[UpgradeEffectRuntime] HPRegen 적용 - slot:{i}, stacks:{stacks}, id:{hpRegen.id}, interval:{hpRegen.tickInterval}, heal:{hpRegen.healAmountPerTick * stacks}");
                }
            }
        }

        if (enableDebugLog)
            Debug.Log($"[UpgradeEffectRuntime] 적용된 효과 코루틴 수: {appliedCount}");
    }

    private void RebuildWeaponDamageModifiers()
    {
        if (upgrade == null)
            return;

        Transform root = transform.root;
        if (root == null)
            return;

        var mods = root.GetComponentInChildren<PlayerWeaponDamageModifiers>(true);
        if (mods == null)
            mods = root.gameObject.AddComponent<PlayerWeaponDamageModifiers>();

        mods.RebuildFromUpgradeSlots(upgrade);
    }

    private void RebuildCompanionCooldownModifiers()
    {
        if (upgrade == null)
            return;

        Transform root = transform.root;
        if (root == null)
            return;

        var mods = root.GetComponentInChildren<PlayerCompanionCooldownModifiers>(true);
        if (mods == null)
            mods = root.gameObject.AddComponent<PlayerCompanionCooldownModifiers>();

        mods.RebuildFromUpgradeSlots(upgrade);
    }

    private void RebuildMoveSpeedModifiers()
    {
        if (upgrade == null)
            return;

        float percentSum = 0f;
        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            UpgradeEffectSO slot = upgrade.GetSlot(i);
            if (slot is not Upgrade_01_04_SpeedUp speedUp)
                continue;

            percentSum += Mathf.Max(0f, speedUp.additiveMoveSpeedPercent) * upgrade.GetStackCount(i);
        }

        ApplyMoveSpeedFromUpgrades(1f + percentSum);
    }

    private void ApplyMoveSpeedFromUpgrades(float multiplier)
    {
        if (playerMovement == null)
            return;

        playerMovement.SetExternalMoveSpeedMultiplier(multiplier);
        if (enableDebugLog)
            Debug.Log($"[UpgradeEffectRuntime] 이동속도 배율 적용: x{multiplier:F3}");
    }

    private void RebuildStaminaRechargeModifiers()
    {
        if (upgrade == null)
            return;

        float percentSum = 0f;
        float delayReductionSum = 0f;
        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            UpgradeEffectSO slot = upgrade.GetSlot(i);
            if (slot is not Upgrade_01_05_SwiftRecovery swift)
                continue;

            int stacks = upgrade.GetStackCount(i);
            percentSum += Mathf.Max(0f, swift.additiveStaminaRegenPercent) * stacks;
            delayReductionSum += Mathf.Max(0f, swift.staminaRechargeDelayReduction) * stacks;
        }

        ApplyStaminaRechargeFromUpgrades(1f + percentSum);
        ApplyStaminaRechargeDelayReductionFromUpgrades(delayReductionSum);
    }

    private void ApplyStaminaRechargeFromUpgrades(float multiplier)
    {
        if (playerStats == null)
            return;

        playerStats.SetExternalStaminaRechargeMultiplier(multiplier);
        if (enableDebugLog)
            Debug.Log($"[UpgradeEffectRuntime] 스태미나 회복 배율 적용: x{multiplier:F3}");
    }

    private void ApplyStaminaRechargeDelayReductionFromUpgrades(float reductionSeconds)
    {
        if (playerStats == null)
            return;

        playerStats.SetExternalStaminaRechargeDelayReduction(reductionSeconds);
        if (enableDebugLog)
            Debug.Log($"[UpgradeEffectRuntime] 스태미나 회복 지연 감소 적용: -{reductionSeconds:F3}s");
    }

    private void StopAllSlotEffects()
    {
        for (int i = 0; i < slotCoroutines.Length; i++)
        {
            if (slotCoroutines[i] == null)
                continue;

            StopCoroutine(slotCoroutines[i]);
            slotCoroutines[i] = null;
        }
    }

    private IEnumerator RunHpRegen(Upgrade_01_01_HPRegen effect, int stacks)
    {
        float interval = Mathf.Max(0.05f, effect.tickInterval);
        float heal = Mathf.Max(0f, effect.healAmountPerTick) * Mathf.Max(1, stacks);
        WaitForSeconds wait = new WaitForSeconds(interval);

        while (true)
        {
            yield return wait;

            if (playerHealth == null)
                yield break;

            if (heal > 0f)
                playerHealth.Heal(heal);
        }
    }
}
