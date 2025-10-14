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

    private float currentEvadeGauge;
    private bool isInvincible = false;

    private NavMeshAgent agent;

    // ───────── Ammo 스냅샷 저장 구조 ─────────
    private struct AmmoSnapshot
    {
        public int magazine;
        public int reserve;
    }
    private readonly Dictionary<WeaponDataSO_Gun, AmmoSnapshot> gunAmmoSnapshots = new();

    // 리로드 중 메시지 쿨다운 (이전 단계에서 도입했다면 유지)
    private float lastReloadMsgTime = -999f;
    private const float RELOAD_MSG_COOLDOWN = 0.3f;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        agent = GetComponent<NavMeshAgent>();
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

        // 회피 시 리로드 인터럽트 (스냅샷 저장은 교체 시점)
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

        // CC / 회피 / 죽음 시 리로드 인터럽트
        if (newState == PlayerState.Knockback ||
            newState == PlayerState.Stun ||
            newState == PlayerState.Evade ||
            newState == PlayerState.Dead)
        {
            weaponBehavior?.GetComponent<WeaponAmmoRuntime>()?.InterruptReload();
        }

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

        // 리로드 중이라면 캔슬로 간주 (이미 InterruptReload 호출 가능)
        if (ammo.IsReloading)
            ammo.InterruptReload();

        int magazine = ammo.CurrentMagazine;
        int reserve = gun.infiniteReserve ? 0 : ammo.CurrentReserve; // infiniteReserve면 의미 없음

        gunAmmoSnapshots[gun] = new AmmoSnapshot { magazine = magazine, reserve = reserve };
        if (debugMode)
            Debug.Log($"[Ammo] 스냅샷 저장 gun={gun.weaponName} mag:{magazine}/{gun.magazineSize} reserve:{(gun.infiniteReserve ? "∞" : reserve.ToString())}");
    }

    public void EquipWeapon(GameObject weaponPrefab)
    {
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
            // WeaponBehavior에 EnsureAmmoInitialized() 가 있다면 호출 (ammo 컴포넌트 생성/초기화)
            weaponBehavior?.EnsureAmmoInitialized();
            var ammo = weaponBehavior.GetComponent<WeaponAmmoRuntime>();

            if (gunAmmoSnapshots.TryGetValue(g, out var snap) && ammo != null)
            {
                ammo.LoadSnapshot(snap.magazine, snap.reserve, triggerAutoReload: true);
            }
            else
            {
                // 스냅 없음 → 초기 상태 그대로
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

        weaponBehavior?.AttackHit();

        yield return new WaitForSeconds(currentWeaponData.cooldown);

        ChangeState(PlayerState.Idle);
        animationController?.EndAttack();

        currentAttackCoroutine = null;
    }

    #region Knockback / CC
    public void ForceApplyKnockback(Vector3 dir, float power, float duration, float stun)
    {
        if (currentAttackCoroutine != null) { StopCoroutine(currentAttackCoroutine); currentAttackCoroutine = null; }
        if (currentKnockbackCoroutine != null) { StopCoroutine(currentKnockbackCoroutine); currentKnockbackCoroutine = null; }
        if (currentEvadeCoroutine != null) { StopCoroutine(currentEvadeCoroutine); currentEvadeCoroutine = null; isInvincible = false; }

        // 리로드 인터럽트
        weaponBehavior?.GetComponent<WeaponAmmoRuntime>()?.InterruptReload();

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
}