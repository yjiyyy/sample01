// NavMeshAgent 의존 제거 버전
// - 이동/속도는 PlayerMovement 내부 baseMoveSpeed / AR 비율 사용
// - agent 필드 및 using UnityEngine.AI 삭제

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

public class PlayerWeaponController : MonoBehaviour
{
    [Header("무기 부착 위치")]
    [SerializeField] private Transform weaponSocket;

    [Header("애니메이션 컨트롤러")]
    [SerializeField] private PlayerAnimationController animationController;

    [Header("플레이어 감지기 (EnemyDetector)")]
    public EnemyDetector enemyDetector;

    [Header("기본 무기 (Weapon_None 프리팹)")]
    [SerializeField] private GameObject defaultWeaponPrefab;

    [Header("회피 설정")]
    [SerializeField] private EvadeDataSO evadeData;

    [Header("디버그 모드")]
    [SerializeField] private bool debugMode = true;

    [Header("차지 메시지 옵션")]
    [Tooltip("체크 시: 1초에 '차지 시작', SO 시간에 '차지 성공' 메시지 출력")]
    [SerializeField] private bool enableChargeMessages = true;

    private PlayerMovement movement;

    // v3 컴포넌트들
    private PlayerRecoil recoilComp;
    private PlayerEquipmentController equipComp;
    private PlayerChargeController chargeComp;
    private PlayerEvadeController evadeComp;
    private PlayerStateMachine fsm;

    private PlayerState state = PlayerState.Idle;
    public PlayerState CurrentState => state;

    private bool chargeInvincible = false;
    private float lastAttackTime = -999f;

    private Coroutine attackRoutine;
    private Coroutine knockbackRoutine;
    private Coroutine arFireRoutine;

    // AR 상태 플래그
    private bool arRotationLocked = false;
    private Vector3 arLockedForward;
    private bool arAllowMoveWhileFiringFlag = false;
    private bool arAutoResumeWhileHeld = false;

    private Transform meleeSpawnPointCache;

    private float lastReloadMsgTime = -999f;
    private const float RELOAD_MSG_COOLDOWN = 0.3f;

    private bool pendingSwitchToDefault = false;

    public void RequestSwitchToDefault()
    {
        pendingSwitchToDefault = true;
        if (debugMode) Debug.Log("[PlayerWeaponController] RequestSwitchToDefault() → 보류");
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
            if (debugMode) Debug.Log("[PlayerWeaponController] 보류 전환 실행 → 기본 무기로");
            EquipWeapon(null);
        }
        else
        {
            if (debugMode) Debug.Log("[PlayerWeaponController] ammo 회복됨 → 전환 취소");
        }
    }

    public void CancelPendingSwitch()
    {
        if (pendingSwitchToDefault)
        {
            pendingSwitchToDefault = false;
            if (debugMode) Debug.Log("[PlayerWeaponController] Pending switch 취소");
        }
    }

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();

        recoilComp = GetComponent<PlayerRecoil>() ?? gameObject.AddComponent<PlayerRecoil>();
        equipComp = GetComponent<PlayerEquipmentController>() ?? gameObject.AddComponent<PlayerEquipmentController>();
        chargeComp = GetComponent<PlayerChargeController>() ?? gameObject.AddComponent<PlayerChargeController>();
        evadeComp = GetComponent<PlayerEvadeController>() ?? gameObject.AddComponent<PlayerEvadeController>();
        fsm = GetComponent<PlayerStateMachine>() ?? gameObject.AddComponent<PlayerStateMachine>();
        fsm.Init(PlayerState.Idle);

        // Root_dummy 탐색
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

        // 서브 컴포넌트 세팅
        equipComp.Setup(weaponSocket, animationController);

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

        evadeComp.Setup(
            evadeData,
            animationController,
            movement,
            () => state,
            s => ChangeState(s)
        );
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

        // 회피 입력
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
                // 차지 유지 정책: chargeComp.CancelAll() 호출하지 않음
            };

            evadeComp.PerformEvade(currentMoveInput, preEvadeCleanup);
            return;
        }

        switch (state)
        {
            case PlayerState.Idle: HandleIdle(); break;
            case PlayerState.Move: HandleMove(); break;
            case PlayerState.Attack:
            case PlayerState.Knockback:
            case PlayerState.Stun:
            case PlayerState.Evade:
                break;
        }
    }

    private void HandleIdle()
    {
        if (IsActionBlocking()) return;
        if (movement.GetVelocityMagnitude() > 0.1f) { ChangeState(PlayerState.Move); return; }
        if (InputManager.Instance.GetAttackInput()) PlayAttack();
    }

    private void HandleMove()
    {
        if (IsActionBlocking()) return;
        if (movement.GetVelocityMagnitude() <= 0.1f) { ChangeState(PlayerState.Idle); return; }
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
        if (state == newState) return;
        var prev = state;
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

    public void EquipWeapon(GameObject weaponPrefab)
    {
        chargeComp?.CancelAll();
        if (arFireRoutine != null) { StopCoroutine(arFireRoutine); arFireRoutine = null; }
        EndARFireState();

        equipComp.Equip(weaponPrefab, defaultWeaponPrefab, debugLogs: debugMode);

        if (enemyDetector != null && equipComp.WeaponBehavior != null)
            enemyDetector.weaponBehavior = equipComp.WeaponBehavior;

        var curData = equipComp.CurrentWeaponData;
        bool isAR = curData is WeaponDataSO_AR;
        if (isAR)
        {
            animationController?.SetUpperBodyLayerEnabled(true);
        }
        else
        {
            animationController?.SetUpperBodyLayerEnabled(false);
            animationController?.EndAttack();
        }
    }

    public void PlayAttack()
    {
        var data = equipComp.CurrentWeaponData;
        if (data == null) return;
        if (IsActionBlocking()) return;

        if (data is WeaponDataSO_AR arData)
        {
            float delta = Time.time - lastAttackTime;
            if (delta < arData.cooldown) return;
            lastAttackTime = Time.time;

            if (arFireRoutine != null) { StopCoroutine(arFireRoutine); arFireRoutine = null; }
            arFireRoutine = StartCoroutine(AssaultRifleFireRoutine(arData));
            return;
        }

        // 탄약 검사 (Gun / Shotgun)
        var sg = data as WeaponDataSO_Shotgun;
        var wb = equipComp.WeaponBehavior;
        var ammoShotgun = wb != null ? wb.GetComponent<WeaponAmmoRuntime>() : null;
        if (sg != null && sg.usesAmmo && ammoShotgun != null)
        {
            if (ammoShotgun.IsReloading)
            {
                if (Time.time - lastReloadMsgTime >= RELOAD_MSG_COOLDOWN)
                {
                    float remain = ammoShotgun.GetReloadRemaining();
                    Debug.Log($"[Ammo] 리로드 중… ({remain:F2}s)");
                    lastReloadMsgTime = Time.time;
                }
                return;
            }
            if (!ammoShotgun.CanFire(sg.consumePerShot))
            {
                if (!ammoShotgun.HasAnyReserveOrInfinite())
                {
                    Debug.Log("[Ammo] Shotgun 탄약 없음 → 기본 무기 전환");
                    if (state == PlayerState.Attack) RequestSwitchToDefault(); else EquipWeapon(null);
                    return;
                }
                ammoShotgun.TryStartReload();
                return;
            }
        }

        var gun = data as WeaponDataSO_Gun;
        var ammoGun = wb != null ? wb.GetComponent<WeaponAmmoRuntime>() : null;
        if (gun != null && gun.usesAmmo && ammoGun != null)
        {
            if (ammoGun.IsReloading)
            {
                if (Time.time - lastReloadMsgTime >= RELOAD_MSG_COOLDOWN)
                {
                    float remain = ammoGun.GetReloadRemaining();
                    Debug.Log($"[Ammo] 리로드 중… ({remain:F2}s)");
                    lastReloadMsgTime = Time.time;
                }
                return;
            }
            if (!ammoGun.CanFire(gun.consumePerShot))
            {
                if (!ammoGun.HasAnyReserveOrInfinite())
                {
                    Debug.Log("[Ammo] Gun 탄약 없음 → 기본 무기 전환");
                    if (state == PlayerState.Attack) RequestSwitchToDefault(); else EquipWeapon(null);
                    return;
                }
                ammoGun.TryStartReload();
                return;
            }
        }

        float delta2 = Time.time - lastAttackTime;
        if (delta2 < data.cooldown) return;
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

    #region Knockback / CC
    public void ForceApplyKnockback(Vector3 dir, float power, float duration, float stun)
    {
        if (attackRoutine != null) { StopCoroutine(attackRoutine); attackRoutine = null; }
        evadeComp?.CancelEvade();
        chargeComp?.CancelAll();
        chargeInvincible = false;
        CancelRecoil();

        equipComp.WeaponBehavior?.GetComponent<WeaponAmmoRuntime>()?.InterruptReload();
        equipComp.WeaponBehavior?.GetComponent<WeaponAmmoRuntime_AR>()?.InterruptReload();

        if (arFireRoutine != null) { StopCoroutine(arFireRoutine); arFireRoutine = null; }
        EndARFireState();

        if (knockbackRoutine != null) { StopCoroutine(knockbackRoutine); knockbackRoutine = null; }
        knockbackRoutine = StartCoroutine(KnockbackRoutine(dir, power, duration, stun));
    }

    public void ApplyKnockback(Vector3 dir, float power, float duration, float stun)
    {
        ForceApplyKnockback(dir, power, duration, stun);
    }

    private IEnumerator KnockbackRoutine(Vector3 dir, float power, float duration, float stun)
    {
        ChangeState(PlayerState.Knockback);

        Vector3 knockDir = dir.normalized; knockDir.y = 0f;
        if (knockDir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(-knockDir);

        movement?.ApplyKnockback(knockDir, power, duration, null);

        yield return new WaitForSeconds(duration);

        if (stun > 0f)
        {
            ChangeState(PlayerState.Stun);
            yield return new WaitForSeconds(stun);
        }

        ChangeState(PlayerState.Idle);
        knockbackRoutine = null;
    }
    #endregion

    public bool IsInvincible() => (evadeComp?.IsInvincible() ?? false) || chargeInvincible;
    public float GetEvadeGauge() => evadeComp != null ? evadeComp.GetEvadeGauge() : 0f;
    public float GetMaxEvadeGauge() => evadeComp != null ? evadeComp.GetMaxEvadeGauge() : (evadeData != null ? evadeData.maxGauge : 100f);
    public bool CanPerformEvade() => evadeComp != null && evadeComp.CanEvade();

    public List<Transform> DetectEnemies()
    {
        if (enemyDetector == null) return new List<Transform>();
        return enemyDetector.GetEnemiesInRange(enemyDetector.viewDistance);
    }

    public WeaponDataSO GetCurrentWeaponData() => equipComp.CurrentWeaponData;

    private void StartRecoilIfNeeded(WeaponDataSO data)
    {
        if (recoilComp == null || data == null) return;
        if (data.recoilDuration <= 0f) return;
        if (Mathf.Approximately(data.recoilPower, 0f)) return;

        recoilComp.StartRecoil(data, () => state == PlayerState.Attack, transform);
    }

    private void CancelRecoil()
    {
        recoilComp?.Cancel();
    }

    /* ───────── AR 전용 상태 ───────── */
    public bool IsARFiring => arFireRoutine != null;
    public bool ARAllowMoveWhileFiring => arAllowMoveWhileFiringFlag && IsARFiring;
    public bool ARIsRotationLocked => arRotationLocked && IsARFiring;
    public Vector3 ARLockedForward => arLockedForward;

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
            Debug.LogWarning("[AR] WeaponBehavior 없음");
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
                            if (debugMode) Debug.Log("[AR] 탄창 비어 있음 → 기본 무기 전환 요청");
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
                            Debug.Log("[Ammo] AR 탄약 고갈 → 기본 무기 전환 요청");
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
        float u = Random.Range(cosMax, 1f);
        float theta = Mathf.Acos(u);
        float phi = Random.Range(0f, Mathf.PI * 2f);
        float sinT = Mathf.Sin(theta);

        Vector3 local = new Vector3(sinT * Mathf.Cos(phi), sinT * Mathf.Sin(phi), Mathf.Cos(theta));
        Quaternion rot = Quaternion.FromToRotation(Vector3.forward, baseDir.normalized);
        return rot * local;
    }
}