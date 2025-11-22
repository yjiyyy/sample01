using System;
using UnityEngine;

/// <summary>
/// NavMeshAgent 제거 버전 Enemy
/// - 이동/회전은 AI가 RequestMove/RequestLook로 지시 → FixedUpdate에서 적용
/// - 모든 강제 이동(Knockback/Push/Rush)은 Transform 기반 + Time.fixedDeltaTime
/// - 좁은 공간 진입 차단 필터 + 낮은 천장(Headroom) 클램프를 함께 적용
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

    [Header("공통 파라미터")]
    [Tooltip("기본 이동 속도(m/s)")]
    public float moveSpeed = 3.5f;
    public bool debugMode = true;

    private Transform player;

    [SerializeField, Tooltip("디버그 확인용 슈퍼아머 마스크")]
    private SuperArmorSource superArmorMask = SuperArmorSource.None;
    public bool HasSuperArmor => superArmorMask != SuperArmorSource.None;
    public bool HasSuperArmorSource(SuperArmorSource src) => (superArmorMask & src) != 0;

    // 이동/회전 버퍼
    private Vector3 desiredMoveDir = Vector3.zero;
    private float desiredSpeed01 = 0f;
    private bool hasMoveRequest = false;

    private Vector3 desiredLookDir = Vector3.zero;
    private bool hasLookRequest = false;

    private const float ROT_SPEED_DEG_PER_SEC = 720f;
    private const float EPS = 0.0001f;

    // ---------------- 좁은 공간(진입 차단) ----------------
    [Header("좁은 공간 차단")]
    [SerializeField] private LayerMask blockingMask;
    [SerializeField, Range(1, 4)] private int overlapIterations = 2;
    [SerializeField, Range(0f, 0.2f)] private float minFactorThreshold = 0.05f;
    [SerializeField, Range(0f, 0.01f)] private float tinyDispThreshold = 0.001f;

    // ---------------- Headroom(낮은 천장) 충돌 관련 ----------------
    [Header("Headroom(낮은 천장) 충돌")]
    [Tooltip("Enemy 머리 공간을 막는 레이어 (Ground 레이어 할당)")]
    [SerializeField] private LayerMask headBlockMask;
    [Tooltip("Enemy 머리 검사 영역 비율(상단 cylindrical 40%)")]
    [SerializeField, Range(0.2f, 0.6f)] private float headPortion = 0.4f;
    [Tooltip("머리 캡슐 반경 감소량")]
    [SerializeField, Range(0f, 0.05f)] private float headMargin = 0.01f;
    [SerializeField, Range(1, 3)] private int headClampIterations = 2;

    private Rigidbody rb;
    private CapsuleCollider capsule;
    // --------------------------------------------------------------

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

        // Rigidbody / Capsule 초기화
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        // 기본 레이어 자동 할당
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
    }

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
    }
#endif

    private void Update()
    {
        if (player == null)
            player = GameObject.FindWithTag("Player")?.transform;

        if (CurrentState == EnemyState.Dead || player == null) return;
        if (CurrentState == EnemyState.ShieldBreak) return;

        ai?.Tick(this, player);
    }

    private void FixedUpdate()
    {
        if (CurrentState == EnemyState.Dead) return;

        float dt = Time.fixedDeltaTime;

        // 이동
        if (hasMoveRequest && desiredMoveDir.sqrMagnitude > EPS && desiredSpeed01 > 0f &&
            CurrentState == EnemyState.Chase)
        {
            Vector3 dir = desiredMoveDir;
            dir.y = 0f;
            float speed = moveSpeed * Mathf.Clamp01(desiredSpeed01);
            Vector3 disp = dir.normalized * speed * dt;

            if (rb != null)
            {
                // 1) 좁은 공간 진입 차단 필터 (Overlap 기반 경계 탐색)
                if (capsule != null)
                {
                    disp = NarrowSpaceSimpleUtil.FilterCapsuleDisplacement(
                        capsule,
                        rb.position,
                        disp,
                        blockingMask,
                        overlapIterations,
                        minFactorThreshold,
                        tinyDispThreshold
                    );
                }

                // 2) 낮은 천장 진입 클램프 (머리 영역만)
                if (capsule != null && headClampIterations > 0 && headPortion > 0f)
                {
                    disp = NarrowSpaceUtil.ClampHeadroomHorizontal(
                        capsule,
                        rb.position,
                        disp,
                        headBlockMask,
                        headClampIterations,
                        headPortion,
                        headMargin
                    );
                }

                if (disp.sqrMagnitude > EPS)
                    rb.MovePosition(rb.position + disp);
            }
            else
            {
                // Rigidbody 없으면 Transform 이동
                transform.position += disp;
            }
        }

        // 회전
        if (hasLookRequest && desiredLookDir.sqrMagnitude > EPS)
        {
            Vector3 ld = desiredLookDir;
            ld.y = 0f;
            Quaternion target = Quaternion.LookRotation(ld.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, ROT_SPEED_DEG_PER_SEC * dt);
        }

        // 요청 플래그 리셋
        hasMoveRequest = false;
        hasLookRequest = false;
        desiredMoveDir = Vector3.zero;
        desiredSpeed01 = 0f;
        desiredLookDir = Vector3.zero;
    }

    public void SetState(EnemyState newState, bool force = false)
    {
        if (!force && CurrentState == newState) return;

        if (debugMode)
            Debug.Log($"[Enemy] State {CurrentState} → {newState}");

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

    public void SetAttackState() => SetState(EnemyState.Attack);
    public void SetChaseState() => SetState(EnemyState.Chase);

    public bool IsShieldBreaking()
    {
        if (TryGetComponent(out EnemyHealth h)) return h.IsShieldBreak();
        return false;
    }

    // 이동/회전 요청 API (AI 사용)
    public void RequestMove(Vector3 dir, float speed01)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude <= EPS || speed01 <= 0f)
        {
            hasMoveRequest = false;
            return;
        }
        desiredMoveDir = dir.normalized;
        desiredSpeed01 = Mathf.Clamp01(speed01);
        hasMoveRequest = true;
    }

    public void RequestLook(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude <= EPS)
        {
            hasLookRequest = false;
            return;
        }
        desiredLookDir = dir.normalized;
        hasLookRequest = true;
    }

    // SuperArmor 관리
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