using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerChargeController : MonoBehaviour
{
    // 주입
    private Func<WeaponDataSO> getWeaponData;
    private Func<PlayerState> getState;
    private Action<PlayerState> changeState;
    private Action<bool> setInvincible; // 차지 무적 토글(컨트롤러로 전달)
    private Transform spawnPoint;
    private PlayerAnimationController anim;
    private bool enableChargeMessages;
    private bool debugMode;

    // 내부 상태
    private bool chargeHoldActive = false;
    private float chargeHoldElapsed = 0f;
    private bool chargeStartMsgDone = false;
    private bool chargeSuccessMsgDone = false;
    private bool chargeExecuted = false;
    private bool chargeReady = false;

    private Coroutine chargeSpawnRoutine;
    private Coroutine chargedMaintainRoutine;

    // New: continuous charge
    private Coroutine continuousRoutine;
    private Coroutine movementRoutine;
    private Coroutine perCycleSpawnRoutine;
    private Coroutine pendingContinuousStarter; // NEW: 연속 시작 대기 코루틴 참조
    private Coroutine faceNearestRoutine; // NEW: nearest facing coroutine
    private bool continuousActive = false;
    private PlayerChargeAttackSO activeContinuousSlot = null;
    private float superArmorRemaining = 0f;

    /// <summary>차지 공격 중 슈퍼아머 유효 여부. ForceApplyKnockback 시 방향 전환 스킵용.</summary>
    public bool HasSuperArmorActive => superArmorRemaining > 0f;

    private WeaponDataSO chargeWeaponProxy;
    private PlayerWeaponController weaponCtrl;

    public void Setup(
        PlayerAnimationController animCtrl,
        Transform meleeSpawnPoint,
        Func<WeaponDataSO> getWeapon,
        Func<PlayerState> getCurrentState,
        Action<PlayerState> changeStateAction,
        Action<bool> setInvincibleAction,
        bool enableMessages,
        bool debug)
    {
        anim = animCtrl;
        spawnPoint = meleeSpawnPoint != null ? meleeSpawnPoint : transform;
        getWeaponData = getWeapon;
        getState = getCurrentState;
        changeState = changeStateAction;
        setInvincible = setInvincibleAction;
        enableChargeMessages = enableMessages;
        debugMode = debug;
        weaponCtrl = GetComponent<PlayerWeaponController>();
    }

    private bool IsHoldActive()
    {
        if (weaponCtrl == null) weaponCtrl = GetComponent<PlayerWeaponController>();
        return weaponCtrl != null && weaponCtrl.IsTimeHoldActive;
    }

    public void Tick()
    {
        var data = getWeaponData != null ? getWeaponData() : null;
        // ---- 변경: AR 무기일 경우 차지 슬롯을 무시하도록 처리 ----
        PlayerChargeAttackSO slot = null;
        if (data != null && !(data is WeaponDataSO_AR))
            slot = data.chargeSlot;
        // ----------------------------------------------------

        // Down: 홀드 시작
        if (!chargeHoldActive && InputManager.Instance.GetAttackDown())
        {
            if (slot == null)
            {
                if (debugMode) Debug.Log("[Charge] 시작 불가: 현재 무기에 차지 슬롯 없음");
            }
            else
            {
                chargeHoldActive = true;
                chargeHoldElapsed = 0f;
                chargeStartMsgDone = false;
                chargeSuccessMsgDone = false;
                chargeExecuted = false;
                chargeReady = false;
                if (debugMode) Debug.Log("[Charge] 홀드 시작");
            }
        }

        // Hold 유지: 메시지/성공 플래그
        if (chargeHoldActive && InputManager.Instance.GetAttack())
        {
            if (!IsHoldActive())
                chargeHoldElapsed += Time.deltaTime;
            float held = chargeHoldElapsed;

            if (enableChargeMessages && !chargeStartMsgDone && held >= 1.0f)
            {
                chargeStartMsgDone = true;
                Debug.Log("차지 시작");
            }

            if (slot != null && !chargeReady && held >= slot.holdSuccessTime)
            {
                chargeReady = true;
                if (enableChargeMessages && !chargeSuccessMsgDone)
                {
                    chargeSuccessMsgDone = true;
                    Debug.Log("차지 성공");
                }

                // 변경: 단발 차지는 ready 시 상태를 Attack으로 강제하지 않음(이동/회피 허용).
                // 연속 차지만 ready 시 상태 전환/루프 시작을 시도한다.
                if (slot.continuousWhileHeld)
                {
                    if (IsChargeExecutionAllowedNow())
                    {
                        // 연속 차지는 시작 시 Attack 상태로 전환
                        changeState?.Invoke(PlayerState.Attack);

                        if (pendingContinuousStarter != null) { StopCoroutine(pendingContinuousStarter); pendingContinuousStarter = null; }
                        continuousRoutine = StartCoroutine(ContinuousChargeLoop(slot));
                    }
                    else
                    {
                        // 상태 허용 대기 (버튼 해제 시 자동 취소)
                        if (pendingContinuousStarter != null) StopCoroutine(pendingContinuousStarter);
                        pendingContinuousStarter = StartCoroutine(StartContinuousWhenAllowed(slot));
                        if (debugMode) Debug.Log("[Charge] 연속 차지 시작 대기 등록");
                    }
                }
                else
                {
                    // 단발 차지: ready 상태로 머무르며 이동/회피 허용.
                    // (발사는 버튼 해제 시 검사에서 처리)
                    if (debugMode) Debug.Log("[Charge] 단발 차지 준비 완료(이동/회피 허용)");
                }
            }
        }

        // Up: 발사 시도 / 또는 연속 차지 중단
        if (chargeHoldActive && InputManager.Instance.GetAttackUp())
        {
            bool fired = false;

            // ---- 변경: Up 시에도 current weapon이 AR이면 차지 슬롯 무시 ----
            var data2 = getWeaponData != null ? getWeaponData() : null;
            var slot2 = (data2 != null && !(data2 is WeaponDataSO_AR)) ? data2.chargeSlot : null;
            // ------------------------------------------------------------

            // If continuous loop is active for this slot, stop it and don't fire single shot
            if (slot2 != null && slot2.continuousWhileHeld && continuousActive)
            {
                CancelContinuous();
                if (debugMode) Debug.Log("[Charge] 연속 차지 중단(버튼 해제)");
            }
            else
            {
                if (slot2 == null)
                {
                    if (debugMode) Debug.Log("[Charge] 취소: 방출 시점에 차지 슬롯 없음");
                }
                else if (!chargeReady)
                {
                    if (debugMode) Debug.Log("[Charge] 실패: 성공 시간 도달 전 방출");
                }
                else if (!IsChargeExecutionAllowedNow())
                {
                    if (debugMode) Debug.Log("[Charge] 취소: 방출 시점 상태가 Idle/Move 아님");
                }
                else if (!chargeExecuted)
                {
                    ExecuteChargeAttack(slot2);
                    chargeExecuted = true;
                    fired = true;
                }
            }

            // 플래그 리셋
            chargeHoldActive = false;
            chargeHoldElapsed = 0f;
            chargeStartMsgDone = false;
            chargeSuccessMsgDone = false;
            chargeReady = false;
            chargeExecuted = false;

            if (fired && debugMode) Debug.Log("[Charge] 릴리스 → 발사 완료");
        }
    }

    public void CancelAll()
    {
        // 홀드/플래그
        chargeHoldActive = false;
        chargeHoldElapsed = 0f;
        chargeStartMsgDone = false;
        chargeSuccessMsgDone = false;
        chargeReady = false;
        chargeExecuted = false;

        // 스폰 대기 중단
        if (chargeSpawnRoutine != null)
        {
            StopCoroutine(chargeSpawnRoutine);
            chargeSpawnRoutine = null;
        }

        // per-cycle spawn 취소
        if (perCycleSpawnRoutine != null)
        {
            StopCoroutine(perCycleSpawnRoutine);
            perCycleSpawnRoutine = null;
        }

        // pending continuous starter 취소
        if (pendingContinuousStarter != null)
        {
            StopCoroutine(pendingContinuousStarter);
            pendingContinuousStarter = null;
        }

        // 유지 코루틴 중단
        if (chargedMaintainRoutine != null)
        {
            StopCoroutine(chargedMaintainRoutine);
            chargedMaintainRoutine = null;
        }

        // 연속 차지 중단
        CancelContinuous();

        // 차지 무적 해제
        setInvincible?.Invoke(false);
    }

    private void CancelContinuous()
    {
        // Stop continuous activity and cleanup related coroutines and effects,
        // then restore player state to Move/Idle based on current movement speed.
        continuousActive = false;

        if (continuousRoutine != null)
        {
            StopCoroutine(continuousRoutine);
            continuousRoutine = null;
        }

        if (movementRoutine != null)
        {
            StopCoroutine(movementRoutine);
            movementRoutine = null;
        }

        if (faceNearestRoutine != null)
        {
            StopCoroutine(faceNearestRoutine);
            faceNearestRoutine = null;
        }

        // pending continuous starter 취소
        if (pendingContinuousStarter != null)
        {
            StopCoroutine(pendingContinuousStarter);
            pendingContinuousStarter = null;
        }

        // Stop outstanding per-cycle spawn if running
        if (perCycleSpawnRoutine != null)
        {
            StopCoroutine(perCycleSpawnRoutine);
            perCycleSpawnRoutine = null;
            if (debugMode) Debug.Log("[Charge] per-cycle spawn coroutine stopped");
        }

        // Stop outstanding single-shot spawn/maintain routines (if any)
        if (chargeSpawnRoutine != null)
        {
            StopCoroutine(chargeSpawnRoutine);
            chargeSpawnRoutine = null;
        }

        if (chargedMaintainRoutine != null)
        {
            StopCoroutine(chargedMaintainRoutine);
            chargedMaintainRoutine = null;
        }

        // Ensure charge-applied invincibility is cleared
        setInvincible?.Invoke(false);

        // Restore state to Move or Idle depending on current velocity
        var pm = GetComponent<PlayerMovement>();

        // Clear look override and rotation multiplier
        if (pm != null)
        {
            pm.ClearLookOverride();
            pm.ResetRotationMultiplier();
        }

        // 변경: 현재 상태가 넉백/스턴/죽음/회피이면 상태 복귀 호출을 하지 않음(덮어쓰기 방지)
        var s = getState != null ? getState() : PlayerState.Idle;
        if (s != PlayerState.Knockback && s != PlayerState.Stun && s != PlayerState.Dead && s != PlayerState.Evade)
        {
            changeState?.Invoke(pm != null && pm.GetVelocityMagnitude() > 0.1f ? PlayerState.Move : PlayerState.Idle);
        }

        if (debugMode) Debug.Log("[Charge] CancelContinuous → 상태 복귀 실행");

        activeContinuousSlot = null;
    }

    private bool IsChargeExecutionAllowedNow()
    {
        var s = getState != null ? getState() : PlayerState.Idle;
        // 변경: 단발/연속 차지 시작 조건으로서 Idle/Move일 때만 허용.
        // (연속 차지는 시작 시 내부에서 Attack으로 전환함)
        return s == PlayerState.Idle || s == PlayerState.Move;
    }

    private void ExecuteChargeAttack(PlayerChargeAttackSO slot)
    {
        if (slot == null) return;

        changeState?.Invoke(PlayerState.Attack);

        // 발동 무적
        if (slot.invincibilityDuration > 0f)
        {
            setInvincible?.Invoke(true);
            StartCoroutine(EndInvincibleLater(slot.invincibilityDuration));
        }

        // 애니메이션
        string animName = slot.chargedClip != null ? slot.chargedClip.name : slot.chargedStateName;
        if (string.IsNullOrEmpty(animName)) animName = "Attack_Charged01";
        anim?.PlayChargedAttack(animName);

        // 히트박스 스폰
        if (chargeSpawnRoutine != null)
        {
            StopCoroutine(chargeSpawnRoutine);
            chargeSpawnRoutine = null;
        }
        chargeSpawnRoutine = StartCoroutine(ChargeHitboxSpawnRoutine(slot));

        // 유지 시간
        float dur = (slot.duration > 0f) ? slot.duration : 0.8f;
        if (chargedMaintainRoutine != null) { StopCoroutine(chargedMaintainRoutine); chargedMaintainRoutine = null; }
        chargedMaintainRoutine = StartCoroutine(ChargedAttackMaintainRoutine(dur));
    }

    private IEnumerator ChargedAttackMaintainRoutine(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (IsHoldActive())
            {
                yield return null;
                continue;
            }

            var s = getState != null ? getState() : PlayerState.Idle;
            if (s == PlayerState.Knockback || s == PlayerState.Stun ||
                s == PlayerState.Dead || s == PlayerState.Evade)
            {
                chargedMaintainRoutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Idle/Move 복귀 — 현재 상태가 넉백/스턴/죽음/회피가 아니면만 복귀
        var move = GetComponent<PlayerMovement>();
        var cur = getState != null ? getState() : PlayerState.Idle;
        if (cur != PlayerState.Knockback && cur != PlayerState.Stun && cur != PlayerState.Dead && cur != PlayerState.Evade)
        {
            changeState?.Invoke(move != null && move.GetVelocityMagnitude() > 0.1f ? PlayerState.Move : PlayerState.Idle);
        }
        chargedMaintainRoutine = null;
    }

    private IEnumerator ChargeHitboxSpawnRoutine(PlayerChargeAttackSO slot)
    {
        if (slot.hitBoxPrefab == null)
        {
            Debug.LogWarning("⚠ 차지 힛박스 프리팹이 비어 있습니다.");
            yield break;
        }

        // Prepare duration limit (if duration not set, use same default as maintain routine)
        float maxDuration = (slot.duration > 0f) ? slot.duration : 0.8f;

        // Defensive: if spawnCount is zero or list empty, nothing to do
        if (slot.spawnCount <= 0 || slot.spawnDelays == null || slot.spawnDelays.Count == 0)
        {
            if (debugMode) Debug.Log("[Charge] spawnDelays 비어있음 → 스폰 없음");
            chargeSpawnRoutine = null;
            yield break;
        }

        // Ensure spawn delays are in ascending order (OnValidate already sorts, but double-check)
        List<float> delays = new List<float>(slot.spawnDelays);
        delays.Sort();

        Transform spawn = spawnPoint != null ? spawnPoint : transform;

        EnsureChargeWeaponProxy(slot);

        float cycleElapsed = 0f;

        for (int i = 0; i < delays.Count; i++)
        {
            float d = delays[i];
            // Skip scheduled spawns that are after the allowed duration
            if (d > maxDuration)
            {
                if (debugMode)
                    Debug.Log($"[Charge] Scheduled spawn at {d:F2}s exceeds duration {maxDuration:F2}s → 무시");
                continue;
            }

            // Wait until target delay, checking cancel conditions each frame
            while (cycleElapsed < d)
            {
                if (IsHoldActive())
                {
                    yield return null;
                    continue;
                }

                var s = getState != null ? getState() : PlayerState.Idle;
                if (s == PlayerState.Knockback || s == PlayerState.Stun ||
                    s == PlayerState.Dead || s == PlayerState.Evade)
                {
                    // Cancel entire spawn sequence
                    chargeSpawnRoutine = null;
                    yield break;
                }

                cycleElapsed += Time.deltaTime;
                yield return null;
            }

            // Time reached: spawn one hitbox
            GameObject hb = Instantiate(slot.hitBoxPrefab, spawn.position, spawn.rotation);

            if (hb.TryGetComponent<HitBox_PC>(out var hitbox))
            {
                hitbox.SetWeapon(chargeWeaponProxy);

                if (slot.enableAreaDot)
                {
                    float dmgPerTick = slot.dotDamagePerTick > 0f ? slot.dotDamagePerTick : slot.damage;
                    float interval = Mathf.Max(0.01f, slot.dotTickInterval);

                    hitbox.Initialize(
                        dmgPerTick,
                        slot.range,
                        slot.knockbackPower,
                        slot.hitBoxLifetime,
                        allowDup: true,
                        dupInterval: interval
                    );
                }
                else
                {
                    hitbox.Initialize(
                        slot.damage,
                        slot.range,
                        slot.knockbackPower,
                        slot.hitBoxLifetime
                    );
                }
            }
            else
            {
                Debug.LogWarning("⚠ 차지 힛박스 프리팹에 HitBox_PC 컴포넌트가 없습니다.");
            }

#if UNITY_EDITOR
            if (debugMode)
            {
                Debug.Log($"[Charge] HB Spawn(idx:{i}, Delay {d:F2}s) │ dmg:{slot.damage}, range:{slot.range}, kb:{slot.knockbackPower}, life:{slot.hitBoxLifetime}, dup:{slot.enableAreaDot}");
            }
#endif
        }

        chargeSpawnRoutine = null;
    }

    // New: spawn routine for a single cycle (spawnDelays are relative to cycle start)
    private IEnumerator ChargeHitboxSpawnRoutineCycle(PlayerChargeAttackSO slot)
    {
        if (slot.hitBoxPrefab == null) yield break;

        float maxDuration = Mathf.Max(0.0001f, slot.duration);

        if (slot.spawnCount <= 0 || slot.spawnDelays == null || slot.spawnDelays.Count == 0) yield break;

        List<float> delays = slot.spawnDelays;
        // assume sorted by OnValidate

        Transform spawn = spawnPoint != null ? spawnPoint : transform;
        EnsureChargeWeaponProxy(slot);

        float cycleElapsed = 0f;

        for (int i = 0; i < delays.Count; i++)
        {
            float d = delays[i];
            if (d > maxDuration) continue;
            while (cycleElapsed < d)
            {
                if (IsHoldActive())
                {
                    yield return null;
                    continue;
                }

                var s = getState != null ? getState() : PlayerState.Idle;
                bool inSuper = slot.grantSuperArmor && superArmorRemaining > 0f;
                if (!inSuper && (s == PlayerState.Knockback || s == PlayerState.Stun ||
                    s == PlayerState.Dead || s == PlayerState.Evade))
                {
                    // ensure perCycleSpawnRoutine cleared on early exit
                    perCycleSpawnRoutine = null;
                    yield break;
                }
                cycleElapsed += Time.deltaTime;
                if (slot.grantSuperArmor && superArmorRemaining > 0f)
                    superArmorRemaining = Mathf.Max(0f, superArmorRemaining - Time.deltaTime);
                yield return null;
            }

            GameObject hb = Instantiate(slot.hitBoxPrefab, spawn.position, spawn.rotation);
            if (hb.TryGetComponent<HitBox_PC>(out var hitbox))
            {
                hitbox.SetWeapon(chargeWeaponProxy);
                if (slot.enableAreaDot)
                {
                    float dmgPerTick = slot.dotDamagePerTick > 0f ? slot.dotDamagePerTick : slot.damage;
                    float interval = Mathf.Max(0.01f, slot.dotTickInterval);

                    hitbox.Initialize(
                        dmgPerTick,
                        slot.range,
                        slot.knockbackPower,
                        slot.hitBoxLifetime,
                        allowDup: true,
                        dupInterval: interval
                    );
                }
                else
                {
                    hitbox.Initialize(
                        slot.damage,
                        slot.range,
                        slot.knockbackPower,
                        slot.hitBoxLifetime
                    );
                }
            }
            else
            {
                Debug.LogWarning("⚠ 차지 힛박스 프리팹에 HitBox_PC 컴포넌트가 없습니다.");
            }
            yield return null; // allow a frame to breathe between spawns
        }

        // clear reference on normal completion
        perCycleSpawnRoutine = null;
    }

    // Movement during charged attack (FixedUpdate-basis)
    private IEnumerator ChargedMovementRoutine(PlayerChargeAttackSO slot, PlayerMovement pm, float maxDuration)
    {
        float elapsed = 0f;
        // use WaitForFixedUpdate for frame-independent physics movement
        var wait = new WaitForFixedUpdate();
        while (elapsed < maxDuration && continuousActive && InputManager.Instance.GetAttack())
        {
            if (IsHoldActive())
            {
                yield return wait;
                continue;
            }

            // Respect player's input direction; use camera relative mapping to match PlayerMovement semantics
            Vector2 raw = InputManager.Instance.GetMoveInput();
            Vector3 inputVec = new Vector3(raw.x, 0f, raw.y);

            float mult = Mathf.Clamp01(slot.moveSpeedDuringAttack);
            if (mult > 0f && inputVec.sqrMagnitude > 0.0001f)
            {
                Vector3 camRel = pm.CameraRelative(inputVec).normalized;
                float baseSpeed = pm.GetBaseMoveSpeed();
                Vector3 disp = camRel * baseSpeed * mult * Time.fixedDeltaTime;
                pm.MovePhysicsDisplacement(disp);

                // 중요 변경: 이동 입력 있을 때마다 카메라 상대 방향으로 look override를 계속 갱신
                // -> PlayerMovement.HandleRotation이 이 오버라이드를 따라 부드럽게 회전함(공격 상태여도 적용).
                pm.SetLookOverride(camRel);

                // (faceNearestWhileHeld이 true면 FaceNearestWhileHeldRoutine이 우선으로 override 함 — 여기선 ChargedMovementRoutine은 faceNearest=false일 때 사용됨)
            }

            elapsed += Time.fixedDeltaTime;
            yield return wait;
        }

        // 루틴 끝나면 override 해제
        if (pm != null) pm.ClearLookOverride();

        movementRoutine = null;
    }

    // New: Face nearest while held — always look at nearest (no orbit)
    private IEnumerator FaceNearestWhileHeldRoutine(PlayerChargeAttackSO slot, PlayerMovement pm)
    {
        var wait = new WaitForFixedUpdate();
        EnemyDetector detector = GetComponent<EnemyDetector>();
        while (continuousActive && InputManager.Instance.GetAttack())
        {
            // cancel conditions
            var s = getState != null ? getState() : PlayerState.Idle;
            if (s == PlayerState.Knockback || s == PlayerState.Stun ||
                s == PlayerState.Dead || s == PlayerState.Evade) break;

            Transform nearest = null;
            if (detector != null)
            {
                var list = detector.GetEnemiesInRange(slot.range);
                float best = float.MaxValue;
                Vector3 pos = transform.position;
                foreach (var t in list)
                {
                    if (t == null) continue;
                    float sq = (t.position - pos).sqrMagnitude;
                    if (sq < best) { best = sq; nearest = t; }
                }
            }

            if (nearest != null)
            {
                Vector3 toTarget = nearest.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    // apply as look override so PlayerMovement.HandleRotation will rotate toward it every FixedUpdate
                    pm.SetLookOverride(toTarget.normalized);
                }
            }
            else
            {
                // no nearest: follow movement input like normal movement
                Vector2 raw = InputManager.Instance.GetMoveInput();
                Vector3 inputVec = new Vector3(raw.x, 0f, raw.y);
                if (inputVec.sqrMagnitude > 0.0001f)
                {
                    Vector3 camRel = pm.CameraRelative(inputVec).normalized;
                    pm.SetLookOverride(camRel);
                }
                else
                {
                    // no input — clear override so PlayerMovement can preserve its last look or other systems can control it
                    pm.ClearLookOverride();
                }
            }

            yield return wait;
        }
        // cleanup done by caller
        yield break;
    }

    private IEnumerator OrbitWhileHeldRoutine(PlayerChargeAttackSO slot, PlayerMovement pm, float maxDuration)
    {
        // NOTE: orbit semantics have been changed: when faceNearestWhileHeld is true, FaceNearestWhileHeldRoutine handles facing.
        float elapsed = 0f;
        var wait = new WaitForFixedUpdate();
        float detectTimer = 0f;
        const float detectInterval = 0.1f; // 샘플링 간격(성능)
        EnemyDetector detector = GetComponent<EnemyDetector>();
        Transform nearest = null;

        while (elapsed < maxDuration && continuousActive && InputManager.Instance.GetAttack())
        {
            if (IsHoldActive())
            {
                yield return wait;
                continue;
            }

            // 상태 취소 검사
            var s = getState != null ? getState() : PlayerState.Idle;
            if (s == PlayerState.Knockback || s == PlayerState.Stun ||
                s == PlayerState.Dead || s == PlayerState.Evade)
            {
                break;
            }

            // 탐지(샘플링)
            detectTimer -= Time.fixedDeltaTime;
            if (detectTimer <= 0f)
            {
                detectTimer = detectInterval;
                nearest = null;
                if (detector != null)
                {
                    var list = detector.GetEnemiesInRange(slot.range);
                    float bestSqr = float.MaxValue;
                    Vector3 pos = transform.position;
                    foreach (var t in list)
                    {
                        if (t == null) continue;
                        float sq = (t.position - pos).sqrMagnitude;
                        if (sq < bestSqr) { bestSqr = sq; nearest = t; }
                    }
                }
            }

            float mult = Mathf.Clamp01(slot.moveSpeedDuringAttack);

            // movement & facing
            if (nearest != null && !slot.faceNearestWhileHeld)
            {
                // If faceNearestWhileHeld is NOT set, original orbit behavior (tangent orbit) kept.
                Vector3 toTarget = nearest.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude < 0.0001f) toTarget = transform.forward;
                toTarget.Normalize();

                // rotate smoothly toward target (apply multiplier)
                Quaternion targetRot = Quaternion.LookRotation(toTarget, Vector3.up);
                float rotSpeed = pm.rotationSpeedDegPerSec * mult;
                var rb = pm != null ? pm.GetComponent<Rigidbody>() : null;
                if (rotSpeed > 0f)
                {
                    if (rb != null) rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, targetRot, rotSpeed * Time.fixedDeltaTime));
                    else transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotSpeed * Time.fixedDeltaTime);
                }

                // compute tangent for orbit (horizontal)
                Vector3 tangent = Vector3.Cross(Vector3.up, toTarget).normalized;

                // direction sign: use player horizontal input x if provided, otherwise default clockwise(=+1)
                float sign = 1f;
                var raw = InputManager.Instance.GetMoveInput();
                if (Mathf.Abs(raw.x) > 0.05f) sign = Mathf.Sign(raw.x);

                if (mult > 0f)
                {
                    float baseSpeed = pm.GetBaseMoveSpeed();
                    Vector3 disp = tangent * sign * baseSpeed * mult * Time.fixedDeltaTime;
                    pm.MovePhysicsDisplacement(disp);
                }
            }
            else
            {
                // fallback to input-based movement (same as ChargedMovementRoutine)
                Vector2 raw = InputManager.Instance.GetMoveInput();
                Vector3 inputVec = new Vector3(raw.x, 0f, raw.y);
                if (mult > 0f && inputVec.sqrMagnitude > 0.0001f)
                {
                    Vector3 camRel = pm.CameraRelative(inputVec).normalized;
                    float baseSpeed = pm.GetBaseMoveSpeed();
                    Vector3 disp = camRel * baseSpeed * mult * Time.fixedDeltaTime;
                    pm.MovePhysicsDisplacement(disp);

                    if (disp.sqrMagnitude > 0.000001f)
                    {
                        // If faceNearestWhileHeld is true, facing is handled by FaceNearestWhileHeldRoutine (via look override).
                        if (!slot.faceNearestWhileHeld)
                        {
                            Quaternion target = Quaternion.LookRotation(disp.normalized, Vector3.up);
                            var rb = pm.GetComponent<Rigidbody>();
                            float rotSpeed = pm.rotationSpeedDegPerSec * mult;
                            if (rb != null) rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, target, rotSpeed * Time.fixedDeltaTime));
                            else transform.rotation = Quaternion.RotateTowards(transform.rotation, target, rotSpeed * Time.fixedDeltaTime);
                        }
                    }
                }
            }

            elapsed += Time.fixedDeltaTime;
            yield return wait;
        }

        movementRoutine = null;
    }

    private IEnumerator ContinuousChargeLoop(PlayerChargeAttackSO slot)
    {
        if (slot == null) yield break;
        // Ensure only one active continuous loop at a time
        continuousActive = true;
        activeContinuousSlot = slot;

        // Apply initial state
        changeState?.Invoke(PlayerState.Attack);

        // invincibility at start of continuous (same as single-shot behavior)
        if (slot.invincibilityDuration > 0f)
        {
            setInvincible?.Invoke(true);
            StartCoroutine(EndInvincibleLater(slot.invincibilityDuration));
        }

        // SuperArmor
        if (slot.grantSuperArmor)
        {
            superArmorRemaining = Mathf.Max(0f, slot.superArmorDuration);
        }
        else
        {
            superArmorRemaining = 0f;
        }

        PlayerMovement pm = GetComponent<PlayerMovement>();

        // Set rotation multiplier according to slot.moveSpeedDuringAttack (applies to rotation)
        if (pm != null)
        {
            pm.SetRotationMultiplier(Mathf.Clamp01(slot.moveSpeedDuringAttack));
        }

        try
        {
            while (continuousActive && InputManager.Instance.GetAttack())
            {
                // Start a single cycle

                // If faceNearestWhileHeld is requested, start/ensure face coroutine runs to always set look override.
                if (pm != null && slot.faceNearestWhileHeld)
                {
                    if (faceNearestRoutine == null)
                    {
                        faceNearestRoutine = StartCoroutine(FaceNearestWhileHeldRoutine(slot, pm));
                    }
                }
                else
                {
                    // ensure no lingering faceNearest routine
                    if (faceNearestRoutine != null)
                    {
                        StopCoroutine(faceNearestRoutine);
                        faceNearestRoutine = null;
                        if (pm != null) pm.ClearLookOverride();
                    }
                }

                // Start cycle facing: sample input/nearest for immediate facing (keeps previous behavior but rotation override will run each FixedUpdate)
                if (pm != null)
                {
                    bool rotated = false;
                    if (slot.faceNearestWhileHeld)
                    {
                        // initial immediate sample handled by FaceNearestWhileHeldRoutine on next fixed frame; do nothing here
                        rotated = true;
                    }

                    if (!rotated)
                    {
                        Vector2 raw = InputManager.Instance.GetMoveInput();
                        Vector3 inputVec = new Vector3(raw.x, 0f, raw.y);
                        if (inputVec.sqrMagnitude > 0.0001f)
                        {
                            Vector3 camRel = pm.CameraRelative(inputVec).normalized;
                            pm.SetLookOverride(camRel.normalized); // apply an immediate look override so rotation starts following input
                        }
                    }   
                }

                string animName = slot.chargedClip != null ? slot.chargedClip.name : slot.chargedStateName;
                if (string.IsNullOrEmpty(animName)) animName = "Attack_Charged01";
                anim?.PlayChargedAttack(animName);

                // start per-cycle hitbox spawn (store reference so it can be cancelled)
                if (perCycleSpawnRoutine != null)
                {
                    StopCoroutine(perCycleSpawnRoutine);
                    perCycleSpawnRoutine = null;
                }
                perCycleSpawnRoutine = StartCoroutine(ChargeHitboxSpawnRoutineCycle(slot));

                // start movement during this cycle if requested
                if (slot.moveDuringAttack && pm != null)
                {
                    if (movementRoutine != null) { StopCoroutine(movementRoutine); movementRoutine = null; }

                    // If configured to face nearest while held, start face routine; otherwise input-based movement
                    if (slot.faceNearestWhileHeld)
                    {
                        // facing handled by faceNearestRoutine; movement still performed by OrbitWhileHeldRoutine fallback logic
                        movementRoutine = StartCoroutine(OrbitWhileHeldRoutine(slot, pm, slot.duration));
                    }
                    else
                    {
                        movementRoutine = StartCoroutine(ChargedMovementRoutine(slot, pm, slot.duration));
                    }
                }

                // wait for cycle duration while monitoring cancel conditions
                float elapsed = 0f;
                float dur = Mathf.Max(0.0001f, slot.duration);
                while (elapsed < dur)
                {
                    if (IsHoldActive())
                    {
                        yield return null;
                        continue;
                    }

                    var s = getState != null ? getState() : PlayerState.Idle;
                    bool inSuper = slot.grantSuperArmor && superArmorRemaining > 0f;
                    if (!inSuper && (s == PlayerState.Knockback || s == PlayerState.Stun ||
                        s == PlayerState.Dead || s == PlayerState.Evade))
                    {
                        // cancel all
                        continuousActive = false;
                        break;
                    }

                    if (!InputManager.Instance.GetAttack())
                    {
                        continuousActive = false;
                        break;
                    }

                    elapsed += Time.deltaTime;
                    if (slot.grantSuperArmor && superArmorRemaining > 0f)
                        superArmorRemaining = Mathf.Max(0f, superArmorRemaining - Time.deltaTime);
                    yield return null;
                }

                // loop will continue if continuousActive still true and button still held
            }
        }
        finally
        {
            // cleanup
            continuousActive = false;
            if (movementRoutine != null) { StopCoroutine(movementRoutine); movementRoutine = null; }
            if (faceNearestRoutine != null) { StopCoroutine(faceNearestRoutine); faceNearestRoutine = null; }
            // ensure per-cycle spawn coroutine is stopped on exit
            if (perCycleSpawnRoutine != null)
            {
                StopCoroutine(perCycleSpawnRoutine);
                perCycleSpawnRoutine = null;
            }
            activeContinuousSlot = null;

            // restore rotation multiplier & clear look override using existing 'pm' variable
            if (pm != null)
            {
                pm.ResetRotationMultiplier();
                pm.ClearLookOverride();
            }

            // 변경: 넉백/스턴/죽음/회피 상태일 때는 상태 복귀 호출을 하지 않음
            var cur = getState != null ? getState() : PlayerState.Idle;
            if (cur != PlayerState.Knockback && cur != PlayerState.Stun && cur != PlayerState.Dead && cur != PlayerState.Evade)
            {
                changeState?.Invoke(pm != null && pm.GetVelocityMagnitude() > 0.1f ? PlayerState.Move : PlayerState.Idle);
            }
        }
    }

    private void EnsureChargeWeaponProxy(PlayerChargeAttackSO slot)
    {
        if (chargeWeaponProxy == null)
        {
            chargeWeaponProxy = ScriptableObject.CreateInstance<WeaponDataSO>();
            chargeWeaponProxy.weaponName = "ChargeAttack";
        }

        // 넉백/스턴
        chargeWeaponProxy.knockbackPower = slot.knockbackPower;
        chargeWeaponProxy.knockbackDuration = slot.knockbackDuration;
        chargeWeaponProxy.stunDuration = slot.stunDuration;

        chargeWeaponProxy.targetHoldDuration = slot.targetHoldDuration;
        chargeWeaponProxy.attackerHoldDuration = slot.attackerHoldDuration;
        // Legacy mirror for compatibility with any remaining old reads.
        chargeWeaponProxy.targetStateHoldDuration = slot.targetHoldDuration;
        chargeWeaponProxy.targetAnimationHoldDuration = slot.targetHoldDuration;
        chargeWeaponProxy.attackerStateHoldDuration = slot.attackerHoldDuration;
        chargeWeaponProxy.attackerAnimationHoldDuration = slot.attackerHoldDuration;

        // --- 처치 연출 관련 필드 복사 (PlayerChargeAttackSO에서 설정한 값이 그대로 반영되도록) ---
        chargeWeaponProxy.deathMode = slot.deathMode;
        chargeWeaponProxy.ragdollImpulse = slot.ragdollImpulse;
        chargeWeaponProxy.ragdollUpImpulse = slot.ragdollUpImpulse;
        chargeWeaponProxy.ragdollSpinTorque = slot.ragdollSpinTorque;
        // 안전하게 리스트 복사 (null 체크)
        chargeWeaponProxy.sliceTargets = (slot.sliceTargets != null) ? new List<SliceTarget>(slot.sliceTargets) : new List<SliceTarget>();
        chargeWeaponProxy.sliceImpulse = slot.sliceImpulse;

        // Push 옵션 복사 (HitBox에서 weapon.usePushInsteadOfKnockback 검사함)
        chargeWeaponProxy.usePushInsteadOfKnockback = slot.usePushInsteadOfKnockback;

        // 처치 연출 파라미터 관련 필드 추가 복사 완료
    }

    private IEnumerator StartContinuousWhenAllowed(PlayerChargeAttackSO slot)
    {
        // 대기: charge 버튼이 유지되고, 상태가 허용될 때까지 대기
        while (chargeHoldActive && !IsChargeExecutionAllowedNow())
        {
            // 중간에 취소 조건 체크 (넉백/스턴/죽음 등) 또는 버튼 해제 시 취소
            var s = getState != null ? getState() : PlayerState.Idle;
            if (s == PlayerState.Knockback || s == PlayerState.Stun || s == PlayerState.Dead || s == PlayerState.Evade)
            {
                pendingContinuousStarter = null;
                yield break;
            }

            if (!InputManager.Instance.GetAttack())
            {
                pendingContinuousStarter = null;
                yield break;
            }

            yield return null;
        }

        pendingContinuousStarter = null;

        if (chargeHoldActive && IsChargeExecutionAllowedNow() && continuousRoutine == null)
        {
            if (debugMode) Debug.Log("[Charge] 연속 차지 시작 대기 해제 → 시작");
            continuousRoutine = StartCoroutine(ContinuousChargeLoop(slot));
        }
    }

    private IEnumerator EndInvincibleLater(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (IsHoldActive())
            {
                yield return null;
                continue;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        setInvincible?.Invoke(false);
    }
}