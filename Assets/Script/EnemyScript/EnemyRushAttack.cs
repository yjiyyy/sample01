using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Enemy))]
public class EnemyRushAttack : MonoBehaviour
{
    [Header("���� ���� ������")]
    public RushAttackData rushData;

    [Header("�ִϸ��̼� �̸�")]
    public string prepareAnimName = "Rush_Prepare";
    public string rushAnimName = "Rush_Attack";

    private Enemy enemy;
    private Animator animator;
    private Transform player;
    private bool isAttacking = false;
    private float cooldownTimer = 0f;

    void Start()
    {
        enemy = GetComponent<Enemy>();
        animator = GetComponent<Animator>();
        player = GameObject.FindWithTag("Player")?.transform;

        if (player == null)
        {
            Debug.LogError("[EnemyRushAttack] Player�� ã�� �� �����ϴ�.");
        }
    }

    void Update()
    {
        if (cooldownTimer > 0)
        {
            cooldownTimer -= Time.deltaTime;
            return;
        }

        if (!isAttacking && ShouldRushAttack())
        {
            StartCoroutine(PerformRushAttack());
        }
    }

    private bool ShouldRushAttack()
    {
        if (player == null) return false;

        // �Ÿ� ��� ���� �Ǵ� (�߰Ÿ��� ����)
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        return distanceToPlayer < 15f && distanceToPlayer > 5f;
    }

    private IEnumerator PerformRushAttack()
    {
        if (rushData == null)
        {
            Debug.LogError("[EnemyRushAttack] rushData�� �������� �ʾҽ��ϴ�.");
            yield break;
        }

        isAttacking = true;
        cooldownTimer = rushData.cooldown;

        // ���� ���� ����
        enemy.SetStateWithCoroutine(Enemy.PublicEnemyState.Attack, rushData.prepareTime + rushData.rushTime);

        // 1. �غ� �ܰ� - �÷��̾� ����
        animator.Play(prepareAnimName);

        Vector3 targetPosition = player.position;
        Vector3 rushDirection = (targetPosition - transform.position).normalized;
        rushDirection.y = 0f;

        // �÷��̾ �ٶ󺸰� ȸ��
        transform.rotation = Quaternion.LookRotation(rushDirection);

        yield return new WaitForSeconds(rushData.prepareTime);

        // 2. ���� �ܰ�
        animator.Play(rushAnimName);

        float rushElapsed = 0f;
        bool hasHitPlayer = false;

        while (rushElapsed < rushData.rushTime)
        {
            rushElapsed += Time.deltaTime;

            // ���� �̵�
            transform.position += rushDirection * rushData.rushSpeed * Time.deltaTime;

            // �÷��̾�� �浹 üũ
            if (!hasHitPlayer && player != null)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, player.position);
                if (distanceToPlayer < 2.0f) // �浹 �ݰ�
                {
                    hasHitPlayer = true;

                    // �÷��̾� ������ �� �˹� ����
                    if (player.TryGetComponent<PlayerHealth>(out var playerHealth))
                    {
                        playerHealth.ApplyDamage(rushData.damage);
                    }

                    if (player.TryGetComponent<PlayerWeaponController>(out var weaponController))
                    {
                        Vector3 knockbackDir = rushDirection;
                        weaponController.ForceApplyKnockback(knockbackDir, rushData.knockbackPower,
                            rushData.knockbackDuration, rushData.stunDuration);
                    }
                }
            }

            yield return null;
        }

        // 3. 공격 완료 후 쿨다운
        isAttacking = false;

        // 상태 전환은 SetStateWithCoroutine에서 자동으로 처리됨
    }
}