using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Enemy))]
public class EnemyRushAttack : MonoBehaviour
{
    [Header("러쉬 공격 디버깅")]
    public bool debugMode = true;

    private Enemy enemy;
    private EnemyAttackController attackController;

    private RushAttackData rushData;
    private Vector3 rushDirection;
    private Transform targetTransform;
    private bool isRushing = false;

    private Coroutine rushPrepareCoroutine;
    private Coroutine rushCoroutine;

    private GameObject spawnedRushHitbox;

    // 러시 공격 인덱스 저장
    private int rushAttackIndex = -1;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        attackController = GetComponent<EnemyAttackController>();
    }

    public void StartRushAttack(RushAttackData data, Transform target, int attackIndex)
    {
        if (attackController.IsCooldownActive())
        {
            Debug.Log("[EnemyRushAttack] 쿨다운 중입니다. 러시 공격을 중단합니다.");
            return;
        }

        StopAllRushCoroutines();

        rushData = data;
        targetTransform = target;
        rushAttackIndex = attackIndex;

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

        if (targetTransform != null)
        {
            rushDirection = (targetTransform.position - transform.position).normalized;
            rushDirection.y = 0;
            transform.rotation = Quaternion.LookRotation(rushDirection);
        }

        float elapsed = 0;
        while (elapsed < rushData.prepareTime)
        {
            if (attackController.IsCooldownActive())
            {
                Debug.Log("[EnemyRushAttack] 쿨다운 중 준비 동작을 중단합니다.");
                StopAllRushCoroutines();
                yield break;
            }

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

        if (targetTransform != null)
        {
            rushDirection = (targetTransform.position - transform.position).normalized;
            rushDirection.y = 0;
            transform.rotation = Quaternion.LookRotation(rushDirection);
        }

        if (debugMode)
            Debug.Log($"[EnemyRushAttack] 러쉬 시작 - 방향: {rushDirection}, 속도: {rushData.rushSpeed}, 시간: {rushData.rushTime}초");

        if (enemy.agent != null && enemy.agent.isOnNavMesh)
        {
            enemy.agent.isStopped = true;
            enemy.agent.velocity = Vector3.zero;
            enemy.agent.ResetPath();
        }

        SpawnRushHitbox();

        float elapsed = 0;
        while (elapsed < rushData.rushTime)
        {
            transform.position += rushDirection * rushData.rushSpeed * Time.deltaTime;

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

        DespawnRushHitbox();
        isRushing = false;
        rushCoroutine = null;

        if (rushAttackIndex >= 0)
        {
            attackController.BeginCooldown(rushAttackIndex); // 본동작 완료 시 쿨다운 시작
        }
        enemy.SetState(Enemy.EnemyState.Chase);

        if (debugMode)
            Debug.Log("[EnemyRushAttack] 러쉬 완료. 추격 상태로 전환.");
    }

    public void InterruptCooldown()
    {
        attackController.InterruptCooldown();
        Debug.Log("[EnemyRushAttack] 공격받아 쿨다운 강제 해제.");
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

        DespawnRushHitbox();
        isRushing = false;
    }

    private void SpawnRushHitbox()
    {
        if (spawnedRushHitbox != null) return;

        if (rushData.hitBoxPrefab == null)
        {
            Debug.LogWarning("[EnemyRushAttack] rushData.hitBoxPrefab이 비었습니다.");
            return;
        }

        spawnedRushHitbox = Instantiate(rushData.hitBoxPrefab, transform);
        spawnedRushHitbox.transform.localPosition = Vector3.zero;
        spawnedRushHitbox.transform.localRotation = Quaternion.identity;

        if (spawnedRushHitbox.TryGetComponent<HitBox_Enemy>(out var hb))
        {
            float life = rushData.hitBoxLifetime > 0f ? rushData.hitBoxLifetime : rushData.rushTime;
            hb.Initialize(
                rushData.damage,
                0f, // range는 현재 HitBox_Enemy에서 사용하지 않음
                rushData.knockbackPower,
                rushData.knockbackDuration,
                life,
                rushData.stunDuration
            );
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
}