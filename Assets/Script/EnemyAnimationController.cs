using UnityEngine;

[RequireComponent(typeof(Animator))]
public class EnemyAnimationController : MonoBehaviour
{
    public Animator Animator { get; private set; }

    private void Awake()
    {
        Animator = GetComponent<Animator>();
    }

    public void UpdateMovement(float speed)
    {
        Animator.SetFloat("Speed", speed);
    }

    public void PlayAttack()
    {
        Animator.SetTrigger("Attack");
    }

    public void PlayDeath()
    {
        Animator.SetBool("IsDead", true);
    }

    public bool IsDead()
    {
        return Animator.GetBool("IsDead");
    }

    // 🆕 스턴 전환 + 디버그 로그
    public void PlayStun(bool isStunned)
    {
        Animator.SetBool("IsStun", isStunned);

        if (isStunned)
        {
            Debug.Log($"{name} ▶ Animator 파라미터 IsStun=true (스턴 시작)");
        }
        else
        {
            Debug.Log($"{name} ▶ Animator 파라미터 IsStun=false (스턴 종료)");
        }
    }

    private void Update()
    {
       
    }
}
