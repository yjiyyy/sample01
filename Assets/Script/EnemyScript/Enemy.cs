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
    public enum EnemyState { Chase, Attack, Knockback, Stunned, Dead }
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
                if (animator) animator.Play("Run", 0, 0f);
                if (agent.isOnNavMesh) agent.isStopped = false;
                break;

            case EnemyState.Attack:
                if (animator) animator.Play("Attack", 0, 0f);
                if (agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero; // ⭐ 이동속도까지 강제로 0
                    agent.ResetPath();             // ⭐ 목적지도 초기화
                }
                ai?.OnAttackStarted(this);
                break;

            case EnemyState.Knockback:
                if (agent.isOnNavMesh) agent.isStopped = true;
                break;

            case EnemyState.Stunned:
                if (animator) animator.Play("Stun", 0, 0f);
                if (agent.isOnNavMesh) agent.isStopped = true;
                break;

            case EnemyState.Dead:
                if (agent) agent.enabled = false;
                break;
        }
    }

    // 외부 공개 API (기존 호환)
    public void OnDamage(Vector3 hitDir, WeaponDataSO weapon, float impactScale = 1f)
    {
        if (CurrentState == EnemyState.Dead) return;
        attackCtrl?.InterruptCooldown();
        ai?.InterruptAttack();
        impact?.OnDamage(this, hitDir, weapon, impactScale);
    }

    public void ApplyKnockback(Vector3 dir, WeaponDataSO weapon)
    {
        if (CurrentState == EnemyState.Dead) return;
        attackCtrl?.InterruptCooldown();
        ai?.InterruptAttack();
        impact?.ApplyKnockback(this, dir, weapon);
    }

    public void Die(Vector3 hitDir, WeaponDataSO weapon) => Die(hitDir, weapon, 1f);
    public void Die(Vector3 hitDir, WeaponDataSO weapon, float impactScale)
    {
        if (CurrentState == EnemyState.Dead) return;
        SetState(EnemyState.Dead, true);
        death?.PlayDeath(this, hitDir, weapon, impactScale);
    }

    // 기존 호환 메서드
    public void SetAttackState() => SetState(EnemyState.Attack);
    public void SetChaseState() => SetState(EnemyState.Chase);
}