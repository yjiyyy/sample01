using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Enemy))]
public class EnemyRushAttack : MonoBehaviour
{
    [Header("러쉬 공격 디버깅")]
    public bool debugMode = true;

    // 컴포넌트 캐싱
    private Enemy enemy;
    private EnemyAttackController attackController;
    private EnemyAnimationController animController;

    // 공격 데이터 캐싱
    private RushAttackData rushData;
    private Vector3 rushDirection;
    private Transform targetTransform;
    private bool isRushing = false;

    // 코루틴 참조 관리
    private Coroutine rushPrepareCoroutine;
    private Coroutine rushCoroutine;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        attackController = GetComponent<EnemyAttackController>();
        animController = GetComponent<EnemyAnimationController>();
    }

    public void StartRushAttack(RushAttackData data, Transform target)
    {
        // 이미 진행 중인 코루틴 정리
        StopAllRushCoroutines();

        // 데이터 설정
        rushData = data;
        targetTransform = target;

        if (rushData == null || targetTransform == null)
        {
            Debug.LogError("[EnemyRushAttack] rushData 또는 target이 null입니다!");
            return;
        }

        // 준비 단계 코루틴 시작
        rushPrepareCoroutine = StartCoroutine(RushPrepareRoutine());

        if (debugMode)
            Debug.Log($"[EnemyRushAttack] 러쉬 공격 시작 - 준비 시간: {rushData.prepareTime}초");
    }

    private IEnumerator RushPrepareRoutine()
    {
        // 준비 상태로 전환
        enemy.SetState(Enemy.EnemyState.Attack);

        // 애니메이터 파라미터 설정 (IsRushPrepare = true)
        if (enemy.animator != null)
        {
            enemy.animator.SetBool("IsRushPrepare", true);
            enemy.animator.Play("RushPrepare");

            if (debugMode)
                Debug.Log("[EnemyRushAttack] RushPrepare 애니메이션 재생");
        }

        // 타겟 방향 바라보기
        if (targetTransform != null)
        {
            rushDirection = (targetTransform.position - transform.position).normalized;
            rushDirection.y = 0;
            transform.rotation = Quaternion.LookRotation(rushDirection);
        }

        // 준비 시간 동안 잠시 대기
        float elapsed = 0;
        while (elapsed < rushData.prepareTime)
        {
            // 준비 중에도 타겟 계속 추적
            if (targetTransform != null)
            {
                rushDirection = (targetTransform.position - transform.position).normalized;
                rushDirection.y = 0;
                transform.rotation = Quaternion.LookRotation(rushDirection);
            }

            elapsed += Time.deltaTime;
            yield return null;

            // 만약 Enemy가 knockback 상태가 되었다면 코루틴 종료
            if (enemy.CurrentState != Enemy.EnemyState.Attack)
            {
                if (debugMode)
                    Debug.Log("[EnemyRushAttack] 준비 중 상태 변경됨. 러쉬 취소.");

                StopAllRushCoroutines();
                yield break;
            }
        }

        // 준비 완료 후 실제 돌진 시작
        rushPrepareCoroutine = null;
        rushCoroutine = StartCoroutine(RushAttackRoutine());
    }

    private IEnumerator RushAttackRoutine()
    {
        // 돌진 상태로 전환
        isRushing = true;

        // 애니메이터 파라미터 설정
        if (enemy.animator != null)
        {
            enemy.animator.SetBool("IsRushPrepare", false);
            enemy.animator.SetBool("IsRush", true);
            enemy.animator.Play("Rush");

            if (debugMode)
                Debug.Log("[EnemyRushAttack] Rush 애니메이션 재생");
        }

        // 돌진 시작 시 방향 최종 확정 (이후 변경 없음)
        rushDirection = (targetTransform.position - transform.position).normalized;
        rushDirection.y = 0;
        transform.rotation = Quaternion.LookRotation(rushDirection);

        if (debugMode)
            Debug.Log($"[EnemyRushAttack] 러쉬 시작 - 방향: {rushDirection}, 속도: {rushData.rushSpeed}, 시간: {rushData.rushTime}초");

        // 에이전트 정지 (NavMesh 이동 중단)
        if (enemy.agent != null && enemy.agent.isOnNavMesh)
        {
            enemy.agent.isStopped = true;
            enemy.agent.velocity = Vector3.zero;
            enemy.agent.ResetPath();
        }

        // hitbox를 위한 콜라이더 활성화
        // 러쉬 공격 중에는 OnTriggerEnter를 통해 피해 판정

        // 돌진 시간 동안 지속 이동
        float elapsed = 0;
        while (elapsed < rushData.rushTime)
        {
            // 고정 방향으로 이동
            Vector3 movement = rushDirection * rushData.rushSpeed * Time.deltaTime;
            transform.position += movement;

            if (debugMode && Time.frameCount % 10 == 0)
                Debug.Log($"[EnemyRushAttack] 러쉬 중... elapsed: {elapsed:F2}/{rushData.rushTime}, pos: {transform.position}");

            elapsed += Time.deltaTime;
            yield return null;

            // 만약 Enemy가 knockback 상태가 되었다면 코루틴 종료
            if (enemy.CurrentState != Enemy.EnemyState.Attack)
            {
                if (debugMode)
                    Debug.Log("[EnemyRushAttack] 러쉬 중 상태 변경됨. 러쉬 취소.");

                StopAllRushCoroutines();
                yield break;
            }
        }

        // 돌진 완료 후 정리
        FinishRushAttack();
    }

    private void FinishRushAttack()
    {
        // 애니메이터 파라미터 리셋
        if (enemy.animator != null)
        {
            enemy.animator.SetBool("IsRush", false);
            enemy.animator.SetBool("IsRushPrepare", false);
        }

        // 상태 정리
        isRushing = false;
        rushCoroutine = null;

        // Enemy 상태 복구 (Chase 상태로)
        enemy.SetState(Enemy.EnemyState.Chase);

        if (debugMode)
            Debug.Log("[EnemyRushAttack] 러쉬 완료. 추격 상태로 전환.");
    }

    // 모든 러쉬 관련 코루틴 정리
    private void StopAllRushCoroutines()
    {
        if (rushPrepareCoroutine != null)
        {
            StopCoroutine(rushPrepareCoroutine);
            rushPrepareCoroutine = null;
        }

        if (rushCoroutine != null)
        {
            StopCoroutine(rushCoroutine);
            rushCoroutine = null;
        }

        // 애니메이터 파라미터 리셋
        if (enemy.animator != null)
        {
            enemy.animator.SetBool("IsRush", false);
            enemy.animator.SetBool("IsRushPrepare", false);
        }

        isRushing = false;
    }

    // 외부에서 강제 중단용 메소드
    public void CancelRushAttack()
    {
        StopAllRushCoroutines();
    }

    // OnTriggerEnter에서 적용할 충돌 체크 로직
    private void OnTriggerEnter(Collider other)
    {
        // 러쉬 중일 때만 충돌 판정
        if (!isRushing || rushData == null) return;

        if (other.CompareTag("Player"))
        {
            // 플레이어에게 데미지 및 넉백 적용
            if (other.TryGetComponent<PlayerHealth>(out var playerHealth))
            {
                Vector3 hitDir = rushDirection;
                playerHealth.ApplyDamage(rushData.damage, hitDir, null);

                if (debugMode)
                    Debug.Log($"[EnemyRushAttack] 플레이어 충돌! 데미지: {rushData.damage}");
            }

            if (other.TryGetComponent<PlayerWeaponController>(out var weaponController))
            {
                weaponController.ForceApplyKnockback(rushDirection, rushData.knockbackPower, 0.3f, 0f);

                if (debugMode)
                    Debug.Log($"[EnemyRushAttack] 플레이어 넉백! 힘: {rushData.knockbackPower}");
            }
        }
    }
}