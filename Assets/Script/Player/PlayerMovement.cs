using UnityEngine;
using UnityEngine.AI;
using System;
using System.Collections;
using System.Reflection;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("이동 속도 조절")]
    public float moveSpeed = 5f;
    public float acceleration = 100f;
    public float angularSpeed = 720f;
    public float stoppingDistance = 0.01f;
    public bool autoBraking = true;

    [Header("컨트롤 옵션")]
    public bool stopWhenNoInput = true;

    [Header("디버그")]
    public bool debugLogs = false;

    [Header("Visual sync (navmesh -> transform)")]
    [Tooltip("Agent의 nextPosition을 LateUpdate에서 직접 적용하여 update 타이밍 문제를 완화합니다.")]
    public bool visualSyncEnabled = true;
    [Tooltip("Visual smoothing 사용 여부: true면 transform 위치를 Lerp로 부드럽게 보간합니다.")]
    public bool visualSmoothingEnabled = true;
    [Tooltip("Visual smoothing speed (클수록 빠르게 따라감)")]
    public float visualSmoothingSpeed = 10f;

    private NavMeshAgent agent;
    private Rigidbody rb;
    private Camera mainCam;

    private bool isKnockbacked = false;
    private Vector3 knockbackDirection;
    private float knockbackSpeed;
    private float knockbackDuration;
    private float knockbackTimer;

    private Vector3 lastInput = Vector3.zero;
    private Vector3 lastPosition;

    // --- AR BackStep 관련 ---
    private bool arBackStepActive = false;
    [Header("AR BackStep Settings")]
    [Tooltip("각도가 이 값(도) 이상이면 BackStep으로 진입")]
    [SerializeField] private float enterBackstepAngle = 120f;
    [Tooltip("각도가 이 값(도) 이하이면 BackStep에서 복귀")]
    [SerializeField] private float exitBackstepAngle = 100f;

    private PlayerAnimationController anim; // Animator helper
    // -------------------------

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        mainCam = Camera.main;

        // NavMeshAgent 설정 (명시적)
        agent.updateRotation = false;
        agent.updateUpAxis = true;
        // 변경: 자동으로 transform을 적용하지 않음. LateUpdate에서 통일적으로 적용.
        agent.updatePosition = false;

        agent.speed = moveSpeed;
        agent.acceleration = acceleration;
        agent.angularSpeed = angularSpeed;
        agent.stoppingDistance = stoppingDistance;
        agent.autoBraking = autoBraking;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

        // Rigidbody: NavMeshAgent가 transform을 제어하므로 기본적으로 kinematic으로 둡니다.
        // (넉백 등 물리적 힘을 줄 필요가 있으면 해당 구간에서 일시적으로 false로 토글)
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.mass = 1f;
        rb.linearDamping = 5f;
        rb.angularDamping = 5f;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

        GameManager.Instance.playerTransform = this.transform;
        lastPosition = transform.position;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            Vector3 fixedPos = transform.position;
            fixedPos.y = hit.position.y + 0.05f;
            transform.position = fixedPos;
            rb.position = fixedPos;
            // Since we disabled updatePosition, ensure agent nextPosition aligns initially
            agent.nextPosition = fixedPos;
        }

        // PlayerAnimationController 참조 (존재하면 사용)
        anim = GetComponent<PlayerAnimationController>();

        if (debugLogs) Debug.Log("[PlayerMovement] Start() completed - agent.updatePosition set to false; visualSyncEnabled=" + visualSyncEnabled);
    }

    void Update()
    {
        if (isKnockbacked)
        {
            knockbackTimer += Time.deltaTime;
            float t = Mathf.Clamp01(knockbackTimer / knockbackDuration);
            float currentSpeed = knockbackSpeed * (1f - t);
            transform.position += knockbackDirection * currentSpeed * Time.deltaTime;

            if (knockbackTimer >= knockbackDuration)
            {
                isKnockbacked = false;
                Debug.Log("[PlayerMovement] 넉백 종료");
            }
            return;
        }

        var weaponCtrl = GetComponent<PlayerWeaponController>();

        // ── AR 연사 중 이동 속도 보정: AR이고 ARAllowMoveWhileFiring이면 SO의 moveSpeedWhileFiring 사용 (A)
        float a_mul = 1f; // A
        float b_mul = 1f; // B (하체 애니 재생속도)
        WeaponDataSO_AR arData = null;
        if (weaponCtrl != null && weaponCtrl.IsARFiring && weaponCtrl.ARAllowMoveWhileFiring)
        {
            arData = weaponCtrl.GetCurrentWeaponData() as WeaponDataSO_AR;
            if (arData != null)
            {
                // A: moveSpeedWhileFiring (기존 필드)
                a_mul = Mathf.Max(0f, arData.moveSpeedWhileFiring);
                // B: animPlaybackSpeedWhileFiring (새 필드)
                b_mul = Mathf.Max(0f, arData.animPlaybackSpeedWhileFiring);
            }
        }

        // 최종 이동 속도: baseMoveSpeed * (A * B)
        float finalSpeedMul = a_mul * b_mul;
        if (agent != null)
        {
            agent.speed = moveSpeed * finalSpeedMul;
        }
        // ─────────────────────────────────────────────────────────────

        if (weaponCtrl != null)
        {
            // Attack 상태 예외: AR 연사 + 이동 허용이면 이동 가능
            bool isAttackBlocking = weaponCtrl.CurrentState == PlayerState.Attack &&
                                    !(weaponCtrl.IsARFiring && weaponCtrl.ARAllowMoveWhileFiring);

            if (isAttackBlocking ||
                weaponCtrl.CurrentState == PlayerState.Knockback ||
                weaponCtrl.CurrentState == PlayerState.Stun ||
                weaponCtrl.CurrentState == PlayerState.Dead ||
                weaponCtrl.CurrentState == PlayerState.Evade)
            {
                // AR 전용 BackStep은 끄기
                if (arBackStepActive)
                {
                    arBackStepActive = false;
                    if (anim != null) anim.SetBackStep(false);
                }

                // CC 진입 시 하체 속도 리셋
                if (anim != null) anim.SetLowerBodyPlaybackSpeed(1f);

                if (CanUseAgent())
                {
                    agent.ResetPath();
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                }
                return;
            }
        }

        Vector2 moveInput = InputManager.Instance.GetMoveInput();
        lastInput = new Vector3(moveInput.x, 0, moveInput.y);

        if (lastInput.magnitude > 0.1f)
        {
            if (CanUseAgent())
            {
                agent.isStopped = false;

                Vector3 moveDir = CameraRelative(lastInput);
                if (moveDir.sqrMagnitude > 0.0001f) moveDir.Normalize();
                else moveDir = Vector3.zero;

                Vector3 displacement = moveDir * agent.speed * Time.deltaTime;

                if (agent.isOnNavMesh)
                {
                    // 수동 이동 모드 진입 시 기존 경로 제거
                    if (agent.hasPath)
                        agent.ResetPath();

                    agent.Move(displacement);
                }
                else
                {
                    transform.position += displacement;
                }

                // --- AR BackStep 판정 로직 ---
                bool considerARBack = false;
                if (weaponCtrl != null && weaponCtrl.IsARFiring && weaponCtrl.ARAllowMoveWhileFiring)
                    considerARBack = true;

                if (considerARBack)
                {
                    Vector3 facing;
                    if (weaponCtrl != null && weaponCtrl.IsARFiring && weaponCtrl.ARIsRotationLocked)
                        facing = weaponCtrl.ARLockedForward;
                    else
                        facing = transform.forward;

                    facing.y = 0f; moveDir.y = 0f;
                    if (facing.sqrMagnitude < 0.0001f) facing = Vector3.forward;
                    if (moveDir.sqrMagnitude < 0.0001f) moveDir = Vector3.forward;
                    facing.Normalize(); moveDir.Normalize();

                    float signed = Vector3.SignedAngle(facing, moveDir, Vector3.up);
                    float absAngle = Mathf.Abs(signed);

                    if (!arBackStepActive && absAngle >= enterBackstepAngle)
                    {
                        arBackStepActive = true;
                        if (anim != null) anim.SetBackStep(true);
                    }
                    else if (arBackStepActive && absAngle <= exitBackstepAngle)
                    {
                        arBackStepActive = false;
                        if (anim != null) anim.SetBackStep(false);
                    }
                }
                else
                {
                    if (arBackStepActive)
                    {
                        arBackStepActive = false;
                        if (anim != null) anim.SetBackStep(false);
                    }
                }
                // --- /AR BackStep 판정 끝 ---

                if (weaponCtrl != null && weaponCtrl.IsARFiring && weaponCtrl.ARAllowMoveWhileFiring && anim != null)
                {
                    float lowerSpeed = 1f;
                    if (arData != null)
                        lowerSpeed = Mathf.Max(0f, arData.animPlaybackSpeedWhileFiring);
                    anim.SetLowerBodyPlaybackSpeed(lowerSpeed);
                }
                else
                {
                    if (anim != null) anim.SetLowerBodyPlaybackSpeed(1f);
                }

                bool lockRot = weaponCtrl != null && weaponCtrl.IsARFiring && weaponCtrl.ARIsRotationLocked;
                if (!lockRot && moveDir.sqrMagnitude > 0.000001f)
                {
                    Quaternion rot = Quaternion.LookRotation(moveDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 20f);
                }
            }
        }
        else if (stopWhenNoInput)
        {
            if (CanUseAgent())
            {
                agent.ResetPath();
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }

            if (arBackStepActive)
            {
                arBackStepActive = false;
                if (anim != null) anim.SetBackStep(false);
            }

            if (anim != null) anim.SetLowerBodyPlaybackSpeed(1f);
        }

        if (InputManager.Instance.GetDamageTestInput())
        {
            if (TryGetComponent(out PlayerHealth health))
            {
                health.ApplyDamage(10f);
                Debug.Log("[테스트] 플레이어에게 10 데미지 적용 (-키)");
            }
        }

        if (InputManager.Instance.GetHealTestInput())
        {
            if (TryGetComponent(out PlayerHealth health))
            {
                health.Heal(20f);
                Debug.Log("[테스트] 플레이어 체력 20 회복 (=키)");
            }
        }
    }

    void LateUpdate()
    {
        // 1) transform 위치 동기화 (agent.nextPosition 적용)
        if (visualSyncEnabled && agent != null && agent.isOnNavMesh && agent.enabled && !isKnockbacked)
        {
            Vector3 targetPos = agent.nextPosition;

            if (visualSmoothingEnabled)
            {
                float t = Mathf.Clamp01(visualSmoothingSpeed * Time.deltaTime);
                transform.position = Vector3.Lerp(transform.position, targetPos, t);
            }
            else
            {
                transform.position = targetPos;
            }
        }

        bool shouldStop = !isKnockbacked && lastInput.magnitude < 0.01f;

        if (shouldStop)
        {
            if (agent != null && agent.isOnNavMesh)
            {
                // agent.enabled를 끄지 않고 isStopped로 멈춤 처리
                agent.isStopped = true;
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }
            transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y, 0f);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (debugLogs && Time.frameCount % 10 == 0) // 10프레임당 한번만 출력
        {
            if (agent != null)
            {
                Debug.Log($"[PM Debug] frame={Time.frameCount} pos={transform.position.ToString("F3")} nextPos={agent.nextPosition.ToString("F3")} vel={agent.velocity.magnitude:F3} lastInput={lastInput.magnitude:F3}");
            }
        }

        lastPosition = transform.position;
    }

    public void ApplyKnockback(Vector3 direction, float force, float duration, Transform attacker = null)
    {
        isKnockbacked = true;
        knockbackDirection = direction.normalized;

        float finalForce = force;
        if (TryGetComponent(out PlayerHealth health))
            finalForce /= Mathf.Max(0.01f, health.GetWeight());

        knockbackSpeed = finalForce;
        knockbackDuration = duration;
        knockbackTimer = 0f;

        if (CanUseAgent())
        {
            agent.ResetPath();
            agent.isStopped = true;
        }

        if (attacker != null)
        {
            Vector3 lookDir = (attacker.position - transform.position).normalized;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(lookDir);
        }
    }

    // ✅ 넉백 즉시 해제(Evade 우선 적용용)
    public void CancelKnockback()
    {
        if (!isKnockbacked) return;
        isKnockbacked = false;
        knockbackTimer = knockbackDuration;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.velocity = Vector3.zero;
            agent.isStopped = true;   // Evade/Attack 등에서 어차피 정지됨
            agent.ResetPath();
        }
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Debug.Log("[PlayerMovement] CancelKnockback()");
    }

    public bool IsCurrentlyKnockbacked() => isKnockbacked;
    public float GetVelocityMagnitude() => agent != null ? agent.velocity.magnitude : 0f;
    public float GetAnimatorSpeedEstimate()
    {
        // Modified: use both agent.velocity and input-based estimate to avoid zero-speed when using agent.Move
        float agentVel = (agent != null && agent.enabled && agent.isOnNavMesh) ? agent.velocity.magnitude : 0f;
        float inputVel = lastInput.magnitude * moveSpeed;
        return Mathf.Max(agentVel, inputVel);
    }
    private bool CanUseAgent() => agent.enabled && agent.isOnNavMesh;

    private Vector3 CameraRelative(Vector3 input)
    {
        Vector3 camForward = mainCam.transform.forward;
        Vector3 camRight = mainCam.transform.right;
        camForward.y = 0; camRight.y = 0;
        camForward.Normalize(); camRight.Normalize();
        return (camForward * input.z + camRight * input.x).normalized;
    }
}