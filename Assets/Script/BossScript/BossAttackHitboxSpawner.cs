using UnityEngine;

public class BossAttackHitboxSpawner : MonoBehaviour
{
    [Header("��Ʈ�ڽ� ������")]
    public GameObject meleeHitboxPrefab;
    public GameObject jumpHitboxPrefab;

    private Transform bossTransform;
    private Animator animator;

    private void Awake()
    {
        bossTransform = transform;
        animator = GetComponent<Animator>();
    }

    // �ִϸ��̼� �̺�Ʈ���� ȣ��: �̸��� �ݵ�� "AttackHit"
    public void AttackHit()
    {
        // ���� ���� ���� ���� �̸� Ȯ��
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
            Debug.LogWarning("AttackHit �̺�Ʈ�� �߸��� �ִϸ��̼� ���¿��� ȣ��Ǿ����ϴ�.");
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
