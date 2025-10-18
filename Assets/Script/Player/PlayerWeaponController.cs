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

    private GameObject currentWeapon;
    private WeaponBehavior weaponBehavior;
    private WeaponDataSO currentWeaponData;

    private float lastAttackTime = -999f;
    private PlayerMovement movement;
    private PlayerState state = PlayerState.Idle;
    private PlayerState previousState = PlayerState.Idle;

    private Coroutine currentAttackCoroutine;
    private Coroutine currentKnockbackCoroutine;
    private Coroutine currentEvadeCoroutine;

    // ───────── 🆕 리코일 ─────────
    private Coroutine recoilRoutine;

    // 차지 유지 코루틴
    private Coroutine currentChargedAttackCoroutine;

    private float currentEvadeGauge;
    private bool isInvincible = false;

    private NavMeshAgent agent;

    // 차지 진행 상태
    private bool chargeHoldActive = false;
    private float chargeHoldStartTime = 0f;
    private bool chargeStartMsgDone = false;   // 1.0s
    private bool chargeSuccessMsgDone = false; // slot.holdSuccessTime
    private bool chargeExecuted = false;       // 발사 1회 보장
    private bool chargeReady = false;          // holdSuccessTime 달성 여부

    // 무적/스폰 딜레이 루틴
    private Coroutine invincibleRoutine;
    private Coroutine chargeSpawnRoutine;

    // 스폰 포인트 캐시 (Root_dummy)
    private Transform meleeSpawnPointCache;

    // 차지 넉백/스턴 전달용 WeaponDataSO 프록시
    private WeaponDataSO chargeWeaponProxy;

    // Ammo 스냅샷 저장
    private struct AmmoSnapshot { public int magazine; public int reserve; }
    private readonly Dictionary<WeaponDataSO_Gun, AmmoSnapshot> gunAmmoSnapshots = new();

    private float lastReloadMsgTime = -999f;
    private const float RELOAD_MSG_COOLDOWN = 0.3f;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        agent = GetComponent<NavMeshAgent>();

        // Root_dummy 찾기(없으면 자기 transform 사용)
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
    }

    private void Start()
    {
        EquipWeapon(null);
        ChangeState(PlayerState.Idle);
        if (evadeData != null)
            currentEvadeGauge = evadeData.maxGauge;
    }

    private void Update()
    {
        if (state == PlayerState.Dead) return;

        // 홀드-투-릴리스 차지(시작/실행 게이트 분리)
        TickChargeHold();

        if (evadeData != null && currentEvadeGauge < evadeData.maxGauge)
        {
            UpdateEvadeGauge();
        }

        AutoResumeReloadIfNeeded();

        if (InputManager.Instance.GetEvadeInput() && CanEvade() && state != PlayerState.Evade)
        {
            Vector2 currentMoveInput = InputManager.Instance.GetMoveInput();
            PerformEvade(currentMoveInput);
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

    // ───────── 차지 슬롯/게이트 헬퍼 ─────────
    private PlayerChargeAttackSO GetCurrentChargeSlot()
    {
        return currentWeaponData != null ? currentWeaponData.chargeSlot : null;
    }

    private bool IsChargeExecutionAllowedNow()
    {
        // 실행(발사)은 Idle/Move일 때만 허용
        return state == PlayerState.Idle || state == PlayerState.Move;
    }

    private void TickChargeHold()
    {
        var slot = GetCurrentChargeSlot();

        // Down: 홀드 시작 (게이트: 현재 무기의 chargeSlot 존재) → 상태 무관
        if (!chargeHoldActive && InputManager.Instance.GetAttackDown())
        {
            if (slot == null)
            {
                if (debugMode) Debug.Log("[Charge] 시작 불가: 현재 무기에 차지 슬롯 없음");
            }
            else
            {
                chargeHoldActive = true;
                chargeHoldStartTime = Time.time;
                chargeStartMsgDone = false;
                chargeSuccessMsgDone = false;
                chargeExecuted = false;
                chargeReady = false;

                if (debugMode) Debug.Log("[Charge] 홀드 시작 (상태 무관)");
            }
        }

        // Hold 중: 메시지/성공 플래그만 (즉시 발사 금지)
        if (chargeHoldActive && InputManager.Instance.GetAttack())
        {
            float held = Time.time - chargeHoldStartTime;

            // 1초 고정: 차지 시작 메시지
            if (enableChargeMessages && !chargeStartMsgDone && held >= 1.0f)
            {
                chargeStartMsgDone = true;
                Debug.Log("차지 시작");
            }

            // SO 지정 시간: 차지 성공(발사 준비) 플래그
            if (slot != null && !chargeReady && held >= slot.holdSuccessTime)
            {
                chargeReady = true;
                if (enableChargeMessages && !chargeSuccessMsgDone)
                {
                    chargeSuccessMsgDone = true;
                    Debug.Log("차지 성공");
                }
            }
        }

        // Up: 발사 시도 (게이트: Idle/Move 상태에서만 실행, Evade 중에 Up이면 취소)
        if (chargeHoldActive && InputManager.Instance.GetAttackUp())
        {
            bool fired = false;

            var slot2 = GetCurrentChargeSlot();
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
                if (debugMode) Debug.Log("[Charge] 취소: 방출 시점 상태가 Idle/Move 아님(Evade 포함) → 실행 불가");
            }
            else if (!chargeExecuted)
            {
                ExecuteChargeAttack(slot2);
                chargeExecuted = true;
                fired = true;
            }

            // 플래그 리셋(Up 시에는 성공/실패 모두 홀드 종료)
            chargeHoldActive = false;
            chargeStartMsgDone = false;
            chargeSuccessMsgDone = false;
            chargeReady = false;
            chargeExecuted = false;

            if (fired && debugMode) Debug.Log("[Charge] 릴리스 → 발사 완료");
        }
    }

    private void AutoResumeReloadIfNeeded()
    {
        if (weaponBehavior == null) return;
        var gun = currentWeaponData as WeaponDataSO_Gun;
        var ammo = weaponBehavior is { } wb ? wb.GetComponent<WeaponAmmoRuntime>() : null;
        if (gun == null || ammo == null || !gun.usesAmmo) return;

        if ((state == PlayerState.Idle || state == PlayerState.Move) &&
            !ammo.IsReloading &&
            ammo.IsMagazineEmpty() &&
            ammo.HasAnyReserveOrInfinite())
        {
            ammo.TryStartReload();
        }
    }

    #region Evade
    private void UpdateEvadeGauge()
    {
        if (evadeData == null) return;
        if (currentEvadeGauge < evadeData.maxGauge)
        {
            currentEvadeGauge += evadeData.rechargeRate * Time.deltaTime;
            currentEvadeGauge = Mathf.Min(currentEvadeGauge, evadeData.maxGauge);
        }
    }

    private bool CanEvade()
    {
        if (evadeData == null) return false;
        return currentEvadeGauge >= evadeData.evadeCost;
    }

    public void PerformEvade(Vector2 moveInput)
    {
        if (!CanEvade()) return;

        Vector3 initialDirection;
        if (moveInput.magnitude > 0.1f)
        {
            initialDirection = new Vector3(moveInput.x, 0, moveInput.y);
        }
        else
        {
            initialDirection = transform.forward;
        }

        if (Camera.main != null)
        {
            Vector3 camForward = Camera.main.transform.forward;
            Vector3 camRight = Camera.main.transform.right;
            camForward.y = 0; camRight.y = 0;
            camForward.Normalize(); camRight.Normalize();
            initialDirection = (camForward * initialDirection.z + camRight * initialDirection.x).normalized;
        }

        // 기존 코루틴 정리
        if (currentAttackCoroutine != null) { StopCoroutine(currentAttackCoroutine); currentAttackCoroutine = null; }
        if (currentKnockbackCoroutine != null) { StopCoroutine(currentKnockbackCoroutine); currentKnockbackCoroutine = null; }
        if (currentEvadeCoroutine != null) { StopCoroutine(currentEvadeCoroutine); currentEvadeCoroutine = null; }
        CancelRecoil(); // 🆕 회피 시 리코일 취소

        // 회피 시 리ロード 인터럽트
        weaponBehavior?.GetComponent<WeaponAmmoRuntime>()?.InterruptReload();

        currentEvadeGauge -= evadeData.evadeCost;

        if (evadeData.allowDirectionChangeWhileEvading)
            currentEvadeCoroutine = StartCoroutine(DynamicEvadeRoutine(initialDirection));
        else
            currentEvadeCoroutine = StartCoroutine(FixedEvadeRoutine(initialDirection));
    }

    private IEnumerator FixedEvadeRoutine(Vector3 fixedDirection)
    {
        ChangeState(PlayerState.Evade);

        float elapsed = 0f;
        Vector3 evadeDir = fixedDirection.normalized;
        evadeDir.y = 0f;
        isInvincible = true;

        if (evadeDir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(evadeDir);

        while (elapsed < evadeData.evadeDuration)
        {
            float t = elapsed / evadeData.evadeDuration;
            float speedMul = evadeData.speedCurve.Evaluate(t);
            transform.position += evadeDir * (evadeData.evadeSpeed * speedMul) * Time.deltaTime;

            if (elapsed >= evadeData.invincibilityDuration)
                isInvincible = false;

            elapsed += Time.deltaTime;
            yield return null;
        }

        FinishEvade();
    }

    private IEnumerator DynamicEvadeRoutine(Vector3 initialDirection)
    {
        ChangeState(PlayerState.Evade);

        float elapsed = 0f;
        Vector3 currentDirection = initialDirection.normalized;
        currentDirection.y = 0f;
        isInvincible = true;

        while (elapsed < evadeData.evadeDuration)
        {
            float t = elapsed / evadeData.evadeDuration;

            Vector2 input = InputManager.Instance.GetMoveInput();
            if (input.magnitude >= evadeData.minInputMagnitude)
            {
                Vector3 newDir = new Vector3(input.x, 0, input.y);
                if (Camera.main != null)
                {
                    Vector3 camF = Camera.main.transform.forward;
                    Vector3 camR = Camera.main.transform.right;
                    camF.y = 0; camR.y = 0;
                    camF.Normalize(); camR.Normalize();
                    newDir = (camF * newDir.z + camR * newDir.x).normalized;
                    newDir.y = 0f;
                }

                float lerp = evadeData.directionChangeSensitivity * Time.deltaTime;
                currentDirection = Vector3.Lerp(currentDirection, newDir, lerp).normalized;

                if (currentDirection.sqrMagnitude > 0.01f)
                {
                    Quaternion target = Quaternion.LookRotation(currentDirection, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, target, lerp);
                }
            }

            float speedMul = evadeData.speedCurve.Evaluate(t);
            transform.position += currentDirection * (evadeData.evadeSpeed * speedMul) * Time.deltaTime;

            if (elapsed >= evadeData.invincibilityDuration)
                isInvincible = false;

            elapsed += Time.deltaTime;
            yield return null;
        }

        FinishEvade();
    }

    private void FinishEvade()
    {
        isInvincible = false;
        animationController?.EndEvade();
        if (movement.GetVelocityMagnitude() > 0.1f) ChangeState(PlayerState.Move);
        else ChangeState(PlayerState.Idle);
        currentEvadeCoroutine = null;
    }
    #endregion

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
        previousState = state;
        state = newState;

        // CC / 죽음 시에만 차지 취소 (회피는 홀드 유지)
        if (newState == PlayerState.Knockback ||
            newState == PlayerState.Stun ||
            newState == PlayerState.Dead)
        {
            weaponBehavior?.GetComponent<WeaponAmmoRuntime>()?.InterruptReload();
            CancelChargeAll();
        }

        // 🆕 Attack이 아닌 상태로 전환되면 리코일 취소
        if (newState != PlayerState.Attack)
            CancelRecoil();

        animationController?.ForceAnimationByState(newState);
    }

    /// <summary>
    /// 현재 장착 중인 Gun 탄약 상태를 스냅샷에 저장 (무기 파괴 직전 호출)
    /// </summary>
    private void SaveCurrentGunSnapshot()
    {
        if (weaponBehavior == null || currentWeaponData == null) return;
        if (currentWeaponData is not WeaponDataSO_Gun gun || !gun.usesAmmo) return;

        var ammo = weaponBehavior.GetComponent<WeaponAmmoRuntime>();
        if (ammo == null || !ammo.IsInitialized) return;

        if (ammo.IsReloading)
            ammo.InterruptReload();

        int magazine = ammo.CurrentMagazine;
        int reserve = gun.infiniteReserve ? 0 : ammo.CurrentReserve;

        gunAmmoSnapshots[gun] = new AmmoSnapshot { magazine = magazine, reserve = reserve };
        if (debugMode)
            Debug.Log($"[Ammo] 스냅샷 저장 gun={gun.weaponName} mag:{magazine}/{gun.magazineSize} reserve:{(gun.infiniteReserve ? "∞" : reserve.ToString())}");
    }

    public void EquipWeapon(GameObject weaponPrefab)
    {
        // 무기 교체 시 차지 즉시 취소(무기별 차지 다름)
        CancelChargeAll();

        // 1) 기존 무기 스냅샷 저장
        SaveCurrentGunSnapshot();

        // 2) 기존 무기 제거
        if (currentWeapon != null)
            Destroy(currentWeapon);

        // 3) 새 프리팹 결정
        GameObject prefabToSpawn = weaponPrefab != null ? weaponPrefab : defaultWeaponPrefab;
        if (prefabToSpawn == null)
        {
            Debug.LogError("❌ 기본 무기 프리팹이 연결되지 않았습니다.");
            return;
        }

        // 4) 인스턴스 생성
        currentWeapon = Instantiate(prefabToSpawn, weaponSocket);
        currentWeapon.transform.localPosition = Vector3.zero;
        currentWeapon.transform.localRotation = Quaternion.identity;

        // 5) 참조 갱신
        weaponBehavior = currentWeapon.GetComponent<WeaponBehavior>();
        currentWeaponData = weaponBehavior != null ? weaponBehavior.data : null;

        // 6) Gun이면 초기화 후 스냅샷 복원
        if (currentWeaponData is WeaponDataSO_Gun g && g.usesAmmo)
        {
            weaponBehavior?.EnsureAmmoInitialized();
            var ammo = weaponBehavior.GetComponent<WeaponAmmoRuntime>();

            if (gunAmmoSnapshots.TryGetValue(g, out var snap) && ammo != null)
            {
                ammo.LoadSnapshot(snap.magazine, snap.reserve, triggerAutoReload: true);
            }
            else
            {
                if (debugMode)
                    Debug.Log($"[Ammo] 스냅샷 없음 → 기본 초기화 gun={g.weaponName}");
            }
        }

        // 7) 애니메이션 AOC 적용
        if (animationController != null && currentWeaponData != null && currentWeaponData.overrideController != null)
        {
            animationController.GetAnimator().runtimeAnimatorController = currentWeaponData.overrideController;
        }

        Debug.Log($"무기 장착됨 → {currentWeaponData?.weaponName ?? "null"}");
    }

    public void PlayAttack()
    {
        if (currentWeaponData == null) return;
        if (IsActionBlocking()) return;

        // Gun 탄약 게이트
        var gun = currentWeaponData as WeaponDataSO_Gun;
        var ammo = weaponBehavior != null ? weaponBehavior.GetComponent<WeaponAmmoRuntime>() : null;

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
        if (delta < currentWeaponData.cooldown) return;
        lastAttackTime = Time.time;

        if (currentAttackCoroutine != null)
        {
            StopCoroutine(currentAttackCoroutine);
            currentAttackCoroutine = null;
        }
        currentAttackCoroutine = StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        ChangeState(PlayerState.Attack);
        animationController?.PlayAttack(currentWeaponData);

        // 🆕 리코일 시작 (SO 파라미터 기반)
        StartRecoilIfNeeded(currentWeaponData);

        weaponBehavior?.AttackHit();

        yield return new WaitForSeconds(currentWeaponData.cooldown);

        ChangeState(PlayerState.Idle);
        animationController?.EndAttack();

        // 안전상 리코일 정리
        CancelRecoil();

        currentAttackCoroutine = null;
    }

    // ───── 차지 발사(릴리스 시점) ─────
    private void ExecuteChargeAttack(PlayerChargeAttackSO slot)
    {
        if (slot == null) return;

        // 상태 전환(차지 공격 유지 시작)
        ChangeState(PlayerState.Attack);

        // 무적창 적용(발사 시점)
        if (slot.invincibilityDuration > 0f)
        {
            if (invincibleRoutine != null) StopCoroutine(invincibleRoutine);
            invincibleRoutine = StartCoroutine(GrantTemporalInvincibility(slot.invincibilityDuration));
        }

        // 애니메이션 이름 결정
        string animName = slot.chargedClip != null ? slot.chargedClip.name : slot.chargedStateName;
        if (string.IsNullOrEmpty(animName)) animName = "Attack_Charged01";
        animationController?.PlayChargedAttack(animName);

        // 히트박스 지연 생성 루틴 시작
        if (chargeSpawnRoutine != null)
        {
            StopCoroutine(chargeSpawnRoutine);
            chargeSpawnRoutine = null;
        }
        chargeSpawnRoutine = StartCoroutine(ChargeHitboxSpawnRoutine(slot));

        // 유지 시간 코루틴 시작 (slot.duration, 기본 0.8s)
        float dur = (slot.duration > 0f) ? slot.duration : 0.8f;
        if (currentChargedAttackCoroutine != null) { StopCoroutine(currentChargedAttackCoroutine); currentChargedAttackCoroutine = null; }
        currentChargedAttackCoroutine = StartCoroutine(ChargedAttackMaintainRoutine(dur));
    }

    private IEnumerator ChargedAttackMaintainRoutine(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // 하드 CC/죽음/회피로 상태 전환되면 즉시 종료 (Evade 중에는 유지하지 않음)
            if (state == PlayerState.Knockback || state == PlayerState.Stun ||
                state == PlayerState.Dead || state == PlayerState.Evade)
            {
                currentChargedAttackCoroutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 끝나면 Idle/Move 복귀
        if (movement != null && movement.GetVelocityMagnitude() > 0.1f) ChangeState(PlayerState.Move);
        else ChangeState(PlayerState.Idle);

        currentChargedAttackCoroutine = null;
    }

    private IEnumerator ChargeHitboxSpawnRoutine(PlayerChargeAttackSO slot)
    {
        if (slot.hitBoxPrefab == null)
        {
            Debug.LogWarning("⚠ 차지 힛박스 프리팹이 비어 있습니다.");
            yield break;
        }

        // 스폰 딜레이 대기
        if (slot.spawnDelay > 0f)
        {
            float waited = 0f;
            while (waited < slot.spawnDelay)
            {
                // CC/죽음/회피로 끊기면 종료 (Evade 중에는 스폰 대기도 취소)
                if (state == PlayerState.Knockback ||
                    state == PlayerState.Stun ||
                    state == PlayerState.Dead ||
                    state == PlayerState.Evade)
                {
                    chargeSpawnRoutine = null;
                    yield break;
                }
                float step = Mathf.Min(Time.deltaTime, slot.spawnDelay - waited);
                waited += step;
                yield return null;
            }
        }

        // 스폰 포인트
        Transform spawn = meleeSpawnPointCache != null ? meleeSpawnPointCache : transform;

        // 넉백/스턴 전달용 SO 프록시 준비
        EnsureChargeWeaponProxy(slot);

        // 1회 생성
        GameObject hb = Instantiate(slot.hitBoxPrefab, spawn.position, spawn.rotation);

        if (hb.TryGetComponent<HitBox_PC>(out var hitbox))
        {
            hitbox.SetWeapon(chargeWeaponProxy);

            if (slot.enableAreaDot)
            {
                // 드릴형 중복 히트(틱)
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
                // 즉발 1회
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

        if (debugMode)
        {
            Debug.Log($"[Charge] HB Spawn(Delay {slot.spawnDelay:F2}s) │ dmg:{slot.damage}, range:{slot.range}, kb:{slot.knockbackPower}, life:{slot.hitBoxLifetime}, dup:{slot.enableAreaDot}");
        }

        chargeSpawnRoutine = null;
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

        // 처치 연출/랙돌/슬라이스 파라미터 복사
        chargeWeaponProxy.deathType = slot.deathType;
        chargeWeaponProxy.ragdollImpulse = slot.ragdollImpulse;
        chargeWeaponProxy.upwardImpulse = slot.upwardImpulse;
        chargeWeaponProxy.torqueImpulse = slot.torqueImpulse;
        chargeWeaponProxy.sliceForce = slot.sliceForce;

        // 리스트는 참조 공유 대신 매번 새 인스턴스로 복사
        if (slot.possibleSliceParts != null && slot.possibleSliceParts.Count > 0)
            chargeWeaponProxy.possibleSliceParts = new List<BodySliceType>(slot.possibleSliceParts);
        else
            chargeWeaponProxy.possibleSliceParts = new List<BodySliceType>();
    }

    private IEnumerator GrantTemporalInvincibility(float duration)
    {
        isInvincible = true;
        yield return new WaitForSeconds(duration);
        isInvincible = false;
        invincibleRoutine = null;
    }

    #region Knockback / CC
    public void ForceApplyKnockback(Vector3 dir, float power, float duration, float stun)
    {
        if (currentAttackCoroutine != null) { StopCoroutine(currentAttackCoroutine); currentAttackCoroutine = null; }
        if (currentKnockbackCoroutine != null) { StopCoroutine(currentKnockbackCoroutine); currentKnockbackCoroutine = null; }
        if (currentEvadeCoroutine != null) { StopCoroutine(currentEvadeCoroutine); currentEvadeCoroutine = null; isInvincible = false; }
        if (currentChargedAttackCoroutine != null) { StopCoroutine(currentChargedAttackCoroutine); currentChargedAttackCoroutine = null; }
        CancelRecoil(); // 🆕 CC 시 리코일 즉시 취소

        // 리로드 인터럽트
        weaponBehavior?.GetComponent<WeaponAmmoRuntime>()?.InterruptReload();

        // 차지/스폰 모두 취소
        CancelChargeAll();

        currentKnockbackCoroutine = StartCoroutine(KnockbackRoutine(dir, power, duration, stun));
    }

    public void ApplyKnockback(Vector3 dir, float power, float duration, float stun)
    {
        ForceApplyKnockback(dir, power, duration, stun);
    }

    private IEnumerator KnockbackRoutine(Vector3 dir, float power, float duration, float stun)
    {
        ChangeState(PlayerState.Knockback);

        float resistance = 1f;
        if (TryGetComponent(out PlayerHealth health))
            resistance = Mathf.Max(health.GetWeight(), 0.01f);

        float elapsed = 0f;
        Vector3 knockDir = dir.normalized; knockDir.y = 0f;

        if (knockDir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(-knockDir);

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float currentSpeed = Mathf.Lerp(power / resistance, 0f, t);
            transform.position += knockDir * currentSpeed * Time.deltaTime;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (stun > 0f)
        {
            ChangeState(PlayerState.Stun);
            yield return new WaitForSeconds(stun);
        }

        ChangeState(PlayerState.Idle);
        currentKnockbackCoroutine = null;
    }
    #endregion

    private void CancelChargeAll()
    {
        // 차지 홀드/플래그
        chargeHoldActive = false;
        chargeStartMsgDone = false;
        chargeSuccessMsgDone = false;
        chargeExecuted = false;
        chargeReady = false;

        // 스폰 대기 중단
        if (chargeSpawnRoutine != null)
        {
            StopCoroutine(chargeSpawnRoutine);
            chargeSpawnRoutine = null;
        }

        // 발사 후 유지 코루틴 중단
        if (currentChargedAttackCoroutine != null)
        {
            StopCoroutine(currentChargedAttackCoroutine);
            currentChargedAttackCoroutine = null;
        }
    }

    // Invincible check
    public bool IsInvincible() => isInvincible;

    // Evade gauge getters
    public float GetEvadeGauge() => currentEvadeGauge;
    public float GetMaxEvadeGauge() => evadeData?.maxGauge ?? 100f;
    public bool CanPerformEvade() => CanEvade();

    // Enemy detector proxy
    public List<Transform> DetectEnemies()
    {
        if (enemyDetector == null)
            return new List<Transform>();
        return enemyDetector.GetEnemiesInRange(enemyDetector.viewDistance);
    }

    public WeaponDataSO GetCurrentWeaponData() => currentWeaponData;
    public PlayerState CurrentState => state;

    /* ───────── 🆕 리코일 내부 구현 ───────── */
    private void StartRecoilIfNeeded(WeaponDataSO data)
    {
        if (data == null) return;
        if (data.recoilDuration <= 0f) return;
        if (Mathf.Approximately(data.recoilPower, 0f)) return;

        CancelRecoil();
        recoilRoutine = StartCoroutine(RecoilRoutine(data));
    }

    private void CancelRecoil()
    {
        if (recoilRoutine != null)
        {
            StopCoroutine(recoilRoutine);
            recoilRoutine = null;
        }
    }

    private IEnumerator RecoilRoutine(WeaponDataSO data)
    {
        // Start delay (Attack 상태 유지되는 동안만)
        float delay = Mathf.Max(0f, data.recoilStartDelay);
        float waited = 0f;
        while (waited < delay)
        {
            if (state != PlayerState.Attack) { recoilRoutine = null; yield break; }
            float step = Mathf.Min(Time.deltaTime, delay - waited);
            waited += step;
            yield return null;
        }

        if (state != PlayerState.Attack) { recoilRoutine = null; yield break; }

        // 방향 스냅샷 (+면 뒤로, -면 앞으로)
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 dir = (data.recoilPower >= 0f ? -forward : forward);
        float speedAbs = Mathf.Abs(data.recoilPower);
        float duration = Mathf.Max(0f, data.recoilDuration);

        float elapsed = 0f;

        // 내부 감쇠 커브: v(t) = 4t(1-t) (t∈[0,1]) → 초반 가속 후 감속 정지
        while (elapsed < duration)
        {
            if (state != PlayerState.Attack) { recoilRoutine = null; yield break; }

            float t = duration > 0f ? (elapsed / duration) : 1f;
            float speedMul = 4f * t * (1f - t); // 0→최대(0.5)→0
            float currentSpeed = speedAbs * Mathf.Max(0f, speedMul);

            transform.position += dir * currentSpeed * Time.deltaTime;

            elapsed += Time.deltaTime;
            yield return null;
        }

        recoilRoutine = null;
    }
}