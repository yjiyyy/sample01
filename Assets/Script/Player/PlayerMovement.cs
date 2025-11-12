using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    // NOTE:
    // Agent 인스펙터 값(agent.speed / agent.angularSpeed / agent.acceleration / stoppingDistance)
    // 을 유일한 이동 파라미터 소스로 사용합니다 (옵션1).
    // PlayerMovement 인스펙터에 중복된 base* 필드는 제거되었습니다.
    // NavMeshAgent 컴포넌트에서 값을 조정하세요.

    // Backstep 각도: 인스펙터 필드 제거 -> 내부 상수로 고정 (히스테리시스)
    private const float BACKSTEP_ENTER_ANGLE = 120f;
    private const float BACKSTEP_EXIT_ANGLE = 100f;

    // Fallback move speed when no agent available (should be rare)
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

    // 캐시: agent 원래 값(AR 발사 중 배율 적용 후 복원용)
    private float agentDefaultSpeed;
    private float agentDefaultAngularSpeed;
    private float agentDefaultAcceleration;

    // 넉백 상태
    private bool isKnockbacked = false;
    private Coroutine knockbackRoutine;

    // 입력/상태 캐시
    private Vector3 lastInput = Vector3.zero;
    private Vector3 lastPosition;

    // Backstep state (internal)
    private bool backStepActive = false;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        mainCam = Camera.main;
        weaponCtrl = GetComponent<PlayerWeaponController>();
        anim = GetComponent<PlayerAnimationController>();

        if (agent == null) Debug.LogError("[PM] NavMeshAgent is missing!");
        else
        {
            // agent가 transform 위치를 직접 제어하도록 유지
            agent.updatePosition = true;
            agent.updateRotation = false; // rotation은 스크립트에서 제어 (또는 AR 고정 시 강제)
            agent.updateUpAxis = true;

            // 기본값 캐시
            agentDefaultSpeed = agent.speed;
            agentDefaultAngularSpeed = agent.angularSpeed;
            agentDefaultAcceleration = agent.acceleration;
        }

        // Rigidbody 세팅: agent가 transform을 제어하므로 kinematic
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        }

        lastPosition = transform.position;
        if (debugLogs) Debug.Log("[PM] Initialized (agent authoritative)");
    }

    void Update()
    {
        if (isKnockbacked) return; // 넉백 중 입력 무시

        // 입력
        Vector2 raw = InputManager.Instance.GetMoveInput();
        lastInput = new Vector3(raw.x, 0f, raw.y);

        // 샘플링 로그: 매 10프레임마다 출력 (스팸 방지)
        if (debugLogs && Time.frameCount % 10 == 0)
        {
            Vector2 rawDbg = raw;
            float lastMag = lastInput.magnitude;
            float agentSpeedVal = (agent != null) ? agent.speed : 0f;
            Debug.Log($"[PM DEBUG] raw={rawDbg:F3}, lastInput={lastInput}, inputMag={lastMag:F3}, agentSpeed={agentSpeedVal:F3}, dt={Time.deltaTime:F4}");
        }

        bool isARFiring = weaponCtrl != null && weaponCtrl.IsARFiring;
        bool arAllowMove = weaponCtrl != null && weaponCtrl.ARAllowMoveWhileFiring;
        bool arRotationLocked = weaponCtrl != null && weaponCtrl.ARIsRotationLocked;

        // AR 연사 중 무기 데이터가 있고 이동 배율이 있으면 agent.speed 적용
        if (agent != null)
        {
            // 기본값으로 보정(항상 기본값 유지 후 조건부로 덮어쓰기)
            agent.speed = agentDefaultSpeed;
            agent.angularSpeed = agentDefaultAngularSpeed;
            agent.acceleration = agentDefaultAcceleration;

            if (isARFiring && arAllowMove && weaponCtrl != null)
            {
                var arData = weaponCtrl.GetCurrentWeaponData() as WeaponDataSO_AR;
                if (arData != null)
                {
                    // moveSpeedWhileFiring은 배율(예: 0.8)이라고 가정
                    float newSpeed = agentDefaultSpeed * Mathf.Max(0f, arData.moveSpeedWhileFiring);
                    if (debugLogs) Debug.Log($"[PM AR] AR firing move speed applied. default={agentDefaultSpeed}, multiplier={arData.moveSpeedWhileFiring}, new={newSpeed}");
                    agent.speed = newSpeed;
                    // anim playback speed handled separately via anim.SetLowerBodyPlaybackSpeed
                }
            }
        }

        // AR 회전 고정: 누르고 있으면 매프레임 고정 (안정성 체크 포함)
        if (isARFiring && arRotationLocked && weaponCtrl != null)
        {
            Vector3 lockedF = weaponCtrl.ARLockedForward;
            if (lockedF.sqrMagnitude > 0.0001f)
            {
                // 즉시 고정 (요청대로 누르고 있으면 각도 고정)
                transform.rotation = Quaternion.LookRotation(lockedF.normalized, Vector3.up);
                if (debugLogs && Time.frameCount % 30 == 0) Debug.Log($"[PM AR ROT] AR rotation locked. forward={lockedF.normalized}");
            }
        }

        // 상태에 따른 입력 차단(공격 등)
        if (weaponCtrl != null)
        {
            bool isAttackBlocking = weaponCtrl.CurrentState == PlayerState.Attack &&
                                    !(isARFiring && arAllowMove);
            if (isAttackBlocking ||
                weaponCtrl.CurrentState == PlayerState.Knockback ||
                weaponCtrl.CurrentState == PlayerState.Stun ||
                weaponCtrl.CurrentState == PlayerState.Dead ||
                weaponCtrl.CurrentState == PlayerState.Evade)
            {
                // 상태 차단 시 agent 멈춤
                if (agent != null && agent.isOnNavMesh)
                {
                    agent.ResetPath();
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                }
                anim?.SetLowerBodyPlaybackSpeed(1f);
                if (debugLogs) Debug.Log($"[PM STATE] Movement blocked by state={weaponCtrl.CurrentState}");
                return;
            }
        }

        // 이동 처리
        if (lastInput.sqrMagnitude > 0.0001f)
        {
            // 입력 세기(아날로그) 반영: lastInput의 x/z 축을 사용하여 0..1 범위로 계산
            float inputMag = Mathf.Clamp01(new Vector2(lastInput.x, lastInput.z).magnitude);

            Vector3 moveDir = CameraRelative(lastInput);
            moveDir.y = 0f;
            if (moveDir.sqrMagnitude > 0.0001f) moveDir.Normalize();

            // AR 모드: 이동 허용 여부 고려
            if (isARFiring && !arAllowMove)
            {
                // AR 공격 중 이동 불가
                if (agent != null && agent.isOnNavMesh)
                {
                    agent.ResetPath();
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                }
                anim?.SetLowerBodyPlaybackSpeed(1f);
                if (debugLogs) Debug.Log("[PM AR] AR firing and movement not allowed -> early return");
                return;
            }

            // facing 기준 통일 (AR locked 우선, 아니면 transform.forward)
            Vector3 facing = transform.forward;
            if (isARFiring && weaponCtrl != null && weaponCtrl.ARIsRotationLocked)
            {
                Vector3 locked = weaponCtrl.ARLockedForward;
                if (locked.sqrMagnitude > 0.0001f) facing = locked.normalized;
            }

            // 실제 이동: agent 권한자 사용 (NavMesh 위에 없으면 transform 폴백)
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                // inputMag을 곱해 아날로그 입력에 따라 속도가 달라지도록 함
                Vector3 displacement = moveDir * agent.speed * inputMag * Time.deltaTime;
                agent.Move(displacement);

                if (debugLogs && Time.frameCount % 10 == 0)
                {
                    Debug.Log($"[PM MOVE] agent.Move disp={displacement}, moveDir={moveDir}, speed={agent.speed}, inputMag={inputMag:F3}");
                }

                // 회전: AR 고정이 아닐 때만 moveDir 향하도록 설정
                if (!(isARFiring && arRotationLocked))
                {
                    RotateTowardsDir(moveDir, agent.angularSpeed);
                }

                // Backstep(앞/뒤) 판정 — 동일 로직 유지
                float signed = Vector3.SignedAngle(facing.normalized, moveDir.normalized, Vector3.up);
                float absAngle = Mathf.Abs(signed);

                if (!backStepActive && absAngle >= BACKSTEP_ENTER_ANGLE)
                {
                    backStepActive = true;
                    anim?.SetBackStep(true);
                    if (debugLogs) Debug.Log("[PM BACKSTEP] Entered backstep");
                }
                else if (backStepActive && absAngle <= BACKSTEP_EXIT_ANGLE)
                {
                    backStepActive = false;
                    anim?.SetBackStep(false);
                    if (debugLogs) Debug.Log("[PM BACKSTEP] Exited backstep");
                }

                // 하체 애니 속도 반영: 기존 로직 유지 (필요하면 inputMag으로 추가 보정 가능)
                if (anim != null)
                {
                    float lowerSpeed = 1f;
                    if (weaponCtrl != null && isARFiring && weaponCtrl.ARAllowMoveWhileFiring)
                    {
                        var arData = weaponCtrl.GetCurrentWeaponData() as WeaponDataSO_AR;
                        if (arData != null) lowerSpeed = Mathf.Max(0f, arData.animPlaybackSpeedWhileFiring);
                    }
                    anim.SetLowerBodyPlaybackSpeed(lowerSpeed);
                }
            }
            else
            {
                // NavMesh 밖 폴백: 동일하게 inputMag을 곱함
                float fallbackSpeed = (agent != null) ? Mathf.Max(agent.speed, FALLBACK_MOVE_SPEED) : FALLBACK_MOVE_SPEED;
                Vector3 disp = moveDir * fallbackSpeed * inputMag * Time.deltaTime;
                transform.position += disp;

                if (debugLogs && Time.frameCount % 10 == 0)
                    Debug.Log($"[PM MOVE FALLBACK] posDelta={disp}, fallbackSpeed={fallbackSpeed}, inputMag={inputMag:F3}");

                if (!(isARFiring && arRotationLocked))
                    RotateTowardsDir(moveDir, (agent != null) ? agent.angularSpeed : 720f);

                // Backstep 판정 (동일 로직)
                float signed = Vector3.SignedAngle(facing.normalized, moveDir.normalized, Vector3.up);
                float absAngle = Mathf.Abs(signed);

                if (!backStepActive && absAngle >= BACKSTEP_ENTER_ANGLE)
                {
                    backStepActive = true;
                    anim?.SetBackStep(true);
                    if (debugLogs) Debug.Log("[PM BACKSTEP] Entered backstep (fallback)");
                }
                else if (backStepActive && absAngle <= BACKSTEP_EXIT_ANGLE)
                {
                    backStepActive = false;
                    anim?.SetBackStep(false);
                    if (debugLogs) Debug.Log("[PM BACKSTEP] Exited backstep (fallback)");
                }
            }
        }
        else
        {
            // 입력이 없는 경우 필요하다면 에이전트 정지 처리
            if (stopWhenNoInput && agent != null && agent.isOnNavMesh)
            {
                if (!agent.isStopped)
                {
                    agent.isStopped = true;
                    if (debugLogs) Debug.Log("[PM] No input -> agent stopped");
                }
            }
        }
    }

    void LateUpdate()
    {
        // agent.updatePosition == true 이므로 visual-sync 코드 없음.
        // kinematic Rigidbody 동기화
        if (rb != null && rb.isKinematic)
        {
            rb.position = transform.position;
        }

        lastPosition = transform.position;
    }

    private void RotateTowardsDir(Vector3 dir, float angularDegPerSec)
    {
        if (dir.sqrMagnitude < 0.0001f)
        {
            if (debugLogs && Time.frameCount % 30 == 0) Debug.Log("[PM ROT] skip rotate: dir too small");
            return;
        }

        if (debugLogs && Time.frameCount % 10 == 0)
        {
            Debug.Log($"[PM ROT] RotateTowardsDir called. dir={dir}, currentForward={transform.forward}, angularSpeed={angularDegPerSec}");
        }

        Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
        // 즉시 고정 대신 RotateTowards로 부드럽게(하지만 AR locked일때는 Update에서 즉시 고정함)
        float maxDeg = angularDegPerSec * Time.deltaTime;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, maxDeg);
    }

    // 넉백: agent.Move 기반 코루틴 (NavMesh 밖이면 transform 폴백)
    public void ApplyKnockback(Vector3 dir, float force, float duration, Transform attacker = null)
    {
        if (knockbackRoutine != null)
        {
            StopCoroutine(knockbackRoutine);
            knockbackRoutine = null;
        }
        if (debugLogs) Debug.Log($"[PM KNOCK] ApplyKnockback start dir={dir}, force={force}, duration={duration}");
        knockbackRoutine = StartCoroutine(KnockbackRoutine(dir, force, duration, attacker));
    }

    private IEnumerator KnockbackRoutine(Vector3 dir, float force, float duration, Transform attacker)
    {
        isKnockbacked = true;
        Vector3 knockDir = dir.normalized;
        knockDir.y = 0f;
        float elapsed = 0f;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.isStopped = false;
        }

        if (attacker != null)
        {
            Vector3 lookDir = (attacker.position - transform.position);
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(lookDir.normalized);
        }

        while (elapsed < duration)
        {
            float t = 1f - Mathf.Clamp01(elapsed / Mathf.Max(duration, EPS));
            float currentSpeed = force * t;
            Vector3 delta = knockDir * currentSpeed * Time.deltaTime;

            if (agent != null && agent.isOnNavMesh)
                agent.Move(delta);
            else
                transform.position += delta;

            if (rb != null && rb.isKinematic) rb.position = transform.position;

            if (debugLogs && Time.frameCount % 10 == 0)
                Debug.Log($"[PM KNOCK] elapsed={elapsed:F3}, currentSpeed={currentSpeed:F3}, delta={delta}");

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (agent != null && agent.isOnNavMesh)
        {
            agent.velocity = Vector3.zero;
            agent.ResetPath();
            agent.isStopped = true;
        }

        isKnockbacked = false;
        knockbackRoutine = null;
        if (debugLogs) Debug.Log("[PM KNOCK] Knockback finished");
    }

    public void CancelKnockback()
    {
        if (knockbackRoutine != null)
        {
            StopCoroutine(knockbackRoutine);
            knockbackRoutine = null;
        }
        isKnockbacked = false;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.velocity = Vector3.zero;
            agent.isStopped = true;
            agent.ResetPath();
        }

        if (debugLogs) Debug.Log("[PM KNOCK] Knockback cancelled");
    }

    public bool IsCurrentlyKnockbacked() => isKnockbacked;

    // 애니메이터용 속도 추정: 0..+1 범위 (절대값). 
    // 방향 판정은 SetBackStep(bool)로 이미 처리됨.
    public float GetAnimatorSpeedEstimate()
    {
        // Determine facing similar to Update (prefer AR locked if applicable)
        Vector3 facing = transform.forward;
        if (weaponCtrl != null && weaponCtrl.IsARFiring && weaponCtrl.ARIsRotationLocked)
        {
            Vector3 locked = weaponCtrl.ARLockedForward;
            if (locked.sqrMagnitude > 0.0001f) facing = locked.normalized;
        }

        // agent 기반 우선
        if (agent != null && agent.isOnNavMesh)
        {
            float currentSpeed = agent.velocity.magnitude;
            float denom = Mathf.Max(agent.speed, EPS);
            float normalized = Mathf.Clamp01(currentSpeed / denom);

            if (currentSpeed < EPS)
            {
                // 거의 정지이면 입력 기반으로 추정 (절대값)
                return InputBasedAbsNormalizedSpeed(facing);
            }

            // 반환은 절대값(양수)만 — 방향은 SetBackStep(bool)로 처리
            return normalized;
        }

        // agent 비가용 시: 입력 기반 추정
        return InputBasedAbsNormalizedSpeed(facing);
    }

    private float InputBasedAbsNormalizedSpeed(Vector3 facing)
    {
        float inputMag = Mathf.Clamp01(lastInput.magnitude);
        if (inputMag < EPS) return 0f;
        Vector3 camRel = CameraRelative(lastInput);
        if (camRel.sqrMagnitude < EPS) return inputMag;
        // magnitude only -> positive
        return inputMag;
    }

    public float GetVelocityMagnitude()
    {
        if (agent != null && agent.isOnNavMesh) return agent.velocity.magnitude;
        return lastInput.magnitude * FALLBACK_MOVE_SPEED;
    }

    private Vector3 CameraRelative(Vector3 input)
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return new Vector3(input.x, 0f, input.z); // fallback
        Vector3 camF = mainCam.transform.forward;
        Vector3 camR = mainCam.transform.right;
        camF.y = 0f; camR.y = 0f;
        camF.Normalize(); camR.Normalize();
        return (camF * input.z + camR * input.x);
    }
}