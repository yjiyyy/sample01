using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[DisallowMultipleComponent]
public class AngelCurseCompanionDriver : MonoBehaviour
{
    private const int OverlapBufferSize = 48;

    private Upgrade_05_03_AngelCurse config;
    private Transform playerRoot;
    private Transform fireSpawnTransform;
    private Animator companionAnimator;
    private readonly Collider[] overlapBuffer = new Collider[OverlapBufferSize];

    private EnemyHealth currentTarget;
    private float reacquireCountdown;
    private float fireCountdown;

    private PlayableGraph attackPlayableGraph;
    private Coroutine fireCoroutine;
    private Coroutine attackGraphStopCoroutine;

    public bool UsesSameConfig(Upgrade_05_03_AngelCurse angelConfig) =>
        config != null && angelConfig != null && ReferenceEquals(config, angelConfig);

    public void Initialize(Upgrade_05_03_AngelCurse angelConfig, Transform ownerRoot)
    {
        StopFireRoutine();
        StopAttackGraphStopRoutine();
        DestroyAttackPlayableGraph();

        config = angelConfig;
        playerRoot = ownerRoot != null ? ownerRoot : transform.root;

        fireSpawnTransform = ResolveFireSpawnTransform();
        companionAnimator = GetComponentInChildren<Animator>(true);

        currentTarget = null;
        reacquireCountdown = 0f;
        fireCountdown = 0f;
    }

    private void OnDestroy()
    {
        StopFireRoutine();
        StopAttackGraphStopRoutine();
        DestroyAttackPlayableGraph();
    }

    private void StopAttackGraphStopRoutine()
    {
        if (attackGraphStopCoroutine != null)
        {
            StopCoroutine(attackGraphStopCoroutine);
            attackGraphStopCoroutine = null;
        }
    }

    private void StopFireRoutine()
    {
        if (fireCoroutine != null)
        {
            StopCoroutine(fireCoroutine);
            fireCoroutine = null;
        }
    }

    private void DestroyAttackPlayableGraph()
    {
        if (attackPlayableGraph.IsValid())
            attackPlayableGraph.Destroy();
    }

    private Transform ResolveFireSpawnTransform()
    {
        if (config != null && !string.IsNullOrEmpty(config.firePointChildName))
        {
            Transform t = FindDeepChild(transform, config.firePointChildName);
            if (t != null)
                return t;
        }

        Transform muzzleT = FindDeepChild(transform, "Muzzle");
        if (muzzleT != null)
            return muzzleT;

        return transform;
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent == null || string.IsNullOrEmpty(name))
            return null;
        if (parent.name == name)
            return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeepChild(parent.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
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

        attackPlayableGraph = PlayableGraph.Create("AngelCurse_Attack");
        var clipPlayable = AnimationClipPlayable.Create(attackPlayableGraph, config.attackAnimationClip);
        clipPlayable.SetApplyFootIK(false);
        var output = AnimationPlayableOutput.Create(attackPlayableGraph, "AngelCurse_AttackOut", companionAnimator);
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

    private void Update()
    {
        if (config == null || playerRoot == null)
            return;

        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        reacquireCountdown -= dt;
        if (reacquireCountdown <= 0f)
        {
            reacquireCountdown = Mathf.Max(0.05f, config.reacquireInterval);
            TryAcquireTarget();
        }

        if (!IsTargetValid())
        {
            currentTarget = null;
            return;
        }

        FaceTargetXZ();

        fireCountdown -= dt;
        if (fireCountdown <= 0f)
        {
            fireCountdown = PlayerCompanionCooldownModifiers.ApplyCompanionCooldown(
                playerRoot != null ? playerRoot.gameObject : (transform.root != null ? transform.root.gameObject : gameObject),
                config.id,
                config.fireCooldown,
                0.05f);
            TryFire();
        }
    }

    private bool IsTargetValid()
    {
        if (currentTarget == null)
            return false;

        if (currentTarget.GetCurrentHP() <= 0f)
            return false;

        var enemy = currentTarget.GetComponentInParent<Enemy>();
        if (enemy != null && enemy.CurrentState == Enemy.EnemyState.Dead)
            return false;

        Vector3 origin = playerRoot.position;
        float maxSq = config.acquireRange * config.acquireRange;
        if ((currentTarget.transform.position - origin).sqrMagnitude > maxSq * 1.1f)
            return false;

        return true;
    }

    private static Vector3 GetEnemyAimHorizontalReferenceWorld(EnemyHealth hp)
    {
        Transform searchRoot = hp.transform.root;
        var enemy = hp.GetComponentInParent<Enemy>();
        if (enemy != null)
            searchRoot = enemy.transform;

        Collider[] cols = searchRoot.GetComponentsInChildren<Collider>(true);
        if (cols != null && cols.Length > 0)
        {
            Collider best = null;
            float bestVol = -1f;
            for (int i = 0; i < cols.Length; i++)
            {
                Collider c = cols[i];
                if (c == null || !c.enabled)
                    continue;

                Vector3 s = c.bounds.size;
                float vol = Mathf.Abs(s.x * s.y * s.z);
                if (vol > bestVol)
                {
                    bestVol = vol;
                    best = c;
                }
            }

            if (best != null)
                return best.bounds.center;
        }

        return hp.transform.position;
    }

    private void TryAcquireTarget()
    {
        currentTarget = null;

        Vector3 origin = playerRoot.position;
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

            var hp = c.GetComponentInParent<EnemyHealth>();
            if (hp == null || hp.GetCurrentHP() <= 0f)
                continue;

            var enemy = hp.GetComponentInParent<Enemy>();
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

        Vector3 p = currentTarget.transform.position;
        Vector3 self = transform.position;
        Vector3 dir = new Vector3(p.x - self.x, 0f, p.z - self.z);
        if (dir.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    private void TryFire()
    {
        if (config.projectilePrefab == null || currentTarget == null)
            return;

        StopFireRoutine();
        fireSpawnTransform = ResolveFireSpawnTransform();
        fireCoroutine = StartCoroutine(FireSequenceRoutine(currentTarget));
    }

    private IEnumerator FireSequenceRoutine(EnemyHealth targetAtCommit)
    {
        Transform spawnT = fireSpawnTransform != null ? fireSpawnTransform : transform;
        Vector3 targetWorld = GetEnemyAimHorizontalReferenceWorld(targetAtCommit);

        TryPlayAttackAnimation();

        float delay = Mathf.Max(0f, config.projectileSpawnDelay);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        spawnT = fireSpawnTransform != null ? fireSpawnTransform : transform;
        Vector3 spawnPos = spawnT.position;
        Vector3 shotDir = targetWorld - spawnPos;
        shotDir.y = 0f;
        if (shotDir.sqrMagnitude < 0.0001f)
            shotDir = spawnT.forward;
        Quaternion shotRot = Quaternion.LookRotation(shotDir.normalized, Vector3.up);

        GameObject proj = Instantiate(config.projectilePrefab, spawnPos, shotRot);
        var arc = proj.GetComponent<AngelCurseArcProjectile>();
        if (arc != null)
        {
            arc.Initialize(config, spawnPos, targetWorld);
        }
#if UNITY_EDITOR
        else
        {
            Debug.LogWarning($"[AngelCurse] projectilePrefab '{config.projectilePrefab.name}'에 AngelCurseArcProjectile이 없습니다.");
        }
#endif

        fireCoroutine = null;
    }
}
