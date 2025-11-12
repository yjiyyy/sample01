using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    private const float BACKSTEP_ENTER_ANGLE = 120f;
    private const float BACKSTEP_EXIT_ANGLE = 100f;
    private const float FALLBACK_MOVE_SPEED = 5f;
    private const float EPS = 0.0001f;

    [Header("옵션")]
    public bool stopWhenNoInput = true;
    [Tooltip("디버그 로그 최소화: 넉백/회피 같은 주요 이벤트만 남김")]
    public bool debugLogs = false;

    private NavMeshAgent agent;
    private Rigidbody rb;
    private Camera mainCam;
    private PlayerWeaponController weaponCtrl;
    private PlayerAnimationController anim;

    private float agentDefaultSpeed;
    private float agentDefaultAngularSpeed;
    private float agentDefaultAcceleration;

    private bool isKnockbacked = false;
    private Coroutine knockbackRoutine;

    private Vector3 lastInput = Vector3.zero;
    private bool backStepActive = false;

    private Vector3 _lastLookDirection;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        mainCam = Camera.main;
        weaponCtrl = GetComponent<PlayerWeaponController>();
        anim = GetComponent<PlayerAnimationController>();

        if (agent != null)
        {
            // [핵심 수정 1] NavMeshAgent가 자동으로 위치를 업데이트하지 않도록 설정
            // 실제 이동은 이 스크립트가 100% 제어합니다.
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.updateUpAxis = true;

            agentDefaultSpeed = agent.speed;
            agentDefaultAngularSpeed = agent.angularSpeed;
            agentDefaultAcceleration = agent.acceleration;
        }
        else
        {
            Debug.LogError("[PM] NavMeshAgent is missing!");
        }

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        }

        _lastLookDirection = transform.forward;

        if (debugLogs) Debug.Log("[PM] Initialized");
    }

    void Update()
    {
        if (isKnockbacked) return;

        // --- 입력 및 상태 확인 (기존과 동일) ---
        Vector2 raw = InputManager.Instance.GetMoveInput();
        lastInput = new Vector3(raw.x, 0f, raw.y);
        bool hasInput = lastInput.sqrMagnitude > EPS;

        bool isARFiring = weaponCtrl != null && weaponCtrl.IsARFiring;
        bool arAllowMove = weaponCtrl != null && weaponCtrl.ARAllowMoveWhileFiring;
        bool arRotationLocked = weaponCtrl != null && weaponCtrl.ARIsRotationLocked;

        if (agent != null)
        {
            agent.speed = agentDefaultSpeed;
            if (isARFiring && arAllowMove && weaponCtrl != null)
            {
                var arData = weaponCtrl.GetCurrentWeaponData() as WeaponDataSO_AR;
                if (arData != null) agent.speed = agentDefaultSpeed * Mathf.Max(0f, arData.moveSpeedWhileFiring);
            }
        }

        bool movementBlocked = false;
        if (weaponCtrl != null)
        {
            bool isAttackBlocking = weaponCtrl.CurrentState == PlayerState.Attack && !(isARFiring && arAllowMove);
            if (isAttackBlocking || weaponCtrl.CurrentState == PlayerState.Knockback || weaponCtrl.CurrentState == PlayerState.Stun || weaponCtrl.CurrentState == PlayerState.Dead || weaponCtrl.CurrentState == PlayerState.Evade)
            {
                movementBlocked = true;
            }
        }

        // --- 이동 처리 ---
        Vector3 moveDisplacement = Vector3.zero;
        if (hasInput && !movementBlocked)
        {
            Vector3 moveDir = CameraRelative(lastInput); // InputManager에서 이미 정규화됨
            if (isARFiring && !arAllowMove)
            {
                moveDir = Vector3.zero;
            }

            float inputMag = Mathf.Clamp01(lastInput.magnitude);
            // [핵심 수정 2] 이번 프레임에 이동할 '거리(Displacement)' 계산
            moveDisplacement = moveDir * agent.speed * inputMag * Time.deltaTime;
        }

        // [핵심 수정 3] 계산된 거리만큼 transform.position에 직접 적용
        transform.position += moveDisplacement;

        // --- 회전 처리 (기존과 유사) ---
        if (arRotationLocked && isARFiring && weaponCtrl != null)
        {
            Vector3 lockedF = weaponCtrl.ARLockedForward;
            if (lockedF.sqrMagnitude > EPS) _lastLookDirection = lockedF.normalized;
        }
        else if (hasInput && moveDisplacement.sqrMagnitude > EPS)
        {
            _lastLookDirection = moveDisplacement.normalized;
        }

        if (_lastLookDirection.sqrMagnitude > EPS)
        {
            transform.rotation = Quaternion.LookRotation(_lastLookDirection, Vector3.up);
        }

        // --- 애니메이션 처리 (기존과 동일) ---
        Vector3 currentMoveDir = moveDisplacement.normalized;
        float absAngle = Vector3.Angle(transform.forward, currentMoveDir);

        if (hasInput && !movementBlocked)
        {
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

        if (anim != null)
        {
            float lowerSpeed = 1f;
            if (isARFiring && arAllowMove)
            {
                var arData = weaponCtrl.GetCurrentWeaponData() as WeaponDataSO_AR;
                if (arData != null) lowerSpeed = Mathf.Max(0f, arData.animPlaybackSpeedWhileFiring);
            }
            anim.SetLowerBodyPlaybackSpeed(lowerSpeed);
        }
    }

    void LateUpdate()
    {
        // [핵심 수정 4] 실제 이동한 위치를 NavMeshAgent에게 알려주어 동기화
        if (agent != null)
        {
            agent.nextPosition = transform.position;
        }

        if (rb != null && rb.isKinematic)
        {
            rb.position = transform.position;
        }
    }

    public void ApplyKnockback(Vector3 dir, float force, float duration, Transform attacker = null)
    {
        if (knockbackRoutine != null) StopCoroutine(knockbackRoutine);
        if (debugLogs) Debug.Log($"[PM KNOCK] ApplyKnockback start dir={dir}, force={force}, duration={duration}");
        knockbackRoutine = StartCoroutine(KnockbackRoutine(dir, force, duration, attacker));
    }

    private IEnumerator KnockbackRoutine(Vector3 dir, float force, float duration, Transform attacker)
    {
        isKnockbacked = true;
        Vector3 knockDir = dir.normalized;
        knockDir.y = 0f;
        float elapsed = 0f;

        if (attacker != null)
        {
            Vector3 lookDir = (attacker.position - transform.position);
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.0001f) _lastLookDirection = lookDir.normalized;
        }

        while (elapsed < duration)
        {
            float t = 1f - Mathf.Clamp01(elapsed / Mathf.Max(duration, EPS));
            float currentSpeed = force * t;
            // [핵심 수정 5] 넉백 이동량 계산
            Vector3 knockDisplacement = knockDir * currentSpeed * Time.deltaTime;

            // [핵심 수정 6] 넉백 이동량을 transform.position에 직접 적용
            transform.position += knockDisplacement;

            if (_lastLookDirection.sqrMagnitude > EPS)
            {
                transform.rotation = Quaternion.LookRotation(_lastLookDirection, Vector3.up);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        isKnockbacked = false;
        knockbackRoutine = null;
        if (debugLogs) Debug.Log("[PM KNOCK] Knockback finished");
    }

    public void CancelKnockback()
    {
        if (knockbackRoutine != null) StopCoroutine(knockbackRoutine);
        isKnockbacked = false;
        if (debugLogs) Debug.Log("[PM KNOCK] Knockback cancelled");
    }

    public bool IsCurrentlyKnockbacked() => isKnockbacked;

    public float GetAnimatorSpeedEstimate()
    {
        if (agent != null)
        {
            // 직접 이동하므로, agent.velocity 대신 입력 크기를 사용
            float maxSpeed = Mathf.Max(agent.speed, EPS);
            return Mathf.Clamp01(lastInput.magnitude * agent.speed / maxSpeed);
        }
        return lastInput.magnitude > EPS ? 1f : 0f;
    }

    public float GetVelocityMagnitude()
    {
        // 직접 이동하므로, agent.velocity 대신 입력 크기를 사용
        if (agent != null) return lastInput.magnitude * agent.speed;
        return lastInput.magnitude * FALLBACK_MOVE_SPEED;
    }

    // InputManager에서 이미 정규화된 값을 사용하므로 CameraRelative는 그대로 둠
    public Vector3 CameraRelative(Vector3 input)
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return new Vector3(input.x, 0f, input.z);
        Vector3 camF = mainCam.transform.forward;
        Vector3 camR = mainCam.transform.right;
        camF.y = 0f; camR.y = 0f;
        camF.Normalize(); camR.Normalize();
        return (camF * input.z + camR * input.x);
    }
}