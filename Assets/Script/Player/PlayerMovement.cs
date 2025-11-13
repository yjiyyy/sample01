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
        // ─────────────────────────────────────────────────────────
        // Update에서는 입력 수집만 담당 (반응성 유지)
        // ─────────────────────────────────────────────────────────
        if (isKnockbacked) return;

        Vector2 raw = InputManager.Instance.GetMoveInput();
        lastInput = new Vector3(raw.x, 0f, raw.y);
    }

    void FixedUpdate()
    {
        // ─────────────────────────────────────────────────────────
        // FixedUpdate에서 모든 물리 기반 처리 (일관된 타임스텝)
        // ─────────────────────────────────────────────────────────
        if (isKnockbacked) return;

        bool hasInput = lastInput.sqrMagnitude > EPS;

        // --- 상태 확인 ---
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
            if (isAttackBlocking || weaponCtrl.CurrentState == PlayerState.Knockback ||
                weaponCtrl.CurrentState == PlayerState.Stun || weaponCtrl.CurrentState == PlayerState.Dead ||
                weaponCtrl.CurrentState == PlayerState.Evade)
            {
                movementBlocked = true;
            }
        }

        // --- 이동 처리 (고정 타임스텝) ---
        Vector3 moveDisplacement = Vector3.zero;
        if (hasInput && !movementBlocked)
        {
            Vector3 moveDir = CameraRelative(lastInput);
            if (isARFiring && !arAllowMove)
            {
                moveDir = Vector3.zero;
            }

            float inputMag = Mathf.Clamp01(lastInput.magnitude);
            // 고정 타임스텝 사용으로 PC/모바일 일관성 보장
            moveDisplacement = moveDir * agent.speed * inputMag * Time.fixedDeltaTime;
        }

        // 이동 적용
        transform.position += moveDisplacement;

        // --- 회전 처리 (이동과 동기화) ---
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

        // --- BackStep 애니메이션 처리 (이동과 동기화) ---
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
    }

    void LateUpdate()
    {
        // ─────────────────────────────────────────────────────────
        // LateUpdate에서 동기화 및 애니메이션 파라미터 업데이트
        // ─────────────────────────────────────────────────────────

        // ✅ 회피/넉백 중에는 동기화 건너뛰기 (이동이 되돌려지는 것 방지)
        bool skipSync = false;
        if (weaponCtrl != null)
        {
            if (weaponCtrl.CurrentState == PlayerState.Evade ||
                weaponCtrl.CurrentState == PlayerState.Knockback)
            {
                skipSync = true;

                // ✅ 디버그 로그
                if (debugLogs)
                {
                    Debug.Log($"[PM LateUpdate] Skipping NavMesh sync during {weaponCtrl.CurrentState}");
                }
            }
        }

        // ✅ 디버그: NavMeshAgent 상태 확인
        if (debugLogs && agent != null)
        {
            Debug.Log($"[PM LateUpdate] State: {weaponCtrl?.CurrentState}, " +
                     $"skipSync: {skipSync}, " +
                     $"agent.velocity: {agent.velocity}, " +
                     $"agent.hasPath: {agent.hasPath}, " +
                     $"lastInput: {lastInput}, " +
                     $"transform.position: {transform.position}");
        }

        if (!skipSync)
        {
            // NavMeshAgent 동기화
            if (agent != null)
            {
                agent.nextPosition = transform.position;
            }

            // Rigidbody 동기화
            if (rb != null && rb.isKinematic)
            {
                rb.position = transform.position;
            }
        }

        // 애니메이션 하체 속도 설정
        if (anim != null && weaponCtrl != null)
        {
            bool isARFiring = weaponCtrl.IsARFiring;
            bool arAllowMove = weaponCtrl.ARAllowMoveWhileFiring;

            float lowerSpeed = 1f;
            if (isARFiring && arAllowMove)
            {
                var arData = weaponCtrl.GetCurrentWeaponData() as WeaponDataSO_AR;
                if (arData != null) lowerSpeed = Mathf.Max(0f, arData.animPlaybackSpeedWhileFiring);
            }
            anim.SetLowerBodyPlaybackSpeed(lowerSpeed);
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

            // 고정 타임스텝으로 일관된 넉백 거리 보장
            Vector3 knockDisplacement = knockDir * currentSpeed * Time.fixedDeltaTime;
            transform.position += knockDisplacement;

            if (_lastLookDirection.sqrMagnitude > EPS)
            {
                transform.rotation = Quaternion.LookRotation(_lastLookDirection, Vector3.up);
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
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
            float maxSpeed = Mathf.Max(agent.speed, EPS);
            return Mathf.Clamp01(lastInput.magnitude * agent.speed / maxSpeed);
        }
        return lastInput.magnitude > EPS ? 1f : 0f;
    }

    public float GetVelocityMagnitude()
    {
        if (agent != null) return lastInput.magnitude * agent.speed;
        return lastInput.magnitude * FALLBACK_MOVE_SPEED;
    }

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