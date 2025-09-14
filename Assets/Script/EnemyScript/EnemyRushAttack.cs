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

    // 러시 동안 붙일 히트박스
    private GameObject spawnedRushHitbox;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        attackController = GetComponent<EnemyAttackController>();
        animController = GetComponent<EnemyAnimationController>();
    }

    public void StartRushAttack(RushAttackData data, Transform target)
    {
        StopAllRushCoroutines();

        rushData = data;
        targetTransform = target;

        if (rushData == null || targetTransform == null)
        {
            Debug.LogError("[EnemyRushAttack] rushData 또는 target이 null입니다!");
            return;
        }

        rushPrepareCoroutine = StartCoroutine(RushPrepareRoutine());

        if (debugMode)
            Debug.Log($"[EnemyRushAttack] 러쉬 공격 시작 - 준비 시간: {rushData.prepareTime}초");
    }

    private IEnumerator RushPrepareRoutine()
    {
        enemy.SetState(Enemy.EnemyState.Attack);

        if (enemy.animator != null)
        {
            enemy.animator.SetBool("IsRushPrepare", true);
            enemy.animator.Play("RushPrepare");
            if (debugMode) Debug.Log("[EnemyRushAttack] RushPrepare 애니메이션 재생");
        }

        // 타겟 방향 계속 바라보기
        if (targetTransform != null)
        {
            rushDirection = (targetTransform.position - transform.position).normalized;
            rushDirection.y = 0;
            transform.rotation = Quaternion.LookRotation(rushDirection);
        }

        float elapsed = 0;
        while (elapsed < rushData.prepareTime)
        {
            if (targetTransform != null)
            {
                rushDirection = (targetTransform.position - transform.position).normalized;
                rushDirection.y = 0;
                transform.rotation = Quaternion.LookRotation(rushDirection);
            }

            elapsed += Time.deltaTime;
            yield return null;

            if (enemy.CurrentState != Enemy.EnemyState.Attack)
            {
                if (debugMode) Debug.Log("[EnemyRushAttack] 준비 중 상태 변경됨. 러쉬 취소.");
                StopAllRushCoroutines();
                yield break;
            }
        }

        rushPrepareCoroutine = null;
        rushCoroutine = StartCoroutine(RushAttackRoutine());
    }

    private IEnumerator RushAttackRoutine()
    {
        isRushing = true;

        if (enemy.animator != null)
        {
            enemy.animator.SetBool("IsRushPrepare", false);
            enemy.animator.SetBool("IsRush", true);
            enemy.animator.Play("Rush");
            if (debugMode) Debug.Log("[EnemyRushAttack] Rush 애니메이션 재생");
        }

        // 시작 시점 최종 방향 고정
        if (targetTransform != null)
        {
            rushDirection = (targetTransform.position - transform.position).normalized;
            rushDirection.y = 0;
            transform.rotation = Quaternion.LookRotation(rushDirection);
        }

        if (debugMode)
            Debug.Log($"[EnemyRushAttack] 러쉬 시작 - 방향: {rushDirection}, 속도: {rushData.rushSpeed}, 시간: {rushData.rushTime}초");

        // NavMesh 에이전트 정지
        if (enemy.agent != null && enemy.agent.isOnNavMesh)
        {
            enemy.agent.isStopped = true;
            enemy.agent.velocity = Vector3.zero;
            enemy.agent.ResetPath();
        }

        // SO 지정 히트박스 스폰
        SpawnRushHitbox();

        float elapsed = 0;
        while (elapsed < rushData.rushTime)
        {
            // 이동
            Vector3 movement = rushDirection * rushData.rushSpeed * Time.deltaTime;
            transform.position += movement;

            if (debugMode && Time.frameCount % 10 == 0)
                Debug.Log($"[EnemyRushAttack] 러쉬 중... elapsed: {elapsed:F2}/{rushData.rushTime}, pos: {transform.position}");

            elapsed += Time.deltaTime;
            yield return null;

            if (enemy.CurrentState != Enemy.EnemyState.Attack)
            {
                if (debugMode) Debug.Log("[EnemyRushAttack] 러쉬 중 상태 변경됨. 러쉬 취소.");
                StopAllRushCoroutines();
                yield break;
            }
        }

        FinishRushAttack();
    }

    private void FinishRushAttack()
    {
        if (enemy.animator != null)
        {
            enemy.animator.SetBool("IsRush", false);
            enemy.animator.SetBool("IsRushPrepare", false);
        }

        // 히트박스 제거
        DespawnRushHitbox();

        isRushing = false;
        rushCoroutine = null;

        // 상태 복구
        enemy.SetState(Enemy.EnemyState.Chase);

        if (debugMode)
            Debug.Log("[EnemyRushAttack] 러쉬 완료. 추격 상태로 전환.");
    }

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

        if (enemy.animator != null)
        {
            enemy.animator.SetBool("IsRush", false);
            enemy.animator.SetBool("IsRushPrepare", false);
        }

        // 히트박스 제거
        DespawnRushHitbox();

        isRushing = false;
    }

    public void CancelRushAttack()
    {
        StopAllRushCoroutines();
    }

    private void SpawnRushHitbox()
    {
        if (spawnedRushHitbox != null) return;

        if (rushData.hitBoxPrefab == null)
        {
            Debug.LogWarning("[EnemyRushAttack] rushData.hitBoxPrefab이 비었습니다. 러시 동안 히트박스가 생성되지 않습니다.");
            return;
        }

        spawnedRushHitbox = Instantiate(rushData.hitBoxPrefab, transform);
        spawnedRushHitbox.transform.localPosition = Vector3.zero;
        spawnedRushHitbox.transform.localRotation = Quaternion.identity;

        if (spawnedRushHitbox.TryGetComponent<HitBox_Enemy>(out var hb))
        {
            float lifetime = rushData.hitBoxLifetime > 0f ? rushData.hitBoxLifetime : rushData.rushTime;
            hb.Initialize(
                rushData.damage,
                1f, // rush는 range 미사용
                rushData.knockbackPower,
                rushData.knockbackDuration,
                lifetime,
                rushData.stunDuration
            );
        }
        else
        {
            Debug.LogWarning("[EnemyRushAttack] 히트박스 프리팹에 HitBox_Enemy가 없습니다. 프리팹 자체에서 판정해야 합니다.");
        }
    }

    private void DespawnRushHitbox()
    {
        if (spawnedRushHitbox != null)
        {
            Destroy(spawnedRushHitbox);
            spawnedRushHitbox = null;
        }
    }

    // 본체 충돌 데미지는 제거(히트박스 일원화)
    // private void OnTriggerEnter(Collider other) { }
}