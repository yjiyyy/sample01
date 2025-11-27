// Enemy.cs - 전체 파일 (수정됨: MovePhysicsDisplacement / MoveFilteredDisplacement 포함)
//
// 주의: 이 파일은 PlayerMovement의 해당 함수들과 동일한 로직을 재사용하도록 구성되어 있습니다.
// Unity6(6000.0.42f1) 환경, FixedUpdate 기반이며 MovementSettings를 사용합니다.

using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EnemyAnimationController))]
[RequireComponent(typeof(EnemyAttackController))]
[RequireComponent(typeof(EnemyAI))]
[RequireComponent(typeof(EnemyImpact))]
[RequireComponent(typeof(EnemyDeath))]
[DisallowMultipleComponent]
public class Enemy : MonoBehaviour
{
    public enum EnemyState { Chase, Attack, Knockback, Stunned, ShieldBreak, Dead }
    public EnemyState CurrentState { get; private set; } = EnemyState.Chase;

    [Header("Core refs")]
    public Animator animator;
    [HideInInspector] public EnemyAnimationController animCtrl;
    [HideInInspector] public EnemyAttackController attackCtrl;

    [Header("Sub-components")]
    public EnemyAI ai;
    public EnemyImpact impact;
    public EnemyDeath death;

    [Header("Common params")]
    [Tooltip("Base move speed (m/s)")]
    public float moveSpeed = 3.5f;
    public bool debugMode = true;

    [Header("Optional shared settings")]
    [Tooltip("MovementSettings asset (REQUIRED). If not assigned this component will be disabled.")]
    [SerializeField] private MovementSettings movementSettings;

    private Transform player;

    [SerializeField, Tooltip("Super armor flags")]
    private SuperArmorSource superArmorMask = SuperArmorSource.None;
    public bool HasSuperArmor => superArmorMask != SuperArmorSource.None;
    public bool HasSuperArmorSource(SuperArmorSource src) => (superArmorMask & src) != 0;

    // movement requests (from AI)
    private Vector3 desiredMoveDir = Vector3.zero;
    private float desiredSpeed01 = 0f;
    private bool hasMoveRequest = false;

    private Vector3 desiredLookDir = Vector3.zero;
    private bool hasLookRequest = false;

    private const float ROT_SPEED_DEG_PER_SEC = 720f;
    private const float EPS = 0.0001f;

    // Headroom/local masks - inspector assignment but MovementSettings is source of truth
    [Header("Headroom overrides (not used when MovementSettings assigned)")]
    [SerializeField] private LayerMask blockingMask;
    [SerializeField] private LayerMask headBlockMask;

    private Rigidbody rb;
    private CapsuleCollider capsule;

    // reuse buffers (initialized based on movementSettings)
    private Collider[] overlapBuffer;
    private HashSet<int> selfColliderIds;

    // -------------- Impact / ground-correction settings --------------
    [Header("Impact / Ground correction")]
    [Tooltip("한 프레임에 허용되는 최대 상승량 (m). 계단/경사를 부드럽게 오르기 위한 제한.")]
    public float impactMaxStepUp = 0.5f;
    [Tooltip("지면 검출용 Raycast 높이 (m)")]
    public float impactRaycastHeight = 1.0f;
    [Tooltip("지면 레이어 마스크(0이면 MovementSettings.floorMask 사용, 그래도 0이면 전체)")]
    public LayerMask impactGroundLayers = 0;
    [Tooltip("한 프레임에 허용되는 최대 하강량 (m). 음수값이면 하강 제한 없음(권장: 2.0)")]
    public float impactMaxDropLimit = 2.0f;
    [Tooltip("디버그 기즈모 표시")]
    public bool impactDebugGizmos = false;

    // debug helpers
    private Vector3 lastImpactCandidate = Vector3.zero;
    private Vector3 lastImpactHitPoint = Vector3.zero;
    private bool lastImpactHadHit = false;

    private void Awake()
    {
        animCtrl = GetComponent<EnemyAnimationController>();
        attackCtrl = GetComponent<EnemyAttackController>();
        if (animator == null) animator = GetComponent<Animator>();

        ai = GetComponent<EnemyAI>() ?? gameObject.AddComponent<EnemyAI>();
        impact = GetComponent<EnemyImpact>() ?? gameObject.AddComponent<EnemyImpact>();
        death = GetComponent<EnemyDeath>() ?? gameObject.AddComponent<EnemyDeath>();

        player = GameObject.FindWithTag("Player")?.transform;
        SetState(EnemyState.Chase, true);

        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        // MovementSettings required
        if (movementSettings == null)
        {
            Debug.LogError($"[{nameof(Enemy)}] MovementSettings not assigned on GameObject '{gameObject.name}'. Disabling component. Assign a MovementSettings asset to enable movement.");
            this.enabled = false;
            return;
        }

        // default masks if inspector provided them (but MovementSettings is source of truth)
        if (blockingMask == 0)
        {
            int g = LayerMask.NameToLayer("Ground");
            if (g >= 0) blockingMask = 1 << g;
        }
        if (headBlockMask == 0)
        {
            int g = LayerMask.NameToLayer("Ground");
            if (g >= 0) headBlockMask = 1 << g;
        }

        // init overlap buffer & self collider ids using MovementSettings
        int bufSize = Mathf.Max(1, movementSettings.overlapBufferSize);
        overlapBuffer = new Collider[Mathf.Max(1, bufSize)];
        var cols = GetComponentsInChildren<Collider>();
        selfColliderIds = new HashSet<int>(cols.Length);
        for (int i = 0; i < cols.Length; ++i)
            if (cols[i] != null) selfColliderIds.Add(cols[i].GetInstanceID());
    }

    private LayerMask GetBlockingMask() => movementSettings != null ? movementSettings.obstacleMask : blockingMask;
    private LayerMask GetHeadBlockMask() => movementSettings != null ? movementSettings.headMask : headBlockMask;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (GetComponent<EnemyAI>() == null) gameObject.AddComponent<EnemyAI>();
        if (GetComponent<EnemyImpact>() == null) gameObject.AddComponent<EnemyImpact>();
        if (GetComponent<EnemyDeath>() == null) gameObject.AddComponent<EnemyDeath>();
        ai = GetComponent<EnemyAI>();
        impact = GetComponent<EnemyImpact>();
        death = GetComponent<EnemyDeath>();
    }
#endif

    private void Update()
    {
        if (player == null) player = GameObject.FindWithTag("Player")?.transform;
        if (CurrentState == EnemyState.Dead || player == null) return;
        if (CurrentState == EnemyState.ShieldBreak) return;
        ai?.Tick(this, player);
    }

    private void FixedUpdate()
    {
        if (CurrentState == EnemyState.Dead) return;

        float dt = Time.fixedDeltaTime;

        if (hasMoveRequest && desiredMoveDir.sqrMagnitude > EPS && desiredSpeed01 > 0f &&
            CurrentState == EnemyState.Chase)
        {
            Vector3 dir = desiredMoveDir;
            dir.y = 0f;
            float speed = moveSpeed * Mathf.Clamp01(desiredSpeed01);
            Vector3 disp = dir.normalized * speed * dt;

            if (rb != null)
            {
                // 1) Narrow-space filtering (MovementSettings is the source of tuning)
                if (capsule != null)
                {
                    disp = NarrowSpaceSimpleUtil.FilterCapsuleDisplacement(
                        capsule,
                        rb.position,
                        disp,
                        GetBlockingMask(),
                        Mathf.Max(1, movementSettings.overlapIterations),
                        movementSettings.minFactorThreshold,
                        movementSettings.tinyDispThreshold
                    );
                }

                // 2) Headroom clamp: use MovementSettings head values
                if (capsule != null && movementSettings.headClampIterations > 0 && movementSettings.headPortion > 0f)
                {
                    disp = StepChecker.ClampHeadroomHorizontal(
                        capsule,
                        rb.position,
                        disp,
                        GetHeadBlockMask(),
                        Mathf.Max(1, movementSettings.headClampIterations),
                        movementSettings.headPortion,
                        movementSettings.headMargin,
                        overlapBuffer,
                        selfColliderIds
                    );
                }

                if (disp.sqrMagnitude > EPS) rb.MovePosition(rb.position + disp);
            }
            else
            {
                transform.position += disp;
            }
        }

        // rotation
        if (hasLookRequest && desiredLookDir.sqrMagnitude > EPS)
        {
            Vector3 ld = desiredLookDir; ld.y = 0f;
            Quaternion target = Quaternion.LookRotation(ld.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, ROT_SPEED_DEG_PER_SEC * dt);
        }

        // reset requests
        hasMoveRequest = false;
        hasLookRequest = false;
        desiredMoveDir = Vector3.zero;
        desiredSpeed01 = 0f;
        desiredLookDir = Vector3.zero;
    }

    public void SetState(EnemyState newState, bool force = false)
    {
        if (!force && CurrentState == newState) return;
        if (debugMode) Debug.Log($"[Enemy] State {CurrentState} → {newState}");
        CurrentState = newState;

        switch (newState)
        {
            case EnemyState.Chase:
                animCtrl?.SetSignedSpeed(0f);
                animCtrl?.PlayRun(crossFade: false, restart: false);
                break;
            case EnemyState.Attack:
                ai?.ForceClearBackstep();
                animCtrl?.SetSignedSpeed(0f);
                ai?.OnAttackStarted(this);
                break;
            case EnemyState.Knockback:
                ai?.ForceClearBackstep();
                animCtrl?.SetSignedSpeed(0f);
                break;
            case EnemyState.Stunned:
                ai?.ForceClearBackstep();
                animator?.Play("Stun", 0, 0f);
                animCtrl?.SetSignedSpeed(0f);
                break;
            case EnemyState.ShieldBreak:
                ai?.ForceClearBackstep();
                animCtrl?.SetSignedSpeed(0f);
                break;
            case EnemyState.Dead:
                ai?.ForceClearBackstep();
                ClearAllSuperArmor();
                animCtrl?.SetSignedSpeed(0f);
                break;
        }
    }

    public void ApplyKnockback(Vector3 hitDir, WeaponDataSO weapon, float impactScale = 1f)
    {
        if (CurrentState == EnemyState.Dead) return;
        bool allowInterrupt = !HasSuperArmor && CurrentState != EnemyState.ShieldBreak;
        if (allowInterrupt)
        {
            attackCtrl?.InterruptCooldown();
            ai?.InterruptAttack();
        }
        impact?.ApplyKnockback(this, hitDir, weapon, impactScale);
    }

    public void ApplyPush(Vector3 hitDir, WeaponDataSO weapon, float impactScale = 1f)
    {
        if (CurrentState == EnemyState.Dead) return;
        impact?.ApplyPush(this, hitDir, weapon, impactScale);
    }

    public void Die(Vector3 hitDir, WeaponDataSO weapon) => Die(hitDir, weapon, 1f);
    public void Die(Vector3 hitDir, WeaponDataSO weapon, float impactScale)
    {
        if (CurrentState == EnemyState.Dead) return;
        SetState(EnemyState.Dead, true);
        death?.PlayDeath(this, hitDir, weapon, impactScale);
    }

    public void RequestMove(Vector3 dir, float speed01)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude <= EPS || speed01 <= 0f) { hasMoveRequest = false; return; }
        desiredMoveDir = dir.normalized;
        desiredSpeed01 = Mathf.Clamp01(speed01);
        hasMoveRequest = true;
    }

    public void RequestLook(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude <= EPS) { hasLookRequest = false; return; }
        desiredLookDir = dir.normalized;
        hasLookRequest = true;
    }

    public void AddSuperArmor(SuperArmorSource src)
    {
        if (src == SuperArmorSource.None) return;
        superArmorMask |= src;
        if (debugMode) Debug.Log($"[Enemy] AddSuperArmor: {src} => {superArmorMask}");
    }

    public void RemoveSuperArmor(SuperArmorSource src)
    {
        if (src == SuperArmorSource.None) return;
        superArmorMask &= ~src;
        if (debugMode) Debug.Log($"[Enemy] RemoveSuperArmor: {src} => {superArmorMask}");
    }

    public void ClearAllSuperArmor()
    {
        superArmorMask = SuperArmorSource.None;
        if (debugMode) Debug.Log("[Enemy] ClearAllSuperArmor");
    }

    // ------------------- Movement helpers (PlayerMovement-parity) -------------------
    // These largely mirror PlayerMovement.MovePhysicsDisplacement & MoveFilteredDisplacement
    // so Enemy movement/impact uses the same collision/step/headroom semantics as Player.

    // Apply final position via rb.MovePosition
    private void MoveCapsuleDirect(Vector3 newPosition)
    {
        if (rb != null)
            rb.MovePosition(newPosition);
        else
            transform.position = newPosition;
    }

    // Simpler physics displacement path (uses movementSettings parameters).
    public void MovePhysicsDisplacement(Vector3 disp)
    {
        // mirrored from PlayerMovement.MovePhysicsDisplacement
        // preserves lastAttempt variables in PlayerMovement for gizmos; here we skip those debug fields.
        if (rb == null || disp.sqrMagnitude <= EPS) return;

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

        bool strictHeadroomBlock = ms.strictHeadroomBlock;

        // 1) strict headroom block
        if (capsule != null && strictHeadroomBlock && headClampIterations > 0 && headPortion > 0f && headMask != 0)
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
                // blocked by headroom -> abort movement
                if (debugMode) Debug.Log("[EnemyMovement] Movement blocked by strict headroom (target head overlap).");
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
                    ms.pushableMassMultiplier
                );

                // all external pushable -> evaluate crowd
                if (summary.externalCount > 0 && !summary.anyUnpushable)
                {
                    bool crowdBlocks = false;
                    if (summary.totalPushableMass > rb.mass * ms.crowdMassThresholdMultiplier) crowdBlocks = true;
                    if (summary.pushableCount >= ms.crowdCountThreshold) crowdBlocks = true;

                    if (crowdBlocks)
                    {
                        if (debugMode) Debug.Log($"[EnemyMovement] Movement blocked by crowd resistance: totalMass={summary.totalPushableMass:F2}, count={summary.pushableCount}");
                        return;
                    }
                    else
                    {
                        // allow movement and optionally push impulse to overlapped bodies
                        MoveCapsuleDirect(rb.position + disp);

                        if (ms.pushImpulseFactor > 0f)
                        {
                            float impulseBase = Mathf.Clamp01(disp.magnitude) * ms.pushImpulseFactor;
                            MovementPhysics.ApplyPushImpulseToOverlap(overlapBuffer, summary.rawCount, summary.fallbackHits, selfColliderIds, rb, ms.pushableMassMultiplier, impulseBase);
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
                            if (ms.floorMask != 0)
                            {
                                Vector3 steppedCenter = capsule.transform.TransformPoint(capsule.center) + (steppedOrigin - capsule.transform.position);
                                Vector3 steppedBottom = steppedCenter - up * halfLine;
                                if (Physics.Raycast(steppedBottom + up * 0.01f, Vector3.down, out RaycastHit floorHit, floorCheckDepth + 0.01f, ms.floorMask, QueryTriggerInteraction.Ignore))
                                {
                                    if (floorHit.normal.y >= ms.floorThreshold)
                                    {
                                        MoveCapsuleDirect(steppedOrigin);
                                        return;
                                    }
                                    else if (debugMode) Debug.Log($"[EnemyMovement] Step denied: floor normal too shallow {floorHit.normal.y:F3}");
                                }
                                else if (debugMode) Debug.Log("[EnemyMovement] Step denied: no floor found under stepped position");
                            }
                            else if (debugMode) Debug.Log("[EnemyMovement] Step denied: floorMask not set");
                        }
                        else if (debugMode) Debug.Log("[EnemyMovement] Step denied: overlap after stepping (head/obstacle)");
                    }

                    if (debugMode) Debug.Log("[EnemyMovement] Movement blocked: obstacle overlap and cannot step.");
                    return;
                }
            }
        }

        // final apply
        if (disp.sqrMagnitude <= EPS) return;
        MoveCapsuleDirect(rb.position + disp);
    }

    // Slide + capsulecast movement - mirrors PlayerMovement.MoveFilteredDisplacement
    public void MoveFilteredDisplacement(Vector3 disp)
    {
        if (rb == null || disp.sqrMagnitude <= EPS)
        {
            MovePhysicsDisplacement(disp);
            return;
        }

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

    /// <summary>
    /// Unified helper used by EnemyImpact: decide whether to use filtered path or simple physics path.
    /// This ensures headroom/step/slide logic is applied for large displacements.
    /// </summary>
    public void MoveWithGroundCheck(Vector3 disp, float maxStepUp = -1f, float raycastHeight = -1f)
    {
        if (disp.sqrMagnitude <= EPS) return;

        // Prefer using MoveFilteredDisplacement for larger displacements (so capsule cast + slide + step works)
        var ms = movementSettings;
        float tiny = (ms != null) ? ms.tinyDispThreshold : 0.01f;
        if (disp.sqrMagnitude <= tiny * tiny)
        {
            MovePhysicsDisplacement(disp);
            return;
        }

        // If capsule or movementSettings missing, fallback to simple projection.
        if (rb == null || capsule == null || movementSettings == null)
        {
            // fallback with basic ground-projection + clamp like earlier implementation
            float stepUp = (maxStepUp > 0f) ? maxStepUp : Mathf.Max(0f, impactMaxStepUp);
            float rcHeight = (raycastHeight > 0f) ? raycastHeight : Mathf.Max(0.01f, impactRaycastHeight);

            LayerMask layers = impactGroundLayers;
            if (layers == 0 && movementSettings != null) layers = movementSettings.floorMask;
            if (layers == 0) layers = ~0;

            Vector3 currentPos = rb != null ? rb.position : transform.position;
            Vector3 candidate = currentPos + disp;

            Vector3 castOrigin = candidate + Vector3.up * rcHeight;
            if (Physics.Raycast(castOrigin, Vector3.down, out RaycastHit hit, rcHeight * 2f, layers, QueryTriggerInteraction.Ignore))
            {
                float targetY = hit.point.y;
                float deltaY = targetY - currentPos.y;
                if (deltaY > stepUp) targetY = currentPos.y + stepUp;
                if (deltaY < -impactMaxDropLimit) targetY = currentPos.y - impactMaxDropLimit;
                candidate.y = targetY;
            }
            else
            {
                candidate.y = currentPos.y + disp.y;
            }

            MoveCapsuleDirect(candidate);
            return;
        }

        // Use filtered displacement pipeline (same semantics as PlayerMovement)
        MoveFilteredDisplacement(disp);
    }

    void OnDrawGizmosSelected()
    {
        if (!impactDebugGizmos) return;

        Gizmos.color = lastImpactHadHit ? Color.green : Color.red;
        Gizmos.DrawSphere(lastImpactCandidate, 0.05f);

        if (lastImpactHadHit)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(lastImpactHitPoint, 0.06f);
            Gizmos.DrawLine(lastImpactCandidate + Vector3.up * 0.2f, lastImpactHitPoint + Vector3.up * 0.2f);
        }
    }
}