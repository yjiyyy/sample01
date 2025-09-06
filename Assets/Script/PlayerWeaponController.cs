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
    Evade  // ✅ 회피 상태 추가
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

    [Header("✅ 회피 설정")]
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

    // 🆕 현재 실행 중인 코루틴들 추적
    private Coroutine currentAttackCoroutine;
    private Coroutine currentKnockbackCoroutine;
    private Coroutine currentEvadeCoroutine;  // ✅ 회피 코루틴 추가

    // ✅ 회피 관련 변수들
    private float currentEvadeGauge;
    private bool isInvincible = false;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
    }

    private void Start()
    {
        EquipWeapon(null);
        ChangeState(PlayerState.Idle);

        // ✅ 회피 게이지 초기화
        if (evadeData != null)
            currentEvadeGauge = evadeData.maxGauge;
    }

    private void Update()
    {
        if (state == PlayerState.Dead) return;

        // ✅ 회피 게이지 자동 회복
        UpdateEvadeGauge();

        // ✅ 회피 입력 체크 (모든 상태에서 최우선, 단 이미 회피 중이면 무시)
        if (InputManager.Instance.GetEvadeInput() && CanEvade() && state != PlayerState.Evade)
        {
            // 🔹 입력을 감지하는 순간 즉시 이동 방향도 캐시
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
            case PlayerState.Evade:  // ✅ 회피 중에는 다른 입력 무시
                break;
        }
    }

    // ✅ 회피 게이지 관리
    private void UpdateEvadeGauge()
    {
        if (evadeData == null) return;

        if (currentEvadeGauge < evadeData.maxGauge)
        {
            currentEvadeGauge += evadeData.rechargeRate * Time.deltaTime;
            currentEvadeGauge = Mathf.Min(currentEvadeGauge, evadeData.maxGauge);
        }
    }

    // ✅ 회피 가능 여부 체크
    private bool CanEvade()
    {
        if (evadeData == null) return false;
        return currentEvadeGauge >= evadeData.evadeCost;
    }

    // ✅ 회피 실행 (입력을 파라미터로 받음)
    public void PerformEvade(Vector2 moveInput)
    {
        if (!CanEvade()) return;

        // 🔹 1단계: 전달받은 입력으로 회피 방향 계산
        Vector3 initialDirection;

        if (moveInput.magnitude > 0.1f)
        {
            initialDirection = new Vector3(moveInput.x, 0, moveInput.y);
            Debug.Log($"[PerformEvade] 입력 방향 회피: {moveInput}");
        }
        else
        {
            initialDirection = transform.forward;
            Debug.Log($"[PerformEvade] 정면 방향 회피: {transform.forward}");
        }

        // 카메라 상대 좌표로 변환
        if (Camera.main != null)
        {
            Vector3 camForward = Camera.main.transform.forward;
            Vector3 camRight = Camera.main.transform.right;
            camForward.y = 0; camRight.y = 0;
            camForward.Normalize(); camRight.Normalize();

            initialDirection = (camForward * initialDirection.z + camRight * initialDirection.x).normalized;
        }

        // 🔹 2단계: 모든 기존 코루틴 강제 중단
        if (currentAttackCoroutine != null)
        {
            StopCoroutine(currentAttackCoroutine);
            currentAttackCoroutine = null;
            Debug.Log("[PlayerWeaponController] 공격 코루틴 강제 중단 (회피)");
        }

        if (currentKnockbackCoroutine != null)
        {
            StopCoroutine(currentKnockbackCoroutine);
            currentKnockbackCoroutine = null;
            Debug.Log("[PlayerWeaponController] 넉백/스턴 코루틴 강제 중단 (회피)");
        }

        if (currentEvadeCoroutine != null)
        {
            StopCoroutine(currentEvadeCoroutine);
            currentEvadeCoroutine = null;
            Debug.Log("[PlayerWeaponController] 기존 회피 코루틴 강제 중단");
        }

        // 🔹 3단계: 게이지 소비
        currentEvadeGauge -= evadeData.evadeCost;

        // 🔹 4단계: 회피 방식에 따라 다른 코루틴 실행 (핵심!)
        if (evadeData.allowDirectionChangeWhileEvading)
        {
            // 🎮 실시간 방향 변경 방식
            currentEvadeCoroutine = StartCoroutine(DynamicEvadeRoutine(initialDirection));
            Debug.Log("[PerformEvade] 🔄 실시간 방향 변경 회피 시작");
        }
        else
        {
            // 🎯 고정 방향 방식 (핵심!)
            currentEvadeCoroutine = StartCoroutine(FixedEvadeRoutine(initialDirection));
            Debug.Log("[PerformEvade] ➡️ 고정 방향 회피 시작");
        }
    }

    // ✅ 고정 방향 회피 (완전 수정 - 입력 완전 무시!)
    private IEnumerator FixedEvadeRoutine(Vector3 fixedDirection)
    {
        ChangeState(PlayerState.Evade);

        Debug.Log($"[고정 회피] 시작 - 방향:{fixedDirection}, 속도:{evadeData.evadeSpeed}");

        float elapsed = 0f;
        Vector3 evadeDir = fixedDirection.normalized;  // ⭐ 한 번 정하면 절대 안 바뀜!
        evadeDir.y = 0f;

        isInvincible = true;

        // ⭐ 핵심: 처음 정한 방향으로만 쭉 이동 (입력 완전 무시!)
        while (elapsed < evadeData.evadeDuration)
        {
            float t = elapsed / evadeData.evadeDuration;

            // AnimationCurve로 속도 감쇠
            float currentSpeedMultiplier = evadeData.speedCurve.Evaluate(t);
            float currentSpeed = evadeData.evadeSpeed * currentSpeedMultiplier;

            // 🎯 고정된 방향으로만 이동 (입력 절대 안 봄!)
            transform.position += evadeDir * currentSpeed * Time.deltaTime;

            // 무적 시간 체크
            if (elapsed >= evadeData.invincibilityDuration)
            {
                isInvincible = false;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        FinishEvade();
    }

    // ✅ 실시간 방향 변경 회피 (새로운 방식)
    private IEnumerator DynamicEvadeRoutine(Vector3 initialDirection)
    {
        ChangeState(PlayerState.Evade);

        Debug.Log($"[실시간 회피] 시작 - 초기 방향:{initialDirection}, 속도:{evadeData.evadeSpeed}");

        float elapsed = 0f;
        Vector3 currentDirection = initialDirection.normalized;
        currentDirection.y = 0f;

        isInvincible = true;

        // ⭐ 실시간으로 입력을 감지해서 방향 변경
        while (elapsed < evadeData.evadeDuration)
        {
            float t = elapsed / evadeData.evadeDuration;

            // 🔄 실시간 입력 감지 및 방향 업데이트
            Vector2 currentInput = InputManager.Instance.GetMoveInput();
            if (currentInput.magnitude >= evadeData.minInputMagnitude)
            {
                // 새로운 목표 방향 계산
                Vector3 newDirection = new Vector3(currentInput.x, 0, currentInput.y);

                // 카메라 상대 좌표로 변환
                if (Camera.main != null)
                {
                    Vector3 camForward = Camera.main.transform.forward;
                    Vector3 camRight = Camera.main.transform.right;
                    camForward.y = 0; camRight.y = 0;
                    camForward.Normalize(); camRight.Normalize();

                    newDirection = (camForward * newDirection.z + camRight * newDirection.x).normalized;
                    newDirection.y = 0f;
                }

                // 부드럽게 방향 전환 (Lerp 사용)
                float changeSpeed = evadeData.directionChangeSensitivity * Time.deltaTime;
                currentDirection = Vector3.Lerp(currentDirection, newDirection, changeSpeed).normalized;
            }

            // AnimationCurve로 속도 감쇠
            float currentSpeedMultiplier = evadeData.speedCurve.Evaluate(t);
            float currentSpeed = evadeData.evadeSpeed * currentSpeedMultiplier;

            // 이동 적용 (방향 실시간 변경)
            transform.position += currentDirection * currentSpeed * Time.deltaTime;

            // 무적 시간 체크
            if (elapsed >= evadeData.invincibilityDuration)
            {
                isInvincible = false;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        FinishEvade();
    }

    // ✅ 회피 종료 공통 처리
    private void FinishEvade()
    {
        isInvincible = false;
        Debug.Log("[PlayerWeaponController] 회피 완료");

        // 상태 복구 (이동 중이면 Move, 아니면 Idle)
        if (movement.GetVelocityMagnitude() > 0.1f)
        {
            ChangeState(PlayerState.Move);
        }
        else
        {
            ChangeState(PlayerState.Idle);
        }

        // 코루틴 추적 해제
        currentEvadeCoroutine = null;
    }

    private void HandleIdle()
    {
        if (state == PlayerState.Attack || state == PlayerState.Knockback || state == PlayerState.Stun || state == PlayerState.Evade)
            return;

        if (movement.GetVelocityMagnitude() > 0.1f)
        {
            ChangeState(PlayerState.Move);
            return;
        }

        if (InputManager.Instance.GetAttackInput())
            PlayAttack();
    }

    private void HandleMove()
    {
        if (state == PlayerState.Attack || state == PlayerState.Knockback || state == PlayerState.Stun || state == PlayerState.Evade)
            return;

        if (movement.GetVelocityMagnitude() <= 0.1f)
        {
            ChangeState(PlayerState.Idle);
            return;
        }

        if (InputManager.Instance.GetAttackInput())
            PlayAttack();
    }

    /// <summary>
    /// 🆕 상태 변경 시 애니메이션도 강제로 맞춤
    /// </summary>
    private void ChangeState(PlayerState newState)
    {
        if (state == newState) return;

        previousState = state;
        state = newState;

        // 애니메이션 강제 전환
        if (animationController != null)
        {
            animationController.ForceAnimationByState(newState);
        }

        if (debugMode)
        {
            Debug.Log($"[PlayerWeaponController] 상태 변경: {previousState} → {newState}");
        }
    }

    public void EquipWeapon(GameObject weaponPrefab)
    {
        if (currentWeapon != null)
            Destroy(currentWeapon);

        GameObject prefabToSpawn = weaponPrefab != null ? weaponPrefab : defaultWeaponPrefab;
        if (prefabToSpawn == null)
        {
            Debug.LogError("❌ 기본 무기 프리팹이 연결되지 않았습니다.");
            return;
        }

        currentWeapon = Instantiate(prefabToSpawn, weaponSocket);
        currentWeapon.transform.localPosition = Vector3.zero;
        currentWeapon.transform.localRotation = Quaternion.identity;

        weaponBehavior = currentWeapon.GetComponent<WeaponBehavior>();
        currentWeaponData = weaponBehavior != null ? weaponBehavior.data : null;

        if (animationController != null && currentWeaponData != null && currentWeaponData.overrideController != null)
        {
            animationController.GetAnimator().runtimeAnimatorController = currentWeaponData.overrideController;
        }

        Debug.Log($"무기 장착됨 → {currentWeaponData?.weaponName ?? "null"}");
    }

    public void PlayAttack()
    {
        if (currentWeaponData == null) return;
        if (state == PlayerState.Attack || state == PlayerState.Knockback || state == PlayerState.Stun || state == PlayerState.Evade) return;

        float delta = Time.time - lastAttackTime;
        if (delta < currentWeaponData.cooldown) return;

        lastAttackTime = Time.time;

        // 기존 공격 코루틴이 있으면 중단
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

        if (weaponBehavior != null)
            weaponBehavior.AttackHit();

        yield return new WaitForSeconds(currentWeaponData.cooldown);

        ChangeState(PlayerState.Idle);
        animationController?.EndAttack();

        currentAttackCoroutine = null;
    }

    /* ───────── 🆕 강제 넉백 (모든 코루틴 중단 후 새로 시작) ───────── */

    /// <summary>
    /// 기존 모든 액션(공격, 넉백, 스턴, 회피)을 강제로 중단하고 새로운 넉백을 적용
    /// </summary>
    public void ForceApplyKnockback(Vector3 dir, float power, float duration, float stun)
    {
        // 🔹 1단계: 모든 기존 코루틴 강제 중단 (회피 포함)
        if (currentAttackCoroutine != null)
        {
            StopCoroutine(currentAttackCoroutine);
            currentAttackCoroutine = null;
            Debug.Log("[PlayerWeaponController] 공격 코루틴 강제 중단");
        }

        if (currentKnockbackCoroutine != null)
        {
            StopCoroutine(currentKnockbackCoroutine);
            currentKnockbackCoroutine = null;
            Debug.Log("[PlayerWeaponController] 기존 넉백/스턴 코루틴 강제 중단");
        }

        if (currentEvadeCoroutine != null)
        {
            StopCoroutine(currentEvadeCoroutine);
            currentEvadeCoroutine = null;
            isInvincible = false; // 회피 중단 시 무적 해제
            Debug.Log("[PlayerWeaponController] 회피 코루틴 강제 중단");
        }

        // 🔹 2단계: 새로운 넉백 즉시 시작
        currentKnockbackCoroutine = StartCoroutine(KnockbackRoutine(dir, power, duration, stun));
    }

    /// <summary>
    /// 기존 넉백 메서드 (호환성 유지)
    /// </summary>
    public void ApplyKnockback(Vector3 dir, float power, float duration, float stun)
    {
        ForceApplyKnockback(dir, power, duration, stun);
    }

    private IEnumerator KnockbackRoutine(Vector3 dir, float power, float duration, float stun)
    {
        // 🔹 1단계: 넉백 상태 + 강제 애니메이션
        ChangeState(PlayerState.Knockback);

        Debug.Log($"[PlayerWeaponController] 넉백 시작 - Power:{power}, Duration:{duration}");

        // 체력 컴포넌트에서 weight 가져오기
        float resistance = 1f;
        if (TryGetComponent(out Health health))
            resistance = Mathf.Max(health.GetWeight(), 0.01f);

        float elapsed = 0f;
        Vector3 knockDir = dir.normalized;
        knockDir.y = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float currentSpeed = Mathf.Lerp(power / resistance, 0f, t);
            transform.position += knockDir * currentSpeed * Time.deltaTime;

            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.Log("[PlayerWeaponController] 넉백 완료");

        // 🔹 2단계: 스턴 처리 + 강제 애니메이션
        if (stun > 0f)
        {
            ChangeState(PlayerState.Stun);

            Debug.Log($"[PlayerWeaponController] 스턴 시작 ({stun:F2}초)");
            yield return new WaitForSeconds(stun);
            Debug.Log("[PlayerWeaponController] 스턴 완료");
        }

        // 🔹 3단계: 상태 복구 + 강제 애니메이션
        ChangeState(PlayerState.Idle);

        Debug.Log("[PlayerWeaponController] 정상 상태 복구");

        // 🔹 4단계: 코루틴 추적 해제
        currentKnockbackCoroutine = null;
    }

    private IEnumerator KnockbackThenStunRoutine(Vector3 hitDir, WeaponDataSO weapon, float impactScale)
    {
        float resistance = 1f;
        if (TryGetComponent(out Health health))
            resistance = Mathf.Max(health.GetWeight(), 0.01f);

        if (weapon.knockbackDuration > 0f && weapon.knockbackPower > 0f)
        {
            ChangeState(PlayerState.Knockback);

            float elapsed = 0f;
            Vector3 dir = hitDir.normalized;

            while (elapsed < weapon.knockbackDuration)
            {
                float t = elapsed / weapon.knockbackDuration;
                float currentPower = Mathf.Lerp((weapon.knockbackPower / resistance) * impactScale, 0f, t);
                transform.position += dir * currentPower * Time.deltaTime;

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        if (weapon.stunDuration > 0f)
        {
            ChangeState(PlayerState.Stun);
            yield return new WaitForSeconds(weapon.stunDuration);
        }

        ChangeState(PlayerState.Idle);
    }

    // ✅ 무적 상태 체크 (외부에서 사용)
    public bool IsInvincible()
    {
        return isInvincible;
    }

    // ✅ 회피 게이지 정보 (UI용)
    public float GetEvadeGauge() => currentEvadeGauge;
    public float GetMaxEvadeGauge() => evadeData?.maxGauge ?? 100f;
    public bool CanPerformEvade() => CanEvade();

    /* ───────── EnemyDetector 프록시 ───────── */
    public List<Transform> DetectEnemies()
    {
        if (enemyDetector == null)
            return new List<Transform>();

        return enemyDetector.GetEnemiesInRange(enemyDetector.viewDistance);
    }

    public WeaponDataSO GetCurrentWeaponData() => currentWeaponData;
    public PlayerState CurrentState => state;
}