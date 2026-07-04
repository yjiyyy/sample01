using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// PlayerMovement (MovementSettings-required)
/// - All movement/config parameters come from MovementSettings asset.
/// - If MovementSettings is not assigned, the component disables itself at Awake and logs an error.
/// - Uses MovementPhysics and StepChecker for non-alloc overlap/step checks.
/// - Designed for Unity6 (6000.0.42f1), mobile (FixedUpdate + Time.fixedDeltaTime).
/// - Restores public helper APIs used elsewhere: GetAnimatorSpeedEstimate, CameraRelative, SetSuspendFalling, GetVelocityMagnitude.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class PlayerMovement : MonoBehaviour
{
    private const float EPS = 0.0001f;
    private const float BACKSTEP_ENTER_ANGLE = 120f;
    private const float BACKSTEP_EXIT_ANGLE = 100f;

    // FACE angle threshold to match EnemyImpact behavior
    private const float FACE_ANGLE_THRESHOLD = 30f;

    [Header("Core")]
    [Tooltip("MovementSettings asset (REQUIRED). If not assigned this component will be disabled. Assign a MovementSettings asset to enable movement.")]
    [SerializeField] private MovementSettings movementSettings;

    [Header("Player (persistent)")]
    [SerializeField] private float baseMoveSpeed = 10f;
    [SerializeField] private bool stopWhenNoInput = true;
    [SerializeField] public float rotationSpeedDegPerSec = 720f;
    [SerializeField] private bool debugLogs = false;

    // internal refs
    private Rigidbody rb;
    private CapsuleCollider capsule;
    private Camera mainCam;
    private PlayerWeaponController weaponCtrl;
    private PlayerAnimationController anim;

    private bool isKnockbacked = false;
    private Coroutine knockbackRoutine;

    private Vector3 lastInput = Vector3.zero;
    private bool backStepActive = false;
    private Vector3 _lastLookDirection;
    private float currentMoveSpeed = 0f;

    private bool suspendFalling = false;
    private float externalMoveSpeedMultiplier = 1f;

    private StageManager stageManager;
    public Action onPlayerFellOutOfStage;

    // debug state
    private Vector3 lastAttemptedDisp = Vector3.zero;
#pragma warning disable CS0414
    private bool lastAttemptedBlocked = false; // assigned in movement checks; kept for debug use
#pragma warning restore CS0414
    private float lastAttemptedStepH = 0f;

    // non-alloc caches (created in Awake after verifying movementSettings)
    private Collider[] overlapBuffer;
    private HashSet<int> selfColliderIds;

    private float prevLowerBodySpeed = -1f;

    // External override & multiplier support for charge rotations/movement
    private bool lookOverrideActive = false;
    private Vector3 lookOverrideDir = Vector3.zero;

    // Rotation multiplier (applied to rotationSpeedDegPerSec). Default 1.0f
    private float rotationMultiplier = 1f;

    // NOTE: 플레이어는 몬스터 밀집 충돌로 Y 스핀이 생기기 쉬워,
    // 물리 회전은 고정하고(FreezeRotationY) 방향 전환은 코드로만 처리합니다.

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        mainCam = Camera.main;
        weaponCtrl = GetComponent<PlayerWeaponController>();
        anim = GetComponent<PlayerAnimationController>();

        // MovementSettings required
        if (movementSettings == null)
        {
            Debug.LogError($"[{nameof(PlayerMovement)}] MovementSettings not assigned on GameObject '{gameObject.name}'. Disabling component. Assign a MovementSettings asset to enable movement.");
            this.enabled = false;
            return;
        }

        // Rigidbody safety
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
        }

        _lastLookDirection = transform.forward;

#if UNITY_6000_0_OR_NEWER
        stageManager = UnityEngine.Object.FindFirstObjectByType<StageManager>();
        if (stageManager == null)
            stageManager = UnityEngine.Object.FindFirstObjectByType<StageManager>(UnityEngine.FindObjectsInactive.Include);
#else
        stageManager = FindObjectOfType<StageManager>();
#endif

        if (stageManager == null)
            Debug.LogWarning("[PlayerMovement] StageManager not found. KillZone uses default 0.");

        // init overlap buffer & self collider cache using MovementSettings value
        overlapBuffer = new Collider[Mathf.Max(1, movementSettings.overlapBufferSize)];

        var cols = GetComponentsInChildren<Collider>();
        selfColliderIds = new HashSet<int>(cols.Length);
        for (int i = 0; i < cols.Length; ++i)
            if (cols[i] != null)
                selfColliderIds.Add(cols[i].GetInstanceID());
    }

    void Update()
    {
        if (!enabled) return;
        if (weaponCtrl != null && weaponCtrl.IsTimeHoldActive) return;
        if (isKnockbacked) return;

        Vector2 raw = InputManager.Instance.GetMoveInput();
        lastInput = new Vector3(raw.x, 0f, raw.y);

        // 공격 중에는 입력으로 _lastLookDirection을 갱신하지 않음.
        // 단, AR 발사 중이고 이동 허용인 경우만 예외.
        bool isARFiring = weaponCtrl != null && weaponCtrl.IsARFiring;
        bool arAllowMove = weaponCtrl != null && weaponCtrl.ARAllowMoveWhileFiring;
        bool inAttackState = weaponCtrl != null && weaponCtrl.CurrentState == PlayerState.Attack;

        if (!inAttackState || (isARFiring && arAllowMove))
        {
            if (lastInput.sqrMagnitude > EPS)
            {
                Vector3 camRel = CameraRelative(lastInput);
                camRel.y = 0f;
                if (camRel.sqrMagnitude > EPS)
                {
                    _lastLookDirection = camRel.normalized;
                }
            }
        }
        // else: 공격 중이면 _lastLookDirection 유지
    }

    void FixedUpdate()
    {
        if (!enabled) return;
        if (weaponCtrl != null && weaponCtrl.IsTimeHoldActive) return;

        bool isARFiring = weaponCtrl != null && weaponCtrl.IsARFiring;
        bool arAllowMove = weaponCtrl != null && weaponCtrl.ARAllowMoveWhileFiring;
        currentMoveSpeed = ComputeCurrentMoveSpeed(isARFiring, arAllowMove);

        bool isEvading = weaponCtrl != null && weaponCtrl.CurrentState == PlayerState.Evade;
        if (!isKnockbacked && !isEvading)
        {
            HandleHorizontal();
            HandleRotation(isARFiring);
        }

        // 물리 회전(충돌 토크) 잔여 제거
        if (rb != null)
            rb.angularVelocity = Vector3.zero;

        // 지면에 붙어 있을 때 위쪽으로 쌓이는 속도 제거 (충돌 해소로 인한 계속 떠오름 방지)
        if (rb != null && !suspendFalling && movementSettings != null && movementSettings.groundMask != 0 && IsGrounded())
        {
            var v = rb.linearVelocity;
            if (v.y > 0f)
            {
                v.y = 0f;
                rb.linearVelocity = v;
            }
        }

        CheckKillZone();

        if (!isEvading)
            HandleBackStep(lastInput.sqrMagnitude > 0.0001f, IsMovementBlocked(), lastInput);
    }

    private void HandleHorizontal()
    {
        Vector3 desiredMove = ComputeHorizontalDisplacement();
        if (desiredMove.sqrMagnitude <= EPS) return;

        MoveFilteredDisplacement(desiredMove);

        if (desiredMove.sqrMagnitude > EPS)
            _lastLookDirection = desiredMove.normalized;
    }

    private Vector3 ComputeHorizontalDisplacement()
    {
        if (IsMovementBlocked() || (stopWhenNoInput && lastInput.sqrMagnitude <= EPS))
            return Vector3.zero;

        Vector3 camRel = CameraRelative(lastInput);
        float speed = currentMoveSpeed;
        if (speed <= EPS) return Vector3.zero;

        float inputMag = Mathf.Clamp01(lastInput.magnitude);
        return camRel.normalized * speed * inputMag * Time.fixedDeltaTime;
    }

    private bool IsMovementBlocked()
    {
        if (weaponCtrl == null) return false;
        PlayerState state = weaponCtrl.CurrentState;
        bool isARFiring = weaponCtrl.IsARFiring;
        bool arAllowMove = weaponCtrl != null && weaponCtrl.ARAllowMoveWhileFiring;
        bool meleeComboAllowMove = weaponCtrl != null && weaponCtrl.MeleeComboAllowMove;
        bool attackBlocking = state == PlayerState.Attack && !(isARFiring && arAllowMove) && !meleeComboAllowMove;

        if (attackBlocking ||
            state == PlayerState.Knockback ||
            state == PlayerState.Stun ||
            state == PlayerState.Dead ||
            state == PlayerState.Evade)
            return true;

        return false;
    }

    // Core movement checks & apply. Uses MovementSettings + MovementCollisionSolver.
    public void MovePhysicsDisplacement(Vector3 disp)
    {
        lastAttemptedDisp = disp;
        lastAttemptedBlocked = false;
        lastAttemptedStepH = 0f;

        if (rb == null || disp.sqrMagnitude <= EPS) return;

        var result = MovementCollisionSolver.TryResolvePosition(
            rb.position,
            disp,
            capsule,
            movementSettings,
            overlapBuffer,
            selfColliderIds);

        lastAttemptedBlocked = result.blocked;
        lastAttemptedStepH = result.stepHeight;

        if (result.blocked)
        {
            if (debugLogs) Debug.Log("[PlayerMovement] Movement blocked by background collision.");
            return;
        }

        if (result.moved)
            MoveCapsuleDirect(result.finalPosition);
    }

    private void MoveCapsuleDirect(Vector3 newPosition)
    {
        rb.MovePosition(newPosition);
    }

    public void MoveFilteredDisplacement(Vector3 disp)
    {
        lastAttemptedDisp = disp;
        lastAttemptedBlocked = false;
        lastAttemptedStepH = 0f;

        if (rb == null || disp.sqrMagnitude <= EPS) return;

        var result = MovementCollisionSolver.Solve(
            rb,
            capsule,
            disp,
            movementSettings,
            overlapBuffer,
            selfColliderIds);

        lastAttemptedBlocked = result.blocked;
        lastAttemptedStepH = result.stepHeight;

        if (result.blocked)
        {
            if (debugLogs) Debug.Log("[PlayerMovement] Filtered movement blocked by background collision.");
            return;
        }

        if (result.moved)
            MoveCapsuleDirect(result.finalPosition);
    }

    private void HandleRotation(bool isARFiring)
    {
        // Attack/Knockback/Stun/Evade 중에는 회전 보정 스킵
        if (weaponCtrl != null)
        {
            bool blockByAttack = weaponCtrl.CurrentState == PlayerState.Attack && !weaponCtrl.IsARFiring;
            bool blockByCC = weaponCtrl.CurrentState == PlayerState.Knockback ||
                             weaponCtrl.CurrentState == PlayerState.Stun ||
                             weaponCtrl.CurrentState == PlayerState.Evade;
            if (blockByAttack || blockByCC)
                return;
        }

        // If look override is active, prefer it (used by charge controller)
        Vector3 desiredDir = lookOverrideActive ? lookOverrideDir : _lastLookDirection;
        bool arRotationLocked = weaponCtrl != null && weaponCtrl.ARIsRotationLocked;

        if (arRotationLocked && isARFiring && weaponCtrl != null)
        {
            Vector3 lockedF = weaponCtrl.ARLockedForward;
            if (lockedF.sqrMagnitude > EPS)
                desiredDir = lockedF.normalized;
        }

        if (desiredDir.sqrMagnitude > EPS)
        {
            Quaternion targetRot = Quaternion.LookRotation(desiredDir, Vector3.up);
            float effectiveRotSpeed = rotationSpeedDegPerSec * Mathf.Clamp01(rotationMultiplier);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRot,
                effectiveRotSpeed * Time.fixedDeltaTime
            );
        }
    }

    private void HandleBackStep(bool hasInput, bool movementBlocked, Vector3 moveInput)
    {
        Vector3 currentMoveDir = moveInput.sqrMagnitude > EPS ? moveInput.normalized : Vector3.zero;
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

    /// <summary>캡슐 발밑에서 짧게 아래로 레이캐스트해 지면인지 확인. 지면에 붙었을 때 위로 쌓이는 속도 제거용.</summary>
    private bool IsGrounded()
    {
        if (capsule == null || movementSettings == null) return false;
        var ms = movementSettings;
        if (ms.groundMask == 0) return false;

        Vector3 centerWorld = transform.TransformPoint(capsule.center);
        float halfH = Mathf.Max(capsule.height * 0.5f - capsule.radius, 0f);
        Vector3 bottom = centerWorld - transform.up * halfH;
        float checkDist = ms.floorCheckDepth + 0.05f;
        if (Physics.Raycast(bottom + Vector3.up * 0.01f, Vector3.down, out RaycastHit hit, checkDist, ms.groundMask, QueryTriggerInteraction.Ignore))
            return hit.normal.y >= ms.floorSlopeThreshold;
        return false;
    }

    private void CheckKillZone()
    {
        float limit = stageManager != null ? stageManager.killY : 0f;
        if (transform.position.y <= limit)
        {
            if (stageManager != null) stageManager.HandlePlayerFall(gameObject);
            onPlayerFellOutOfStage?.Invoke();
        }
    }

    void LateUpdate()
    {
        if (anim != null && weaponCtrl != null)
        {
            bool isARFiring = weaponCtrl.IsARFiring;
            bool arAllowMove = weaponCtrl != null && weaponCtrl.ARAllowMoveWhileFiring;
            float lowerSpeed = 1f;

            if (isARFiring && arAllowMove && weaponCtrl != null)
            {
                var arData = weaponCtrl.GetCurrentWeaponData() as WeaponDataSO_AR;
                if (arData != null) lowerSpeed = Mathf.Max(0f, arData.animPlaybackSpeedWhileFiring);
            }

            if (!Mathf.Approximately(prevLowerBodySpeed, lowerSpeed))
            {
                anim.SetLowerBodyPlaybackSpeed(lowerSpeed);
                prevLowerBodySpeed = lowerSpeed;
            }
        }
    }

    private float ComputeCurrentMoveSpeed(bool isARFiring, bool arAllowMove)
    {
        float speed = baseMoveSpeed * Mathf.Max(0f, externalMoveSpeedMultiplier);
        if (isARFiring && arAllowMove && weaponCtrl != null)
        {
            var arData = weaponCtrl.GetCurrentWeaponData() as WeaponDataSO_AR;
            if (arData != null) speed *= Mathf.Max(0f, arData.moveSpeedWhileFiring);
        }
        return speed;
    }

    /// <summary>
    /// 업그레이드 등 외부 시스템이 이동속도 배율을 적용할 때 사용합니다. (1 = 기본속도)
    /// </summary>
    public void SetExternalMoveSpeedMultiplier(float multiplier)
    {
        externalMoveSpeedMultiplier = Mathf.Max(0f, multiplier);
    }

    // Public helper APIs required by other scripts
    public float GetAnimatorSpeedEstimate()
    {
        // Return normalized estimate used by animator lower-body playback.
        float inputMag = Mathf.Clamp01(lastInput.magnitude);
        if (baseMoveSpeed <= EPS) return inputMag > EPS ? 1f : 0f;
        return Mathf.Clamp01(inputMag * (currentMoveSpeed / baseMoveSpeed));
    }

    // Expose base move speed for external scripts (charge controller uses this)
    public float GetBaseMoveSpeed()
    {
        return baseMoveSpeed;
    }

    // External API: override look direction (used by charge logic). Call ClearLookOverride() to resume normal behavior.
    public void SetLookOverride(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < EPS) return;
        lookOverrideDir = dir.normalized;
        lookOverrideActive = true;
    }

    public void ClearLookOverride()
    {
        lookOverrideActive = false;
    }

    // External API: rotation speed multiplier (applied to rotationSpeedDegPerSec). Use 1f to reset to normal.
    public void SetRotationMultiplier(float mult)
    {
        rotationMultiplier = Mathf.Clamp01(mult);
    }

    public void ResetRotationMultiplier()
    {
        rotationMultiplier = 1f;
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

    public void SetSuspendFalling(bool suspend)
    {
        suspendFalling = suspend;
        if (rb == null) return;
        if (suspend)
        {
            rb.useGravity = false;
            var v = rb.linearVelocity;
            v.y = 0f;
            rb.linearVelocity = v;
        }
        else
        {
            rb.useGravity = true;
        }
    }

    public float GetVelocityMagnitude()
    {
        float inputMag = Mathf.Clamp01(lastInput.magnitude);
        if (IsMovementBlocked()) return 0f;
        return inputMag * currentMoveSpeed;
    }

    /// <summary>이동 입력이 있는지 여부 (차단 여부와 무관). 콤보 윈도우에서 Move 상태 전환 판정용.</summary>
    public bool HasMovementInput() => lastInput.sqrMagnitude > EPS;

    /// <summary>저장된 입력을 초기화. enabled=false 구간 후 복구 시 잔여 입력으로 인한 잘못된 이동 방지.</summary>
    public void ClearStoredInput() => lastInput = Vector3.zero;

    // Knockback (mass-aware)
    /// <param name="faceHitDirection">true면 피격 방향으로 회전. Push·슈퍼아머·공격 중에는 false.</param>
    public void ApplyKnockback(Vector3 dir, float force, float duration, Transform attacker = null, bool faceHitDirection = true)
    {
        // 넉백 시작 자체는 허용한다.
        // (CC 중복 차단은 PlayerWeaponController.ForceApplyKnockback 진입부에서 처리)
        if (weaponCtrl != null)
        {
            if (weaponCtrl.CurrentState == PlayerState.Dead) return;
            if (weaponCtrl.IsInvincible()) return;
        }

        // 확실한 넉백 판정일 때만 피격 방향으로 회전 (Push·슈퍼아머·공격 중은 유지)
        if (faceHitDirection && !isKnockbacked)
            FaceKnockback(dir);

        if (knockbackRoutine != null) StopCoroutine(knockbackRoutine);
        if (debugLogs) Debug.Log($"[PM KNOCK] start dir={dir}, force={force}, dur={duration}, rb.mass={(rb != null ? rb.mass : -1f)}");
        knockbackRoutine = StartCoroutine(KnockbackRoutine(dir, force, duration, attacker));
    }

    private IEnumerator KnockbackRoutine(Vector3 dir, float force, float duration, Transform attacker)
    {
        isKnockbacked = true;
        Vector3 knockDir = dir.normalized; knockDir.y = 0f;
        float elapsed = 0f;

        // Note: we intentionally do NOT change _lastLookDirection here.
        // FaceKnockback has already set transform.rotation and _lastLookDirection to the knockback-facing direction.

        // Read mass from Rigidbody (if present). Use a safe minimum to avoid div by zero.
        float massVal = 1f;
        if (rb != null) massVal = Mathf.Max(0.0001f, rb.mass);

        while (elapsed < duration)
        {
            // 타격감 일관성: 홀드(히트스톱) 중에는 푸시/넉백 이동을 잠시 멈춘다.
            // elapsed를 증가시키지 않아 홀드가 끝난 뒤 남은 이동이 이어진다.
            if (weaponCtrl != null && weaponCtrl.IsTimeHoldActive)
            {
                yield return new WaitForFixedUpdate();
                continue;
            }

            float t = 1f - Mathf.Clamp01(elapsed / Mathf.Max(duration, EPS));
            // mass-aware speed: heavier => smaller speed for a given impulse/power
            float currentSpeed = (force / massVal) * t;
            Vector3 disp = knockDir * currentSpeed * Time.fixedDeltaTime;
            MovePhysicsDisplacement(disp);

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

    // Public-facing FaceKnockback: same semantics as EnemyImpact.FaceHit
    public void FaceKnockback(Vector3 hitDir)
    {
        // 수평 방향만 사용
        Vector3 look = -hitDir;
        look.y = 0f;
        if (look.sqrMagnitude < 0.0001f) return;
        look.Normalize();

        // 현재 정면과 너무 비슷하면 생략(프로젝트 상수 유지)
        Vector3 currentFwd = transform.forward;
        currentFwd.y = 0f;
        if (currentFwd.sqrMagnitude < 0.0001f) currentFwd = Vector3.forward;

        float angle = Vector3.Angle(currentFwd, look);
        if (angle < FACE_ANGLE_THRESHOLD) return;

        // 물리 친화 회전 스냅
        Quaternion target = Quaternion.LookRotation(look, Vector3.up);

        // FreezeRotationY 상태이므로 transform.rotation으로 방향 전환만 적용
        transform.rotation = target;

        // 이후 로직에서 이 전방을 참조하도록 업데이트
        _lastLookDirection = look;
    }

    // Debug gizmos and other helpers remain unchanged...
}