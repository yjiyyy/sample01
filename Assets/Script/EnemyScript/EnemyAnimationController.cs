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

    public void PlayKnockback()
    {
        if (Animator != null)
        {
            int randomKnockback = Random.Range(1, 4);
            Animator.Play($"Knockback0{randomKnockback}", 0, 0f);
            Debug.Log($"Knockback 애니메이션 재생: Knockback0{randomKnockback}");
        }
    }
}