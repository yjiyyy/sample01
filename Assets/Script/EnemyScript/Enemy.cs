using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy (MovementSettings-required)
/// - Uses MovementSettings for all movement/headroom/narrow-space tuning.
/// - If MovementSettings is not assigned, this component disables itself at Awake and logs an error.
/// - Reuses overlapBuffer and selfColliderIds for non-alloc checks.
/// </summary>
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

    // Headroom/local masks - kept only for inspector assignment but NOT used when MovementSettings is present.
    [Header("Headroom overrides (not used when MovementSettings assigned)")]
    [SerializeField] private LayerMask blockingMask;
    [SerializeField] private LayerMask headBlockMask;

    private Rigidbody rb;
    private CapsuleCollider capsule;

    // reuse buffers
    private Collider[] overlapBuffer;
    private HashSet<int> selfColliderIds;

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
}