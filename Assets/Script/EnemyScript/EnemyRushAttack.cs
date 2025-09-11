using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Enemy))]
public class EnemyRushAttack : MonoBehaviour
{
    [Header("돌진 공격 데이터")]
    public RushAttackData rushData;

    [Header("애니메이션 이름")]
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
            Debug.LogError("[EnemyRushAttack] Player를 찾을 수 없습니다.");
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

        // 거리 기반 공격 판단 (중거리일 때만)
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        return distanceToPlayer < 15f && distanceToPlayer > 5f;
    }

    private IEnumerator PerformRushAttack()
    {
        if (rushData == null)
        {
            Debug.LogError("[EnemyRushAttack] rushData가 설정되지 않았습니다.");
            yield break;
        }

        isAttacking = true;
        cooldownTimer = rushData.cooldown;

        // 1. 준비 단계 - 플레이어 조준
        // Enemy 클래스를 통해 상태 변경 - 직접 열거형 사용 대신
        enemy.SetAttackState();

        animator.Play(prepareAnimName);

        Vector3 targetPosition = player.position;
        Vector3 rushDirection = (targetPosition - transform.position).normalized;
        rushDirection.y = 0f;

        // 방향 편차 적용 (옵션)
        if (rushData.allowDirectionDeviation)
        {
            Vector3 deviation = new Vector3(
                Random.Range(-rushData.directionDeviationAmount, rushData.directionDeviationAmount),
                0,
                Random.Range(-rushData.directionDeviationAmount, rushData.directionDeviationAmount)
            );
            rushDirection += deviation;
            rushDirection.Normalize();
        }

        // 플레이어를 바라보게 회전
        transform.rotation = Quaternion.LookRotation(rushDirection);

        // 준비 시간동안 대기
        float prepareElapsed = 0.0f;
        while (prepareElapsed < rushData.prepareTime)
        {
            prepareElapsed += Time.deltaTime;

            // 준비 단계에서 천천히 이동 (선택 사항)
            transform.position += rushDirection * rushData.prepareSpeed * Time.deltaTime;

            yield return null;
        }

        // 2. 돌진 단계
        animator.Play(rushAnimName);

        float rushElapsed = 0f;
        bool hasHitPlayer = false;

        while (rushElapsed < rushData.rushTime)
        {
            rushElapsed += Time.deltaTime;

            // 돌진 이동
            transform.position += rushDirection * rushData.rushSpeed * Time.deltaTime;

            // 플레이어와 충돌 체크
            if (!hasHitPlayer && player != null)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, player.position);
                if (distanceToPlayer < 2.0f) // 충돌 반경
                {
                    hasHitPlayer = true;

                    // 플레이어 데미지 및 넉백 적용
                    if (player.TryGetComponent<PlayerHealth>(out var playerHealth))
                    {
                        playerHealth.ApplyDamage(rushData.damage);
                    }

                    if (player.TryGetComponent<PlayerWeaponController>(out var weaponController))
                    {
                        Vector3 knockbackDir = rushDirection;
                        weaponController.ForceApplyKnockback(knockbackDir, rushData.knockbackPower,
                            0.5f, 0.2f);
                    }
                }
            }

            yield return null;
        }

        // 3. 마무리 및 쿨다운
        isAttacking = false;

        // Enemy 컴포넌트에 Chase 상태로 복귀
        enemy.SetChaseState();
    }
}