// (파일 전체 — AR 쿨다운 관련 로직을 수정한 버전)
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
    // 🆕 AR 연사 루틴
    private Coroutine arFireRoutine;

    // 🆕 AR 상태 플래그
    private bool arRotationLocked = false;
    private Vector3 arLockedForward;
    private bool arAllowMoveWhileFiringFlag = false;
    private bool arAutoResumeWhileHeld = false;

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

                // AR 연사 취소
                if (arFireRoutine != null) { StopCoroutine(arFireRoutine); arFireRoutine = null; }
                EndARFireState();

                CancelRecoil();
                // 리로드 인터럽트
                equipComp.WeaponBehavior?.GetComponent<WeaponAmmoRuntime>()?.InterruptReload();
                equipComp.WeaponBehavior?.GetComponent<WeaponAmmoRuntime_AR>()?.InterruptReload();
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
            equipComp.WeaponBehavior?.GetComponent<WeaponAmmoRuntime_AR>()?.InterruptReload();
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
        // AR 연사 종료
        if (arFireRoutine != null) { StopCoroutine(arFireRoutine); arFireRoutine = null; }
        EndARFireState();

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

        // 🆕 Assault Rifle: 홀드 연사 진입 (cooldown 간격)
        if (data is WeaponDataSO_AR arData)
        {
            // ---- 변경: AR도 일반 무기와 동일한 쿨다운 검사를 통과해야 연사 루틴 시작 ----
            float delta = Time.time - lastAttackTime;
            if (delta < arData.cooldown)
                return;
            // 첫 발 허용 시점(시작) 기준으로 lastAttackTime 갱신
            lastAttackTime = Time.time;
            // --------------------------------------------------------------

            if (arFireRoutine != null) { StopCoroutine(arFireRoutine); arFireRoutine = null; }
            arFireRoutine = StartCoroutine(AssaultRifleFireRoutine(arData));
            return;
        }

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

        float delta2 = Time.time - lastAttackTime;
        if (delta2 < data.cooldown) return;
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
        equipComp.WeaponBehavior?.GetComponent<WeaponAmmoRuntime_AR>()?.InterruptReload();

        // AR 연사 취소
        if (arFireRoutine != null) { StopCoroutine(arFireRoutine); arFireRoutine = null; }
        EndARFireState();

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

    /* ───────── Assault Rifle 전용 ───────── */
    public bool IsARFiring => arFireRoutine != null;
    public bool ARAllowMoveWhileFiring => arAllowMoveWhileFiringFlag && IsARFiring;
    public bool ARIsRotationLocked => arRotationLocked && IsARFiring;
    public Vector3 ARLockedForward => arLockedForward;

    private void BeginARFireState(WeaponDataSO_AR arData)
    {
        arAllowMoveWhileFiringFlag = arData.allowMoveWhileFiring;
        arAutoResumeWhileHeld = arData.autoReloadResumeWhileHeld;
        arRotationLocked = arData.lockRotationDuringFiring;

        // 스냅샷 forward
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
        // 상태 진입
        ChangeState(PlayerState.Attack);
        animationController?.PlayAttack(ar); // 초기 한 번 재생
        BeginARFireState(ar);

        var wb = equipComp.WeaponBehavior;
        if (wb == null)
        {
            Debug.LogWarning("[AR] WeaponBehavior 없음");
            ChangeState(PlayerState.Idle);
            EndARFireState();
            yield break;
        }

        // 탄약 런타임
        var ammo = wb.GetComponent<WeaponAmmoRuntime_AR>();
        if (ammo == null) { ammo = wb.gameObject.AddComponent<WeaponAmmoRuntime_AR>(); }
        ammo.Initialize(ar, force: false);

        float interval = Mathf.Max(0.01f, ar.cooldown);
        float nextTime = Time.time;

        // 연사 루프
        while (true)
        {
            // 중단 조건: CC/죽음/회피 등
            if (state == PlayerState.Knockback || state == PlayerState.Stun || state == PlayerState.Dead || state == PlayerState.Evade)
                break;

            // 홀드 체크
            bool holding = InputManager.Instance.GetAttack();
            if (!holding)
                break;

            // 리로드 중 처리
            if (ammo.IsReloading)
            {
                // 홀드+자동재개 ON이면 리로드 동안에도 회전 잠금/상태 유지
                if (arAutoResumeWhileHeld && ar.lockRotationDuringFiring)
                {
                    // 리로드 완료까지 대기
                    yield return null;
                    continue;
                }
                // 옵션 OFF거나 홀드 해제 → 종료
                break;
            }

            // 탄 발사
            if (Time.time >= nextTime)
            {
                if (ammo.CanFire(ar.consumePerShot))
                {
                    if (ammo.TryConsumeForShot(ar.consumePerShot))
                    {
                        // 매 탄마다 리코일
                        StartRecoilIfNeeded(ar);

                        // 기준 방향(locked 또는 현재 forward)
                        Vector3 baseDir = arRotationLocked ? arLockedForward : transform.forward;
                        baseDir.y = 0f; // 기준을 평면 전방으로 잡되, preserveVertical == true일 때는 콘 샘플이 y를 갖게 함
                        if (baseDir.sqrMagnitude < 0.0001f) baseDir = Vector3.forward;
                        baseDir.Normalize();

                        Vector3 shootDir;
                        if (ar.spreadAngle > 0f)
                        {
                            float halfAngle = ar.spreadAngle * 0.5f;
                            // 3D 콘 샘플링 (구면 균등)
                            shootDir = RandomDirectionInCone(baseDir, halfAngle);
                        }
                        else
                        {
                            shootDir = baseDir;
                        }

                        // ─── 추가: 발사 시 애니메이션을 매샷 재생 ───
                        animationController?.PlayAttack(ar);
                        // --------------------------------------------

                        // AR은 FireProjectileForced를 사용하므로 preserveVertical 플래그로 y 보존 여부 지정
                        wb.FireProjectileForced(shootDir, ar.spread3D);

                        // 발사 시점에 lastAttackTime 갱신(쿨다운 일관성)
                        lastAttackTime = Time.time;

                        nextTime += interval;
                        // 드리프트 보정
                        if (Time.time - nextTime > interval) nextTime = Time.time + interval;
                    }
                }
                else
                {
                    // 자동 리로드 정책
                    if (ar.autoReloadOnEmpty && ammo.HasAnyReserveOrInfinite())
                    {
                        ammo.TryStartReload();
                        if (arAutoResumeWhileHeld && holding && ar.lockRotationDuringFiring)
                        {
                            // 리로드 완료 후 즉시 발사 가능하게 nextTime 리셋
                            nextTime = Time.time;
                            // 루프 유지(회전 잠금 유지)
                            yield return null;
                            continue;
                        }
                    }

                    // 탄약 없음 또는 옵션 OFF → 종료
                    break;
                }
            }

            yield return null;
        }

        // 종료
        animationController?.EndAttack();
        // 이동 속도에 따라 Idle/Move 결정
        if (movement != null && movement.GetVelocityMagnitude() > 0.1f)
            ChangeState(PlayerState.Move);
        else
            ChangeState(PlayerState.Idle);

        EndARFireState();
        arFireRoutine = null;
    }

    // ---------- 헬퍼: baseDir 기준 콘 내부 균등 샘플(halfAngle in degrees) ----------
    private Vector3 RandomDirectionInCone(Vector3 baseDir, float halfAngleDeg)
    {
        if (halfAngleDeg <= 0f) return baseDir.normalized;

        float halfRad = Mathf.Deg2Rad * halfAngleDeg;
        float cosMax = Mathf.Cos(halfRad);
        float u = Random.Range(cosMax, 1f); // cosθ 균등 분포
        float theta = Mathf.Acos(u);
        float phi = Random.Range(0f, Mathf.PI * 2f);
        float sinTheta = Mathf.Sin(theta);

        // 로컬(전방 z축 기준) 벡터
        Vector3 local = new Vector3(sinTheta * Mathf.Cos(phi), sinTheta * Mathf.Sin(phi), Mathf.Cos(theta));
        // 회전해서 baseDir 축에 맞춤
        Quaternion rot = Quaternion.FromToRotation(Vector3.forward, baseDir.normalized);
        return rot * local;
    }
}