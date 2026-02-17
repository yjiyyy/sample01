using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Enemy))]
public class RushAttackBehavior : MonoBehaviour
{
    [SerializeField] private RushAttackData data;

    private Enemy enemy;
    private Transform target;

    private Coroutine rushRoutine;
    private GameObject spawnedHitbox;

    // 마지막 돌진 방향(마무리 감속 때 사용)
    private Vector3 lastRushDir = Vector3.forward;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
    }

    public void SetTarget(Transform t) => target = t;

    public void StartRush()
    {
        if (data == null || enemy == null) return;
        if (rushRoutine != null) StopCoroutine(rushRoutine);

        rushRoutine = StartCoroutine(RushFlow());
    }

    private IEnumerator RushFlow()
    {
        // 상태 전환 및 슈퍼아머
        enemy.SetState(Enemy.EnemyState.Attack);
        if (data.grantSuperArmor) enemy.AddSuperArmor(SuperArmorSource.Attack);
        else enemy.RemoveSuperArmor(SuperArmorSource.Attack);

        // 1) 준비 단계
        yield return StartCoroutine(PreparePhase());

        // 2) 공격(돌진) 단계
        yield return StartCoroutine(AttackPhase());

        // 3) 마무리(감속) 단계
        yield return StartCoroutine(FinishPhase());

        // 종료 정리
        enemy.RemoveSuperArmor(SuperArmorSource.Attack);
        if (enemy.CurrentState == Enemy.EnemyState.Attack)
            enemy.SetState(Enemy.EnemyState.Chase);

        rushRoutine = null;
    }

    private IEnumerator PreparePhase()
    {
        // 애니메이션: 준비 클립 우선 재생
        if (enemy.animCtrl?.Animator != null && data.prepareClip != null)
        {
            enemy.animCtrl.Animator.speed = 1f;
            enemy.animCtrl.Animator.Play(data.prepareClip.name, 0, 0f);
        }
        else if (enemy.animCtrl?.Animator != null)
        {
            // 폴백: 컨트롤러에 준비 상태가 있다면 이름으로 재생
            enemy.animCtrl.Animator.Play("RushPrepare", 0, 0f);
        }

        float elapsed = 0f;
        while (elapsed < data.prepareDuration)
        {
            // 중단 조건
            if (enemy.CurrentState != Enemy.EnemyState.Attack ||
                enemy.CurrentState == Enemy.EnemyState.ShieldBreak)
            {
                CancelNoCooldown();
                yield break;
            }

            // 타겟 바라보기(루트 모션 없음, 수평 회전만)
            if (target != null)
            {
                Vector3 dir = target.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(dir.normalized);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator AttackPhase()
    {
        // 공격 애니
        if (enemy.animCtrl?.Animator != null)
        {
            enemy.animCtrl.Animator.speed = 1f;
            if (data.attackClip != null)
                enemy.animCtrl.Animator.Play(data.attackClip.name, 0, 0f);
            else if (!string.IsNullOrEmpty(data.attackName))
                enemy.animCtrl.Animator.Play(data.attackName, 0, 0f);
            else
                enemy.animCtrl.Animator.Play("Rush", 0, 0f);
        }

        // 히트박스 스폰(수명: attackDuration 또는 지정값)
        SpawnHitbox();

        // 초기 방향
        Vector3 rushDir = transform.forward;
        rushDir.y = 0f;
        if (rushDir.sqrMagnitude < 0.0001f) rushDir = Vector3.forward;

        bool useDeviation = data.allowDirectionDeviation;
        float baseWeight = Mathf.Clamp01(data.directionDeviationAmount);

        float elapsed = 0f;
        while (elapsed < data.attackDuration)
        {
            if (enemy.CurrentState != Enemy.EnemyState.Attack ||
                enemy.CurrentState == Enemy.EnemyState.ShieldBreak)
            {
                DespawnHitbox();
                CancelNoCooldown();
                yield break;
            }

            // 방향 보정(스무스하게 타겟을 따라감)
            if (useDeviation && baseWeight > 0f && target != null)
            {
                Vector3 desired = target.position - transform.position;
                desired.y = 0f;
                if (desired.sqrMagnitude > 0.0001f)
                {
                    desired.Normalize();
                    // Fixed timestep 기준 가중치(60fps 체감 유지)
                    float dtWeight = 1f - Mathf.Pow(1f - baseWeight, Time.fixedDeltaTime * 60f);
                    rushDir = Vector3.Slerp(rushDir, desired, dtWeight).normalized;

                    if (rushDir.sqrMagnitude > 0.0001f)
                        transform.rotation = Quaternion.LookRotation(rushDir);
                }
            }

            // 이동: FixedUpdate 기반, FPS 독립
            Vector3 disp = rushDir * data.rushSpeed * Time.fixedDeltaTime;
            enemy.MoveFilteredDisplacement(disp);

            elapsed += Time.fixedDeltaTime;
            lastRushDir = rushDir;
            yield return new WaitForFixedUpdate();
        }

        // 공격 구간 종료 → 히트박스 제거(마무리 동안 비활성)
        DespawnHitbox();
    }

    private IEnumerator FinishPhase()
    {
        // 마무리 애니 재생(있을 때만)
        if (enemy.animCtrl?.Animator != null && data.finishClip != null)
        {
            enemy.animCtrl.Animator.speed = 1f;
            enemy.animCtrl.Animator.Play(data.finishClip.name, 0, 0f);
        }

        float dur = Mathf.Max(0f, data.finishDuration);
        float elapsed = 0f;

        // rushSpeed → 0 선형 감속
        float initialSpeed = Mathf.Max(0f, data.rushSpeed);

        Vector3 dir = lastRushDir;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();

        while (elapsed < dur)
        {
            if (enemy.CurrentState != Enemy.EnemyState.Attack ||
                enemy.CurrentState == Enemy.EnemyState.ShieldBreak)
            {
                CancelNoCooldown();
                yield break;
            }

            float t = Mathf.Clamp01(elapsed / dur);
            float currentSpeed = initialSpeed * (1f - t);

            Vector3 disp = dir * currentSpeed * Time.fixedDeltaTime;
            enemy.MoveFilteredDisplacement(disp);

            // 시선은 마지막 방향 유지
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir);

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }

    private void SpawnHitbox()
    {
        if (data.hitBoxPrefab == null || spawnedHitbox != null) return;

        spawnedHitbox = Instantiate(data.hitBoxPrefab, transform.position, transform.rotation, transform);

        if (spawnedHitbox.TryGetComponent<HitBox_Enemy>(out var hb))
        {
            float life = data.hitBoxLifetime > 0f ? data.hitBoxLifetime : data.attackDuration;
            hb.Initialize(
                data.damage,
                data.range,
                data.knockbackPower,
                data.knockbackDuration,
                life,
                data.stunDuration,
                data.allowDuplicateHit,
                data.duplicateHitInterval,
                null
            );
        }
    }

    private void DespawnHitbox()
    {
        if (spawnedHitbox != null)
            Destroy(spawnedHitbox);
        spawnedHitbox = null;
    }

    private void CancelNoCooldown()
    {
        enemy.RemoveSuperArmor(SuperArmorSource.Attack);
        DespawnHitbox();

        // 애니메이터는 파라미터 없이 클립만 재생하므로 추가 해제 불필요
        if (enemy.CurrentState == Enemy.EnemyState.Attack)
            enemy.SetState(Enemy.EnemyState.Chase);

        rushRoutine = null;
    }
}