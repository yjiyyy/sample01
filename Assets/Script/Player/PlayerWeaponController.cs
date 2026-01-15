// (전체 파일) PlayerWeaponController.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PlayerWeaponController - 통합된 전체 파일
/// - 기존 레포의 API와 호환되도록 public 멤버들을 유지/복원했습니다.
/// - 콤보 무기(comboSlot != null)는 WeaponDataSO.cooldown을 사용하지 않고 콤보 컴포넌트에 위임합니다.
/// - ForceApplyKnockback 등 외부에서 호출되는 공용 메서드를 포함합니다.
/// - Unity6(6000.0.42f1) 환경, 모바일/PC 동일 동작(타이밍은 Time.deltaTime/Time.fixedDeltaTime 기반).
/// </summary>
public enum PlayerState
{
    Idle,
    Move,
    Attack,
    Knockback,
    Stun,
    Dead,
    Evade
}

[DisallowMultipleComponent]
public class PlayerWeaponController : MonoBehaviour
{
    [Header("애니메이션 컴포넌트")]
    [SerializeField] private PlayerAnimationController animationController;

    [Header("플레이어 감지기 (EnemyDetector)")]
    public EnemyDetector enemyDetector;

    [Header("디버그 모드")]
    [SerializeField] private bool debugMode = true;

    [Header("차지 메시지 설정")]
    [Tooltip("체크 시: 1초에 '차지 시작', SO 시간에 '차지 성공' 메시지 출력")]
    [SerializeField] private bool enableChargeMessages = true;

    // 서브컴포넌트
    private PlayerRecoil recoilComp;
    private PlayerEquipmentController equipComp;
    private PlayerChargeController chargeComp;
    private PlayerEvadeController evadeComp;
    private PlayerStateMachine fsm;

    private PlayerMovement movement;
    private PlayerState state = PlayerState.Idle;
    public PlayerState CurrentState => state;

    // runtime flags / state
    private bool chargeInvincible = false;
    private float lastAttackTime = -999f;
    private Coroutine attackRoutine;
    private Coroutine knockbackRoutine;
    private Coroutine arFireRoutine;

    // AR 관련 플래그(외부에서 사용되는 프로퍼티 제공)
    private bool arRotationLocked = false;
    private Vector3 arLockedForward;
    private bool arAllowMoveWhileFiringFlag = false;
    private bool arAutoResumeWhileHeld = false;

    private Transform meleeSpawnPointCache;
    private float lastReloadMsgTime = -999f;
    private const float RELOAD_MSG_COOLDOWN = 0.3f;

    private bool pendingSwitchToDefault = false;

    // Evade data applied by PlayerFacade
    private EvadeDataSO appliedEvadeData = null;

    // State-change suppression flag:
    // 콤보를 강제로 취소할 때 콤보 내부에서 호출하는 ChangeState 호출이 넉백/스턴을 덮어쓰지 못하도록
    // 아주 짧은 구간(콤보 Cancel 직전/직후) 동안 ChangeState를 무시하도록 사용합니다.
    private bool suppressStateChangeRequests = false;

    // ------------------ Public compatibility APIs ------------------

    // Invincibility check used by enemy hitboxes / other systems
    public bool IsInvincible() => (evadeComp?.IsInvincible() ?? false) || chargeInvincible;

    // ForceApplyKnockback: 적 공격(폭발/충격 등)이 플레이어에 넉백을 바로 적용할 때 외부에서 호출
    // 시그니처: (Vector3 dir, float power, float duration, float stun)
    public void ForceApplyKnockback(Vector3 dir, float power, float duration, float stun)
    {
        // Dead 상태면 무시
        if (state == PlayerState.Dead)
        {
            if (debugMode) Debug.Log("[PlayerWeaponController] ForceApplyKnockback ignored because state==Dead");
            return;
        }

        // 취소/정리
        if (attackRoutine != null) { StopCoroutine(attackRoutine); attackRoutine = null; }
        evadeComp?.CancelEvade();
        chargeComp?.CancelAll();
        chargeInvincible = false;
        CancelRecoil();

        equipComp?.WeaponBehavior?.GetComponent<WeaponAmmoRuntime>()?.InterruptReload();
        equipComp?.WeaponBehavior?.GetComponent<WeaponAmmoRuntime_AR>()?.InterruptReload();

        if (arFireRoutine != null) { StopCoroutine(arFireRoutine); arFireRoutine = null; }
        EndARFireState();

        // --- 콤보 즉시 중단 처리: 콤보는 EndCombo에서 ChangeState(Idle/Move)를 호출하므로
        //     콤보 종료 도중 상태 변경이 넉백을 덮어쓰지 않도록 일시적으로 차단합니다.
        try
        {
            var wb = equipComp?.WeaponBehavior;
            var comboComp = wb != null ? wb.GetComponent<MeleeComboBehavior>() : null;
            if (comboComp != null)
            {
                if (debugMode) Debug.Log("[PlayerWeaponController] ForceApplyKnockback -> cancelling combo immediately");
                suppressStateChangeRequests = true;
                try { comboComp.CancelCombo(); } catch { }
                suppressStateChangeRequests = false;
            }
        }
        catch
        {
            // 안전하게 무시
            suppressStateChangeRequests = false;
        }

        // 기존 넉백 코루틴 정리 후 새로 시작
        if (knockbackRoutine != null) { StopCoroutine(knockbackRoutine); knockbackRoutine = null; }
        knockbackRoutine = StartCoroutine(KnockbackRoutine_Internal(dir, power, duration, stun));
    }

    // AR state exposers used by PlayerMovement
    public bool IsARFiring => arFireRoutine != null;
    public bool ARAllowMoveWhileFiring => arAllowMoveWhileFiringFlag && IsARFiring;
    public bool ARIsRotationLocked => arRotationLocked && IsARFiring;
    public Vector3 ARLockedForward => arLockedForward;

    // Evade gauge accessors used by UI
    public float GetEvadeGauge() => evadeComp != null ? evadeComp.GetEvadeGauge() : 0f;
    public float GetMaxEvadeGauge() => evadeComp != null ? evadeComp.GetMaxEvadeGauge() : 0f;

    // Weapon data accessor expected by movement/other systems
    public WeaponDataSO GetCurrentWeaponData() => equipComp != null ? equipComp.CurrentWeaponData : null;

    // Expose enemies detection for WeaponBehavior compatibility
    public List<Transform> DetectEnemies()
    {
        if (enemyDetector == null) return new List<Transform>();
        return enemyDetector.GetEnemiesInRange(enemyDetector.viewDistance);
    }

    // For PlayerFacade to apply evade data at runtime/editor
    public void ApplyEvadeData(EvadeDataSO data)
    {
        appliedEvadeData = data;
        if (evadeComp != null)
        {
            evadeComp.Setup(appliedEvadeData, animationController, movement, () => state, s => ChangeState(s));
        }
    }

    // Recoil helpers
    public void CancelRecoil()
    {
        recoilComp?.Cancel();
    }

    public void StartRecoilIfNeeded(WeaponDataSO data)
    {
        if (recoilComp == null || data == null) return;
        if (data.recoilDuration <= 0f) return;
        if (Mathf.Approximately(data.recoilPower, 0f)) return;

        recoilComp.StartRecoil(data, () => state == PlayerState.Attack, transform);
    }

    // ------------------ End compatibility APIs ------------------

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();

        recoilComp = GetComponent<PlayerRecoil>() ?? gameObject.AddComponent<PlayerRecoil>();
        equipComp = GetComponent<PlayerEquipmentController>() ?? gameObject.AddComponent<PlayerEquipmentController>();
        chargeComp = GetComponent<PlayerChargeController>() ?? gameObject.AddComponent<PlayerChargeController>();
        evadeComp = GetComponent<PlayerEvadeController>() ?? gameObject.AddComponent<PlayerEvadeController>();
        fsm = GetComponent<PlayerStateMachine>() ?? gameObject.AddComponent<PlayerStateMachine>();
        fsm.Init(PlayerState.Idle);

        // Root_dummy 타겟 캐시 (melee spawn point)
        Transform[] all = GetComponentsInChildren<Transform>(true);
        foreach (var t in all)
        {
            if (t != null && t.name == "Root_dummy")
            {
                meleeSpawnPointCache = t;
                break;
            }
        }
        if (meleeSpawnPointCache == null) meleeSpawnPointCache = transform;

        // Setup subcomponents - backward compatible Setup overloads in equipComp
        equipComp.Setup(animationController);

        chargeComp.Setup(
            animationController,
            meleeSpawnPointCache,
            () => equipComp.CurrentWeaponData,
            () => state,
            s => ChangeState(s),
            inv => chargeInvincible = inv,
            enableChargeMessages,
            debugMode
        );

        if (evadeComp != null)
            evadeComp.Setup(appliedEvadeData, animationController, movement, () => state, s => ChangeState(s));
    }

    private void Start()
    {
        EquipWeapon(null);
        ChangeState(PlayerState.Idle);
    }

    private void Update()
    {
        if (state == PlayerState.Dead) return;

        chargeComp?.Tick();
        evadeComp?.TickRecharge(Time.deltaTime);
        AutoResumeReloadIfNeeded();

        if (InputManager.Instance.GetEvadeInput() && (evadeComp?.CanEvade() ?? false) && state != PlayerState.Evade)
        {
            Vector2 currentMoveInput = InputManager.Instance.GetMoveInput();

            System.Action preEvadeCleanup = () =>
            {
                if (attackRoutine != null) { StopCoroutine(attackRoutine); attackRoutine = null; }
                if (knockbackRoutine != null) { StopCoroutine(knockbackRoutine); knockbackRoutine = null; }
                movement?.CancelKnockback();

                if (arFireRoutine != null) { StopCoroutine(arFireRoutine); arFireRoutine = null; }
                EndARFireState();

                CancelRecoil();
                equipComp.WeaponBehavior?.GetComponent<WeaponAmmoRuntime>()?.InterruptReload();
                equipComp.WeaponBehavior?.GetComponent<WeaponAmmoRuntime_AR>()?.InterruptReload();
            };

            evadeComp.PerformEvade(currentMoveInput, preEvadeCleanup);
            return;
        }

        switch (state)
        {
            case PlayerState.Idle: HandleIdle(); break;
            case PlayerState.Move: HandleMove(); break;
            case PlayerState.Attack:
                {
                    // 콤보 실행 중에도 공격 버튼을 콤보 컴포넌트에 전달하여 다음 스텝으로 넘어가도록 처리
                    var data = equipComp != null ? equipComp.CurrentWeaponData : null;
                    if (data != null && data.comboSlot != null)
                    {
                        var wb = equipComp.WeaponBehavior;
                        if (wb != null)
                        {
                            var comboComp = wb.GetComponent<MeleeComboBehavior>();
                            if (comboComp != null && InputManager.Instance.GetAttackInput())
                            {
                                comboComp.OnPress();
                            }
                        }
                    }
                }
                break;
            case PlayerState.Knockback:
            case PlayerState.Stun:
            case PlayerState.Evade:
                break;
        }
    }

    private void HandleIdle()
    {
        if (IsActionBlocking()) return;
        if (movement != null && movement.GetVelocityMagnitude() > 0.1f) { ChangeState(PlayerState.Move); return; }
        if (InputManager.Instance.GetAttackInput()) PlayAttack();
    }

    private void HandleMove()
    {
        if (IsActionBlocking()) return;
        if (movement != null && movement.GetVelocityMagnitude() <= 0.1f) { ChangeState(PlayerState.Idle); return; }
        if (InputManager.Instance.GetAttackInput()) PlayAttack();
    }

    private bool IsActionBlocking()
    {
        return state == PlayerState.Attack ||
               state == PlayerState.Knockback ||
               state == PlayerState.Stun ||
               state == PlayerState.Evade;
    }

    private void ChangeState(PlayerState newState)
    {
        // 상태 변경 요청이 일시 차단되어 있으면 무시합니다.
        if (suppressStateChangeRequests)
        {
            if (debugMode) Debug.Log($"[PlayerWeaponController] ChangeState suppressed: {newState}");
            return;
        }

        if (state == newState) return;
        state = newState;
        fsm?.Set(newState);

        if (newState == PlayerState.Knockback ||
            newState == PlayerState.Stun ||
            newState == PlayerState.Dead)
        {
            equipComp.WeaponBehavior?.GetComponent<WeaponAmmoRuntime>()?.InterruptReload();
            equipComp.WeaponBehavior?.GetComponent<WeaponAmmoRuntime_AR>()?.InterruptReload();
            chargeComp?.CancelAll();
            ExecutePendingSwitchIfAnyImmediate();
        }

        if (newState != PlayerState.Attack)
            CancelRecoil();

        animationController?.ForceAnimationByState(newState);
    }

    public void RequestSwitchToDefault()
    {
        pendingSwitchToDefault = true;
        if (debugMode) Debug.Log("[PlayerWeaponController] RequestSwitchToDefault() → queued");
    }

    public void ExecutePendingSwitchIfAnyImmediate()
    {
        if (!pendingSwitchToDefault) return;
        pendingSwitchToDefault = false;

        var wb = equipComp != null ? equipComp.WeaponBehavior : null;
        bool shouldSwitch = true;

        if (wb != null)
        {
            var gunAmmo = wb.GetComponent<WeaponAmmoRuntime>();
            var arAmmo = wb.GetComponent<WeaponAmmoRuntime_AR>();
            if (gunAmmo != null)
            {
                shouldSwitch = gunAmmo.IsMagazineEmpty() && !gunAmmo.HasAnyReserveOrInfinite();
            }
            else if (arAmmo != null)
            {
                shouldSwitch = arAmmo.IsMagazineEmpty() && !arAmmo.HasAnyReserveOrInfinite();
            }
        }

        if (shouldSwitch)
        {
            if (debugMode) Debug.Log("[PlayerWeaponController] pending switch → default weapon");
            EquipWeapon(null);
        }
        else
        {
            if (debugMode) Debug.Log("[PlayerWeaponController] ammo replenished → cancel pending");
        }
    }

    public void CancelPendingSwitch()
    {
        if (pendingSwitchToDefault)
        {
            pendingSwitchToDefault = false;
            if (debugMode) Debug.Log("[PlayerWeaponController] Pending switch canceled");
        }
    }

    public void EquipWeapon(GameObject weaponPrefab)
    {
        // null -> equip default prefab via equipComp
        if (weaponPrefab == null)
        {
            equipComp?.EquipDefault(this.transform.root);
            return;
        }

        equipComp?.EquipPrefab(weaponPrefab, this.transform.root);
    }

    // PlayAttack: 콤보 무기면 WeaponDataSO.cooldown을 사용하지 않고 MeleeComboBehavior에 위임
    public void PlayAttack()
    {
        var data = equipComp.CurrentWeaponData;
        if (data == null) return;
        if (IsActionBlocking()) return;

        // ----------------- Melee combo handling -----------------
        if (data.comboSlot != null)
        {
            var wb = equipComp.WeaponBehavior;
            if (wb == null)
            {
                if (debugMode) Debug.LogWarning("[Combo] WeaponBehavior missing");
                return;
            }

            // Ensure MeleeComboBehavior exists and is setup
            var comboComp = wb.GetComponent<MeleeComboBehavior>();
            if (comboComp == null) comboComp = wb.gameObject.AddComponent<MeleeComboBehavior>();

            comboComp.Setup(
                data.comboSlot,
                animationController,
                meleeSpawnPointCache,
                () => equipComp.CurrentWeaponData,
                () => state,
                s => ChangeState(s),
                movement,
                this,
                debugMode
            );

            // 콤보인 경우 WeaponDataSO.cooldown 검사 및 lastAttackTime 갱신을 하지 않음 (요청 사항)
            comboComp.OnPress();
            return;
        }
        // -------------------------------------------------------

        if (data is WeaponDataSO_AR arData)
        {
            float delta = Time.time - lastAttackTime;
            if (delta < arData.cooldown) return;
            lastAttackTime = Time.time;

            if (arFireRoutine != null) { StopCoroutine(arFireRoutine); arFireRoutine = null; }
            arFireRoutine = StartCoroutine(AssaultRifleFireRoutine(arData));
            return;
        }

        var sg = data as WeaponDataSO_Shotgun;
        var wb2 = equipComp.WeaponBehavior;
        var ammoShotgun = wb2 != null ? wb2.GetComponent<WeaponAmmoRuntime>() : null;
        if (sg != null && sg.usesAmmo && ammoShotgun != null)
        {
            if (ammoShotgun.IsReloading)
            {
                if (Time.time - lastReloadMsgTime >= RELOAD_MSG_COOLDOWN)
                {
                    float remain = ammoShotgun.GetReloadRemaining();
                    Debug.Log($"[Ammo] Reloading… ({remain:F2}s)");
                    lastReloadMsgTime = Time.time;
                }
                return;
            }
            if (!ammoShotgun.CanFire(sg.consumePerShot))
            {
                if (!ammoShotgun.HasAnyReserveOrInfinite())
                {
                    Debug.Log("[Ammo] Shotgun out of ammo → switch to default");
                    if (state == PlayerState.Attack) RequestSwitchToDefault(); else EquipWeapon(null);
                    return;
                }
                ammoShotgun.TryStartReload();
                return;
            }
        }

        var gun = data as WeaponDataSO_Gun;
        var ammoGun = wb2 != null ? wb2.GetComponent<WeaponAmmoRuntime>() : null;
        if (gun != null && gun.usesAmmo && ammoGun != null)
        {
            if (ammoGun.IsReloading)
            {
                if (Time.time - lastReloadMsgTime >= RELOAD_MSG_COOLDOWN)
                {
                    float remain = ammoGun.GetReloadRemaining();
                    Debug.Log($"[Ammo] Reloading… ({remain:F2}s)");
                    lastReloadMsgTime = Time.time;
                }
                return;
            }
            if (!ammoGun.CanFire(gun.consumePerShot))
            {
                if (!ammoGun.HasAnyReserveOrInfinite())
                {
                    Debug.Log("[Ammo] Gun out of ammo → switch to default");
                    if (state == PlayerState.Attack) RequestSwitchToDefault(); else EquipWeapon(null);
                    return;
                }
                ammoGun.TryStartReload();
                return;
            }
        }

        float deltaGeneral = Time.time - lastAttackTime;
        if (deltaGeneral < data.cooldown) return;
        lastAttackTime = Time.time;

        if (attackRoutine != null) { StopCoroutine(attackRoutine); attackRoutine = null; }
        attackRoutine = StartCoroutine(AttackRoutine(data));
    }

    private IEnumerator AttackRoutine(WeaponDataSO data)
    {
        ChangeState(PlayerState.Attack);
        animationController?.PlayAttack(data);
        StartRecoilIfNeeded(data);
        equipComp.WeaponBehavior?.AttackHit();

        yield return new WaitForSeconds(data.cooldown);

        ChangeState(PlayerState.Idle);
        animationController?.EndAttack();
        CancelRecoil();
        attackRoutine = null;
    }

    private void AutoResumeReloadIfNeeded()
    {
        var data = equipComp.CurrentWeaponData;
        var wb = equipComp.WeaponBehavior;
        if (wb == null || data == null) return;

        var gun = data as WeaponDataSO_Gun;
        var ammoGun = wb.GetComponent<WeaponAmmoRuntime>();
        if (gun != null && ammoGun != null && gun.usesAmmo)
        {
            if ((state == PlayerState.Idle || state == PlayerState.Move) &&
                !ammoGun.IsReloading &&
                ammoGun.IsMagazineEmpty() &&
                ammoGun.HasAnyReserveOrInfinite())
            {
                ammoGun.TryStartReload();
            }
            return;
        }

        var ar = data as WeaponDataSO_AR;
        var ammoAR = wb.GetComponent<WeaponAmmoRuntime_AR>();
        if (ar != null && ammoAR != null && ar.usesAmmo)
        {
            if ((state == PlayerState.Idle || state == PlayerState.Move) &&
                !ammoAR.IsReloading &&
                ammoAR.IsMagazineEmpty() &&
                ammoAR.HasAnyReserveOrInfinite())
            {
                ammoAR.TryStartReload();
            }
            return;
        }

        var sg = data as WeaponDataSO_Shotgun;
        var ammoSg = wb.GetComponent<WeaponAmmoRuntime>();
        if (sg != null && ammoSg != null && sg.usesAmmo)
        {
            if ((state == PlayerState.Idle || state == PlayerState.Move) &&
                !ammoSg.IsReloading &&
                ammoSg.IsMagazineEmpty() &&
                ammoSg.HasAnyReserveOrInfinite())
            {
                ammoSg.TryStartReload();
            }
            return;
        }
    }

    #region Knockback / CC (internal coroutine used by ForceApplyKnockback)
    private IEnumerator KnockbackRoutine_Internal(Vector3 dir, float power, float duration, float stun)
    {
        if (state == PlayerState.Dead)
        {
            if (debugMode) Debug.Log("[PlayerWeaponController] KnockbackRoutine_Internal aborted because state==Dead at start.");
            knockbackRoutine = null;
            yield break;
        }

        ChangeState(PlayerState.Knockback);

        Vector3 knockDir = dir.normalized; knockDir.y = 0f;

        if (movement != null)
        {
            movement.FaceKnockback(dir);
        }
        else
        {
            if (knockDir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(-knockDir);
        }

        if (state == PlayerState.Dead)
        {
            if (debugMode) Debug.Log("[PlayerWeaponController] KnockbackRoutine_Internal stopping because state became Dead after FaceKnockback.");
            knockbackRoutine = null;
            yield break;
        }

        if (movement != null && movement.enabled)
        {
            movement.ApplyKnockback(knockDir, power, duration, null);
        }
        else
        {
            if (debugMode) Debug.Log("[PlayerWeaponController] Knockback skipped because movement component is missing or disabled.");
        }

        float elapsed = 0f;
        float waitDur = Mathf.Max(0f, duration);
        while (elapsed < waitDur)
        {
            if (state == PlayerState.Dead)
            {
                if (debugMode) Debug.Log("[PlayerWeaponController] KnockbackRoutine_Internal interrupted by Dead state during wait.");
                knockbackRoutine = null;
                yield break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (state == PlayerState.Dead)
        {
            if (debugMode) Debug.Log("[PlayerWeaponController] Skipping stun because state==Dead.");
            knockbackRoutine = null;
            yield break;
        }

        if (stun > 0f)
        {
            ChangeState(PlayerState.Stun);

            float stunElapsed = 0f;
            while (stunElapsed < stun)
            {
                if (state == PlayerState.Dead)
                {
                    if (debugMode) Debug.Log("[PlayerWeaponController] Stun interrupted by Dead state.");
                    knockbackRoutine = null;
                    yield break;
                }
                stunElapsed += Time.deltaTime;
                yield return null;
            }
        }

        if (state != PlayerState.Dead)
        {
            if (movement != null && movement.GetVelocityMagnitude() > 0.1f)
                ChangeState(PlayerState.Move);
            else
                ChangeState(PlayerState.Idle);
        }

        knockbackRoutine = null;
    }
    #endregion

    public bool IsInvinciblePublic() => IsInvincible();

    // AR helpers
    private void BeginARFireState(WeaponDataSO_AR arData)
    {
        arAllowMoveWhileFiringFlag = arData.allowMoveWhileFiring;
        arAutoResumeWhileHeld = arData.autoReloadResumeWhileHeld;
        arRotationLocked = arData.lockRotationDuringFiring;

        Vector3 fwd = transform.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        arLockedForward = fwd.normalized;
    }

    private void EndARFireState()
    {
        arRotationLocked = false;
        arAllowMoveWhileFiringFlag = false;
        arAutoResumeWhileHeld = false;
    }

    private IEnumerator AssaultRifleFireRoutine(WeaponDataSO_AR ar)
    {
        ChangeState(PlayerState.Attack);
        animationController?.PlayAttack(ar, true);
        BeginARFireState(ar);

        var wb = equipComp.WeaponBehavior;
        if (wb == null)
        {
            Debug.LogWarning("[AR] WeaponBehavior missing");
            ChangeState(PlayerState.Idle);
            EndARFireState();
            yield break;
        }

        var ammo = wb.GetComponent<WeaponAmmoRuntime_AR>();
        if (ammo == null) ammo = wb.gameObject.AddComponent<WeaponAmmoRuntime_AR>();
        ammo.Initialize(ar, force: false);

        float interval = Mathf.Max(0.01f, ar.cooldown);
        float nextTime = Time.time;

        while (true)
        {
            if (state == PlayerState.Knockback || state == PlayerState.Stun || state == PlayerState.Dead || state == PlayerState.Evade)
                break;

            bool holding = InputManager.Instance.GetAttack();
            if (!holding) break;

            if (ammo.IsReloading)
            {
                if (arAutoResumeWhileHeld && ar.lockRotationDuringFiring)
                {
                    yield return null;
                    continue;
                }
                break;
            }

            if (Time.time >= nextTime)
            {
                if (ammo.CanFire(ar.consumePerShot))
                {
                    if (ammo.TryConsumeForShot(ar.consumePerShot))
                    {
                        StartRecoilIfNeeded(ar);

                        Vector3 baseDir = arRotationLocked ? arLockedForward : transform.forward;
                        baseDir.y = 0f;
                        if (baseDir.sqrMagnitude < 0.0001f) baseDir = Vector3.forward;
                        baseDir.Normalize();

                        Vector3 shootDir = (ar.spreadAngle > 0f)
                            ? RandomDirectionInCone(baseDir, ar.spreadAngle * 0.5f)
                            : baseDir;

                        animationController?.PlayAttack(ar, true);
                        wb.FireProjectileForced(shootDir, ar.spread3D);

                        lastAttackTime = Time.time;

                        if (ammo.IsMagazineEmpty() && !ammo.HasAnyReserveOrInfinite())
                        {
                            RequestSwitchToDefault();
                            if (debugMode) Debug.Log("[AR] magazine empty → request default switch");
                        }

                        nextTime += interval;
                        if (Time.time - nextTime > interval)
                            nextTime = Time.time + interval;
                    }
                }
                else
                {
                    if (ar.autoReloadOnEmpty && ammo.HasAnyReserveOrInfinite())
                    {
                        ammo.TryStartReload();
                        if (arAutoResumeWhileHeld && holding && ar.lockRotationDuringFiring)
                        {
                            nextTime = Time.time;
                            yield return null;
                            continue;
                        }
                    }
                    else
                    {
                        if (!ammo.HasAnyReserveOrInfinite())
                        {
                            Debug.Log("[Ammo] AR out of ammo → default switch");
                            RequestSwitchToDefault();
                        }
                    }
                    break;
                }
            }

            yield return null;
        }

        animationController?.EndAttack();
        if (movement != null && movement.GetVelocityMagnitude() > 0.1f)
            ChangeState(PlayerState.Move);
        else
            ChangeState(PlayerState.Idle);

        EndARFireState();
        arFireRoutine = null;
    }

    private Vector3 RandomDirectionInCone(Vector3 baseDir, float halfAngleDeg)
    {
        if (halfAngleDeg <= 0f) return baseDir.normalized;

        float halfRad = Mathf.Deg2Rad * halfAngleDeg;
        float cosMax = Mathf.Cos(halfRad);
        float u = UnityEngine.Random.Range(cosMax, 1f);
        float theta = Mathf.Acos(u);
        float phi = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        float sinT = Mathf.Sin(theta);

        Vector3 local = new Vector3(sinT * Mathf.Cos(phi), sinT * Mathf.Sin(phi), Mathf.Cos(theta));
        Quaternion rot = Quaternion.FromToRotation(Vector3.forward, baseDir.normalized);
        return rot * local;
    }
}