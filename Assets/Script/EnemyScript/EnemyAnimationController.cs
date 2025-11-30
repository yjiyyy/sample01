using UnityEngine;
using System.Collections;

/// <summary>
/// EnemyAnimationController
/// - Animator 캐시 및 안전한 파라미터 설정 헬퍼 제공
/// - 이제 animation hold(애니메이션 일시정지) 요청을 카운팅 방식으로 안전하게 관리합니다.
/// </summary>
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

    // ----------------- 추가된 메서드 -----------------

    // Animation hold (centralized) 구현:
    // - 여러 요청이 겹쳐 들어와도 복원 실패가 발생하지 않도록 카운팅 방식으로 관리.
    // - 최초 요청 시 현재 Animator.speed를 savedAnimSpeed에 저장하고 speed=0으로 설정.
    // - 각 요청은 내부에서 duration 후 ReleaseAnimationHold()을 호출.
    // - 모든 요청이 완료되면 savedAnimSpeed로 복원.
    private int animationHoldCount = 0;
    private float savedAnimSpeed = 1f;

    /// <summary>
    /// 요청: 애니메이터를 duration 초간 일시정지 요청.
    /// - duration <= 0 : 즉시 일시정지(복원 요청은 별도 ReleaseAnimationHold 호출 혹은 후속 Start가 복원될 때까지 유지)
    /// </summary>
    public void StartAnimationHold(float duration)
    {
        if (Animator == null) return;

        if (animationHoldCount == 0)
        {
            // 최초 요청 시 현재 속도 저장
            savedAnimSpeed = Animator.speed;
        }

        animationHoldCount = Mathf.Max(0, animationHoldCount) + 1;
        Animator.speed = 0f;

        if (duration > 0f)
        {
            // 내부에서 복원 처리하는 코루틴은 animCtrl이 관리하므로
            // PushRoutine이 중단되더라도 복원은 보장됩니다.
            StartCoroutine(AnimationHoldCoroutine(duration));
        }
    }

    private IEnumerator AnimationHoldCoroutine(float duration)
    {
        float end = Time.time + duration;
        while (Time.time < end)
        {
            yield return null;
        }
        ReleaseAnimationHold();
    }

    /// <summary>
    /// 요청 해제: 이전 StartAnimationHold에 대응하여 카운트를 줄이고,
    /// 카운트가 0이 되면 저장된 속도로 복원합니다.
    /// </summary>
    public void ReleaseAnimationHold()
    {
        if (animationHoldCount <= 0) return;

        animationHoldCount--;
        if (animationHoldCount <= 0)
        {
            animationHoldCount = 0;
            if (Animator != null)
            {
                Animator.speed = savedAnimSpeed;
            }
        }
    }

    // ----------------- 추가된 메서드 -----------------
    /// <summary>
    /// Find 애니메이션을 재생하기 위한 트리거 호출.
    /// - Animator에 trigger "Find"가 있어야 합니다.
    /// </summary>
    public void PlayFind()
    {
        Animator?.SetTrigger("Find");
    }
}