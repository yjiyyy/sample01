using System.Collections.Generic;
using UnityEngine;

public class HitBox_PC_Projectile : MonoBehaviour
{
    private const bool EnableChainDebugLog = true;

    private float speed;
    private float lifetime;
    private float damage;
    private Vector3 moveDir;
    private float traveledDistance;

    // 샷건 전용: 거리 기반 수명 + 선형 감쇠
    private bool shotgunDistanceModel;
    private float shotgunBaseDamage;
    private float shotgunMaxTravelDistance;
    private float shotgunFalloffStartDistance;
    private float shotgunMinDamageMultiplier = 1f;
    private bool sniperRicochetModel;
    private LayerMask sniperRicochetLayers = ~0;
    private float sniperRicochetSpeedMultiplier = 1f;
    private const float RICOCHET_PUSH_EPS = 0.01f;

    private WeaponDataSO weapon;

    // 관통 관련
    // remainingPierce: 남은 '히트(데미지 적용) 가능 횟수'
    private int remainingPierce = 0;
    private readonly HashSet<EnemyHealth> hitSet = new HashSet<EnemyHealth>();
    private readonly HashSet<int> chainHitTargetRootIds = new HashSet<int>();

    // 체인탄 상태
    private bool isChainProjectile = false;
    private int remainingChainBounces = 0;
    private float chainSearchRadius = 0f;
    private float chainDamagePerHit = 0f;
    private float chainTargetHoldDuration = 0f;
    private GameObject chainOwnerRoot;
    private bool chainSpawned = false;

    private static void ChainLog(string message)
    {
        if (!EnableChainDebugLog)
            return;
        Debug.Log($"[ChainShots] {message}");
    }

    // --- New: defensive components refs ---
    private Collider _coll;
    private Rigidbody _rb;

    // Small tolerance for arrival snapping (unused for normal straight movement)
    private const float ARRIVAL_EPS = 0.05f;

    public void SetWeapon(WeaponDataSO w)
    {
        weapon = w;

        // If weapon SO provides pierce count and we haven't set remainingPierce explicitly,
        // use it as default so missing params from caller won't break piercing.
        // Read common weapon SOs that define pierceCount (Gun, AR)
        if (weapon is WeaponDataSO_Gun g)
        {
            remainingPierce = Mathf.Max(0, g.pierceCount);
        }
        else if (weapon is WeaponDataSO_AR ar)
        {
            remainingPierce = Mathf.Max(0, ar.pierceCount);
        }
        // NOTE: WeaponDataSO_Launcher does not define pierceCount in this project,
        // so we must NOT attempt to read it (would cause CS1061).
    }

    public void OverridePierceCount(int pierceCount)
    {
        remainingPierce = Mathf.Max(0, pierceCount);
    }

    public void SetupChainProjectile(
        GameObject ownerRoot,
        int remainBounceCount,
        float searchRadius,
        float damagePerHit,
        float targetHoldDuration,
        HashSet<int> inheritedHitIds)
    {
        isChainProjectile = true;
        chainOwnerRoot = ownerRoot;
        remainingChainBounces = Mathf.Max(0, remainBounceCount);
        chainSearchRadius = Mathf.Max(0f, searchRadius);
        chainDamagePerHit = Mathf.Max(0f, damagePerHit);
        chainTargetHoldDuration = Mathf.Max(0f, targetHoldDuration);
        chainSpawned = false;

        chainHitTargetRootIds.Clear();
        if (inheritedHitIds != null)
        {
            foreach (int id in inheritedHitIds)
                chainHitTargetRootIds.Add(id);
        }

        ChainLog($"SetupChainProjectile | owner:{(ownerRoot != null ? ownerRoot.name : "null")} remainBounce:{remainingChainBounces} radius:{chainSearchRadius:F2} dmg:{chainDamagePerHit:F2} hold:{chainTargetHoldDuration:F2} inheritedHitCount:{chainHitTargetRootIds.Count}");
    }

    private void Awake()
    {
        // Defensive: ensure Collider is trigger so physics doesn't 'trap' transform-based movement.
        _coll = GetComponent<Collider>();
        if (_coll != null)
        {
            if (!_coll.isTrigger)
            {
                Debug.Log($"[Projectile] Forcing collider.isTrigger=true on {name} to avoid physics-stopping.");
                _coll.isTrigger = true;
            }
        }

        // If a Rigidbody exists, set it kinematic so physics won't override transform moves.
        _rb = GetComponent<Rigidbody>();
        if (_rb != null && !_rb.isKinematic)
        {
            Debug.Log($"[Projectile] Forcing Rigidbody.isKinematic=true on {name} to allow transform movement.");
            _rb.isKinematic = true;
        }
    }

    /* ───────── 기존 API: 방향으로 즉시 발사(호환 유지) ───────── */
    public void InitializeTowards(Vector3 direction, float dmg, float spd, float life)
    {
        // If remainingPierce wasn't set by SetWeapon, keep it as 0 (no pierce)
        shotgunDistanceModel = false;
        sniperRicochetModel = false;
        damage = dmg;
        speed = spd;
        lifetime = life;
        moveDir = direction.normalized;
        traveledDistance = 0f;

        Destroy(gameObject, lifetime);
    }

    // 오버로드: 피어스 카운트까지 설정 (명시적 값이 있으면 우선 적용)
    public void InitializeTowards(Vector3 direction, float dmg, float spd, float life, int pierceCount)
    {
        InitializeTowards(direction, dmg, spd, life);
        remainingPierce = Mathf.Max(0, pierceCount);
        if (remainingPierce > 0)
            Debug.Log($"[Projectile] InitializeTowards set pierce={remainingPierce}");
    }

    /* ───────── New API: 발사 시점의 타깃 위치를 고정해서 발사 ───────── */
    // targetPos: world-space 고정 좌표
    // maintainTargetHeight: true이면 발사체의 y를 targetPos.y로 고정하고 XZ 평면으로만 이동
    public void InitializeTowardsTargetPosition(Vector3 targetPos, float dmg, float spd, float life, bool maintainTargetHeight = true)
    {
        shotgunDistanceModel = false;
        sniperRicochetModel = false;
        damage = dmg;
        speed = spd;
        lifetime = life;
        traveledDistance = 0f;

        if (maintainTargetHeight)
        {
            // fix y to our current spawn y
            Vector3 p = transform.position;
            transform.position = new Vector3(p.x, targetPos.y, p.z);

            Vector3 horiz = new Vector3(targetPos.x - transform.position.x, 0f, targetPos.z - transform.position.z);
            if (horiz.sqrMagnitude < 0.0001f) horiz = transform.forward;
            moveDir = horiz.normalized;
        }
        else
        {
            Vector3 dir3 = (targetPos - transform.position);
            if (dir3.sqrMagnitude < 0.0001f) dir3 = transform.forward;
            moveDir = dir3.normalized;
        }

        Destroy(gameObject, lifetime);
    }

    public void InitializeShotgun(
        Vector3 direction,
        float pelletDamage,
        float projectileSpeed,
        float maxTravelDistance,
        float falloffStartDistance,
        float minDamageMultiplier,
        int pierceCount)
    {
        shotgunDistanceModel = true;
        sniperRicochetModel = false;
        shotgunBaseDamage = Mathf.Max(0f, pelletDamage);
        shotgunMaxTravelDistance = Mathf.Max(0.01f, maxTravelDistance);
        shotgunFalloffStartDistance = Mathf.Max(0f, falloffStartDistance);
        shotgunMinDamageMultiplier = Mathf.Clamp01(minDamageMultiplier);

        damage = shotgunBaseDamage;
        speed = Mathf.Max(0f, projectileSpeed);
        moveDir = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        traveledDistance = 0f;
        remainingPierce = Mathf.Max(0, pierceCount);

        // fail-safe: 속도가 0이어도 오브젝트가 무한히 남지 않게 보장
        lifetime = speed > 0.0001f
            ? Mathf.Max(0.05f, shotgunMaxTravelDistance / speed + 0.1f)
            : 0.25f;
        Destroy(gameObject, lifetime);
    }

    public void InitializeSniper(
        Vector3 direction,
        float dmg,
        float spd,
        float life,
        int pierceCount,
        LayerMask ricochetLayers,
        float ricochetSpeedMultiplier)
    {
        shotgunDistanceModel = false;
        sniperRicochetModel = true;
        sniperRicochetLayers = ricochetLayers;
        sniperRicochetSpeedMultiplier = Mathf.Max(0f, ricochetSpeedMultiplier);

        damage = Mathf.Max(0f, dmg);
        speed = Mathf.Max(0f, spd);
        lifetime = Mathf.Max(0.01f, life);
        moveDir = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        traveledDistance = 0f;
        remainingPierce = Mathf.Max(0, pierceCount);

        Destroy(gameObject, lifetime);
    }

    // 오버로드: 피어스 카운트까지 설정
    public void InitializeTowardsTargetPosition(Vector3 targetPos, float dmg, float spd, float life, int pierceCount, bool maintainTargetHeight = true)
    {
        InitializeTowardsTargetPosition(targetPos, dmg, spd, life, maintainTargetHeight);
        remainingPierce = Mathf.Max(0, pierceCount);
        if (remainingPierce > 0)
            Debug.Log($"[Projectile] InitializeTowardsTargetPosition set pierce={remainingPierce}");
    }

    void Update()
    {
        float delta = speed * Time.deltaTime;
        if (sniperRicochetModel && delta > 0f)
        {
            MoveWithRicochet(delta);
        }
        else
        {
            transform.position += moveDir * delta;
            traveledDistance += Mathf.Max(0f, delta);
        }

        if (shotgunDistanceModel && traveledDistance >= shotgunMaxTravelDistance)
        {
            Destroy(gameObject);
        }
    }

    private void MoveWithRicochet(float distance)
    {
        Vector3 origin = transform.position;
        float remain = Mathf.Max(0f, distance);
        int safety = 0;

        while (remain > 0f && safety < 4)
        {
            if (Physics.Raycast(origin, moveDir, out RaycastHit hit, remain, sniperRicochetLayers, QueryTriggerInteraction.Ignore) &&
                hit.collider != null &&
                !hit.collider.CompareTag("Enemy"))
            {
                float travel = Mathf.Max(0f, hit.distance);
                origin += moveDir * travel;
                traveledDistance += travel;

                Vector3 reflected = Vector3.Reflect(moveDir, hit.normal);
                reflected.y = 0f;
                if (reflected.sqrMagnitude < 0.0001f)
                    reflected = -moveDir;
                moveDir = reflected.normalized;

                speed = Mathf.Max(0f, speed * sniperRicochetSpeedMultiplier);
                remain -= travel;

                origin += moveDir * RICOCHET_PUSH_EPS;
                safety++;
                continue;
            }

            origin += moveDir * remain;
            traveledDistance += remain;
            remain = 0f;
        }

        transform.position = origin;
    }

    private float GetCurrentDamage()
    {
        if (!shotgunDistanceModel)
            return damage;

        if (traveledDistance <= shotgunFalloffStartDistance)
            return shotgunBaseDamage;

        if (traveledDistance >= shotgunMaxTravelDistance)
            return shotgunBaseDamage * shotgunMinDamageMultiplier;

        float t = Mathf.InverseLerp(shotgunFalloffStartDistance, shotgunMaxTravelDistance, traveledDistance);
        float mul = Mathf.Lerp(1f, shotgunMinDamageMultiplier, t);
        return shotgunBaseDamage * mul;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Enemy")) return;

        // 대상 Health 찾기(중복 타격 방지용 키)
        var hp = other.GetComponentInParent<EnemyHealth>();
        if (hp == null)
        {
            Debug.LogWarning($"❌ [Projectile] {other.name}에서 EnemyHealth를 찾지 못했습니다.");
            return;
        }

        int targetRootId = GetTargetRootId(hp);

        // 같은 프로젝타일이 이미 맞춘 "타겟 루트"는 체인/일반 여부와 무관하게 재타격 금지.
        // (하나의 몬스터에 EnemyHealth가 복수거나 콜라이더가 많아도 루트 단위로 1회만)
        if (chainHitTargetRootIds.Contains(targetRootId))
        {
            ChainLog($"OnHit skip | already-hit root target:{hp.name} targetRootId:{targetRootId}");
            return;
        }

        // 이미 같은 EnemyHealth에 타격했으면 중복 무시
        if (hitSet.Contains(hp))
            return;

        // 1) 데미지 먼저 적용(치명타 여부 판정 선행)
        Vector3 damageDir = moveDir;
        damageDir.y = 0f;
        if (damageDir.sqrMagnitude < 0.0001f) damageDir = Vector3.back;
        damageDir = damageDir.normalized;

        Vector3? hitPoint = other.ClosestPoint(transform.position);
        float currentDamage = GetCurrentDamage();
        ChainLog($"OnHit | projectile:{name} isChain:{isChainProjectile} target:{hp.name} targetRootId:{targetRootId} hitPoint:{(hitPoint.HasValue ? hitPoint.Value.ToString("F3") : "null")} damage:{currentDamage:F2}");
        float impactScale = shotgunDistanceModel
            ? Mathf.Max(shotgunMinDamageMultiplier, currentDamage / Mathf.Max(0.0001f, shotgunBaseDamage))
            : 1f;
        hp.ApplyDamage(currentDamage, damageDir, weapon, impactScale, hitPoint);
        GameObject ownerRoot = transform.root != null ? transform.root.gameObject : gameObject;
        if (!isChainProjectile)
        {
            PlayerWeaponDamageModifiers.TryApplyVampiricPunchOnHit(ownerRoot, weapon, currentDamage);
            PlayerWeaponDamageModifiers.TryApplyBleedingPunchOnHit(ownerRoot, weapon, hp);
            ApplyAttackerHoldFromWeapon();
        }
        else
        {
            ApplyChainTargetHold(hp);
        }
        Debug.Log($"✅ [Projectile] EnemyHealth에 {currentDamage} 데미지 적용!");

        chainHitTargetRootIds.Add(targetRootId);
        // 2) 살아있으면만 넉백/푸시/회전 적용 (체인탄은 비활성)
        if (!isChainProjectile && other.GetComponentInParent<Enemy>() is Enemy enemy)
        {
            if (enemy.CurrentState != Enemy.EnemyState.Dead)
            {
                Vector3 knockbackDir = moveDir;
                knockbackDir.y = 0f;
                if (knockbackDir.sqrMagnitude < 0.0001f) knockbackDir = Vector3.back;
                knockbackDir = knockbackDir.normalized;

                if (PlayerWeaponDamageModifiers.TryBuildStunningPunchProxyOnHit(ownerRoot, weapon, out var stunProxy))
                {
                    enemy.ApplyKnockback(knockbackDir, stunProxy, impactScale);
                }
                else if (weapon != null && weapon.usePushInsteadOfKnockback)
                {
                    enemy.ApplyPush(knockbackDir, weapon, impactScale);
                    Debug.Log($"💥 Projectile 충돌 │ Push 방향: {knockbackDir}");
                }
                else
                {
                    enemy.ApplyKnockback(knockbackDir, weapon, impactScale);
                    Debug.Log($"💥 Projectile 충돌 │ 넉백 방향: {knockbackDir}");
                }
            }
        }

        TrySpawnChainProjectile(hp, hitPoint, ownerRoot);

        // 관통 처리(단순화)
        hitSet.Add(hp);

        // pierceCount 의미: "추가 관통 횟수"
        // - remainingPierce <= 0: 이번 타격 후 파괴
        // - remainingPierce > 0: 이번 타격 후 1회 차감하고 계속 비행
        if (remainingPierce <= 0)
        {
            Debug.Log($"[Projectile] No pierce left -> Destroying on hit. (hp:{hp.name})");
            Destroy(gameObject);
            return;
        }
        else
        {
            remainingPierce--;
            Debug.Log($"[Projectile] Pierce consumed -> remainingPierce={remainingPierce}. Continuing flight.");
        }
    }

    private void TrySpawnChainProjectile(EnemyHealth currentTarget, Vector3? hitPoint, GameObject ownerRoot)
    {
        if (chainSpawned || currentTarget == null)
        {
            ChainLog($"TrySpawnChainProjectile skip | chainSpawned:{chainSpawned} currentTargetNull:{currentTarget == null}");
            return;
        }

        if (weapon == null || weapon.projectilePrefab == null)
        {
            ChainLog($"TrySpawnChainProjectile skip | weapon/projectilePrefab null weapon:{weapon == null}");
            return;
        }

        // 일반 탄은 최초 1회만 체인 스트림을 생성
        if (!isChainProjectile)
        {
            if (!PlayerWeaponDamageModifiers.TryGetChainShotsConfig(ownerRoot, weapon, out var cfg))
            {
                ChainLog($"TrySpawnChainProjectile base skip | Chain config not found. weapon:{weapon.name} damageType:{weapon.damageType} category:{weapon.category}");
                return;
            }

            if (cfg.bounceCount <= 0 || cfg.searchRadius <= 0f || cfg.damageMultiplier <= 0f)
            {
                ChainLog($"TrySpawnChainProjectile base skip | Invalid config bounce:{cfg.bounceCount} radius:{cfg.searchRadius:F2} dmgMul:{cfg.damageMultiplier:F2}");
                return;
            }

            float chainDamage = Mathf.Max(0f, GetCurrentDamage() * cfg.damageMultiplier);
            if (chainDamage <= 0f)
            {
                ChainLog($"TrySpawnChainProjectile base skip | chainDamage <= 0 (base:{damage:F2}, mul:{cfg.damageMultiplier:F2})");
                return;
            }

            ChainLog($"TrySpawnChainProjectile base ok | from:{currentTarget.name} bounce:{cfg.bounceCount} radius:{cfg.searchRadius:F2} chainDamage:{chainDamage:F2}");

            SpawnChainBullet(
                currentTarget,
                hitPoint,
                ownerRoot,
                cfg.bounceCount,
                cfg.searchRadius,
                chainDamage,
                cfg.chainTargetHoldDuration);
            return;
        }

        if (remainingChainBounces <= 0 || chainSearchRadius <= 0f || chainDamagePerHit <= 0f)
        {
            ChainLog($"TrySpawnChainProjectile chain skip | remain:{remainingChainBounces} radius:{chainSearchRadius:F2} dmg:{chainDamagePerHit:F2}");
            return;
        }

        ChainLog($"TrySpawnChainProjectile chain ok | from:{currentTarget.name} remain:{remainingChainBounces} radius:{chainSearchRadius:F2} dmg:{chainDamagePerHit:F2}");

        SpawnChainBullet(
            currentTarget,
            hitPoint,
            chainOwnerRoot != null ? chainOwnerRoot : ownerRoot,
            remainingChainBounces,
            chainSearchRadius,
            chainDamagePerHit,
            chainTargetHoldDuration);
    }

    private void SpawnChainBullet(
        EnemyHealth currentTarget,
        Vector3? hitPoint,
        GameObject ownerRoot,
        int bounceCount,
        float searchRadius,
        float damagePerHit,
        float targetHoldDuration)
    {
        if (bounceCount <= 0)
        {
            ChainLog("SpawnChainBullet skip | bounceCount <= 0");
            return;
        }

        Transform fromRoot = currentTarget.transform.root;
        Vector3 searchCenter = fromRoot != null ? fromRoot.position : currentTarget.transform.position;
        EnemyHealth nextTarget = FindNearestChainTarget(searchCenter, searchRadius);
        if (nextTarget == null)
        {
            ChainLog($"SpawnChainBullet stop | no next target center:{searchCenter:F3} radius:{searchRadius:F2} hitCount:{chainHitTargetRootIds.Count}");
            return;
        }

        Vector3 spawnPos = hitPoint ?? transform.position;
        Vector3 targetPos = nextTarget.transform.root != null ? nextTarget.transform.root.position : nextTarget.transform.position;
        Vector3 dir = targetPos - spawnPos;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
        {
            ChainLog($"SpawnChainBullet stop | zero direction spawn:{spawnPos:F3} target:{targetPos:F3}");
            return;
        }
        dir.Normalize();

        GameObject spawned = Instantiate(weapon.projectilePrefab, spawnPos, Quaternion.LookRotation(dir, Vector3.up));
        if (!spawned.TryGetComponent(out HitBox_PC_Projectile chainProj))
        {
            ChainLog($"SpawnChainBullet stop | prefab missing HitBox_PC_Projectile prefab:{weapon.projectilePrefab.name}");
            Destroy(spawned);
            return;
        }

        WeaponDataSO chainWeapon = BuildChainWeaponProxy(weapon, targetHoldDuration);
        chainProj.SetWeapon(chainWeapon);
        chainProj.OverridePierceCount(0); // 요구사항: 체인탄은 관통 없음
        chainProj.SetupChainProjectile(
            ownerRoot,
            bounceCount - 1,
            searchRadius,
            damagePerHit,
            targetHoldDuration,
            chainHitTargetRootIds);

        chainProj.InitializeTowards(dir, damagePerHit, speed, lifetime);
        ChainLog($"SpawnChainBullet success | next:{nextTarget.name} spawn:{spawnPos:F3} dir:{dir:F3} speed:{speed:F2} life:{lifetime:F2} damage:{damagePerHit:F2} nextRemainBounce:{bounceCount - 1}");
        chainSpawned = true;
    }

    private EnemyHealth FindNearestChainTarget(Vector3 center, float radius)
    {
        Collider[] colliders = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Collide);
        EnemyHealth best = null;
        float bestSqr = float.PositiveInfinity;
        var seen = new HashSet<int>();

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider c = colliders[i];
            if (c == null || !c.CompareTag("Enemy"))
                continue;

            EnemyHealth hp = c.GetComponentInParent<EnemyHealth>();
            if (hp == null)
                continue;

            int id = GetTargetRootId(hp);
            if (!seen.Add(id))
                continue;
            if (chainHitTargetRootIds.Contains(id))
                continue;
            if (hp.GetCurrentHP() <= 0f)
                continue;

            Transform t = hp.transform.root != null ? hp.transform.root : hp.transform;
            Vector3 to = t.position - center;
            to.y = 0f;
            float sqr = to.sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = hp;
            }
        }

        int selectedRootId = best != null ? GetTargetRootId(best) : 0;
        ChainLog($"FindNearestChainTarget | center:{center:F3} radius:{radius:F2} overlap:{colliders.Length} selected:{(best != null ? best.name : "null")} selectedRootId:{selectedRootId}");

        return best;
    }

    private static int GetTargetRootId(EnemyHealth hp)
    {
        if (hp == null)
            return 0;
        Transform root = hp.transform.root != null ? hp.transform.root : hp.transform;
        return root.GetInstanceID();
    }

    private static WeaponDataSO BuildChainWeaponProxy(WeaponDataSO source, float chainTargetHoldDuration)
    {
        var proxy = ScriptableObject.CreateInstance<WeaponDataSO>();
        proxy.hideFlags = HideFlags.HideAndDontSave;

        // 체인탄이 다음 체인을 생성할 때 필요한 발사 참조값은 유지
        proxy.projectilePrefab = source.projectilePrefab;
        proxy.hitEffectPrefab = source.hitEffectPrefab;
        proxy.damageType = source.damageType;
        proxy.category = source.category;

        // 체인탄은 군중제어를 상속하지 않음
        proxy.knockbackDuration = 0f;
        proxy.knockbackPower = 0f;
        proxy.jerkIntensity = 0f;
        proxy.jerkDuration = 0f;
        proxy.stunDuration = 0f;
        proxy.usePushInsteadOfKnockback = false;

        // 체인탄 전용 타겟 홀드 시간
        proxy.targetHoldDuration = Mathf.Max(0f, chainTargetHoldDuration);

        // death 연출 관련 필드는 전체 상속
        proxy.deathMode = source.deathMode;
        proxy.ragdollImpulse = source.ragdollImpulse;
        proxy.ragdollUpImpulse = source.ragdollUpImpulse;
        proxy.ragdollSpinTorque = source.ragdollSpinTorque;
        proxy.sliceTargets = source.sliceTargets != null ? new List<SliceTarget>(source.sliceTargets) : new List<SliceTarget>();
        proxy.sliceImpulse = source.sliceImpulse;

        return proxy;
    }

    private void ApplyChainTargetHold(EnemyHealth targetHealth)
    {
        if (targetHealth == null || chainTargetHoldDuration <= 0f)
            return;

        Enemy enemy = targetHealth.GetComponentInParent<Enemy>();
        if (enemy == null || enemy.CurrentState == Enemy.EnemyState.Dead)
            return;

        enemy.StartStateHold(chainTargetHoldDuration);
        enemy.animCtrl?.StartAnimationHold(chainTargetHoldDuration);
        ChainLog($"ApplyChainTargetHold | target:{enemy.name} duration:{chainTargetHoldDuration:F2}");
    }

    private void ApplyAttackerHoldFromWeapon()
    {
        if (weapon == null) return;

        float hold = weapon.attackerHoldDuration;
        if (hold <= 0f)
            hold = Mathf.Max(weapon.attackerStateHoldDuration, weapon.attackerAnimationHoldDuration);
        if (hold <= 0f) return;

        var attackerCtrl = transform.root != null ? transform.root.GetComponentInChildren<PlayerWeaponController>() : null;
        if (attackerCtrl == null)
            attackerCtrl = GameObject.FindWithTag("Player")?.GetComponentInChildren<PlayerWeaponController>();
        if (attackerCtrl == null) return;

        attackerCtrl.StartStateHold(hold);
        attackerCtrl.StartAnimationHold(hold);
    }
}