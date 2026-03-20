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
        if (rb != null && !suspendFalling && movementSettings != null && movementSettings.floorMask != 0 && IsGrounded())
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
        bool attackBlocking = state == PlayerState.Attack && !(isARFiring && arAllowMove);

        if (attackBlocking ||
            state == PlayerState.Knockback ||
            state == PlayerState.Stun ||
            state == PlayerState.Dead ||
            state == PlayerState.Evade)
            return true;

        return false;
    }

    // Core movement checks & apply. Uses movementSettings fields.
    public void MovePhysicsDisplacement(Vector3 disp)
    {
        lastAttemptedDisp = disp;
        lastAttemptedBlocked = false;
        lastAttemptedStepH = 0f;

        if (rb == null || disp.sqrMagnitude <= EPS) return;

        // local copies for hot paths
        var ms = movementSettings;
        LayerMask headMask = ms.headMask;
        float headPortion = ms.headPortion;
        float headMargin = ms.headMargin;
        int headClampIterations = Mathf.Max(1, ms.headClampIterations);

        LayerMask obstacleMask = ms.obstacleMask;
        LayerMask floorMask = ms.floorMask;

        float collisionSkin = ms.collisionSkin;
        float floorThreshold = ms.floorThreshold;
        int slideIterations = Mathf.Clamp(ms.slideIterations, 0, 4);
        float tinyDispThreshold = ms.tinyDispThreshold;

        float maxStepHeight = ms.maxStepHeight;
        int stepSearchIterations = Mathf.Max(1, ms.stepSearchIterations);
        float floorCheckDepth = ms.floorCheckDepth;
        float minStepProbeDistance = ms.minStepProbeDistance;

        float pushableMassMultiplier = ms.pushableMassMultiplier;
        float pushImpulseFactor = ms.pushImpulseFactor;
        float crowdMassThresholdMultiplier = ms.crowdMassThresholdMultiplier;
        int crowdCountThreshold = ms.crowdCountThreshold;

        // 1) strict headroom block
        if (capsule != null && ms.strictHeadroomBlock && headClampIterations > 0 && headPortion > 0f && headMask != 0)
        {
            Transform t = capsule.transform;
            Vector3 worldCenterNow = t.TransformPoint(capsule.center) + (rb.position - t.position);

            float radius = capsule.radius;
            float height = capsule.height;
            float cylLen = Mathf.Max(height - 2f * radius, 0f);
            float headCylLen = cylLen * Mathf.Clamp01(headPortion);
            float topLine = (height * 0.5f) - radius;
            float usedRadius = Mathf.Max(radius - headMargin, radius * 0.5f);
            Vector3 up = t.up;

            Vector3 topSphereNow = worldCenterNow + up * topLine;
            Vector3 bottomHeadNow = topSphereNow - up * headCylLen;

            bool currentHeadOverlap = StepChecker.CheckHeadOverlap(
                topSphereNow, bottomHeadNow, usedRadius, headMask, overlapBuffer, selfColliderIds);

            Vector3 targetOrigin = rb.position + disp;
            Vector3 worldCenterAtTarget = t.TransformPoint(capsule.center) + (targetOrigin - t.position);

            Vector3 topSphereTarget = worldCenterAtTarget + up * topLine;
            Vector3 bottomHeadTarget = topSphereTarget - up * headCylLen;

            bool targetHeadOverlap = StepChecker.CheckHeadOverlap(
                topSphereTarget, bottomHeadTarget, usedRadius, headMask, overlapBuffer, selfColliderIds);

            if (!currentHeadOverlap && targetHeadOverlap)
            {
                lastAttemptedBlocked = true;
                if (debugLogs) Debug.Log("[PlayerMovement] Movement blocked by strict headroom (target head overlap).");
                return;
            }
        }

        // 2) headroom clamp (partial allow)
        if (capsule != null && headClampIterations > 0 && headPortion > 0f)
        {
            disp = StepChecker.ClampHeadroomHorizontal(
                capsule,
                rb.position,
                disp,
                ms.headMask,
                headClampIterations,
                headPortion,
                headMargin,
                overlapBuffer,
                selfColliderIds
            );
        }

        // 3) final overlap check at target origin
        if (capsule != null)
        {
            LayerMask obsMask = obstacleMask;
            if (obsMask != 0)
            {
                Transform t = capsule.transform;
                Vector3 targetOrigin = rb.position + disp;
                Vector3 worldCenterAtTarget = t.TransformPoint(capsule.center) + (targetOrigin - t.position);

                float radius = capsule.radius;
                float height = capsule.height;
                float halfLine = Mathf.Max(height * 0.5f - radius, 0f);
                Vector3 up = t.up;

                Vector3 topTarget = worldCenterAtTarget + up * halfLine;
                Vector3 bottomTarget = worldCenterAtTarget - up * halfLine;

                var summary = MovementPhysics.EvaluateCapsuleOverlapForMovement(
                    bottomTarget,
                    topTarget,
                    radius,
                    obsMask,
                    overlapBuffer,
                    selfColliderIds,
                    rb,
                    pushableMassMultiplier
                );

                // all external pushable -> evaluate crowd
                if (summary.externalCount > 0 && !summary.anyUnpushable)
                {
                    bool crowdBlocks = false;
                    if (summary.totalPushableMass > rb.mass * crowdMassThresholdMultiplier) crowdBlocks = true;
                    if (summary.pushableCount >= crowdCountThreshold) crowdBlocks = true;

                    if (crowdBlocks)
                    {
                        lastAttemptedBlocked = true;
                        if (debugLogs) Debug.Log($"[PlayerMovement] Movement blocked by crowd resistance: totalMass={summary.totalPushableMass:F2}, count={summary.pushableCount}");
                        return;
                    }
                    else
                    {
                        // allow movement and optionally push impulse to overlapped bodies
                        MoveCapsuleDirect(rb.position + disp);

                        if (pushImpulseFactor > 0f)
                        {
                            float impulseBase = Mathf.Clamp01(disp.magnitude) * pushImpulseFactor;
                            MovementPhysics.ApplyPushImpulseToOverlap(overlapBuffer, summary.rawCount, summary.fallbackHits, selfColliderIds, rb, pushableMassMultiplier, impulseBase);
                        }
                        return;
                    }
                }

                // some external and at least one unpushable -> try step then block
                bool foundAnyExternal = summary.externalCount > 0;
                if (foundAnyExternal)
                {
                    Vector3 probeOrigin = targetOrigin;
                    if (disp.sqrMagnitude > EPS)
                    {
                        Vector3 dir = disp.normalized;
                        float probeDist = Mathf.Max(disp.magnitude, minStepProbeDistance);
                        probeOrigin = rb.position + dir * probeDist;
                    }

                    float foundStep = StepChecker.FindValidStepHeight(
                        capsule,
                        probeOrigin,
                        maxStepHeight,
                        stepSearchIterations,
                        overlapBuffer,
                        selfColliderIds,
                        obsMask,
                        ms.headMask,
                        out bool canStep);

                    if (canStep && foundStep > EPS)
                    {
                        Vector3 steppedOrigin = targetOrigin + Vector3.up * foundStep;
                        if (!StepChecker.WouldCapsuleOverlap(capsule, steppedOrigin, obsMask | ms.headMask, overlapBuffer, selfColliderIds))
                        {
                            if (floorMask != 0)
                            {
                                Vector3 steppedCenter = capsule.transform.TransformPoint(capsule.center) + (steppedOrigin - capsule.transform.position);
                                Vector3 steppedBottom = steppedCenter - up * halfLine;
                                if (Physics.Raycast(steppedBottom + up * 0.01f, Vector3.down, out RaycastHit floorHit, floorCheckDepth + 0.01f, floorMask, QueryTriggerInteraction.Ignore))
                                {
                                    if (floorHit.normal.y >= floorThreshold)
                                    {
                                        lastAttemptedStepH = foundStep;
                                        MoveCapsuleDirect(steppedOrigin);
                                        return;
                                    }
                                    else if (debugLogs) Debug.Log($"[PlayerMovement] Step denied: floor normal too shallow {floorHit.normal.y:F3}");
                                }
                                else if (debugLogs) Debug.Log("[PlayerMovement] Step denied: no floor found under stepped position");
                            }
                            else if (debugLogs) Debug.Log("[PlayerMovement] Step denied: floorMask not set");
                        }
                        else if (debugLogs) Debug.Log("[PlayerMovement] Step denied: overlap after stepping (head/obstacle)");
                    }

                    lastAttemptedBlocked = true;
                    if (debugLogs) Debug.Log("[PlayerMovement] Movement blocked: obstacle overlap and cannot step.");
                    return;
                }
            }
        }

        // final apply
        if (disp.sqrMagnitude <= EPS) return;
        MoveCapsuleDirect(rb.position + disp);
    }

    private void MoveCapsuleDirect(Vector3 newPosition)
    {
        rb.MovePosition(newPosition);
    }

    // Slide + capsulecast movement (keeps same semantics)
    public void MoveFilteredDisplacement(Vector3 disp)
    {
        lastAttemptedDisp = disp;
        lastAttemptedBlocked = false;
        lastAttemptedStepH = 0f;

        if (rb == null || disp.sqrMagnitude <= EPS) return;

        var ms = movementSettings;
        LayerMask obsMask = ms.obstacleMask;
        float tinyDispThreshold = ms.tinyDispThreshold;
        float collisionSkin = ms.collisionSkin;
        float floorThreshold = ms.floorThreshold;

        if (disp.sqrMagnitude <= tinyDispThreshold * tinyDispThreshold)
        {
            MovePhysicsDisplacement(disp);
            return;
        }

        if (capsule == null)
        {
            MovePhysicsDisplacement(disp);
            return;
        }

        Vector3 remaining = disp;
        Vector3 totalMove = Vector3.zero;

        int maxIters = Mathf.Max(0, ms.slideIterations) + 1;
        for (int iter = 0; iter < maxIters; ++iter)
        {
            if (remaining.sqrMagnitude <= tinyDispThreshold * tinyDispThreshold) break;

            Vector3 origin = rb.position;
            Vector3 dir = remaining.normalized;
            float dist = remaining.magnitude;

            Transform t = capsule.transform;
            Vector3 worldCenterNow = t.TransformPoint(capsule.center) + (origin - t.position);

            float radius = capsule.radius;
            float height = capsule.height;
            float halfLine = Mathf.Max(height * 0.5f - radius, 0f);
            Vector3 up = t.up;
            Vector3 top = worldCenterNow + up * halfLine;
            Vector3 bottom = worldCenterNow - up * halfLine;

            RaycastHit hit;
            bool h = Physics.CapsuleCast(
                bottom,
                top,
                radius,
                dir,
                out hit,
                dist + collisionSkin,
                obsMask,
                QueryTriggerInteraction.Ignore);

            if (!h)
            {
                totalMove += remaining;
                remaining = Vector3.zero;
                break;
            }

            // treat gentle slope as floor
            if (hit.normal.y >= floorThreshold)
            {
                totalMove += remaining;
                remaining = Vector3.zero;
                break;
            }

            // attempt step near hit point
            if (hit.normal.y < floorThreshold)
            {
                Vector3 probeOrigin = hit.point - dir * 0.02f + Vector3.up * 0.02f;
                float probeDist = Mathf.Max(ms.minStepProbeDistance, 0.02f);
                Vector3 probeCandidate = rb.position + dir * probeDist;

                Vector3 tryOrigin = probeOrigin;
                float foundStep = StepChecker.FindValidStepHeight(
                    capsule,
                    tryOrigin,
                    ms.maxStepHeight,
                    Mathf.Max(1, ms.stepSearchIterations),
                    overlapBuffer,
                    selfColliderIds,
                    obsMask,
                    ms.headMask,
                    out bool canStep);

                if (!canStep)
                {
                    tryOrigin = probeCandidate;
                    foundStep = StepChecker.FindValidStepHeight(
                        capsule,
                        tryOrigin,
                        ms.maxStepHeight,
                        Mathf.Max(1, ms.stepSearchIterations),
                        overlapBuffer,
                        selfColliderIds,
                        obsMask,
                        ms.headMask,
                        out canStep);
                }

                if (canStep && foundStep > EPS)
                {
                    Vector3 targetOrigin = rb.position + disp;
                    Vector3 steppedOrigin = targetOrigin + Vector3.up * foundStep;
                    if (!StepChecker.WouldCapsuleOverlap(capsule, steppedOrigin, obsMask, overlapBuffer, selfColliderIds))
                    {
                        if (ms.floorMask != 0)
                        {
                            Vector3 steppedCenter = t.TransformPoint(capsule.center) + (steppedOrigin - t.position);
                            Vector3 steppedBottom = steppedCenter - up * halfLine;
                            if (Physics.Raycast(steppedBottom + Vector3.up * 0.01f, Vector3.down, out RaycastHit floorHit2, ms.floorCheckDepth + 0.01f, ms.floorMask, QueryTriggerInteraction.Ignore))
                            {
                                if (floorHit2.normal.y >= ms.floorThreshold)
                                {
                                    lastAttemptedStepH = foundStep;
                                    MoveCapsuleDirect(steppedOrigin);
                                    return;
                                }
                            }
                        }
                    }
                }
            }

            // slide logic
            float allowed = Mathf.Max(hit.distance - collisionSkin, 0f);
            Vector3 allowedPart = dir * allowed;
            totalMove += allowedPart;

            float leftover = dist - allowed;
            if (leftover <= tinyDispThreshold)
            {
                remaining = Vector3.zero;
                break;
            }

            Vector3 remainingAfter = remaining - allowedPart;
            Vector3 slide = Vector3.ProjectOnPlane(remainingAfter, hit.normal);

            if (slide.sqrMagnitude <= tinyDispThreshold * tinyDispThreshold)
            {
                remaining = Vector3.zero;
                break;
            }

            remaining = slide;
        }

        if (totalMove.sqrMagnitude > EPS) MovePhysicsDisplacement(totalMove);
    }

    private void HandleRotation(bool isARFiring)
    {
        // Attack/Knockback/Stun/Evade 중에는 회전 보정 스킵
        if (weaponCtrl != null &&
            (weaponCtrl.CurrentState == PlayerState.Attack ||
             weaponCtrl.CurrentState == PlayerState.Knockback ||
             weaponCtrl.CurrentState == PlayerState.Stun ||
             weaponCtrl.CurrentState == PlayerState.Evade))
            return;

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
        if (ms.floorMask == 0) return false;

        Vector3 centerWorld = transform.TransformPoint(capsule.center);
        float halfH = Mathf.Max(capsule.height * 0.5f - capsule.radius, 0f);
        Vector3 bottom = centerWorld - transform.up * halfH;
        float checkDist = ms.floorCheckDepth + 0.05f;
        if (Physics.Raycast(bottom + Vector3.up * 0.01f, Vector3.down, out RaycastHit hit, checkDist, ms.floorMask, QueryTriggerInteraction.Ignore))
            return hit.normal.y >= ms.floorThreshold;
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
        float speed = baseMoveSpeed;
        if (isARFiring && arAllowMove && weaponCtrl != null)
        {
            var arData = weaponCtrl.GetCurrentWeaponData() as WeaponDataSO_AR;
            if (arData != null) speed *= Mathf.Max(0f, arData.moveSpeedWhileFiring);
        }
        return speed;
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

    // Knockback (mass-aware)
    public void ApplyKnockback(Vector3 dir, float force, float duration, Transform attacker = null)
    {
        // 넉백 시작 자체는 허용한다.
        // (CC 중복 차단은 PlayerWeaponController.ForceApplyKnockback 진입부에서 처리)
        if (weaponCtrl != null)
        {
            if (weaponCtrl.CurrentState == PlayerState.Dead) return;
        }

        // Ensure facing matches knockback, but only when the knockback is starting.
        // If we call FaceKnockback every hit while knockback is already active,
        // multiple monsters around the player can keep changing the facing and look like "spin".
        if (!isKnockbacked)
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