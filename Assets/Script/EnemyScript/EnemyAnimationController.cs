using UnityEngine;

[RequireComponent(typeof(Animator))]
public class EnemyAnimationController : MonoBehaviour
{
    public Animator Animator { get; private set; }

    // 내부 캐시(선택 최적화)
    private readonly int hashSpeed = Animator.StringToHash("Speed");

    private void Awake()
    {
        Animator = GetComponent<Animator>();
    }

    /// <summary>
    /// SignedSpeed 직접 설정 (Forward: +0~+1, Backstep: -1, Idle: 0)
    /// Blend Tree: -1(Backstep) / 0(Idle) / +1(Run) 구성 전제
    /// </summary>
    public void SetSignedSpeed(float signedSpeed)
    {
        if (!Animator) return;
        Animator.SetFloat(hashSpeed, signedSpeed);
    }

    /// <summary>
    /// 기존 호환 (양수 속도만 들어오는 경우)
    /// </summary>
    public void UpdateMovement(float speed)
    {
        if (!Animator) return;
        Animator.SetFloat(hashSpeed, speed);
    }

    public void PlayAttack()
    {
        Animator?.SetTrigger("Attack");
    }

    public void PlayDeath()
    {
        Animator?.SetBool("IsDead", true);
    }

    public bool IsDead() => Animator != null && Animator.GetBool("IsDead");

    public void PlayStun(bool isStunned)
    {
        if (!Animator) return;
        Animator.SetBool("IsStun", isStunned);
        if (isStunned)
            Debug.Log($"{name} ▶ IsStun=true");
    }

    public void PlayKnockback()
    {
        if (!Animator) return;
        int randomKnockback = Random.Range(1, 4);
        Animator.Play($"Knockback0{randomKnockback}", 0, 0f);
    }
}