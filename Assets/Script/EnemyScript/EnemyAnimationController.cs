using UnityEngine;

[RequireComponent(typeof(Animator))]
public class EnemyAnimationController : MonoBehaviour
{
    public Animator Animator { get; private set; }

    // 내부 캐시
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
#if UNITY_EDITOR
        if (isStunned) Debug.Log($"{name} ▶ IsStun=true");
#endif
    }

    public void PlayKnockback()
    {
        if (!Animator) return;
        int randomKnockback = Random.Range(1, 4);
        Animator.Play($"Knockback0{randomKnockback}", 0, 0f);
    }

    /// <summary>
    /// 이동 기본 상태(BlendTree Root 또는 Run 상태) 강제 재생.
    /// AnyState -> Run + ResetToM 트리거 제거로 인해
    /// 이제 이동 복귀는 코드에서 명시적으로 PlayRun 호출로만 처리.
    /// </summary>
    /// <param name="crossFade">부드럽게 전환할지 여부</param>
    /// <param name="fadeDuration">CrossFade 시간</param>
    /// <param name="restart">이미 Run 상태여도 처음부터 다시 재생할지</param>
    public void PlayRun(bool crossFade = false, float fadeDuration = 0.1f, bool restart = false)
    {
        if (!Animator) return;

        // 현재 상태가 이미 Run이면 (재시작 옵션 아니면) 스킵
        var info = Animator.GetCurrentAnimatorStateInfo(0);
        if (!restart && info.IsName("Run"))
            return;

        if (crossFade)
            Animator.CrossFadeInFixedTime("Run", fadeDuration);
        else
            Animator.Play("Run", 0, 0f);
    }
}