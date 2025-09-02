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
    private readonly int hashStun = Animator.StringToHash("Stun");

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

    /* ───────── 공격 실행 ───────── */
    public void PlayAttack(WeaponDataSO weaponData)
    {
        if (animator == null) return;

        float randomIndex = Random.Range(0, 3); // 0f, 1f, 2f
        animator.SetFloat(hashAttackIndex, randomIndex);
        animator.SetBool(hashIsAttacking, true);

        Debug.Log($"[Anim] Attack 시작 → Index:{randomIndex}, 무기:{weaponData?.weaponName}");
    }

    public void EndAttack()
    {
        animator.SetBool(hashIsAttacking, false);
        Debug.Log("[Anim] Attack 종료 (쿨타임 종료)");
    }

    /* ───────── 애니메이션 이벤트 ───────── */
    public void AttackHit()
    {
        Debug.Log("💥 [AnimEvent] AttackHit() 호출됨");
        weaponBehavior?.AttackHit();
    }

    public void OnAttackStart() => Debug.Log("🕒 [AnimEvent] OnAttackStart() 호출됨");
    public void OnAttackEnd() => Debug.Log("✅ [AnimEvent] OnAttackEnd() 호출됨");

    /* ───────── 넉백 / 스턴 ───────── */
    public void PlayKnockback()
    {
        animator.SetTrigger(hashKnockback);
        Debug.Log("[Anim] Knockback 실행");
    }

    public void PlayStun()
    {
        animator.SetTrigger(hashStun);
        Debug.Log("[Anim] Stun 실행");
    }

    /* ───────── 사망 ───────── */
    public void PlayDeath()
    {
        animator.SetBool(hashIsDead, true);
        animator.speed = 1f;
        Debug.Log("[Anim] Death 실행");
    }

    public void ResetDeath()
    {
        animator.SetBool(hashIsDead, false);
        Debug.Log("[Anim] Death 해제");
    }

    public Animator GetAnimator() => animator;
}
