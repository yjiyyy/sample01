using UnityEngine;

public class EnemyAttack : MonoBehaviour
{
    [Header("Attack Settings")]
    public float damage = 10f;
    public float attackRange = 1.2f;
    public float attackRadius = 0.7f;
    public LayerMask playerLayer;

    [Header("Hit Origin Offset")]
    public Vector3 hitOffset = new Vector3(0, 1f, 0.5f);

    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    // �� �Լ��� �ִϸ��̼� �̺�Ʈ���� ȣ���
    public void AttackHit()
    {
        Vector3 center = transform.position + transform.TransformDirection(hitOffset);
        Collider[] hits = Physics.OverlapSphere(center, attackRadius, playerLayer);

        foreach (var hit in hits)
        {
            Health hp = hit.GetComponent<Health>();
            if (hp != null)
            {
                hp.ApplyDamage(damage);
                Debug.Log($"{gameObject.name} hit {hit.name}");
            }
        }
    }

    // �����Ϳ��� ���� ���� �ð�ȭ
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 center = Application.isPlaying
            ? transform.position + transform.TransformDirection(hitOffset)
            : transform.position + hitOffset;

        Gizmos.DrawWireSphere(center, attackRadius);
    }
}
