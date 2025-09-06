using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerMovement))]
public class PlayerAnimationController : MonoBehaviour
{
    private Animator animator;
    private PlayerMovement movement;

    [SerializeField] public WeaponBehavior weaponBehavior;

    // Animator 파라미터 해시
    private readonly int hashSpeed = Animator.StringToHash("Speed");
    private readonly int hashAttackIndex = Animator.StringToHash("AttackIndex");
    private readonly int hashIsAttacking = Animator.StringToHash("IsAttacking");
    private readonly int hashIsDead = Animator.StringToHash("IsDead");
    private readonly int hashKnockback = Animator.StringToHash("Knockback");
    private readonly int hashKnockbackIndex = Animator.StringToHash("KnockbackIndex");
    private readonly int hashStun = Animator.StringToHash("Stun");
    private readonly int hashIsEvading = Animator.StringToHash("IsEvading"); // ✅ 회피 파라미터 추가

    void Awake()
    {
        animator = GetComponent<Animator>();
        movement = GetComponent<PlayerMovement>();
    }

    void Update()
    {
        float speed = movement.GetVelocityMagnitude();
        animator.SetFloat(hashSpeed, speed);
    }

    /* ───────── 🆕 상태별 강제 애니메이션 전환 (블렌드 트리 대응) ───────── */

    /// <summary>
    /// 플레이어 상태가 변경될 때 애니메이션을 강제로 맞춤
    /// </summary>
    public void ForceAnimationByState(PlayerState newState)
    {
        if (animator == null) return;

        // 🔹 1단계: 모든 애니메이터 파라미터 리셋
        ResetAllAnimatorParams();

        // 🔹 2단계: 상태에 맞는 애니메이션 강제 재생
        switch (newState)
        {
            case PlayerState.Idle:
                animator.SetFloat(hashSpeed, 0f);
                animator.Play("Idle/Run", 0, 0f);
                Debug.Log("[PlayerAnim] 강제 전환 → Idle");
                break;

            case PlayerState.Move:
                animator.SetFloat(hashSpeed, 1f); // Run 애니메이션 재생
                animator.Play("Idle/Run", 0, 0f);
                Debug.Log("[PlayerAnim] 강제 전환 → Run");
                break;

            case PlayerState.Attack:
                // Attack은 별도 메서드에서 처리 (랜덤 인덱스 등)
                Debug.Log("[PlayerAnim] Attack 상태 - PlayAttack() 별도 호출 필요");
                break;

            case PlayerState.Knockback:
                // 🔹 블렌드 트리 방식: KnockbackIndex로 랜덤 선택
                float randomKnockbackIndex = Random.Range(0, 3); // 0f, 1f, 2f
                animator.SetFloat(hashKnockbackIndex, randomKnockbackIndex);
                animator.SetTrigger(hashKnockback);
                animator.Play("Knockback_Blend Tree", 0, 0f);
                Debug.Log($"[PlayerAnim] 강제 전환 → Knockback (Index: {randomKnockbackIndex})");
                break;

            case PlayerState.Stun:
                animator.SetTrigger(hashStun);
                animator.Play("Stun", 0, 0f);
                Debug.Log("[PlayerAnim] 강제 전환 → Stun");
                break;

            case PlayerState.Evade: // ✅ 회피 상태 추가
                animator.SetBool(hashIsEvading, true);
                animator.Play("Evade", 0, 0f);
                Debug.Log("[PlayerAnim] 강제 전환 → Evade");
                break;

            case PlayerState.Dead:
                animator.SetBool(hashIsDead, true);
                animator.Play("Death", 0, 0f);
                Debug.Log("[PlayerAnim] 강제 전환 → Death");
                break;
        }
    }

    /// <summary>
    /// 모든 애니메이터 파라미터를 안전한 상태로 리셋
    /// </summary>
    private void ResetAllAnimatorParams()
    {
        // 트리거 리셋
        animator.ResetTrigger(hashKnockback);
        animator.ResetTrigger(hashStun);

        // Bool 리셋
        animator.SetBool(hashIsAttacking, false);
        animator.SetBool(hashIsDead, false);
        animator.SetBool(hashIsEvading, false); // ✅ 회피 Bool 리셋 추가

        // Float 리셋
        animator.SetFloat(hashAttackIndex, 0f);
        animator.SetFloat(hashKnockbackIndex, 0f);

        Debug.Log("[PlayerAnim] 모든 애니메이터 파라미터 리셋 완료");
    }

    /* ───────── 공격 실행 (기존 유지) ───────── */
    public void PlayAttack(WeaponDataSO weaponData)
    {
        if (animator == null) return;

        // 공격 시에도 파라미터 리셋 후 설정
        ResetAllAnimatorParams();

        float randomIndex = Random.Range(0, 3); // 0f, 1f, 2f
        animator.SetFloat(hashAttackIndex, randomIndex);
        animator.SetBool(hashIsAttacking, true);

        // Attack 애니메이션 강제 재생
        animator.Play("Attack_BlendTree", 0, 0f);

        Debug.Log($"[PlayerAnim] Attack 시작 → Index:{randomIndex}, 무기:{weaponData?.weaponName}");
    }

    public void EndAttack()
    {
        animator.SetBool(hashIsAttacking, false);
        Debug.Log("[PlayerAnim] Attack 종료 (쿨타임 종료)");
    }

    /* ───────── 기존 호환 메서드들 ───────── */
    public void PlayKnockback()
    {
        ForceAnimationByState(PlayerState.Knockback);
    }

    public void PlayStun()
    {
        ForceAnimationByState(PlayerState.Stun);
    }

    public void PlayDeath()
    {
        ForceAnimationByState(PlayerState.Dead);
    }

    public void ResetDeath()
    {
        animator.SetBool(hashIsDead, false);
        Debug.Log("[PlayerAnim] Death 해제");
    }

    /* ───────── 애니메이션 이벤트 (기존 유지) ───────── */
    public void AttackHit()
    {
        Debug.Log("💥 [AnimEvent] AttackHit() 호출됨");
        weaponBehavior?.AttackHit();
    }

    public void OnAttackStart() => Debug.Log("🕒 [AnimEvent] OnAttackStart() 호출됨");
    public void OnAttackEnd() => Debug.Log("✅ [AnimEvent] OnAttackEnd() 호출됨");

    public Animator GetAnimator() => animator;
}