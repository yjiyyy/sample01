using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class Boss : MonoBehaviour
{
    public float detectionRange = 10f;

    private Transform player;
    private NavMeshAgent agent;
    private Animator animator;
    private BossAttackDecider attackDecider;

    private float attackTimer;
    private bool isAttacking;

    private enum BossState { Idle, Chase, Attack, Dead }
    private BossState currentState;
    public Vector3 GetPlayerPosition()
    {
        return player != null ? player.position : transform.position;
    }
    void Start()
    {
        player = GameManager.Instance.playerTransform;
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        attackDecider = new BossAttackDecider();

        currentState = BossState.Chase;
        StartCoroutine(WaitForPlayer());
    }

    void Update()
    {
        if (currentState == BossState.Dead || player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);
        attackTimer += Time.deltaTime;

        if (isAttacking)
        {
            agent.ResetPath();
            animator.SetFloat("Speed", 0);
            return;
        }

        switch (currentState)
        {
            case BossState.Chase:
                agent.SetDestination(player.position);
                BossAttackPattern pattern = attackDecider.ChoosePattern(distance);

                if (pattern != null && distance <= detectionRange)
                {
                    currentState = BossState.Attack;
                    ExecuteAttack(pattern);
                }
                break;

            case BossState.Attack:
                // 비어 있음: 공격은 ExecuteAttack 내부에서 처리
                break;
        }

        animator.SetFloat("Speed", agent.velocity.magnitude);
    }

    private void ExecuteAttack(BossAttackPattern pattern)
    {
        isAttacking = true;
        agent.ResetPath();
        pattern.Execute(this);
    }

    public void PlayAnimation(string triggerName)
    {
        animator.ResetTrigger("Attack_Melee");
        animator.ResetTrigger("Attack_Jump");
        animator.ResetTrigger("Attack_Arena");
        animator.SetTrigger(triggerName);
    }

    public IEnumerator ResumeChaseAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        isAttacking = false;
        currentState = BossState.Chase;
    }

    private IEnumerator WaitForPlayer()
    {
        while (GameManager.Instance.playerTransform == null)
            yield return null;

        player = GameManager.Instance.playerTransform;
    }
}
