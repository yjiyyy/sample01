using UnityEngine;

public class BossAttackHitboxSpawner : MonoBehaviour
{
    [Header("히트박스 프리팹")]
    public GameObject meleeHitboxPrefab;
    public GameObject jumpHitboxPrefab;

    private Transform bossTransform;
    private Animator animator;

    private void Awake()
    {
        bossTransform = transform;
        animator = GetComponent<Animator>();
    }

    // 애니메이션 이벤트에서 호출: 이름은 반드시 "AttackHit"
    public void AttackHit()
    {
        // 현재 실행 중인 상태 이름 확인
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

        if (stateInfo.IsName("Attack_Melee"))
        {
            SpawnHitbox(meleeHitboxPrefab);
        }
        else if (stateInfo.IsName("Attack_Jump"))
        {
            SpawnHitbox(jumpHitboxPrefab);
        }
        else
        {
            Debug.LogWarning("AttackHit 이벤트가 잘못된 애니메이션 상태에서 호출되었습니다.");
        }
    }

    private void SpawnHitbox(GameObject hitboxPrefab)
    {
        if (hitboxPrefab != null)
        {
            Instantiate(hitboxPrefab, bossTransform.position, bossTransform.rotation);
        }
    }
}
