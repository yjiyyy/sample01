using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// Upgrade_05_01_AngelShooter SO 설정에 따라 주기적으로 적을 찾아 투사체를 발사합니다.
/// companionPrefab 인스턴스에 런타임으로 붙입니다.
/// 피격 시 넉백/푸시/처치/래그돌은 런타임 WeaponDataSO 프록시로 투사체에 전달합니다.
/// </summary>
[DisallowMultipleComponent]
public class AngelShooterCompanionDriver : MonoBehaviour
{
    private const int OverlapBufferSize = 48;

    private Upgrade_05_01_AngelShooter config;
    private Transform playerRoot;
    private Transform fireSpawnTransform;
    private readonly Collider[] overlapBuffer = new Collider[OverlapBufferSize];

    private WeaponDataSO hitStyleProxy;
    private Animator companionAnimator;
    private PlayableGraph attackPlayableGraph;
    private Coroutine fireCoroutine;
    private Coroutine attackGraphStopCoroutine;

    private EnemyHealth currentTarget;
    private float reacquireCountdown;
    private float fireCountdown;

    public bool UsesSameConfig(Upgrade_05_01_AngelShooter angelConfig) =>
        config != null && angelConfig != null && ReferenceEquals(config, angelConfig);

    public void Initialize(Upgrade_05_01_AngelShooter angelConfig, Transform ownerRoot)
    {
        StopFireRoutine();
        StopAttackGraphStopRoutine();
        DestroyAttackPlayableGraph();

        config = angelConfig;
        playerRoot = ownerRoot != null ? ownerRoot : transform.root;

        fireSpawnTransform = ResolveFireSpawnTransform();
        companionAnimator = GetComponentInChildren<Animator>(true);

        reacquireCountdown = 0f;
        fireCountdown = 0f;

        RebuildHitStyleProxy();
    }

    private void OnDestroy()
    {
        StopFireRoutine();
        StopAttackGraphStopRoutine();
        DestroyAttackPlayableGraph();
        DestroyHitStyleProxy();
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

    private void DestroyHitStyleProxy()
    {
        if (hitStyleProxy == null)
            return;

        Destroy(hitStyleProxy);
        hitStyleProxy = null;
    }

    private void DestroyAttackPlayableGraph()
    {
        if (attackPlayableGraph.IsValid())
            attackPlayableGraph.Destroy();
    }

    private void RebuildHitStyleProxy()
    {
        DestroyHitStyleProxy();
        if (config == null)
            return;

        hitStyleProxy = ScriptableObject.CreateInstance<WeaponDataSO>();
        hitStyleProxy.name = "AngelShooter_RuntimeHitStyle";
        WeaponDataSO w = hitStyleProxy;

        w.id = config.id;
        w.weaponName = config.upgradeName;
        w.damageType = AttackDamageType.ProjectileGun;
        w.damage = Mathf.Max(0f, config.damage);

        w.knockbackDuration = Mathf.Max(0f, config.knockbackDuration);
        w.knockbackPower = Mathf.Max(0f, config.knockbackPower);
        w.jerkIntensity = Mathf.Max(0f, config.jerkIntensity);
        w.jerkDuration = Mathf.Max(0f, config.jerkDuration);
        w.usePushInsteadOfKnockback = config.usePushInsteadOfKnockback;
        w.stunDuration = Mathf.Max(0f, config.stunDuration);
        w.targetHoldDuration = Mathf.Max(0f, config.targetHoldDuration);
        w.attackerHoldDuration = Mathf.Max(0f, config.attackerHoldDuration);

        w.deathMode = config.deathMode;
        w.ragdollImpulse = Mathf.Max(0f, config.ragdollImpulse);
        w.ragdollUpImpulse = config.ragdollUpImpulse;
        w.ragdollSpinTorque = Mathf.Max(0f, config.ragdollSpinTorque);

        w.hitEffectPrefab = config.hitEffectPrefab;
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

        attackPlayableGraph = PlayableGraph.Create("AngelShooter_Attack");
        var clipPlayable = AnimationClipPlayable.Create(attackPlayableGraph, config.attackAnimationClip);
        clipPlayable.SetApplyFootIK(false);
        var output = AnimationPlayableOutput.Create(attackPlayableGraph, "AngelShooter_AttackOut", companionAnimator);
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
            fireCountdown = Mathf.Max(0.05f, config.fireCooldown);
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

    /// <summary>
    /// 수평 조준(XZ는 적 콜라이더 중심 우선) + Y는 발사 트랜스폼(Fire_Point 등) 높이 유지.
    /// </summary>
    private Vector3 GetAimWorldPointPreservingSpawnHeight(Transform spawnT, EnemyHealth hp)
    {
        if (spawnT == null)
            return Vector3.zero;

        if (hp == null)
            return spawnT.position + spawnT.forward;

        Vector3 xzRef = GetEnemyAimHorizontalReferenceWorld(hp);
        return new Vector3(xzRef.x, spawnT.position.y, xzRef.z);
    }

    /// <summary>적 본체 기준으로 XZ 조준에 쓸 월드 좌표(가장 큰 활성 콜라이더 bounds.center, 없으면 루트)</summary>
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
        Vector3 startSpawnPos = spawnT.position;
        Vector3 aimWorld = GetAimWorldPointPreservingSpawnHeight(spawnT, targetAtCommit);
        Vector3 dir3 = aimWorld - startSpawnPos;
        if (dir3.sqrMagnitude < 0.0001f)
            dir3 = spawnT.forward;

        Vector3 fireDirection = dir3.normalized;

        TryPlayAttackAnimation();

        float delay = Mathf.Max(0f, config.projectileSpawnDelay);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        spawnT = fireSpawnTransform != null ? fireSpawnTransform : transform;
        Vector3 spawnPos = spawnT.position;
        Quaternion shotRot = Quaternion.LookRotation(fireDirection, Vector3.up);

        if (config.muzzleEffectPrefab != null)
        {
            GameObject fx = Instantiate(config.muzzleEffectPrefab, spawnPos, shotRot);
            float muzzleLife = Mathf.Max(0.05f, config.muzzleEffectLifetime);
            Destroy(fx, muzzleLife);
        }

        float spd = Mathf.Max(0f, config.projectileSpeed);
        float projectileLife = spd > 0.01f
            ? Mathf.Clamp(config.acquireRange / spd * 2.5f, 1.5f, 10f)
            : 3f;

        GameObject proj = Instantiate(config.projectilePrefab, spawnPos, shotRot);
        var hit = proj.GetComponent<HitBox_PC_Projectile>();
        if (hit != null)
        {
            if (hitStyleProxy != null)
                hit.SetWeapon(hitStyleProxy);

            hit.InitializeTowards(fireDirection, Mathf.Max(0f, config.damage), spd, projectileLife);
        }
#if UNITY_EDITOR
        else
        {
            Debug.LogWarning($"[AngelShooter] projectilePrefab '{config.projectilePrefab.name}'에 HitBox_PC_Projectile이 없습니다.");
        }
#endif

        fireCoroutine = null;
    }
}
