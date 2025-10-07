using System;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
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
    [HideInInspector] public NavMeshAgent agent;
    [HideInInspector] public EnemyAnimationController animCtrl;
    [HideInInspector] public EnemyAttackController attackCtrl;

    [Header("Sub-components")]
    public EnemyAI ai;
    public EnemyImpact impact;
    public EnemyDeath death;

    [Header("공통 파라미터")]
    public float moveSpeed = 3.5f;
    public bool debugMode = true;

    private Transform player;

    [SerializeField, Tooltip("디버그 확인용")] private SuperArmorSource superArmorMask = SuperArmorSource.None;
    public bool HasSuperArmor => superArmorMask != SuperArmorSource.None;
    public bool HasSuperArmorSource(SuperArmorSource src) => (superArmorMask & src) != 0;

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

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animCtrl = GetComponent<EnemyAnimationController>();
        attackCtrl = GetComponent<EnemyAttackController>();
        if (animator == null) animator = GetComponent<Animator>();

        ai = GetComponent<EnemyAI>() ?? gameObject.AddComponent<EnemyAI>();
        impact = GetComponent<EnemyImpact>() ?? gameObject.AddComponent<EnemyImpact>();
        death = GetComponent<EnemyDeath>() ?? gameObject.AddComponent<EnemyDeath>();

        agent.speed = moveSpeed;
        agent.updateRotation = false;

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

    public void SetState(EnemyState newState, bool force = false)
    {
        if (!force && CurrentState == newState) return;

        if (debugMode)
            Debug.Log($"[Enemy] State {CurrentState} → {newState}");

        CurrentState = newState;

        switch (newState)
        {
            case EnemyState.Chase:
                if (agent && agent.isOnNavMesh) agent.isStopped = false;
                animCtrl?.SetSignedSpeed(0f); // 다음 프레임 AI가 갱신
                animCtrl?.PlayRun(crossFade: false, restart: false);
                break;

            case EnemyState.Attack:
                ai?.ForceClearBackstep();
                if (agent && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                    agent.ResetPath();
                }
                animCtrl?.SetSignedSpeed(0f);
                ai?.OnAttackStarted(this);
                break;

            case EnemyState.Knockback:
                ai?.ForceClearBackstep();
                if (agent && agent.isOnNavMesh) agent.isStopped = true;
                animCtrl?.SetSignedSpeed(0f);
                break;

            case EnemyState.Stunned:
                ai?.ForceClearBackstep();
                if (animator) animator.Play("Stun", 0, 0f);
                if (agent && agent.isOnNavMesh) agent.isStopped = true;
                animCtrl?.SetSignedSpeed(0f);
                break;

            case EnemyState.ShieldBreak:
                ai?.ForceClearBackstep();
                if (agent && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                    agent.ResetPath();
                }
                animCtrl?.SetSignedSpeed(0f);
                break;

            case EnemyState.Dead:
                ai?.ForceClearBackstep();
                if (agent) agent.enabled = false;
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
}