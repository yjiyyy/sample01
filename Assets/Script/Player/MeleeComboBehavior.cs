// MeleeComboBehavior.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MeleeComboBehavior
/// - WeaponBehavior???? ??? ?????? ???????.
/// </summary>
[DisallowMultipleComponent]
public class MeleeComboBehavior : MonoBehaviour
{
    private MeleeComboSO comboData;
    private PlayerAnimationController animCtrl;
    private Transform spawnPoint;
    private Func<WeaponDataSO> getWeaponData;
    private Func<PlayerState> getCurrentState;
    private Action<PlayerState> changeState;
    private PlayerMovement playerMovement;
    private PlayerWeaponController ownerController;

    // runtime
    private int currentStepIndex = 0;
    private float stepElapsed = 0f;
    private bool comboActive = false;
    private Coroutine activeStepRoutine;

    // proxies
    private List<WeaponDataSO> stepProxies = new List<WeaponDataSO>();
    private WeaponDataSO lastWeaponForProxy = null;

    // movement restore
    private bool prevMovementEnabled = true;
    private bool didDisableMovementForCombo = false;

    // defensive
    private bool debugMode = false;

    /// <summary>ignoreTimeAfterInput ???? ?? ????(??) ??? ????? ????. ???? ??? ?????.</summary>
    private const float MOVEMENT_UNLOCK_DELAY = 0.1f;
    private const float NO_MOVE_SWITCH_NEAR_STEP_END = 0.1f;

    public void Setup(
        MeleeComboSO combo,
        PlayerAnimationController anim,
        Transform meleeSpawnPoint,
        Func<WeaponDataSO> getWeapon,
        Func<PlayerState> getState,
        Action<PlayerState> changeStateAction,
        PlayerMovement movement,
        PlayerWeaponController owner,
        bool debug = false)
    {
        comboData = combo;
        animCtrl = anim;
        spawnPoint = meleeSpawnPoint != null ? meleeSpawnPoint : transform;
        getWeaponData = getWeapon;
        getCurrentState = getState;
        changeState = changeStateAction;
        playerMovement = movement;
        ownerController = owner;
        debugMode = debug;

        EnsureProxies(force: true);
    }

    /// <summary>??? ???? ?? ????. ??? ?? ???? ??? ?????? ???.</summary>
    public bool IsComboActive => comboActive;

    public void OnPress()
    {
        if (comboData == null || comboData.steps == null || comboData.steps.Count == 0) return;

        // If not active, start combo
        if (!comboActive)
        {
            StartCombo();
            return;
        }

        // If active, try to advance if inside input window
        TryAdvanceOnInput();
    }

    private void StartCombo()
    {
        if (comboData == null || comboData.steps.Count == 0) return;

        // Guard: if current player state blocks attacking, ignore
        var s = getCurrentState != null ? getCurrentState() : PlayerState.Idle;
        if (s == PlayerState.Knockback || s == PlayerState.Stun || s == PlayerState.Dead || s == PlayerState.Evade)
        {
            if (debugMode) Debug.Log("[Combo] Start blocked by state: " + s);
            return;
        }

        comboActive = true;
        currentStepIndex = 0;
        stepElapsed = 0f;

        // Lock movement: save and disable if currently enabled
        if (playerMovement != null)
        {
            prevMovementEnabled = playerMovement.enabled;
            if (playerMovement.enabled)
            {
                playerMovement.enabled = false;
                didDisableMovementForCombo = true;
            }
            else
            {
                didDisableMovementForCombo = false;
            }
        }
        else
        {
            didDisableMovementForCombo = false;
        }

        ownerController?.SetMeleeComboAllowMove(false);

        // Set owner state to Attack
        changeState?.Invoke(PlayerState.Attack);

        // start step
        if (activeStepRoutine != null) StopCoroutine(activeStepRoutine);
        activeStepRoutine = StartCoroutine(StepRoutine(currentStepIndex));
    }

    private void TryAdvanceOnInput()
    {
        if (!comboActive) return;
        if (comboData == null) return;
        if (currentStepIndex < 0 || currentStepIndex >= comboData.steps.Count) return;

        var step = comboData.steps[currentStepIndex];
        if (step == null) return;

        // Check elapsed vs ignoreTime
        if (stepElapsed >= step.ignoreTimeAfterInput && stepElapsed < step.stepDuration)
        {
            AdvanceToNextStep();
        }
        else
        {
            if (debugMode) Debug.Log($"[Combo] Press outside input window (elapsed:{stepElapsed:F2}, ignore:{step.ignoreTimeAfterInput:F2}, dur:{step.stepDuration:F2})");
        }
    }

    private void AdvanceToNextStep()
    {
        if (activeStepRoutine != null)
        {
            try { StopCoroutine(activeStepRoutine); } catch { }
            activeStepRoutine = null;
        }

        ownerController?.SetMeleeComboAllowMove(false);
        if (playerMovement != null && didDisableMovementForCombo == false)
        {
            playerMovement.enabled = false;
            didDisableMovementForCombo = true;
        }

        changeState?.Invoke(PlayerState.Attack);
        currentStepIndex++;
        if (currentStepIndex >= comboData.steps.Count)
        {
            if (comboData.loop)
            {
                currentStepIndex = 0;
            }
            else
            {
                EndCombo();
                return;
            }
        }

        stepElapsed = 0f;
        activeStepRoutine = StartCoroutine(StepRoutine(currentStepIndex));
    }

    private IEnumerator StepRoutine(int stepIndex)
    {
        if (comboData == null || stepIndex < 0 || stepIndex >= comboData.steps.Count)
        {
            EndCombo();
            yield break;
        }

        var step = comboData.steps[stepIndex];
        if (step == null)
        {
            EndCombo();
            yield break;
        }

        // ??? ???? ????? (???? SO?? ???, trailEmitDuration>0?? ????)
        var wbTrail = GetComponent<WeaponBehavior>();
        if (wbTrail != null && step.trailEmitDuration > 0f)
            wbTrail.StartTrailEmitWindow(step.trailEmitStartDelay, step.trailEmitDuration);

        // Play animation
        // ????: ???? animClip?? ?????? ???? ??????? ????? ???????. (???? ????? ??? ????)
        if (step.animClip != null)
        {
            string animName = step.animClip.name;
            try
            {
                animCtrl?.PlayChargedAttack(animName);
            }
            catch { try { animCtrl?.PlayChargedAttack(animName); } catch { } }
        }
        else
        {
            // animClip?? ?????? ?????? ??????? ?????? (?????? ????)
            if (debugMode) Debug.Log("[Combo] step has no animClip -> no animation will be played for this step.");
        }

        // Spawn hitbox after hitboxSpawnDelay
        float spawnTime = Time.time + step.hitboxSpawnDelay;
        while (Time.time < spawnTime)
        {
            var st = getCurrentState != null ? getCurrentState() : PlayerState.Idle;
            if (st == PlayerState.Knockback || st == PlayerState.Stun || st == PlayerState.Dead || st == PlayerState.Evade)
            {
                EndCombo();
                yield break;
            }
            yield return null;
        }

        // Determine prefab (step -> weapon default)
        GameObject prefabToSpawn = step.hitBoxPrefab;
        var weapon = getWeaponData != null ? getWeaponData() : null;
        if (prefabToSpawn == null && weapon != null)
            prefabToSpawn = weapon.meleeHitboxPrefab;

        if (prefabToSpawn != null)
        {
            GameObject hb = Instantiate(prefabToSpawn, spawnPoint != null ? spawnPoint.position : transform.position, spawnPoint != null ? spawnPoint.rotation : transform.rotation);
            if (hb != null)
            {
                if (hb.TryGetComponent<HitBox_PC>(out var hitbox))
                {
                    EnsureProxies(force: false);

                    WeaponDataSO proxy = (stepIndex < stepProxies.Count && stepProxies[stepIndex] != null) ? stepProxies[stepIndex] : null;
                    if (proxy == null)
                    {
                        proxy = ScriptableObject.CreateInstance<WeaponDataSO>();
                        proxy.weaponName = "ComboStepProxy";
                        CopyStepToProxy(step, proxy, weapon);
                    }

                    hitbox.SetWeapon(proxy);

                    float dmg = proxy != null ? proxy.damage : 0f;
                    float rng = proxy != null ? proxy.range : 2.5f;
                    float kb = proxy != null ? proxy.knockbackPower : 0f;
                    float life = Mathf.Max(0.01f, step.hitBoxLifetime > 0f ? step.hitBoxLifetime : (weapon != null ? weapon.hitBoxLifetime : 0.15f));

                    if (step.allowDuplicateHit)
                    {
                        hitbox.Initialize(dmg, rng, kb, life, allowDup: true, dupInterval: step.duplicateInterval);
                    }
                    else
                    {
                        hitbox.Initialize(dmg, rng, kb, life);
                    }
                }
                else
                {
                    Debug.LogWarning("[Combo] HitBox prefab missing HitBox_PC component.");
                }
            }
        }
        else if (weapon != null && weapon.UseWeaponCollider)
        {
            // Bat ??: ???? ???? HitBox_PC ?????? ???? (?????? ???? ??)
            EnsureProxies(force: false);

            WeaponDataSO proxy = (stepIndex < stepProxies.Count && stepProxies[stepIndex] != null) ? stepProxies[stepIndex] : null;
            if (proxy == null)
            {
                proxy = ScriptableObject.CreateInstance<WeaponDataSO>();
                proxy.weaponName = "ComboStepProxy";
                CopyStepToProxy(step, proxy, weapon);
            }

            if (step.allowDuplicateHit && debugMode)
                Debug.Log("[Combo] allowDuplicateHit?? ???? ?????? ??????? ??????? ??????.");

            float colliderLife = Mathf.Max(0.01f, step.hitBoxLifetime > 0f ? step.hitBoxLifetime : weapon.hitBoxLifetime);
            var wb = GetComponent<WeaponBehavior>();
            if (wb != null)
                wb.ActivateMeleeColliderHitboxForCombo(proxy, colliderLife);
            else
                Debug.LogWarning("[Combo] ???? WeaponCollider ??????? WeaponBehavior?? ???????.");
        }
        else if (weapon != null)
        {
            Debug.LogWarning($"[Combo] '{step.name}': SpawnPrefab ??????? hitBoxPrefab/meleeHitboxPrefab?? ??? ??????.");
        }

        // Step timing loop
        stepElapsed = 0f;
        float dur = Mathf.Max(0.01f, step.stepDuration);

        while (stepElapsed < dur)
        {
            var st = getCurrentState != null ? getCurrentState() : PlayerState.Idle;
            if (st == PlayerState.Knockback || st == PlayerState.Stun || st == PlayerState.Dead || st == PlayerState.Evade)
            {
                EndCombo();
                yield break;
            }

            float movementUnlockAt = step.ignoreTimeAfterInput + MOVEMENT_UNLOCK_DELAY;
            bool inMovementWindow = stepElapsed >= movementUnlockAt;
            bool nearStepEnd = stepElapsed >= dur - NO_MOVE_SWITCH_NEAR_STEP_END;
            if (inMovementWindow)
            {
                if (didDisableMovementForCombo)
                {
                    ownerController?.SetMeleeComboAllowMove(true);
                    if (playerMovement != null)
                    {
                        playerMovement.enabled = prevMovementEnabled;
                        playerMovement.ClearStoredInput();
                        didDisableMovementForCombo = false;
                    }
                }
                if (!nearStepEnd && playerMovement != null && playerMovement.HasMovementInput() && changeState != null)
                    changeState(PlayerState.Move);
            }

            stepElapsed += Time.deltaTime;
            yield return null;
        }

        // Step expired -> end combo
        EndCombo();
    }

    private void EndCombo()
    {
        if (!comboActive) return;

        comboActive = false;
        ownerController?.SetMeleeComboAllowMove(false);

        if (activeStepRoutine != null)
        {
            try { StopCoroutine(activeStepRoutine); } catch { }
            activeStepRoutine = null;
        }

        // restore movement.enabled only if we explicitly disabled it for combo
        if (playerMovement != null && didDisableMovementForCombo)
        {
            playerMovement.enabled = prevMovementEnabled;
            playerMovement.ClearStoredInput();
        }

        if (changeState != null)
        {
            if (playerMovement != null && playerMovement.GetVelocityMagnitude() > 0.1f)
                changeState(PlayerState.Move);
            else
                changeState(PlayerState.Idle);
        }

        currentStepIndex = 0;
        stepElapsed = 0f;

        if (debugMode) Debug.Log("[Combo] Ended");
    }

    public void CancelCombo()
    {
        EndCombo();
    }

    private void OnDisable()
    {
        EndCombo();
        CleanupProxies();
    }

    private void OnDestroy()
    {
        EndCombo();
        CleanupProxies();
    }

    private void EnsureProxies(bool force = false)
    {
        if (comboData == null || comboData.steps == null) return;

        var weaponDefault = getWeaponData != null ? getWeaponData() : null;
        bool needRebuild = force || lastWeaponForProxy != weaponDefault || stepProxies.Count != comboData.steps.Count;

        if (!needRebuild) return;

        for (int i = 0; i < stepProxies.Count; i++)
        {
            if (stepProxies[i] != null)
            {
                try { Destroy(stepProxies[i]); } catch { }
            }
        }
        stepProxies.Clear();

        for (int i = 0; i < comboData.steps.Count; i++)
        {
            var step = comboData.steps[i];
            if (step == null)
            {
                stepProxies.Add(null);
                continue;
            }

            var proxy = ScriptableObject.CreateInstance<WeaponDataSO>();
            proxy.weaponName = $"ComboStepProxy_{i}";
            proxy.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;

            CopyStepToProxy(step, proxy, weaponDefault);

            stepProxies.Add(proxy);
        }

        lastWeaponForProxy = weaponDefault;
    }

    private void CopyStepToProxy(MeleeComboStepSO step, WeaponDataSO proxy, WeaponDataSO weaponDefault)
    {
        if (proxy == null || step == null) return;

        proxy.weaponName = $"ComboStepProxy_{step.name}";

        proxy.cooldown = step.cooldown >= 0f ? step.cooldown : (weaponDefault != null ? weaponDefault.cooldown : 0.5f);
        proxy.damage = step.damage >= 0f ? step.damage : (weaponDefault != null ? weaponDefault.damage : 0f);
        proxy.range = step.range >= 0f ? step.range : (weaponDefault != null ? weaponDefault.range : 2.5f);

        proxy.hitBoxLifetime = step.hitBoxLifetime > 0f ? step.hitBoxLifetime : (weaponDefault != null ? weaponDefault.hitBoxLifetime : 0.15f);

        proxy.knockbackDuration = step.knockbackDuration >= 0f ? step.knockbackDuration : (weaponDefault != null ? weaponDefault.knockbackDuration : 0f);
        proxy.knockbackPower = step.knockbackPower >= 0f ? step.knockbackPower : (weaponDefault != null ? weaponDefault.knockbackPower : 0f);
        proxy.jerkIntensity = step.jerkIntensity >= 0f ? step.jerkIntensity : (weaponDefault != null ? weaponDefault.jerkIntensity : 0f);
        proxy.jerkDuration = step.jerkDuration >= 0f ? step.jerkDuration : (weaponDefault != null ? weaponDefault.jerkDuration : 0f);

        proxy.stunDuration = step.stunDuration >= 0f ? step.stunDuration : (weaponDefault != null ? weaponDefault.stunDuration : 0f);

        proxy.usePushInsteadOfKnockback = step.usePushInsteadOfKnockback;

        proxy.targetHoldDuration = step.targetHoldDuration >= 0f ? step.targetHoldDuration : (weaponDefault != null ? weaponDefault.targetHoldDuration : 0f);
        proxy.attackerHoldDuration = step.attackerHoldDuration >= 0f ? step.attackerHoldDuration : (weaponDefault != null ? weaponDefault.attackerHoldDuration : 0f);

        proxy.deathMode = step.deathMode;
        proxy.ragdollImpulse = step.ragdollImpulse >= 0f ? step.ragdollImpulse : (weaponDefault != null ? weaponDefault.ragdollImpulse : 0f);
        proxy.ragdollUpImpulse = step.ragdollUpImpulse >= 0f ? step.ragdollUpImpulse : (weaponDefault != null ? weaponDefault.ragdollUpImpulse : 0f);
        proxy.ragdollSpinTorque = step.ragdollSpinTorque >= 0f ? step.ragdollSpinTorque : (weaponDefault != null ? weaponDefault.ragdollSpinTorque : 0f);
        proxy.sliceTargets = (step.sliceTargets != null && step.sliceTargets.Count > 0) ? new List<SliceTarget>(step.sliceTargets) : (weaponDefault != null ? new List<SliceTarget>(weaponDefault.sliceTargets) : new List<SliceTarget>());
        proxy.sliceImpulse = step.sliceImpulse >= 0f ? step.sliceImpulse : (weaponDefault != null ? weaponDefault.sliceImpulse : 0f);

        proxy.recoilStartDelay = step.recoilStartDelay >= 0f ? step.recoilStartDelay : (weaponDefault != null ? weaponDefault.recoilStartDelay : 0f);
        proxy.recoilPower = step.recoilPower >= 0f ? step.recoilPower : (weaponDefault != null ? weaponDefault.recoilPower : 0f);
        proxy.recoilDuration = step.recoilDuration >= 0f ? step.recoilDuration : (weaponDefault != null ? weaponDefault.recoilDuration : 0f);

        proxy.hitEffectPrefab = step.hitEffectPrefab != null
            ? step.hitEffectPrefab
            : (weaponDefault != null ? weaponDefault.hitEffectPrefab : null);
    }

    private void CleanupProxies()
    {
        if (stepProxies == null) return;
        for (int i = 0; i < stepProxies.Count; i++)
        {
            var p = stepProxies[i];
            if (p != null)
            {
                try { Destroy(p); } catch { }
            }
        }
        stepProxies.Clear();
        lastWeaponForProxy = null;
    }
}