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
    // Backstep 상태 추가
    public enum EnemyState { Chase, Attack, Backstep, Knockback, Stunned, ShieldBreak, Dead }
    public EnemyState CurrentState { get; private set; } = EnemyState.Chase;

    [Header("Core refs")]
    public Animator animator; // Inspector 연결 권장
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

    // SuperArmor 마스크
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
        if (CurrentState == EnemyState.ShieldBreak) return; // 그로기 동안 AI 비활성
        ai?.Tick(this, player);
    }

    public void SetState(EnemyState newState, bool force = false)
    {
        if (!force && CurrentState == newState) return;

        if (debugMode) Debug.Log($"[Enemy] State {CurrentState} → {newState}");
        CurrentState = newState;

        switch (newState)
        {
            case EnemyState.Chase:
                if (agent && agent.isOnNavMesh) agent.isStopped = false;
                break;

            case EnemyState.Attack:
                if (agent && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                    agent.ResetPath();
                }
                ai?.OnAttackStarted(this);
                break;

            case EnemyState.Backstep:
                if (agent && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                    agent.ResetPath();
                }
                // 애니는 EnemyAI에서 직접 Play("Backstep")
                break;

            case EnemyState.Knockback:
                if (agent && agent.isOnNavMesh) agent.isStopped = true;
                break;

            case EnemyState.Stunned:
                if (animator) animator.Play("Stun", 0, 0f);
                if (agent && agent.isOnNavMesh) agent.isStopped = true;
                break;

            case EnemyState.ShieldBreak:
                if (agent && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                    agent.ResetPath();
                }
                break;

            case EnemyState.Dead:
                if (agent) agent.enabled = false;
                ClearAllSuperArmor();
                break;
        }
    }

    // 넉백 적용
    public void ApplyKnockback(Vector3 hitDir, WeaponDataSO weapon, float impactScale = 1f)
    {
        if (CurrentState == EnemyState.Dead) return;

        bool allowInterrupt = !HasSuperArmor && CurrentState != EnemyState.ShieldBreak;
        if (allowInterrupt)
        {
            attackCtrl?.InterruptCooldown();
            ai?.InterruptAttack(); // Backstep/Attack 모두 중단
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

    // 편의 (기존 호출부 있을 수 있음)
    public void SetAttackState() => SetState(EnemyState.Attack);
    public void SetChaseState() => SetState(EnemyState.Chase);

    public bool IsShieldBreaking()
    {
        if (TryGetComponent(out EnemyHealth h)) return h.IsShieldBreak();
        return false;
    }
}