using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy (MovementSettings-required)
/// - MovementSettings SO is the source of truth for movement/headroom/obstacle masks. 
/// - Super-armor/state related to shield is now handled by EnemyHealth (currentShield > 0f).
/// - Death logic is delegated to EnemyDie component.
/// </summary>
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
    public EnemyDie dieCtrl;

    [Header("Common params")]
    [Tooltip("Base move speed (m/s)")]
    public float moveSpeed = 3.5f;
    public bool debugMode = true;

    [Header("Optional shared settings (REQUIRED)")]
    [Tooltip("MovementSettings asset (REQUIRED). If not assigned this component will be disabled.")]
    [SerializeField] private MovementSettings movementSettings;

    private Transform player;

    private Vector3 desiredMoveDir = Vector3.zero;
    private float desiredSpeed01 = 0f;
    private bool hasMoveRequest = false;

    private Vector3 desiredLookDir = Vector3.zero;
    private bool hasLookRequest = false;

    private bool lookLockActive = false;
    private Vector3 lockedLookDir = Vector3.forward;
    private float lookLockExpireTime = -1f;

    private HashSet<SuperArmorSource> manualSuperArmor = new HashSet<SuperArmorSource>();

    private const float ROT_SPEED_DEG_PER_SEC = 720f;
    private const float EPS = 0.0001f;

    private Rigidbody rb;
    private CapsuleCollider capsule;

    private Collider[] overlapBuffer;
    private HashSet<int> selfColliderIds;

    private void Awake()
    {
        animCtrl = GetComponent<EnemyAnimationController>();
        attackCtrl = GetComponent<EnemyAttackController>();
        if (animator == null) animator = GetComponent<Animator>();

        ai = GetComponent<EnemyAI>() ?? gameObject.AddComponent<EnemyAI>();
        impact = GetComponent<EnemyImpact>() ?? gameObject.AddComponent<EnemyImpact>();
        dieCtrl = GetComponent<EnemyDie>() ?? gameObject.AddComponent<EnemyDie>();

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

        if (dieCtrl != null)
        {
            dieCtrl.animator = animator;
            dieCtrl.rootRb = rb;
            dieCtrl.rootCollider = capsule != null ? (Collider)capsule : GetComponent<Collider>();
            dieCtrl.excludeRoot = this.transform;
        }

        if (movementSettings == null)
        {
            Debug.LogError($"[{nameof(Enemy)}] MovementSettings not assigned on GameObject '{gameObject.name}'.  Disabling Enemy component.  Assign a MovementSettings asset to enable movement.");
            this.enabled = false;
            return;
        }

        int bufSize = Mathf.Max(1, movementSettings.overlapBufferSize);
        overlapBuffer = new Collider[Mathf.Max(1, bufSize)];
        var cols = GetComponentsInChildren<Collider>();
        selfColliderIds = new HashSet<int>(cols.Length);
        for (int i = 0; i < cols.Length; ++i)
            if (cols[i] != null) selfColliderIds.Add(cols[i].GetInstanceID());
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (GetComponent<EnemyAI>() == null) gameObject.AddComponent<EnemyAI>();
        if (GetComponent<EnemyImpact>() == null) gameObject.AddComponent<EnemyImpact>();
        if (GetComponent<EnemyDie>() == null) gameObject.AddComponent<EnemyDie>();
        ai = GetComponent<EnemyAI>();
        impact = GetComponent<EnemyImpact>();
        dieCtrl = GetComponent<EnemyDie>();
    }
#endif

    private void Update()
    {
        // ✅ 플레이어 타겟 갱신 (죽은 플레이어는 타겟으로 취급하지 않음)
        if (player == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null)
            {
                var ph = p.GetComponent<PlayerHealth>() ?? p.GetComponentInChildren<PlayerHealth>();
                if (ph != null && ph.GetCurrentHP() <= 0f)
                {
                    // 죽은 플레이어는 타겟으로 잡지 않음
                    player = null;
                }
                else
                {
                    player = p.transform;
                }
            }
        }
        else
        {
            // 이미 캐시된 타겟도 죽었는지 재검사 (죽었다면 즉시 해제)
            var ph = player.GetComponent<PlayerHealth>() ?? player.GetComponentInChildren<PlayerHealth>();
            if (ph != null && ph.GetCurrentHP() <= 0f)
            {
                player = null;
            }
        }

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
                if (disp.sqrMagnitude > EPS) rb.MovePosition(rb.position + disp);
            }
            else
            {
                transform.position += disp;
            }
        }

        if (lookLockActive)
        {
            Quaternion lockedQ = Quaternion.LookRotation(lockedLookDir, Vector3.up);
            if (rb != null)
                rb.MoveRotation(lockedQ);
            else
                transform.rotation = lockedQ;
        }
        else if (hasLookRequest && desiredLookDir.sqrMagnitude > EPS)
        {
            Vector3 ld = desiredLookDir; ld.y = 0f;
            Quaternion target = Quaternion.LookRotation(ld.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, ROT_SPEED_DEG_PER_SEC * dt);
        }

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
                attackCtrl?.InterruptCooldown();
                if (animator) animator.speed = 1f;
                ai?.ForceClearBackstep();
                animCtrl?.SetSignedSpeed(0f);
                break;

            case EnemyState.Stunned:
                attackCtrl?.InterruptCooldown();
                if (animator)
                {
                    animator.speed = 1f;
                    animator.Play("Stun", 0, 0f);
                }
                ai?.ForceClearBackstep();
                animCtrl?.SetSignedSpeed(0f);
                break;

            case EnemyState.ShieldBreak:
                attackCtrl?.InterruptCooldown();
                if (animator) animator.speed = 1f;
                ai?.ForceClearBackstep();
                animCtrl?.SetSignedSpeed(0f);
                break;

            case EnemyState.Dead:
                attackCtrl?.InterruptCooldown();
                if (animator) animator.speed = 1f;
                UnlockLookDirection();
                ai?.ForceClearBackstep();
                animCtrl?.SetSignedSpeed(0f);
                break;
        }
    }

    public void ApplyKnockback(Vector3 hitDir, WeaponDataSO weapon, float impactScale = 1f)
    {
        if (CurrentState == EnemyState.Dead) return;

        var health = GetComponent<EnemyHealth>();
        bool hasSuperArmor = (health != null && health.HasSuperArmor) || (manualSuperArmor != null && manualSuperArmor.Count > 0);

        bool allowInterrupt = !hasSuperArmor && CurrentState != EnemyState.ShieldBreak;
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

        var mode = weapon != null ? weapon.deathMode : DeathMode.Animation;

        if (mode == DeathMode.Animation)
        {
            FaceHitDirectionImmediate(hitDir);
        }

        SetState(EnemyState.Dead, true);

        if (dieCtrl != null)
        {
            dieCtrl.Die(hitDir, weapon, impactScale);
        }
        else
        {
            animator?.SetTrigger("Die");
            Destroy(this.gameObject, 7f);
        }
    }

    private void FaceHitDirectionImmediate(Vector3 hitDir)
    {
        Vector3 look = -hitDir;
        look.y = 0f;

        if (look.sqrMagnitude < 0.0001f) return;
        look.Normalize();

        Quaternion faceQ = Quaternion.LookRotation(look, Vector3.up);

        transform.rotation = faceQ;
        if (rb != null)
        {
            rb.rotation = faceQ;
        }
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
        if (lookLockActive) { hasLookRequest = false; return; }

        dir.y = 0f;
        if (dir.sqrMagnitude <= EPS) { hasLookRequest = false; return; }
        desiredLookDir = dir.normalized;
        hasLookRequest = true;
    }

    private void MoveCapsuleDirect(Vector3 newPosition)
    {
        if (rb != null)
            rb.MovePosition(newPosition);
        else
            transform.position = newPosition;
    }

    public void MovePhysicsDisplacement(Vector3 disp)
    {
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
        float tinyDispThreshold = ms.tinyDispThreshold;

        float maxStepHeight = ms.maxStepHeight;
        int stepSearchIterations = Mathf.Max(1, ms.stepSearchIterations);
        float floorCheckDepth = ms.floorCheckDepth;
        float minStepProbeDistance = ms.minStepProbeDistance;

        bool strictHeadroomBlock = ms.strictHeadroomBlock;

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
                if (debugMode) Debug.Log("[EnemyMovement] Movement blocked by strict headroom (target head overlap).");
                return;
            }
        }

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

                if (summary.externalCount > 0 && !summary.anyUnpushable)
                {
                    bool crowdBlocks = false;
                    if (summary.totalPushableMass > rb.mass * ms.crowdMassThresholdMultiplier) crowdBlocks = true;
                    if (summary.pushableCount >= ms.crowdCountThreshold) crowdBlocks = true;

                    if (crowdBlocks)
                    {
                        if (debugMode) Debug.Log($"[EnemyMovement] Movement blocked by crowd resistance:  totalMass={summary.totalPushableMass:F2}, count={summary.pushableCount}");
                        return;
                    }
                    else
                    {
                        MoveCapsuleDirect(rb.position + disp);

                        if (ms.pushImpulseFactor > 0f)
                        {
                            float impulseBase = Mathf.Clamp01(disp.magnitude) * ms.pushImpulseFactor;
                            MovementPhysics.ApplyPushImpulseToOverlap(overlapBuffer, summary.rawCount, summary.fallbackHits, selfColliderIds, rb, ms.pushableMassMultiplier, impulseBase);
                        }
                        return;
                    }
                }

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
                                if (Physics.Raycast(steppedBottom + up * 0.01f, Vector3.down, out RaycastHit floorHit, ms.floorCheckDepth + 0.01f, ms.floorMask, QueryTriggerInteraction.Ignore))
                                {
                                    if (floorHit.normal.y >= ms.floorThreshold)
                                    {
                                        MoveCapsuleDirect(steppedOrigin);
                                        return;
                                    }
                                    else if (debugMode) Debug.Log($"[EnemyMovement] Step denied:  floor normal too shallow {floorHit.normal.y:F3}");
                                }
                                else if (debugMode) Debug.Log("[EnemyMovement] Step denied: no floor found under stepped position");
                            }
                            else if (debugMode) Debug.Log("[EnemyMovement] Step denied: floorMask not set");
                        }
                        else if (debugMode) Debug.Log("[EnemyMovement] Step denied: overlap after stepping (head/obstacle)");
                    }

                    if (debugMode) Debug.Log("[EnemyMovement] Movement blocked:  obstacle overlap and cannot step.");
                    return;
                }
            }
        }

        if (disp.sqrMagnitude <= EPS) return;
        MoveCapsuleDirect(rb.position + disp);
    }

    public void MoveFilteredDisplacement(Vector3 disp)
    {
        if (rb == null || disp.sqrMagnitude <= EPS)
        {
            MovePhysicsDisplacement(disp);
            return;
        }

        var ms = movementSettings;
        float tinyDispThreshold = ms.tinyDispThreshold;

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
                dist + ms.collisionSkin,
                ms.obstacleMask,
                QueryTriggerInteraction.Ignore);

            if (!h)
            {
                totalMove += remaining;
                remaining = Vector3.zero;
                break;
            }

            if (hit.normal.y >= ms.floorThreshold)
            {
                totalMove += remaining;
                remaining = Vector3.zero;
                break;
            }

            if (hit.normal.y < ms.floorThreshold)
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
                    ms.obstacleMask,
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
                        ms.obstacleMask,
                        ms.headMask,
                        out canStep);
                }

                if (canStep && foundStep > EPS)
                {
                    Vector3 targetOrigin = rb.position + disp;
                    Vector3 steppedOrigin = targetOrigin + Vector3.up * foundStep;
                    if (!StepChecker.WouldCapsuleOverlap(capsule, steppedOrigin, ms.obstacleMask, overlapBuffer, selfColliderIds))
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

            float allowed = Mathf.Max(hit.distance - ms.collisionSkin, 0f);
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

    public void AddSuperArmor(SuperArmorSource src)
    {
        if (src == SuperArmorSource.None) return;
        manualSuperArmor.Add(src);
    }

    public void RemoveSuperArmor(SuperArmorSource src)
    {
        if (src == SuperArmorSource.None) return;
        manualSuperArmor.Remove(src);
    }

    public void ClearAllSuperArmor()
    {
        manualSuperArmor.Clear();
    }

    // Public helpers to query manual/combined super-armor state
    public bool HasManualSuperArmor()
    {
        return manualSuperArmor != null && manualSuperArmor.Count > 0;
    }

    public bool HasAnySuperArmor()
    {
        var health = GetComponent<EnemyHealth>();
        bool healthSA = (health != null && health.HasSuperArmor);
        return healthSA || HasManualSuperArmor();
    }

    public void LockLookDirection(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f) dir = transform.forward;
        lockedLookDir = dir.normalized;
        lookLockActive = true;
        lookLockExpireTime = -1f;
    }

    public void LockLookDirection(Vector3 dir, float duration)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f) dir = transform.forward;
        lockedLookDir = dir.normalized;
        lookLockActive = true;
        if (duration > 0f)
            lookLockExpireTime = Time.time + duration;
        else
            lookLockExpireTime = -1f;
    }

    public void UnlockLookDirection()
    {
        lookLockActive = false;
        lookLockExpireTime = -1f;
    }

    private void LateUpdate()
    {
        if (!lookLockActive) return;

        if (lookLockExpireTime >= 0f && Time.time >= lookLockExpireTime)
        {
            UnlockLookDirection();
            return;
        }

        transform.rotation = Quaternion.LookRotation(lockedLookDir, Vector3.up);
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (!lookLockActive) return;

        if (lookLockExpireTime >= 0f && Time.time >= lookLockExpireTime)
        {
            UnlockLookDirection();
            return;
        }

        Quaternion lockedQ = Quaternion.LookRotation(lockedLookDir, Vector3.up);
        if (rb != null)
            rb.MoveRotation(lockedQ);
        else
            transform.rotation = lockedQ;
    }

    public bool IsLookLocked => lookLockActive;
}