using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[DisallowMultipleComponent]
public class AngelLotteryCompanionDriver : MonoBehaviour
{
    private Upgrade_05_05_AngelLottery config;
    private Transform playerRoot;
    private Transform fireSpawnTransform;
    private Animator companionAnimator;

    private float fireCountdown;
    private Coroutine fireCoroutine;
    private PlayableGraph attackPlayableGraph;
    private Coroutine attackGraphStopCoroutine;

    public bool UsesSameConfig(Upgrade_05_05_AngelLottery angelConfig) =>
        config != null && angelConfig != null && ReferenceEquals(config, angelConfig);

    public void Initialize(Upgrade_05_05_AngelLottery angelConfig, Transform ownerRoot)
    {
        StopFireRoutine();
        StopAttackGraphStopRoutine();
        DestroyAttackPlayableGraph();

        config = angelConfig;
        playerRoot = ownerRoot != null ? ownerRoot : transform.root;
        fireSpawnTransform = ResolveFireSpawnTransform();
        companionAnimator = GetComponentInChildren<Animator>(true);
        fireCountdown = 0f;
    }

    private void OnDestroy()
    {
        StopFireRoutine();
        StopAttackGraphStopRoutine();
        DestroyAttackPlayableGraph();
    }

    private void Update()
    {
        if (config == null || playerRoot == null)
            return;

        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        fireCountdown -= dt;
        if (fireCountdown > 0f)
            return;

        fireCountdown = PlayerCompanionCooldownModifiers.ApplyCompanionCooldown(
            playerRoot != null ? playerRoot.gameObject : (transform.root != null ? transform.root.gameObject : gameObject),
            config.id,
            config.fireCooldown,
            0.05f);
        TryFire();
    }

    private void TryFire()
    {
        if (config.projectilePrefabs == null || config.projectilePrefabs.Length == 0)
            return;

        StopFireRoutine();
        fireSpawnTransform = ResolveFireSpawnTransform();
        fireCoroutine = StartCoroutine(FireSequenceRoutine());
    }

    private IEnumerator FireSequenceRoutine()
    {
        TryPlayAttackAnimation();

        float delay = Mathf.Max(0f, config.projectileSpawnDelay);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (!TryPickRandomGroundPointAroundPlayer(out Vector3 groundPoint))
        {
            fireCoroutine = null;
            yield break;
        }

        GameObject selected = PickRandomProjectilePrefab();
        if (selected == null)
        {
            fireCoroutine = null;
            yield break;
        }

        Transform spawnT = fireSpawnTransform != null ? fireSpawnTransform : transform;
        Vector3 spawnPos = spawnT.position;
        Vector3 to = groundPoint - spawnPos;
        to.y = 0f;
        Quaternion shotRot = to.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(to.normalized, Vector3.up)
            : spawnT.rotation;

        GameObject proj = Instantiate(selected, spawnPos, shotRot);
        if (proj.TryGetComponent(out AngelLotteryArcProjectile arc))
        {
            arc.Initialize(config, spawnPos, groundPoint);
        }
#if UNITY_EDITOR
        else
        {
            Debug.LogWarning($"[AngelLottery] projectilePrefab '{selected.name}'에 AngelLotteryArcProjectile이 없습니다.");
        }
#endif

        fireCoroutine = null;
    }

    private bool TryPickRandomGroundPointAroundPlayer(out Vector3 groundPoint)
    {
        groundPoint = playerRoot != null ? playerRoot.position : transform.position;
        if (playerRoot == null || config == null)
            return false;

        Vector3 center = playerRoot.position;
        Vector2 rnd = Random.insideUnitCircle * Mathf.Max(0.1f, config.randomThrowRange);
        Vector3 candidate = new Vector3(center.x + rnd.x, center.y, center.z + rnd.y);

        float probe = Mathf.Max(1f, config.groundProbeHeight);
        Vector3 rayOrigin = candidate + Vector3.up * probe;
        float rayDistance = probe * 2f;
        LayerMask mask = config.groundLayers.value != 0 ? config.groundLayers : ~0;

        RaycastHit[] hits = Physics.RaycastAll(
            rayOrigin,
            Vector3.down,
            rayDistance,
            mask,
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
        {
            groundPoint = candidate;
            return true;
        }

        int bestIndex = -1;
        float bestDelta = float.MaxValue;
        float baseY = center.y;
        for (int i = 0; i < hits.Length; i++)
        {
            float delta = Mathf.Abs(hits[i].point.y - baseY);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
        {
            groundPoint = candidate;
            return true;
        }

        groundPoint = hits[bestIndex].point;
        return true;
    }

    private GameObject PickRandomProjectilePrefab()
    {
        if (config == null || config.projectilePrefabs == null || config.projectilePrefabs.Length == 0)
            return null;

        int length = config.projectilePrefabs.Length;
        int startIndex = Random.Range(0, length);
        for (int i = 0; i < length; i++)
        {
            int idx = (startIndex + i) % length;
            GameObject prefab = config.projectilePrefabs[idx];
            if (prefab != null)
                return prefab;
        }

        return null;
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

    private void StopFireRoutine()
    {
        if (fireCoroutine != null)
        {
            StopCoroutine(fireCoroutine);
            fireCoroutine = null;
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

        attackPlayableGraph = PlayableGraph.Create("AngelLottery_Attack");
        var clipPlayable = AnimationClipPlayable.Create(attackPlayableGraph, config.attackAnimationClip);
        clipPlayable.SetApplyFootIK(false);
        var output = AnimationPlayableOutput.Create(attackPlayableGraph, "AngelLottery_AttackOut", companionAnimator);
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
