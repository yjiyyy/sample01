using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

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
    private NavMeshAgent agent;

    // v3 컴포넌트들
    private PlayerRecoil recoilComp;
    private PlayerEquipmentController equipComp;
    private PlayerChargeController chargeComp;
    private PlayerEvadeController evadeComp;
    private PlayerStateMachine fsm;

    // 상태
    private PlayerState state = PlayerState.Idle;
    public PlayerState CurrentState => state;

    // 차지 무적 OR 회피 무적을 통합 보고
    private bool chargeInvincible = false;

    // 공격 쿨타임
    private float lastAttackTime = -999f;

    // 코루틴
    private Coroutine attackRoutine;
    // ✅ 넉백 코루틴 핸들 보유(회피 진입 시 중단용)
    private Coroutine knockbackRoutine;

    // 스폰 포인트 캐시 (Root_dummy)
    private Transform meleeSpawnPointCache;

    // 탄약 메시지
    private float lastReloadMsgTime = -999f;
    private const float RELOAD_MSG_COOLDOWN = 0.3f;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        agent = GetComponent<NavMeshAgent>();

        // 런타임 부착
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

        // 차지 입력 틱(상태 무관)
        chargeComp?.Tick();

        // 회피 게이지 충전
        evadeComp?.TickRecharge(Time.deltaTime);

        AutoResumeReloadIfNeeded();

        // 회피 입력
        if (InputManager.Instance.GetEvadeInput() && (evadeComp?.CanEvade() ?? false) && state != PlayerState.Evade)
        {
            Vector2 currentMoveInput = InputManager.Instance.GetMoveInput();

            System.Action preEvadeCleanup = () =>
            {
                if (attackRoutine != null) { StopCoroutine(attackRoutine); attackRoutine = null; }
                // ✅ 넉백 이동/코루틴 즉시 중단(Evade 우선 적용)
                if (knockbackRoutine != null) { StopCoroutine(knockbackRoutine); knockbackRoutine = null; }
                movement?.CancelKnockback();

                CancelRecoil();
                // 리로드 인터럽트
                equipComp.WeaponBehavior?.GetComponent<WeaponAmmoRuntime>()?.InterruptReload();
                // ❌ 정책: 회피 중 차지 유지 → CancelAll() 호출하지 않음
                // chargeComp.CancelAll();
            };

            evadeComp.PerformEvade(currentMoveInput, preEvadeCleanup);
            return;
        }

        switch (state)
        {
            case PlayerState.Idle:
                HandleIdle();
                break;
            case PlayerState.Move:
                HandleMove();
                break;
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

        // CC/죽음 시 차지 취소(정책 유지)
        if (newState == PlayerState.Knockback ||
            newState == PlayerState.Stun ||
            newState == PlayerState.Dead)
        {
            equipComp.WeaponBehavior?.GetComponent<WeaponAmmoRuntime>()?.InterruptReload();
            chargeComp?.CancelAll();
        }

        // Attack 이탈 시 리코일 취소
        if (newState != PlayerState.Attack)
            CancelRecoil();

        animationController?.ForceAnimationByState(newState);
    }

    public void EquipWeapon(GameObject weaponPrefab)
    {
        // 무기 교체 시 차지 취소(정책 유지)
        chargeComp?.CancelAll();

        equipComp.Equip(weaponPrefab, defaultWeaponPrefab, debugLogs: debugMode);

        // EnemyDetector에 무기 연결
        if (enemyDetector != null && equipComp.WeaponBehavior != null)
            enemyDetector.weaponBehavior = equipComp.WeaponBehavior;
    }

    public void PlayAttack()
    {
        var data = equipComp.CurrentWeaponData;
        if (data == null) return;
        if (IsActionBlocking()) return;

        // Gun 탄약 게이트
        var gun = data as WeaponDataSO_Gun;
        var ammo = equipComp.WeaponBehavior != null ? equipComp.WeaponBehavior.GetComponent<WeaponAmmoRuntime>() : null;

        if (gun != null && gun.usesAmmo && ammo != null)
        {
            if (ammo.IsReloading)
            {
                if (Time.time - lastReloadMsgTime >= RELOAD_MSG_COOLDOWN)
                {
                    float remain = ammo.GetReloadRemaining();
                    Debug.Log($"[Ammo] 리로드 중입니다… (남은:{remain:F2}s)");
                    lastReloadMsgTime = Time.time;
                }
                return;
            }

            if (!ammo.CanFire(gun.consumePerShot))
            {
                if (!ammo.HasAnyReserveOrInfinite())
                {
                    Debug.Log("[Ammo] 탄약이 없습니다! (탄창 0 / 예비 0)");
                    return;
                }
                ammo.TryStartReload();
                return;
            }
        }

        float delta = Time.time - lastAttackTime;
        if (delta < data.cooldown) return;
        lastAttackTime = Time.time;

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        attackRoutine = StartCoroutine(AttackRoutine(data));
    }

    private IEnumerator AttackRoutine(WeaponDataSO data)
    {
        ChangeState(PlayerState.Attack);
        animationController?.PlayAttack(data);

        // 리코일 시작
        StartRecoilIfNeeded(data);

        // 타격 실행
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
        var ammo = wb.GetComponent<WeaponAmmoRuntime>();
        if (gun == null || ammo == null || !gun.usesAmmo) return;

        if ((state == PlayerState.Idle || state == PlayerState.Move) &&
            !ammo.IsReloading &&
            ammo.IsMagazineEmpty() &&
            ammo.HasAnyReserveOrInfinite())
        {
            ammo.TryStartReload();
        }
    }

    #region Knockback / CC (이동은 PlayerMovement로 일원화)
    public void ForceApplyKnockback(Vector3 dir, float power, float duration, float stun)
    {
        if (attackRoutine != null) { StopCoroutine(attackRoutine); attackRoutine = null; }

        // 회피 취소
        evadeComp?.CancelEvade();

        // 차지 무적/대기 해제(정책 유지: CC 시 취소)
        chargeComp?.CancelAll();
        chargeInvincible = false;

        // 리코일 취소
        CancelRecoil();

        // 리로드 인터럽트
        equipComp.WeaponBehavior?.GetComponent<WeaponAmmoRuntime>()?.InterruptReload();

        // 기존 넉백 루틴 중단 후 재시작
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
            transform.rotation = Quaternion.LookRotation(-knockDir); // 피격자 기준 공격자 바라보게

        // 실제 이동은 PlayerMovement로 통일
        movement?.ApplyKnockback(knockDir, power, duration, null);

        // 넉백 시간 대기
        yield return new WaitForSeconds(duration);

        // 스턴
        if (stun > 0f)
        {
            ChangeState(PlayerState.Stun);
            yield return new WaitForSeconds(stun);
        }

        ChangeState(PlayerState.Idle);
        knockbackRoutine = null; // ✅ 정리
    }
    #endregion

    // 무적: 회피 OR 차지
    public bool IsInvincible() => (evadeComp?.IsInvincible() ?? false) || chargeInvincible;

    // Evade gauge getters (위임)
    public float GetEvadeGauge() => evadeComp != null ? evadeComp.GetEvadeGauge() : 0f;
    public float GetMaxEvadeGauge() => evadeComp != null ? evadeComp.GetMaxEvadeGauge() : (evadeData != null ? evadeData.maxGauge : 100f);
    public bool CanPerformEvade() => evadeComp != null && evadeComp.CanEvade();

    // Enemy detector proxy
    public List<Transform> DetectEnemies()
    {
        if (enemyDetector == null)
            return new List<Transform>();
        return enemyDetector.GetEnemiesInRange(enemyDetector.viewDistance);
    }

    public WeaponDataSO GetCurrentWeaponData() => equipComp.CurrentWeaponData;

    // 리코일 위임
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
}