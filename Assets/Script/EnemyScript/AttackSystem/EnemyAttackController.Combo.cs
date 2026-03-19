using System.Collections;
using UnityEngine;

/// <summary>
/// Partial: Combo handling (delegates slot execution to Melee StartMelee/FinishMelee).
/// - For each slot (MeleeAttackData) calls StartMelee(slot, -1) so per-slot Melee behavior (lockTiming, target snapshot,
///   forceApplyTime, moving attack, hitbox spawn, etc.) is identical to standalone Melee.
/// - BeginComboMode/EndComboMode used only to suppress per-slot cooldowns and optionally override range.
/// - interSlotDelay and combo-level grantSuperArmor have been removed (per request).
/// </summary>
public partial class EnemyAttackController : MonoBehaviour
{
    private Coroutine comboCoroutine = null;
    private bool isRunningCombo = false;
    private int runningComboIndex = -1;
    private ComboAttackData runningComboData = null;

    /// <summary>
    /// Public entry to start a combo. Single definition to avoid duplicate symbol errors.
    /// </summary>
    public void StartCombo(ComboAttackData comboData, Transform target, int comboIndex)
    {
        if (comboData == null) return;

        // Stop any existing combo
        if (comboCoroutine != null)
        {
            try { StopCoroutine(comboCoroutine); } catch { }
            comboCoroutine = null;
        }

        comboCoroutine = StartCoroutine(ComboRoutine(comboData, target, comboIndex));
        Log($"COMBO START idx={comboIndex} name={(comboData != null ? comboData.attackName : "(null)")}");
    }

    /// <summary>
    /// Interrupt and cleanup combo. Also apply combo cooldown if we know which combo was running.
    /// </summary>
    public void InterruptCombo()
    {
        if (comboCoroutine != null)
        {
            try { StopCoroutine(comboCoroutine); } catch { }
            comboCoroutine = null;
        }

        // ensure combo flags cleaned and apply combo cooldown if available
        if (runningComboIndex >= 0 && runningComboData != null)
        {
            ApplyPerAttackCooldown(runningComboIndex, runningComboData.cooldown);
            ApplyGlobalCooldown();
        }

        EndComboMode();

        // conservative animator/state cleanup
        SafeSetBool("IsAttackPrepare", false);
        SafeSetBool("IsAttack", false);
        if (enemy != null && enemy.CurrentState == Enemy.EnemyState.Attack && !IsHardCrowdControlled())
            enemy.SetState(Enemy.EnemyState.Chase);
        enemy?.RemoveSuperArmor(SuperArmorSource.Attack);

        isRunningCombo = false;
        runningComboIndex = -1;
        runningComboData = null;
    }

    private IEnumerator ComboRoutine(ComboAttackData comboData, Transform target, int comboIndex)
    {
        if (comboData == null) yield break;

        isRunningCombo = true;
        runningComboIndex = comboIndex;
        runningComboData = comboData;

        // Mark executed / clear hold like other attack starters
        MarkExecuted();
        ClearHold();

        // Begin combo mode: suppress per-slot cooldowns and override range only
        float comboRange = comboData != null ? comboData.range : -1f;
        BeginComboMode(comboRange);

        // Ensure enemy is in Attack state
        enemy.SetState(Enemy.EnemyState.Attack);

        var slots = comboData.slots;
        if (slots == null) slots = new MeleeAttackData[0];

        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot == null) continue;

            while (enemy != null && enemy.IsStateHoldActive)
                yield return null;

            // Delegate to Melee StartMelee (pass -1 so per-slot readyTimes are not affected)
            StartMelee(slot, -1);

            // Wait until this melee finishes (attackInProgress becomes false)
            while (attackInProgress)
            {
                if (enemy != null && enemy.IsStateHoldActive)
                {
                    yield return null;
                    continue;
                }

                // If externally interrupted, clean up
                if (!isRunningCombo)
                {
                    EndComboMode();
                    runningComboIndex = -1;
                    runningComboData = null;
                    comboCoroutine = null;
                    yield break;
                }
                yield return null;
            }
        }

        // Combo finished normally: end combo mode and apply combo-level cooldown once
        EndComboMode();
        isRunningCombo = false;
        comboCoroutine = null;

        if (comboIndex >= 0 && comboData != null)
        {
            ApplyPerAttackCooldown(comboIndex, comboData.cooldown);
            ApplyGlobalCooldown();
        }

        // Cleanup animation flags/state
        SafeSetBool("IsAttackPrepare", false);
        SafeSetBool("IsAttack", false);
        if (enemy != null)
        {
            enemy.RemoveSuperArmor(SuperArmorSource.Attack);
            if (enemy.CurrentState == Enemy.EnemyState.Attack && !IsHardCrowdControlled())
                enemy.SetState(Enemy.EnemyState.Chase);
        }

        runningComboIndex = -1;
        runningComboData = null;

        Log($"COMBO END idx={comboIndex} name={comboData.attackName}");
        yield break;
    }

    /// <summary>
    /// Called by higher-level interruption checks (e.g. AI state change) to interrupt combo if running.
    /// Keeps behavior explicit.
    /// </summary>
    private void InterruptComboIfNeeded()
    {
        if (isRunningCombo || comboCoroutine != null)
        {
            Log("INTERRUPT combo -> cancel");
            InterruptCombo();
        }
    }
}