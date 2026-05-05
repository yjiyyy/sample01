using System.Collections;
using UnityEngine;

/// <summary>
/// Upgrade_05_02_AngelSlayer SO 설정에 따라 적을 자동 탐색해 근접 공격합니다.
/// 히트는 보조무기 프리팹 원본 HitBox_PC/Collider를 일정 시간 활성화하는 방식입니다.
/// </summary>
[DisallowMultipleComponent]
public class AngelSlayerCompanionDriver : MonoBehaviour
{
    private const int OverlapBufferSize = 48;

    private Upgrade_05_02_AngelSlayer config;
    private Transform playerRoot;
    private Animator companionAnimator;
    private WeaponDataSO hitStyleProxy;
    private HitBox_PC attachedHitbox;
    private Collider attachedCollider;
    private WeaponTrailController trailController;

    private readonly Collider[] overlapBuffer = new Collider[OverlapBufferSize];
    private EnemyHealth currentTarget;
    private float scanCountdown;
    private float attackCountdown;
    private Coroutine attackRoutine;
    private Coroutine disableHitboxRoutine;

    public bool UsesSameConfig(Upgrade_05_02_AngelSlayer angelConfig) =>
        config != null && angelConfig != null && ReferenceEquals(config, angelConfig);

    public void Initialize(Upgrade_05_02_AngelSlayer angelConfig, Transform ownerRoot)
    {
        StopAttackRoutine();
        StopDisableHitboxRoutine();
        DisableHitboxImmediate();
        CancelTrailImmediate();

        config = angelConfig;
        playerRoot = ownerRoot != null ? ownerRoot : transform.root;
        companionAnimator = GetComponentInChildren<Animator>(true);

        ResolveAttachedHitbox();
        ResolveTrailController();
        RebuildHitStyleProxy();

        currentTarget = null;
        scanCountdown = 0f;
        attackCountdown = 0f;
    }

    private void OnDestroy()
    {
        StopAttackRoutine();
        StopDisableHitboxRoutine();
        DisableHitboxImmediate();
        CancelTrailImmediate();
        DestroyHitStyleProxy();
    }

    private void StopAttackRoutine()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
    }

    private void StopDisableHitboxRoutine()
    {
        if (disableHitboxRoutine != null)
        {
            StopCoroutine(disableHitboxRoutine);
            disableHitboxRoutine = null;
        }
    }

    private void ResolveAttachedHitbox()
    {
        attachedHitbox = GetComponentInChildren<HitBox_PC>(true);
        attachedCollider = attachedHitbox != null ? attachedHitbox.GetComponent<Collider>() : null;
        if (attachedCollider != null)
            attachedCollider.enabled = false;
    }

    private void ResolveTrailController()
    {
        trailController = GetComponentInChildren<WeaponTrailController>(true);
    }

    private void DisableHitboxImmediate()
    {
        if (attachedCollider != null)
            attachedCollider.enabled = false;
    }

    private void CancelTrailImmediate()
    {
        if (trailController != null)
            trailController.CancelTrailImmediate();
    }

    private void DestroyHitStyleProxy()
    {
        if (hitStyleProxy == null)
            return;

        Destroy(hitStyleProxy);
        hitStyleProxy = null;
    }

    private void RebuildHitStyleProxy()
    {
        DestroyHitStyleProxy();
        if (config == null)
            return;

        hitStyleProxy = ScriptableObject.CreateInstance<WeaponDataSO>();
        hitStyleProxy.name = "AngelSlayer_RuntimeHitStyle";

        WeaponDataSO w = hitStyleProxy;
        w.id = config.id;
        w.weaponName = config.upgradeName;
        w.category = WeaponCategory.Secondary;
        w.damageType = AttackDamageType.MeleeWeapon;
        w.damage = Mathf.Max(0f, config.damage);
        w.range = Mathf.Max(0.1f, config.acquireRange);
        w.hitBoxLifetime = Mathf.Max(0.01f, config.hitBoxLifetime);

        w.knockbackDuration = Mathf.Max(0f, config.knockbackDuration);
        w.knockbackPower = Mathf.Max(0f, config.knockbackPower);
        w.jerkIntensity = Mathf.Max(0f, config.jerkIntensity);
        w.jerkDuration = Mathf.Max(0f, config.jerkDuration);
        w.usePushInsteadOfKnockback = false;
        w.stunDuration = Mathf.Max(0f, config.stunDuration);
        w.targetHoldDuration = Mathf.Max(0f, config.targetHoldDuration);
        w.attackerHoldDuration = 0f;

        w.deathMode = config.deathMode;
        w.ragdollImpulse = Mathf.Max(0f, config.ragdollImpulse);
        w.ragdollUpImpulse = config.ragdollUpImpulse;
        w.ragdollSpinTorque = Mathf.Max(0f, config.ragdollSpinTorque);
        w.hitEffectPrefab = config.hitEffectPrefab;
    }

    private void Update()
    {
        if (config == null)
            return;

        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        scanCountdown -= dt;
        if (scanCountdown <= 0f)
        {
            scanCountdown = Mathf.Max(0.05f, config.scanInterval);
            TryAcquireTargetFromCompanion();
        }

        attackCountdown -= dt;
        if (attackCountdown > 0f)
            return;

        if (!IsTargetValidInRange())
            return;

        FaceTargetXZ();

        attackCountdown = Mathf.Max(0.05f, config.attackCooldown);
        StopAttackRoutine();
        attackRoutine = StartCoroutine(AttackRoutine());
    }

    private bool IsTargetValidInRange()
    {
        if (currentTarget == null)
            return false;
        if (currentTarget.GetCurrentHP() <= 0f)
            return false;

        var enemy = currentTarget.GetComponentInParent<Enemy>();
        if (enemy != null && enemy.CurrentState == Enemy.EnemyState.Dead)
            return false;

        Vector3 origin = transform.position;
        float maxSq = config.acquireRange * config.acquireRange;
        return (currentTarget.transform.position - origin).sqrMagnitude <= maxSq;
    }

    private void TryAcquireTargetFromCompanion()
    {
        currentTarget = null;

        Vector3 origin = transform.position;
        float range = Mathf.Max(0.1f, config.acquireRange);
        LayerMask mask = config.enemyLayers.value != 0 ? config.enemyLayers : ~0;

        int hitCount = Physics.OverlapSphereNonAlloc(
            origin,
            range,
            overlapBuffer,
            mask,
            QueryTriggerInteraction.Collide);

        float bestSq = float.MaxValue;
        EnemyHealth best = null;

        for (int i = 0; i < hitCount; i++)
        {
            Collider c = overlapBuffer[i];
            if (c == null)
                continue;

            if (config.enemyLayers.value == 0 && !c.CompareTag("Enemy"))
                continue;

            EnemyHealth hp = c.GetComponentInParent<EnemyHealth>();
            if (hp == null || hp.GetCurrentHP() <= 0f)
                continue;

            Enemy enemy = hp.GetComponentInParent<Enemy>();
            if (enemy != null && enemy.CurrentState == Enemy.EnemyState.Dead)
                continue;

            float sq = (hp.transform.position - origin).sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                best = hp;
            }
        }

        currentTarget = best;
    }

    private void FaceTargetXZ()
    {
        if (currentTarget == null)
            return;

        Vector3 targetPos = currentTarget.transform.position;
        Vector3 selfPos = transform.position;
        Vector3 dir = new Vector3(targetPos.x - selfPos.x, 0f, targetPos.z - selfPos.z);
        if (dir.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    private IEnumerator AttackRoutine()
    {
        if (!string.IsNullOrEmpty(config.attackTriggerName))
            companionAnimator?.SetTrigger(config.attackTriggerName);

        float delay = Mathf.Max(0f, config.hitboxSpawnDelay);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        EnableAttachedHitboxForSwing();
        attackRoutine = null;
    }

    private void EnableAttachedHitboxForSwing()
    {
        if (attachedHitbox == null || attachedCollider == null || hitStyleProxy == null)
            return;

        float life = Mathf.Max(0.01f, config.hitBoxLifetime);
        attachedCollider.enabled = true;
        attachedHitbox.SetWeapon(hitStyleProxy);
        attachedHitbox.InitializeAttached(
            Mathf.Max(0f, config.damage),
            Mathf.Max(0.1f, config.acquireRange),
            Mathf.Max(0f, config.knockbackPower),
            life);

        trailController?.EnableTrail();

        StopDisableHitboxRoutine();
        disableHitboxRoutine = StartCoroutine(DisableHitboxAfterLifetime(life));
    }

    private IEnumerator DisableHitboxAfterLifetime(float lifetime)
    {
        float elapsed = 0f;
        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (attachedCollider != null)
            attachedCollider.enabled = false;
        trailController?.DisableTrail();
        disableHitboxRoutine = null;
    }
}
