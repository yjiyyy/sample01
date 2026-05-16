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

    // ������ ���� ����(������ ���� �� ���)
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
        // ���� ��ȯ �� ���۾Ƹ�
        enemy.SetState(Enemy.EnemyState.Attack);
        if (data.grantSuperArmor) enemy.AddSuperArmor(SuperArmorSource.Attack);
        else enemy.RemoveSuperArmor(SuperArmorSource.Attack);

        // 1) �غ� �ܰ�
        yield return StartCoroutine(PreparePhase());

        // 2) ����(����) �ܰ�
        yield return StartCoroutine(AttackPhase());

        // 3) ������(����) �ܰ�
        yield return StartCoroutine(FinishPhase());

        // ���� ����
        enemy.RemoveSuperArmor(SuperArmorSource.Attack);
        if (enemy.CurrentState == Enemy.EnemyState.Attack)
            enemy.SetState(Enemy.EnemyState.Chase);

        rushRoutine = null;
    }

    private IEnumerator PreparePhase()
    {
        // �ִϸ��̼�: �غ� Ŭ�� �켱 ���
        if (enemy.animCtrl?.Animator != null && data.prepareClip != null)
        {
            enemy.animCtrl.Animator.speed = 1f;
            enemy.animCtrl.Animator.Play(data.prepareClip.name, 0, 0f);
        }
        else if (enemy.animCtrl?.Animator != null)
        {
            // ����: ��Ʈ�ѷ��� �غ� ���°� �ִٸ� �̸����� ���
            enemy.animCtrl.Animator.Play("RushPrepare", 0, 0f);
        }

        float elapsed = 0f;
        while (elapsed < data.prepareDuration)
        {
            // �ߴ� ����
            if (enemy.CurrentState != Enemy.EnemyState.Attack ||
                enemy.CurrentState == Enemy.EnemyState.ShieldBreak)
            {
                CancelNoCooldown();
                yield break;
            }

            // Ÿ�� �ٶ󺸱�(��Ʈ ��� ����, ���� ȸ����)
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
        // ���� �ִ�
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

        // ��Ʈ�ڽ� ����(����: attackDuration �Ǵ� ������)
        SpawnHitbox();

        // �ʱ� ����
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

            // ���� ����(�������ϰ� Ÿ���� ����)
            if (useDeviation && baseWeight > 0f && target != null)
            {
                Vector3 desired = target.position - transform.position;
                desired.y = 0f;
                if (desired.sqrMagnitude > 0.0001f)
                {
                    desired.Normalize();
                    // Fixed timestep ���� ����ġ(60fps ü�� ����)
                    float dtWeight = 1f - Mathf.Pow(1f - baseWeight, Time.fixedDeltaTime * 60f);
                    rushDir = Vector3.Slerp(rushDir, desired, dtWeight).normalized;

                    if (rushDir.sqrMagnitude > 0.0001f)
                        transform.rotation = Quaternion.LookRotation(rushDir);
                }
            }

            // �̵�: FixedUpdate ���, FPS ����
            Vector3 disp = rushDir * data.rushSpeed * Time.fixedDeltaTime;
            enemy.MoveFilteredDisplacement(disp);

            elapsed += Time.fixedDeltaTime;
            lastRushDir = rushDir;
            yield return new WaitForFixedUpdate();
        }

        // ���� ���� ���� �� ��Ʈ�ڽ� ����(������ ���� ��Ȱ��)
        DespawnHitbox();
    }

    private IEnumerator FinishPhase()
    {
        // ������ �ִ� ���(���� ����)
        if (enemy.animCtrl?.Animator != null && data.finishClip != null)
        {
            enemy.animCtrl.Animator.speed = 1f;
            enemy.animCtrl.Animator.Play(data.finishClip.name, 0, 0f);
        }

        float dur = Mathf.Max(0f, data.finishDuration);
        float elapsed = 0f;

        // rushSpeed �� 0 ���� ����
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

            // �ü��� ������ ���� ����
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
                WeaponDataSO.CreatePoisonPlayerHitProxyOrNull(data.isPoisonAttack, data.poisonOnHitStatus),
                data.targetHoldDuration,
                data.usePushInsteadOfKnockback,
                data.attackerHoldDuration
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

        // �ִϸ����ʹ� �Ķ���� ���� Ŭ���� ����ϹǷ� �߰� ���� ���ʿ�
        if (enemy.CurrentState == Enemy.EnemyState.Attack)
            enemy.SetState(Enemy.EnemyState.Chase);

        rushRoutine = null;
    }
}