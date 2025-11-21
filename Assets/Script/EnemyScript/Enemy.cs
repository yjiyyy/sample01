using System;
using UnityEngine;

/// <summary>
/// NavMeshAgent 제거 버전 Enemy
/// - 이동/회전은 AI가 RequestMove/RequestLook로 지시 → FixedUpdate에서 적용
/// - 모든 강제 이동(Knockback/Push/Rush)은 Transform 기반 + Time.fixedDeltaTime
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
            transform.position += dir.normalized * speed * dt;
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