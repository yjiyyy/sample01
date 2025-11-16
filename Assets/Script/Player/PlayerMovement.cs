using UnityEngine;
using System.Collections;

/*
 * PlayerMovement (NavMeshAgent 제거 버전)
 * - 고정 타임스텝(Time.fixedDeltaTime) 기반 이동: PC(60/30fps), 모바일 동일 이동 거리
 * - Root Motion 사용 안 함: transform 직접 이동/회전
 * - 이동 속도/AR 감속을 NavMeshAgent 대신 baseMoveSpeed 필드로 일원화
 * - 회전 부드럽게: RotateTowards (rotationSpeedDegPerSec)
 * - Evade 상태 회전은 PlayerEvadeController가 담당 (여기서는 건너뜀)
 */

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    private const float BACKSTEP_ENTER_ANGLE = 120f;
    private const float BACKSTEP_EXIT_ANGLE = 100f;
    private const float EPS = 0.0001f;

    [Header("이동 옵션")]
    [Tooltip("기본 이동 속도 (m/s)")]
    [SerializeField] private float baseMoveSpeed = 10f;

    [Tooltip("입력이 없을 때 멈춤 여부 (현재 로직에서는 참조 최소)")]
    public bool stopWhenNoInput = true;

    [Header("회전 옵션")]
    [Tooltip("일반 이동 시 초당 회전 가능한 최대 각(deg)")]
    [SerializeField] public float rotationSpeedDegPerSec = 720f;

    [Header("디버그")]
    [Tooltip("넉백/회피/주요 이벤트 로그")]
    public bool debugLogs = false;

    private Rigidbody rb;
    private Camera mainCam;
    private PlayerWeaponController weaponCtrl;
    private PlayerAnimationController anim;

    private bool isKnockbacked = false;
    private Coroutine knockbackRoutine;

    private Vector3 lastInput = Vector3.zero;
    private bool backStepActive = false;

    private Vector3 _lastLookDirection;

    // 현재 프레임 계산된 이동 속도 캐시 (AR 감속 반영)
    private float currentMoveSpeed = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        mainCam = Camera.main;
        weaponCtrl = GetComponent<PlayerWeaponController>();
        anim = GetComponent<PlayerAnimationController>();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        }

        _lastLookDirection = transform.forward;

        if (debugLogs) Debug.Log("[PM] Initialized (NavMeshAgent removed)");
    }

    void Update()
    {
        if (isKnockbacked) return;

        Vector2 raw = InputManager.Instance.GetMoveInput();
        lastInput = new Vector3(raw.x, 0f, raw.y); // 평면 입력
    }

    void FixedUpdate()
    {
        if (isKnockbacked) return;

        bool hasInput = lastInput.sqrMagnitude > EPS;

        // 상태 플래그
        bool isARFiring = weaponCtrl != null && weaponCtrl.IsARFiring;
        bool arAllowMove = weaponCtrl != null && weaponCtrl.ARAllowMoveWhileFiring;
        bool arRotationLocked = weaponCtrl != null && weaponCtrl.ARIsRotationLocked;
        PlayerState currentState = weaponCtrl != null ? weaponCtrl.CurrentState : PlayerState.Idle;

        // 이동 차단 여부
        bool movementBlocked = false;
        if (weaponCtrl != null)
        {
            bool attackBlocking = weaponCtrl.CurrentState == PlayerState.Attack && !(isARFiring && arAllowMove);
            if (attackBlocking ||
                currentState == PlayerState.Knockback ||
                currentState == PlayerState.Stun ||
                currentState == PlayerState.Dead ||
                currentState == PlayerState.Evade)
            {
                movementBlocked = true;
            }
        }

        // 현재 속도 계산 (AR 감속 반영)
        currentMoveSpeed = ComputeCurrentMoveSpeed(isARFiring, arAllowMove);

        // 이동 벡터 계산
        Vector3 moveDisplacement = Vector3.zero;
        if (hasInput && !movementBlocked)
        {
            Vector3 moveDir = CameraRelative(lastInput);
            if (isARFiring && !arAllowMove)
                moveDir = Vector3.zero;

            float inputMag = Mathf.Clamp01(lastInput.magnitude);
            moveDisplacement = moveDir * currentMoveSpeed * inputMag * Time.fixedDeltaTime;

            // 회전용 목표 방향 갱신 (Evade 상태는 EvadeController가 처리)
            if (currentState != PlayerState.Evade && moveDisplacement.sqrMagnitude > EPS)
            {
                _lastLookDirection = moveDisplacement.normalized;
            }
        }

        // 실제 이동 적용
        transform.position += moveDisplacement;

        // 회전 처리 (Evade 상태 제외)
        if (currentState != PlayerState.Evade)
        {
            Vector3 desiredDir = _lastLookDirection;

            if (arRotationLocked && isARFiring && weaponCtrl != null)
            {
                Vector3 lockedF = weaponCtrl.ARLockedForward;
                if (lockedF.sqrMagnitude > EPS)
                    desiredDir = lockedF.normalized;
            }

            if (desiredDir.sqrMagnitude > EPS)
            {
                Quaternion targetRot = Quaternion.LookRotation(desiredDir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRot,
                    rotationSpeedDegPerSec * Time.fixedDeltaTime
                );
            }
        }

        // BackStep 판정
        HandleBackStep(hasInput, movementBlocked, moveDisplacement);
    }

    private void HandleBackStep(bool hasInput, bool movementBlocked, Vector3 moveDisplacement)
    {
        Vector3 currentMoveDir = moveDisplacement.sqrMagnitude > EPS ? moveDisplacement.normalized : Vector3.zero;
        if (hasInput && !movementBlocked && currentMoveDir.sqrMagnitude > EPS)
        {
            float absAngle = Vector3.Angle(transform.forward, currentMoveDir);
            if (!backStepActive && absAngle >= BACKSTEP_ENTER_ANGLE)
            {
                backStepActive = true;
                anim?.SetBackStep(true);
            }
            else if (backStepActive && absAngle <= BACKSTEP_EXIT_ANGLE)
            {
                backStepActive = false;
                anim?.SetBackStep(false);
            }
        }
        else
        {
            if (backStepActive)
            {
                backStepActive = false;
                anim?.SetBackStep(false);
            }
        }
    }

    void LateUpdate()
    {
        // 하체 애니메이션 재생 속도 (AR 사격 중 감속)
        if (anim != null && weaponCtrl != null)
        {
            bool isARFiring = weaponCtrl.IsARFiring;
            bool arAllowMove = weaponCtrl.ARAllowMoveWhileFiring;
            float lowerSpeed = 1f;

            if (isARFiring && arAllowMove)
            {
                var arData = weaponCtrl.GetCurrentWeaponData() as WeaponDataSO_AR;
                if (arData != null)
                    lowerSpeed = Mathf.Max(0f, arData.animPlaybackSpeedWhileFiring);
            }
            anim.SetLowerBodyPlaybackSpeed(lowerSpeed);
        }
    }

    private float ComputeCurrentMoveSpeed(bool isARFiring, bool arAllowMove)
    {
        float speed = baseMoveSpeed;
        if (isARFiring && arAllowMove && weaponCtrl != null)
        {
            var arData = weaponCtrl.GetCurrentWeaponData() as WeaponDataSO_AR;
            if (arData != null)
                speed *= Mathf.Max(0f, arData.moveSpeedWhileFiring); // 비율(0..1)
        }
        return speed;
    }

    // ───────── Knockback ─────────
    public void ApplyKnockback(Vector3 dir, float force, float duration, Transform attacker = null)
    {
        if (knockbackRoutine != null) StopCoroutine(knockbackRoutine);
        if (debugLogs) Debug.Log($"[PM KNOCK] start dir={dir}, force={force}, dur={duration}");
        knockbackRoutine = StartCoroutine(KnockbackRoutine(dir, force, duration, attacker));
    }

    private IEnumerator KnockbackRoutine(Vector3 dir, float force, float duration, Transform attacker)
    {
        isKnockbacked = true;
        Vector3 knockDir = dir.normalized; knockDir.y = 0f;
        float elapsed = 0f;

        if (attacker != null)
        {
            Vector3 lookDir = attacker.position - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > EPS)
                _lastLookDirection = lookDir.normalized;
        }

        while (elapsed < duration)
        {
            float t = 1f - Mathf.Clamp01(elapsed / Mathf.Max(duration, EPS));
            float currentSpeed = force * t;
            Vector3 disp = knockDir * currentSpeed * Time.fixedDeltaTime;
            transform.position += disp;

            if (_lastLookDirection.sqrMagnitude > EPS)
            {
                Quaternion target = Quaternion.LookRotation(_lastLookDirection, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    target,
                    rotationSpeedDegPerSec * Time.fixedDeltaTime
                );
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        isKnockbacked = false;
        knockbackRoutine = null;
        if (debugLogs) Debug.Log("[PM KNOCK] finished");
    }

    public void CancelKnockback()
    {
        if (knockbackRoutine != null) StopCoroutine(knockbackRoutine);
        isKnockbacked = false;
        if (debugLogs) Debug.Log("[PM KNOCK] cancelled");
    }

    public bool IsCurrentlyKnockbacked() => isKnockbacked;

    // 애니메이터 속도 추정 (0~1: 입력 비율 * AR 감속 비율)
    public float GetAnimatorSpeedEstimate()
    {
        float inputMag = Mathf.Clamp01(lastInput.magnitude);
        if (baseMoveSpeed <= EPS) return inputMag > EPS ? 1f : 0f;
        return Mathf.Clamp01(inputMag * (currentMoveSpeed / baseMoveSpeed));
    }

    // 실제 이동 속도(m/s 추정)
    public float GetVelocityMagnitude()
    {
        float inputMag = Mathf.Clamp01(lastInput.magnitude);
        // 이동 차단 시 0
        bool isARFiring = weaponCtrl != null && weaponCtrl.IsARFiring;
        bool arAllowMove = weaponCtrl != null && weaponCtrl.ARAllowMoveWhileFiring;
        PlayerState state = weaponCtrl != null ? weaponCtrl.CurrentState : PlayerState.Idle;
        bool attackBlocking = weaponCtrl != null && weaponCtrl.CurrentState == PlayerState.Attack && !(isARFiring && arAllowMove);

        if (attackBlocking ||
            state == PlayerState.Knockback ||
            state == PlayerState.Stun ||
            state == PlayerState.Dead ||
            state == PlayerState.Evade)
            return 0f;

        return inputMag * currentMoveSpeed;
    }

    public Vector3 CameraRelative(Vector3 input)
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return new Vector3(input.x, 0f, input.z);

        Vector3 camF = mainCam.transform.forward;
        Vector3 camR = mainCam.transform.right;
        camF.y = 0f; camR.y = 0f;
        camF.Normalize(); camR.Normalize();
        return camF * input.z + camR * input.x;
    }
}