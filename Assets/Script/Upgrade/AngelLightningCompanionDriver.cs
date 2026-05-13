using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[DisallowMultipleComponent]
public class AngelLightningCompanionDriver : MonoBehaviour
{
    private const int OverlapBufferSize = 64;

    private Upgrade_05_04_AngelLightning config;
    private Transform playerRoot;
    private Animator companionAnimator;
    private readonly Collider[] overlapBuffer = new Collider[OverlapBufferSize];
    private readonly List<EnemyHealth> candidateTargets = new List<EnemyHealth>(OverlapBufferSize);

    private WeaponDataSO ccWeaponProxy;
    private Coroutine strikeSequenceRoutine;
    private float cycleCountdown;

    private PlayableGraph attackPlayableGraph;
    private Coroutine attackGraphStopCoroutine;

    public bool UsesSameConfig(Upgrade_05_04_AngelLightning angelConfig) =>
        config != null && angelConfig != null && ReferenceEquals(config, angelConfig);

    public void Initialize(Upgrade_05_04_AngelLightning angelConfig, Transform ownerRoot)
    {
        StopStrikeSequenceRoutine();
        StopAttackGraphStopRoutine();
        DestroyAttackPlayableGraph();

        config = angelConfig;
        playerRoot = ownerRoot != null ? ownerRoot : transform.root;
        companionAnimator = GetComponentInChildren<Animator>(true);

        RebuildCCWeaponProxy();
        cycleCountdown = 0f;
    }

    private void OnDestroy()
    {
        StopStrikeSequenceRoutine();
        StopAttackGraphStopRoutine();
        DestroyAttackPlayableGraph();
        DestroyCCWeaponProxy();
    }

    private void StopStrikeSequenceRoutine()
    {
        if (strikeSequenceRoutine != null)
        {
            StopCoroutine(strikeSequenceRoutine);
            strikeSequenceRoutine = null;
        }
    }

    private void StopAttackGraphStopRoutine()
    {
        if (attackGraphStopCoroutine != null)
        {
            StopCoroutine(attackGraphStopCoroutine);
            attackGraphStopCoroutine = null;
        }
    }

    private void DestroyAttackPlayableGraph()
    {
        if (attackPlayableGraph.IsValid())
            attackPlayableGraph.Destroy();
    }

    private void DestroyCCWeaponProxy()
    {
        if (ccWeaponProxy == null)
            return;

        Destroy(ccWeaponProxy);
        ccWeaponProxy = null;
    }

    private void RebuildCCWeaponProxy()
    {
        DestroyCCWeaponProxy();
        if (config == null)
            return;

        ccWeaponProxy = ScriptableObject.CreateInstance<WeaponDataSO>();
        ccWeaponProxy.name = "AngelLightning_RuntimeCCProxy";
        ccWeaponProxy.id = config.id;
        ccWeaponProxy.weaponName = config.upgradeName;
        ccWeaponProxy.category = WeaponCategory.Secondary;
        ccWeaponProxy.damageType = AttackDamageType.ProjectileGun;
        ccWeaponProxy.damage = 0f;
        ccWeaponProxy.knockbackPower = Mathf.Max(0f, config.knockbackPower);
        ccWeaponProxy.knockbackDuration = Mathf.Max(0f, config.knockbackDuration);
        ccWeaponProxy.jerkIntensity = 0f;
        ccWeaponProxy.jerkDuration = 0f;
        ccWeaponProxy.usePushInsteadOfKnockback = false;
        ccWeaponProxy.targetHoldDuration = 0f;
        ccWeaponProxy.attackerHoldDuration = 0f;
    }

    private void Update()
    {
        if (config == null || playerRoot == null || ccWeaponProxy == null)
            return;

        if (strikeSequenceRoutine != null)
            return;

        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        cycleCountdown -= dt;
        if (cycleCountdown > 0f)
            return;

        cycleCountdown = PlayerCompanionCooldownModifiers.ApplyCompanionCooldown(
            playerRoot != null ? playerRoot.gameObject : (transform.root != null ? transform.root.gameObject : gameObject),
            config.id,
            config.cycleCooldown,
            0.05f);
        strikeSequenceRoutine = StartCoroutine(StrikeSequenceRoutine());
    }

    private IEnumerator StrikeSequenceRoutine()
    {
        TryPlayAttackAnimation();

        float delay = Mathf.Max(0f, config.hitDelay);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        CollectRandomTargets();
        if (candidateTargets.Count == 0)
        {
            strikeSequenceRoutine = null;
            yield break;
        }

        int maxCount = Mathf.Min(Mathf.Max(1, config.targetsPerCycle), candidateTargets.Count);
        float interval = Mathf.Max(0f, config.strikeInterval);

        for (int i = 0; i < maxCount; i++)
        {
            EnemyHealth hp = candidateTargets[i];
            if (hp != null)
                ApplyLightningCC(hp);

            if (i < maxCount - 1 && interval > 0f)
                yield return new WaitForSeconds(interval);
        }

        strikeSequenceRoutine = null;
    }

    private void CollectRandomTargets()
    {
        candidateTargets.Clear();
        if (config == null || playerRoot == null)
            return;

        Vector3 origin = playerRoot.position;
        float range = Mathf.Max(0.1f, config.acquireRange);
        LayerMask mask = config.enemyLayers.value != 0 ? config.enemyLayers : ~0;

        int hitCount = Physics.OverlapSphereNonAlloc(
            origin,
            range,
            overlapBuffer,
            mask,
            QueryTriggerInteraction.Collide);

        var dedup = new HashSet<int>();
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
            if (enemy == null || enemy.CurrentState == Enemy.EnemyState.Dead)
                continue;

            int rootId = (enemy.transform.root != null ? enemy.transform.root : enemy.transform).GetInstanceID();
            if (!dedup.Add(rootId))
                continue;

            candidateTargets.Add(hp);
        }

        for (int i = candidateTargets.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            EnemyHealth tmp = candidateTargets[i];
            candidateTargets[i] = candidateTargets[j];
            candidateTargets[j] = tmp;
        }
    }

    private void ApplyLightningCC(EnemyHealth hp)
    {
        if (hp == null || hp.GetCurrentHP() <= 0f)
            return;

        Enemy enemy = hp.GetComponentInParent<Enemy>();
        if (enemy == null || enemy.CurrentState == Enemy.EnemyState.Dead)
            return;

        Vector3 dir = enemy.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            dir = transform.forward;
        dir.Normalize();

        float baseStun = Mathf.Max(0f, config.baseStunDuration);
        float randomizedStun = Mathf.Max(1f, baseStun * Random.Range(0.5f, 1.5f));
        ccWeaponProxy.stunDuration = enemy.HasAnySuperArmor() ? 0f : randomizedStun;

        enemy.ApplyKnockback(dir, ccWeaponProxy, 1f);
        SpawnAttachEffectOnTargetRoot(enemy);
    }

    private void SpawnAttachEffectOnTargetRoot(Enemy enemy)
    {
        if (config == null || config.hitAttachEffectPrefab == null || enemy == null)
            return;

        Transform root = enemy.transform.root != null ? enemy.transform.root : enemy.transform;
        GameObject fx = Instantiate(config.hitAttachEffectPrefab, root.position, Quaternion.identity, root);
        fx.transform.localPosition = Vector3.zero;

        float life = Mathf.Max(0f, config.hitAttachEffectLifetime);
        if (life > 0f)
            Destroy(fx, life);
    }

    private void TryPlayAttackAnimation()
    {
        if (config == null || config.attackAnimationClip == null)
            return;

        if (companionAnimator == null)
            companionAnimator = GetComponentInChildren<Animator>(true);
        if (companionAnimator == null)
            return;

        StopAttackGraphStopRoutine();
        DestroyAttackPlayableGraph();

        attackPlayableGraph = PlayableGraph.Create("AngelLightning_Attack");
        var clipPlayable = AnimationClipPlayable.Create(attackPlayableGraph, config.attackAnimationClip);
        clipPlayable.SetApplyFootIK(false);
        var output = AnimationPlayableOutput.Create(attackPlayableGraph, "AngelLightning_AttackOut", companionAnimator);
        output.SetSourcePlayable(clipPlayable);
        attackPlayableGraph.Play();

        float len = Mathf.Max(0.01f, config.attackAnimationClip.length);
        attackGraphStopCoroutine = StartCoroutine(StopAttackGraphAfterDelay(len));
    }

    private IEnumerator StopAttackGraphAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        DestroyAttackPlayableGraph();
        attackGraphStopCoroutine = null;
    }
}
