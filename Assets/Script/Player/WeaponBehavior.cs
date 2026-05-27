// WeaponBehavior.cs (full version - updated for SO injection + shotgun ammo consumption)
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponBehavior : MonoBehaviour
{
    [Header("무기 데이터")]
    public WeaponDataSO data;

    // 1st spawn points
    private Transform meleeSpawnPoint;
    private Transform projectileSpawnPoint;  // Fire_Point

    // 2nd spawn points (dual-wield)
    private Transform meleeSpawnPoint2;
    private Transform projectileSpawnPoint2;

    /* ─ Ammo (기본 WeaponAmmoRuntime 재사용) ─ */
    [SerializeField] private WeaponAmmoRuntime ammoRuntime;
    public WeaponAmmoRuntime Ammo => ammoRuntime;

    /* ─ 시각화 ─ */
    private LineRenderer previewLR;
    private Material previewMat;
    private const int kPreviewSegments = 36;

    /* ─ 트레일 (SO 기준 발동 타이밍) ─ */
    private WeaponTrailController trailController;
    private Coroutine trailEmitRoutine;
    private PlayerWeaponController cachedPlayerCtrl;

    [Header("발사 옵션")]
    public bool preserveVertical = false;

    private bool initializedOnce = false;

    /// <summary>PlayAttack에서 설정, AttackHit에서 소비. AR 등 AttackHit을 쓰지 않는 경로에서는 무시됨.</summary>
    private AttackVariantHandMode? pendingAttackHandMode = null;

    /// <summary>무기 데이터가 바뀔 때마다 증가. 보너스 샷 지연 스폰이 취소되는 데 사용합니다.</summary>
    private int bonusShotEquipToken;

    private bool IsPlayerTimeHoldActive()
    {
        if (cachedPlayerCtrl == null)
            cachedPlayerCtrl = GetComponentInParent<PlayerWeaponController>();
        if (cachedPlayerCtrl == null && transform.root != null)
            cachedPlayerCtrl = transform.root.GetComponentInChildren<PlayerWeaponController>();
        return cachedPlayerCtrl != null && cachedPlayerCtrl.IsTimeHoldActive;
    }

    /// <summary>업그레이드 등 무기 카테고리별 보정을 적용한 최종 피해.</summary>
    private float ScaleOutgoingDamageForWeapon(WeaponDataSO weaponStats, float baseDamage)
    {
        if (weaponStats == null)
            return Mathf.Max(0f, baseDamage);

        GameObject root = transform.root != null ? transform.root.gameObject : gameObject;
        return PlayerWeaponDamageModifiers.ScaleOutgoingDamage(root, weaponStats.category, baseDamage);
    }

    /// <summary>
    /// 원본 발사체에만 적용할 추가 관통 수치를 조회합니다.
    /// </summary>
    private int GetAdditionalPierceForWeapon(WeaponDataSO weaponStats)
    {
        if (weaponStats == null)
            return 0;

        GameObject root = transform.root != null ? transform.root.gameObject : gameObject;
        return PlayerWeaponDamageModifiers.GetAdditionalProjectilePierceCount(root, weaponStats);
    }

    private static Vector3 BonusShotHorizontalRight(Vector3 dirXZ)
    {
        Vector3 d = dirXZ;
        d.y = 0f;
        if (d.sqrMagnitude < 0.0001f)
            d = Vector3.forward;
        d.Normalize();
        return Vector3.Cross(Vector3.up, d).normalized;
    }

    private Transform ResolveProjectileSpawnForBonus(bool useSecond)
    {
        if (useSecond && projectileSpawnPoint2 == null)
            projectileSpawnPoint2 = ResolveSecondProjectileSpawnPoint();

        Transform spawnPoint = useSecond ? projectileSpawnPoint2 : projectileSpawnPoint;

        if (useSecond && spawnPoint == null)
            return null;

        if (spawnPoint == null)
        {
            if (meleeSpawnPoint != null)
                spawnPoint = meleeSpawnPoint;
            else
                spawnPoint = transform;
        }

        return spawnPoint;
    }

    /// <summary>
    /// 무기 SO의 Attack 페이즈 중 FirePoint에 붙는 FX만, 보너스 탄과 동일한 월드 위치·방향으로 즉시 1회 스폰합니다.
    /// </summary>
    private void SpawnBonusShotFirePointMuzzleFx(bool useSecond, Vector3 flightDirXZ, float lateralOffsetMeters)
    {
        if (data == null)
            return;

        var fxList = AttackFXPhaseResolver.Resolve(data.attackFXPhases, AttackFXPhase.Attack);
        if (fxList == null || fxList.Count == 0)
            return;

        Transform spawnPoint = ResolveProjectileSpawnForBonus(useSecond);
        if (spawnPoint == null)
            return;

        Vector3 dir = flightDirXZ;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            dir = spawnPoint.forward;
        dir.Normalize();

        Vector3 bonusPos = spawnPoint.position + BonusShotHorizontalRight(dir) * Mathf.Max(0f, lateralOffsetMeters);
        Quaternion bonusRot = Quaternion.LookRotation(dir, Vector3.up);

        bool dual = data.dualWield;

        for (int i = 0; i < fxList.Count; i++)
        {
            AttackFXEntry entry = fxList[i];
            if (entry == null || entry.prefab == null)
                continue;
            if (entry.attachRoot != AttackFXAttachRoot.FirePoint)
                continue;

            if (!useSecond)
            {
                if (entry.firePointHand != AttackFXFirePointHand.Main)
                    continue;

                AttackFXEntry.SpawnWorldOneShot(entry.prefab, bonusPos, bonusRot);
            }
            else
            {
                if (entry.firePointHand == AttackFXFirePointHand.OffHand)
                {
                    AttackFXEntry.SpawnWorldOneShot(entry.prefab, bonusPos, bonusRot);
                }
                else if (dual && entry.applyToOffHandWhenDual && entry.firePointHand == AttackFXFirePointHand.Main)
                {
                    AttackFXEntry.SpawnWorldOneShot(entry.prefab, bonusPos, bonusRot);
                }
            }
        }
    }

    private bool TryRollBonusShot(out PlayerWeaponDamageModifiers.BonusShotConfig cfg)
    {
        cfg = default;
        if (data == null)
            return false;

        GameObject root = transform.root != null ? transform.root.gameObject : gameObject;
        if (!PlayerWeaponDamageModifiers.TryGetBonusShotConfig(root, data, out cfg))
            return false;

        return Random.value < cfg.chance;
    }

    private void ScheduleBonusShotSpawn(WeaponDataSO weaponAtFire, float delayUnscaled, System.Action spawn)
    {
        if (weaponAtFire == null || spawn == null)
            return;

        int tok = bonusShotEquipToken;
        StartCoroutine(BonusShotDelayedSpawnRoutine(tok, weaponAtFire, delayUnscaled, spawn));
    }

    private IEnumerator BonusShotDelayedSpawnRoutine(int equipTokenAtSchedule, WeaponDataSO weaponAtSchedule, float delayUnscaled, System.Action spawn)
    {
        float wait = Mathf.Max(0f, delayUnscaled);
        float t = 0f;
        while (t < wait)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!isActiveAndEnabled)
            yield break;
        if (weaponAtSchedule == null || data != weaponAtSchedule)
            yield break;
        if (equipTokenAtSchedule != bonusShotEquipToken)
            yield break;

        Transform rt = transform.root != null ? transform.root : transform;
        if (rt != null)
        {
            var ph = rt.GetComponentInChildren<PlayerHealth>(true);
            if (ph != null && ph.GetCurrentHP() <= 0f)
                yield break;
        }

        spawn.Invoke();
    }

    /// <summary>
    /// 원본과 동일한 직선 비행(평행). 자동조준용 InitializeTowardsTargetPosition 대신 동일 dir 로 InitializeTowards/Sniper.
    /// </summary>
    private void SpawnBonusStandardProjectileParallel(
        bool useSecond,
        Vector3 flightDirXZ,
        float finalDamage,
        float spd,
        float life,
        int pierce,
        float lateralOffsetMeters)
    {
        if (data == null)
            return;

        var projPrefab = data.projectilePrefab;
        if (projPrefab == null)
            return;

        Transform spawnPoint = ResolveProjectileSpawnForBonus(useSecond);
        if (spawnPoint == null)
            return;

        Vector3 dir = flightDirXZ;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            dir = spawnPoint.forward;
        dir.Normalize();

        Vector3 pos = spawnPoint.position + BonusShotHorizontalRight(dir) * Mathf.Max(0f, lateralOffsetMeters);

        SpawnBonusShotFirePointMuzzleFx(useSecond, dir, lateralOffsetMeters);

        GameObject bulletGO = Instantiate(projPrefab, pos, Quaternion.LookRotation(dir, Vector3.up));
        if (!bulletGO.TryGetComponent(out HitBox_PC_Projectile proj))
        {
            Destroy(bulletGO);
            return;
        }

        proj.SetWeapon(data);

        if (data is WeaponDataSO_Sniper snInit)
        {
            proj.InitializeSniper(dir, finalDamage, spd, life, pierce, snInit.ricochetLayers, snInit.ricochetSpeedMultiplier);
        }
        else if (pierce > 0)
            proj.InitializeTowards(dir, finalDamage, spd, life, pierce);
        else
            proj.InitializeTowards(dir, finalDamage, spd, life);
    }

    private void SpawnBonusShotgunPelletParallel(
        bool useSecond,
        Vector3 pelletDir,
        float pelletDamageFinal,
        WeaponDataSO_Shotgun sg,
        int pierceFinal,
        float lateralOffsetMeters)
    {
        if (data == null || sg == null)
            return;

        var projPrefab = data.projectilePrefab;
        if (projPrefab == null)
            return;

        Transform spawnPoint = ResolveProjectileSpawnForBonus(useSecond);
        if (spawnPoint == null)
            return;

        Vector3 dir = pelletDir;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            dir = spawnPoint.forward;
        dir.Normalize();

        Vector3 pos = spawnPoint.position + BonusShotHorizontalRight(dir) * Mathf.Max(0f, lateralOffsetMeters);

        SpawnBonusShotFirePointMuzzleFx(useSecond, dir, lateralOffsetMeters);

        GameObject pelletGO = Instantiate(projPrefab, pos, Quaternion.LookRotation(dir, Vector3.up));
        if (!pelletGO.TryGetComponent(out HitBox_PC_Projectile proj))
        {
            Destroy(pelletGO);
            return;
        }

        proj.SetWeapon(data);
        proj.InitializeShotgun(
            dir,
            pelletDamageFinal,
            sg.projectileSpeed,
            sg.maxTravelDistance,
            sg.falloffStartDistance,
            sg.minDamageMultiplier,
            pierceFinal);
    }

    void Awake()
    {
        if (data != null)
        {
            ApplyDataInternal(data, forceReinit: true);
        }
    }

    public void ApplyData(WeaponDataSO newData, bool forceReinit = true)
    {
        if (newData == null)
        {
            Debug.LogWarning("[WeaponBehavior] ApplyData called with null data");
            return;
        }

        ApplyDataInternal(newData, forceReinit);
    }

    private void ApplyDataInternal(WeaponDataSO newData, bool forceReinit)
    {
        if (!forceReinit && initializedOnce && data == newData)
            return;

        data = newData;
        ClearPendingAttackVariantHandMode();
        bonusShotEquipToken++;

        ResolveSpawnPointsFromSO();
        EnsurePreviewLine();
        EnsureAmmoInitialized();
        EnsureTrail();
        EnsureWeaponHitboxDisabled();

        initializedOnce = true;
    }

    private void ResolveSpawnPointsFromSO()
    {
        if (data == null) return;

        meleeSpawnPoint = PlayerEquipmentController.FindRootDummy(transform.root);
        if (meleeSpawnPoint == null)
            Debug.LogWarning("[WeaponBehavior] meleeSpawnPoint(1) 못 찾음 (playerRoot): Root_Dummy / Root_dummy.");

        string projKey = string.IsNullOrEmpty(data.projectileSpawnPointPathOrName) ? "Fire_Point" : data.projectileSpawnPointPathOrName;
        projKey = NormalizePath(projKey);

        projectileSpawnPoint = FindByNameOrPath(transform, projKey);
        if (projectileSpawnPoint == null)
            Debug.LogWarning($"[WeaponBehavior] projectileSpawnPoint(1) 못 찾음 (weapon): '{projKey}'.");

        meleeSpawnPoint2 = data.dualWield ? PlayerEquipmentController.FindRootDummy(transform.root) : null;
        if (data.dualWield && meleeSpawnPoint2 == null)
            Debug.LogWarning("[WeaponBehavior] meleeSpawnPoint(2) 못 찾음 (playerRoot): Root_Dummy / Root_dummy.");

        projectileSpawnPoint2 = ResolveSecondProjectileSpawnPoint();
    }

    private Transform ResolveSecondProjectileSpawnPoint()
    {
        if (data == null || !data.dualWield) return null;

        Transform leftSocket = PlayerEquipmentController.FindLeftHandWeaponSocket(transform.root, data.socketNames);
        if (leftSocket == null)
            return null;

        string raw = string.IsNullOrEmpty(data.projectileSpawnPoint2PathOrName) ? "Fire_Point" : data.projectileSpawnPoint2PathOrName;
        string key = NormalizePath(raw);

        Transform found = FindByNameOrPath(leftSocket, key);
        if (found != null) return found;

        if (key != "Fire_Point")
            found = FindByNameOrPath(leftSocket, "Fire_Point");

        return found;
    }

    private string NormalizePath(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Replace("\\", "/").Trim();
    }

    private Transform FindByNameOrPath(Transform parent, string pathOrName)
    {
        return PlayerEquipmentController.FindBoneByNameOrPath(parent, pathOrName);
    }

    private Transform FindDeepChildByName(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;

        for (int i = 0; i < parent.childCount; ++i)
        {
            var t = FindDeepChildByName(parent.GetChild(i), name);
            if (t != null) return t;
        }
        return null;
    }

    public Transform GetPrimaryProjectileSpawnPoint()
    {
        return projectileSpawnPoint;
    }

    public void EnsureAmmoInitialized()
    {
        if (data == null) return;

        var gun = data as WeaponDataSO_Gun;
        if (gun != null)
        {
            if (ammoRuntime == null) ammoRuntime = GetComponent<WeaponAmmoRuntime>();
            if (ammoRuntime == null) ammoRuntime = gameObject.AddComponent<WeaponAmmoRuntime>();
            ammoRuntime.Initialize(gun, force: false);
            return;
        }

        var sg = data as WeaponDataSO_Shotgun;
        if (sg != null)
        {
            if (ammoRuntime == null) ammoRuntime = GetComponent<WeaponAmmoRuntime>();
            if (ammoRuntime == null) ammoRuntime = gameObject.AddComponent<WeaponAmmoRuntime>();
            ammoRuntime.Initialize(sg, force: false);
            return;
        }

        // AR handled by WeaponAmmoRuntime_AR component (if present)
        var ar = data as WeaponDataSO_AR;
        if (ar != null)
        {
            var arAmmo = GetComponent<WeaponAmmoRuntime_AR>();
            if (arAmmo == null) arAmmo = gameObject.AddComponent<WeaponAmmoRuntime_AR>();
            arAmmo.Initialize(ar, force: false);
            // note: we don't store AR ammo in ammoRuntime variable (separate component)
        }
    }

    void OnEnable()
    {
        EnsureTrail();
    }

    void OnDisable()
    {
        if (previewLR != null) previewLR.enabled = false;
        CancelTrailImmediate();
    }

    private void EnsureTrail()
    {
        if (trailController != null) return;
        trailController = GetComponent<WeaponTrailController>();
    }

    /// <summary>단타: WeaponDataSO의 trailEmitDuration&gt;0일 때만 지연 후 기록 시작·유지. 콤보 무기는 사용하지 않음.</summary>
    public void StartTrailEmitFromWeaponData(WeaponDataSO weaponData)
    {
        if (weaponData == null) return;
        StartTrailEmitWindow(weaponData.trailEmitStartDelay, weaponData.trailEmitDuration);
    }

    /// <summary>콤보 스텝 등: 시작 지연 후 emitDuration 동안 트레일 기록. duration≤0이면 무시. 새 호출 시 이전 발동 코루틴은 중단.</summary>
    public void StartTrailEmitWindow(float startDelay, float emitDuration)
    {
        if (emitDuration <= 0f) return;
        EnsureTrail();
        if (trailController == null) return;

        if (trailEmitRoutine != null)
        {
            StopCoroutine(trailEmitRoutine);
            trailEmitRoutine = null;
        }

        trailEmitRoutine = StartCoroutine(TrailEmitRoutine(Mathf.Max(0f, startDelay), emitDuration));
    }

    private IEnumerator TrailEmitRoutine(float startDelay, float emitDuration)
    {
        if (emitDuration <= 0f)
        {
            trailEmitRoutine = null;
            yield break;
        }

        float waitStart = 0f;
        while (waitStart < startDelay)
        {
            if (IsPlayerTimeHoldActive())
            {
                yield return null;
                continue;
            }
            waitStart += Time.deltaTime;
            yield return null;
        }

        EnsureTrail();
        trailController?.EnableTrail();

        float emitted = 0f;
        while (emitted < emitDuration)
        {
            if (IsPlayerTimeHoldActive())
            {
                yield return null;
                continue;
            }
            emitted += Time.deltaTime;
            yield return null;
        }

        DisableTrail();
        trailEmitRoutine = null;
    }

    /// <summary>트레일 기록 즉시 시작(외부 수동 제어용).</summary>
    public void EnableTrail()
    {
        EnsureTrail();
        trailController?.EnableTrail();
    }

    /// <summary>트레일 기록 중단(잔상은 trailLifetime 동안 페이드).</summary>
    public void DisableTrail()
    {
        trailController?.DisableTrail();
    }

    /// <summary>회피/넉백/스턴 등으로 공격이 끊길 때 호출. 발동 코루틴 중단 및 트레일 즉시 비움.</summary>
    public void CancelTrailImmediate()
    {
        if (trailEmitRoutine != null)
        {
            StopCoroutine(trailEmitRoutine);
            trailEmitRoutine = null;
        }
        trailController?.CancelTrailImmediate();
    }

    void LateUpdate()
    {
        if (previewLR != null) previewLR.enabled = false;
    }

    public void SetPendingAttackVariantHandMode(AttackVariantHandMode mode) => pendingAttackHandMode = mode;

    public void ClearPendingAttackVariantHandMode() => pendingAttackHandMode = null;

    private AttackVariantHandMode ConsumePendingOrDefaultHandMode()
    {
        if (pendingAttackHandMode.HasValue)
        {
            var m = pendingAttackHandMode.Value;
            pendingAttackHandMode = null;
            return m;
        }

        return data != null && data.dualWield ? AttackVariantHandMode.Both : AttackVariantHandMode.MainOnly;
    }

    private void ScheduleDelayedHitboxesForHandMode(AttackVariantHandMode mode)
    {
        if (data == null) return;
        bool dual = data.dualWield;

        switch (mode)
        {
            case AttackVariantHandMode.MainOnly:
                StartCoroutine(DelayedHitbox(false));
                break;
            case AttackVariantHandMode.OffOnly:
                if (dual)
                    StartCoroutine(DelayedHitbox(true));
                else
                    StartCoroutine(DelayedHitbox(false));
                break;
            case AttackVariantHandMode.Both:
            default:
                StartCoroutine(DelayedHitbox(false));
                if (dual)
                    StartCoroutine(DelayedHitbox(true));
                break;
        }
    }

    public void AttackHit()
    {
        if (data == null)
        {
            Debug.LogWarning("⚠ WeaponDataSO가 비어 있습니다.");
            return;
        }

        // AR/스나이퍼(WeaponDataSO_AR 파생): 실제 발사·탄 소비는 PlayerWeaponController의 ARAttackHit 경로만 사용합니다.
        // 애니메이션 AttackHit 이벤트가 그대로 들어오면 DelayedHitbox→SpawnProjectile이 호출되는데,
        // SpawnProjectile은 WeaponDataSO_Gun일 때만 탄약을 차감하므로 예비탄이 0이어도 발사체가 계속 나가는 현상이 생깁니다.
        if (data is WeaponDataSO_AR)
        {
            ClearPendingAttackVariantHandMode();
            return;
        }

        var handMode = ConsumePendingOrDefaultHandMode();
        ScheduleAttackFXFromData(data, AttackFXPhase.Attack, handMode);
        ScheduleDelayedHitboxesForHandMode(handMode);
    }

    /// <summary>
    /// AR 연사 전용: Gun의 <see cref="AttackHit"/>과 같이 공격 FX 스케줄 후 <see cref="WeaponDataSO.hitboxSpawnDelay"/>만큼 대기한 뒤
    /// <see cref="FireProjectileForced"/>로 탄을 낸다. (DelayedHitbox의 SpawnProjectile 경로와 달리 스프레드/방향 유지)
    /// </summary>
    public void ARAttackHit(Vector3 shootDir, bool preserveVerticalLocal, float damageMultiplier = 1f)
    {
        if (data == null)
        {
            Debug.LogWarning("⚠ WeaponDataSO가 비어 있습니다.");
            return;
        }

        ClearPendingAttackVariantHandMode();

        if (!(data is WeaponDataSO_AR))
        {
            Debug.LogWarning("[WeaponBehavior] ARAttackHit는 WeaponDataSO_AR일 때만 사용하세요.");
            return;
        }

        var handMode = data.dualWield ? AttackVariantHandMode.Both : AttackVariantHandMode.MainOnly;
        ScheduleAttackFXFromData(data, AttackFXPhase.Attack, handMode);
        StartCoroutine(DelayedARProjectileFire(shootDir, preserveVerticalLocal, damageMultiplier));
    }

    private IEnumerator DelayedARProjectileFire(Vector3 shootDir, bool preserveVerticalLocal, float damageMultiplier)
    {
        if (data == null) yield break;

        // AR 듀얼은 메인/오프핸드 지연을 분리 적용:
        // - 메인: hitboxSpawnDelay
        // - 오프핸드: hitboxSpawnDelay2
        StartCoroutine(DelayedForcedProjectileByHand(shootDir, preserveVerticalLocal, useSecond: false, delay: data.hitboxSpawnDelay, damageMultiplier: damageMultiplier));
        if (data.dualWield)
            StartCoroutine(DelayedForcedProjectileByHand(shootDir, preserveVerticalLocal, useSecond: true, delay: data.hitboxSpawnDelay2, damageMultiplier: damageMultiplier));

        yield break;
    }

    private IEnumerator DelayedForcedProjectileByHand(Vector3 shootDir, bool preserveVerticalLocal, bool useSecond, float delay, float damageMultiplier)
    {
        float d = Mathf.Max(0f, delay);
        if (d > 0f)
        {
            float elapsed = 0f;
            while (elapsed < d)
            {
                if (IsPlayerTimeHoldActive())
                {
                    yield return null;
                    continue;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        FireProjectileForced(shootDir, preserveVerticalLocal, useSecond, damageMultiplier);
    }

    /// <summary>공격 FX 스케줄. phase 목록 사용.</summary>
    private void ScheduleAttackFXFromData(WeaponDataSO weaponData, AttackFXPhase phase, AttackVariantHandMode handMode)
    {
        if (weaponData == null) return;
        var fxList = AttackFXPhaseResolver.Resolve(weaponData.attackFXPhases, phase);
        if (fxList == null || fxList.Count == 0) return;

        bool dual = weaponData.dualWield;
        bool includeOffHand = IncludesOffHand(handMode, dual);
        bool includeMainHand = IncludesMainHand(handMode, dual);

        var runtimeList = new List<AttackFXEntry>(fxList.Count * 2);
        for (int i = 0; i < fxList.Count; i++)
        {
            var entry = fxList[i];
            if (entry == null || entry.prefab == null) continue;

            // 메인 손 FX(기본)
            if (includeMainHand)
                runtimeList.Add(entry);

            // 체크된 FirePoint FX만 듀얼의 왼손에 자동 복제
            if (includeOffHand &&
                dual &&
                entry.attachRoot == AttackFXAttachRoot.FirePoint &&
                entry.applyToOffHandWhenDual)
            {
                runtimeList.Add(entry.CreateOffHandClone());
            }
        }

        if (runtimeList.Count == 0) return;
        AttackFXEntry.ScheduleAttackFX(this, runtimeList, ResolveAttackFXRoot, IsPlayerTimeHoldActive);
    }

    private static bool IncludesMainHand(AttackVariantHandMode mode, bool dual)
    {
        switch (mode)
        {
            case AttackVariantHandMode.OffOnly:
                return !dual;
            case AttackVariantHandMode.MainOnly:
            case AttackVariantHandMode.Both:
            default:
                return true;
        }
    }

    private static bool IncludesOffHand(AttackVariantHandMode mode, bool dual)
    {
        if (!dual) return false;
        return mode == AttackVariantHandMode.OffOnly || mode == AttackVariantHandMode.Both;
    }

    /// <summary>플레이어 무기 기준 AttackFX 항목 -> Transform. Custom 경로 비어 있으면 캐릭터 루트.</summary>
    public Transform ResolveAttackFXRoot(AttackFXEntry entry)
    {
        if (entry == null) return GetCharacterRootTransform();

        Transform GetCharacterRootTransform() => transform.root != null ? transform.root : transform;

        switch (entry.attachRoot)
        {
            case AttackFXAttachRoot.AttackerRoot:
                return GetCharacterRootTransform();

            case AttackFXAttachRoot.FirePoint:
                if (entry.firePointHand == AttackFXFirePointHand.OffHand)
                {
                    if (projectileSpawnPoint2 != null) return projectileSpawnPoint2;
                    if (projectileSpawnPoint != null) return projectileSpawnPoint;
                    return GetCharacterRootTransform();
                }

                return projectileSpawnPoint != null ? projectileSpawnPoint : GetCharacterRootTransform();

            case AttackFXAttachRoot.Custom:
                {
                    string path = NormalizePath(entry.attachPathOrName);
                    if (string.IsNullOrEmpty(path))
                        return GetCharacterRootTransform();
                    var found = FindByNameOrPath(transform.root, path);
                    return found != null ? found : GetCharacterRootTransform();
                }

            default:
                return GetCharacterRootTransform();
        }
    }

    private IEnumerator DelayedHitbox(bool useSecond)
    {
        float delay = useSecond ? data.hitboxSpawnDelay2 : data.hitboxSpawnDelay;
        if (delay > 0f)
        {
            float elapsed = 0f;
            while (elapsed < delay)
            {
                if (IsPlayerTimeHoldActive())
                {
                    yield return null;
                    continue;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        if (data is WeaponDataSO_Melee) { SpawnMeleeHitbox(useSecond); yield break; }
        if (data is WeaponDataSO_Gun) { SpawnProjectile(useSecond); yield break; }
        if (data is WeaponDataSO_Shotgun) { SpawnShotgunProjectiles(useSecond); yield break; }
        if (data is WeaponDataSO_Launcher) { SpawnProjectile(useSecond); yield break; }
        if (data is WeaponDataSO_Sniper) { SpawnProjectile(useSecond); yield break; }
        if (data is WeaponDataSO_AR) { SpawnProjectile(useSecond); yield break; }

        SpawnMeleeHitbox(useSecond);
    }

    private void SpawnMeleeHitbox(bool useSecond)
    {
        if (data != null && data.UseWeaponCollider)
        {
            UseWeaponCollider(useSecond, statsForHit: null, lifetimeOverride: null);
            return;
        }

        var prefab = data != null ? data.meleeHitboxPrefab : null;
        Transform spawn = useSecond ? meleeSpawnPoint2 : meleeSpawnPoint;

        if (useSecond && spawn == null) return;

        if (prefab == null || spawn == null)
        {
            Debug.LogWarning("[WeaponBehavior] (SO) meleeHitboxPrefab 또는 meleeSpawnPoint 미연결");
            return;
        }

        GameObject hitboxGO = Instantiate(
            prefab,
            spawn.position,
            PlayerEquipmentController.GetMeleeHitboxSpawnRotation(spawn));

        if (hitboxGO.TryGetComponent(out HitBox_PC hitbox))
        {
            hitbox.SetWeapon(data);
            hitbox.Initialize(ScaleOutgoingDamageForWeapon(data, data.damage), data.range, data.knockbackPower, data.hitBoxLifetime);
        }
    }

    /// <summary>
    /// 근접 콤보 스텝 등: 무기가 WeaponCollider 모드일 때 HitBox_PC를 활성화합니다.
    /// statsForHit에 콤보 스텝 프록시를 넘기면 데미지/넉백 등이 스텝 기준으로 적용됩니다.
    /// (콤보 애니에 AttackHit 이벤트가 있으면 이중 활성화될 수 있으니 콤보 클립에서는 제거 권장)
    /// </summary>
    public void ActivateMeleeColliderHitboxForCombo(WeaponDataSO statsForHit, float lifetime, AttackVariantHandMode handMode)
    {
        if (data == null || statsForHit == null) return;
        if (!data.UseWeaponCollider) return;

        float life = Mathf.Max(0.01f, lifetime);
        bool dual = data.dualWield;

        switch (handMode)
        {
            case AttackVariantHandMode.MainOnly:
                UseWeaponCollider(false, statsForHit, life);
                break;
            case AttackVariantHandMode.OffOnly:
                if (dual)
                    UseWeaponCollider(true, statsForHit, life);
                else
                    UseWeaponCollider(false, statsForHit, life);
                break;
            case AttackVariantHandMode.Both:
            default:
                UseWeaponCollider(false, statsForHit, life);
                if (dual)
                    UseWeaponCollider(true, statsForHit, life);
                break;
        }
    }

    /// <param name="statsForHit">null이면 장착 무기 data 사용(일반 AttackHit 경로)</param>
    /// <param name="lifetimeOverride">null이면 statsForHit.hitBoxLifetime 사용</param>
    private void UseWeaponCollider(bool useSecond, WeaponDataSO statsForHit, float? lifetimeOverride)
    {
        if (data == null) return;

        WeaponDataSO stats = statsForHit != null ? statsForHit : data;
        float life = lifetimeOverride ?? stats.hitBoxLifetime;
        life = Mathf.Max(0.01f, life);

        Transform weaponRoot = transform;
        if (useSecond && data.dualWield)
        {
            var equip = transform.root != null ? transform.root.GetComponent<PlayerEquipmentController>() : null;
            if (equip != null && equip.SecondaryWeapon != null)
                weaponRoot = equip.SecondaryWeapon.transform;
        }

        var hitbox = weaponRoot.GetComponentInChildren<HitBox_PC>(true);
        if (hitbox == null)
        {
            Debug.LogWarning("[WeaponBehavior] meleeHitboxMode=WeaponCollider인데 무기에 HitBox_PC 없음");
            return;
        }

        var col = hitbox.GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogWarning("[WeaponBehavior] meleeHitboxMode=WeaponCollider인데 HitBox_PC에 Collider 없음");
            return;
        }

        col.enabled = true;
        hitbox.SetWeapon(stats);
        hitbox.InitializeAttached(stats.damage, stats.range, stats.knockbackPower, life);
        StartCoroutine(DisableHitboxAfterLifetime(col, life));
    }

    private IEnumerator DisableHitboxAfterLifetime(Collider col, float lifetime)
    {
        float elapsed = 0f;
        while (elapsed < lifetime)
        {
            if (col == null)
                yield break;

            if (IsPlayerTimeHoldActive())
            {
                yield return null;
                continue;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (col != null)
            col.enabled = false;
    }

    private void EnsureWeaponHitboxDisabled()
    {
        if (data == null || !data.UseWeaponCollider) return;
        foreach (var hb in GetComponentsInChildren<HitBox_PC>(true))
        {
            var col = hb.GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }
        if (data.dualWield)
        {
            var equip = transform.root != null ? transform.root.GetComponent<PlayerEquipmentController>() : null;
            if (equip != null && equip.SecondaryWeapon != null)
            {
                foreach (var hb in equip.SecondaryWeapon.GetComponentsInChildren<HitBox_PC>(true))
                {
                    var col = hb.GetComponent<Collider>();
                    if (col != null) col.enabled = false;
                }
            }
        }
    }

    private Transform ChooseAimTarget(PlayerWeaponController playerCtrl, Transform spawnPoint, WeaponDataSO_Gun gunData)
    {
        if (playerCtrl == null || gunData == null) return null;
        if (playerCtrl.enemyDetector == null) return null;

        float maxDist = Mathf.Max(0f, gunData.aimScanDistance);
        float halfAngle = Mathf.Max(0f, gunData.aimScanAngle) * 0.5f;

        if (maxDist <= 0f || halfAngle <= 0f) return null;

        var list = playerCtrl.DetectEnemies();
        if (list == null || list.Count == 0) return null;

        Vector3 origin = spawnPoint != null ? spawnPoint.position : playerCtrl.transform.position;

        Vector3 fwd = playerCtrl.transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        fwd.Normalize();

        Transform best = null;
        float bestSqr = float.PositiveInfinity;
        float maxSqr = maxDist * maxDist;

        for (int i = 0; i < list.Count; i++)
        {
            var t = list[i];
            if (t == null) continue;

            Vector3 to = t.position - origin;
            to.y = 0f;

            float sqr = to.sqrMagnitude;
            if (sqr < 0.0001f) continue;
            if (sqr > maxSqr) continue;

            float ang = Vector3.Angle(fwd, to.normalized);
            if (ang > halfAngle) continue;

            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = t;
            }
        }

        return best;
    }

    private void SpawnProjectile(bool useSecond)
    {
        var projPrefab = data != null ? data.projectilePrefab : null;

        if (useSecond && projectileSpawnPoint2 == null)
        {
            projectileSpawnPoint2 = ResolveSecondProjectileSpawnPoint();
        }

        Transform spawnPoint = useSecond ? projectileSpawnPoint2 : projectileSpawnPoint;

        if (useSecond && spawnPoint == null) return;

        if (projPrefab == null || spawnPoint == null)
        {
            Debug.LogWarning("[WeaponBehavior] (SO) projectilePrefab 또는 projectileSpawnPoint 미연결");
            return;
        }

        var gun = data as WeaponDataSO_Gun;
        if (gun != null && gun.usesAmmo)
        {
            if (ammoRuntime == null)
                ammoRuntime = GetComponent<WeaponAmmoRuntime>();

            if (ammoRuntime == null)
            {
                Debug.LogWarning("[WeaponBehavior] SpawnProjectile: usesAmmo Gun인데 WeaponAmmoRuntime이 없어 발사를 취소합니다.");
                return;
            }

            if (!ammoRuntime.TryConsumeForShot(gun.consumePerShot))
            {
                if (!ammoRuntime.IsReloading && gun.autoReloadOnEmpty)
                    ammoRuntime.TryStartReload();

                if (!ammoRuntime.HasAnyReserveOrInfinite())
                {
                    var pwcFallback = Object.FindFirstObjectByType<PlayerWeaponController>();
                    if (pwcFallback != null) pwcFallback.RequestSwitchToDefault();
                }
                return;
            }
        }

        PlayerWeaponController playerCtrl = Object.FindFirstObjectByType<PlayerWeaponController>();
        Vector3 shootDir = playerCtrl ? playerCtrl.transform.forward : transform.forward;

        Transform targetTransform = null;
        if (playerCtrl != null && gun != null)
        {
            targetTransform = ChooseAimTarget(playerCtrl, spawnPoint, gun);
        }

        if (targetTransform != null)
        {
            Vector3 rawTarget = targetTransform.position;
            Vector3 targetPos = new Vector3(rawTarget.x, spawnPoint.position.y, rawTarget.z);

            Vector3 horiz = targetPos - spawnPoint.position;
            horiz.y = 0f;
            if (horiz.sqrMagnitude < 0.0001f) horiz = spawnPoint.forward;
            Vector3 dirXZ = horiz.normalized;

            GameObject bulletGO = Instantiate(projPrefab, spawnPoint.position, Quaternion.LookRotation(dirXZ, Vector3.up));

            if (bulletGO.TryGetComponent(out HitBox_PC_Projectile_Sector sectorProj))
            {
                Vector3 ppos = bulletGO.transform.position;
                ppos.y = spawnPoint.position.y;
                bulletGO.transform.position = ppos;
                GameObject modRoot = transform.root != null ? transform.root.gameObject : gameObject;
                sectorProj.Initialize(this.data, dirXZ, modRoot);
                return;
            }

            if (bulletGO.TryGetComponent(out HitBox_PC_Projectile proj))
            {
                proj.SetWeapon(this.data);

                float spd = 10f, life = 5f;
                int pierce = 0;
                if (data is WeaponDataSO_Gun g2) { spd = g2.projectileSpeed; life = g2.projectileLifetime; pierce = g2.pierceCount; }
                else if (data is WeaponDataSO_Launcher l2) { spd = l2.projectileSpeed; life = l2.projectileLifetime; }
                else if (data is WeaponDataSO_Sniper sn2) { spd = sn2.projectileSpeed; life = sn2.projectileLifetime; pierce = sn2.pierceCount; }
                else if (data is WeaponDataSO_AR ar2) { spd = ar2.projectileSpeed; life = ar2.projectileLifetime; pierce = ar2.pierceCount; }
                pierce = Mathf.Max(0, pierce + GetAdditionalPierceForWeapon(data));

                float dmg = ScaleOutgoingDamageForWeapon(data, data.damage);
                if (data is WeaponDataSO_Sniper snInit2)
                {
                    proj.InitializeSniper(dirXZ, dmg, spd, life, pierce, snInit2.ricochetLayers, snInit2.ricochetSpeedMultiplier);
                }
                else if (pierce > 0) proj.InitializeTowardsTargetPosition(targetPos, dmg, spd, life, pierce, true);
                else proj.InitializeTowardsTargetPosition(targetPos, dmg, spd, life, true);

                if (TryRollBonusShot(out PlayerWeaponDamageModifiers.BonusShotConfig bonusCfg))
                {
                    bool capUseSecond = useSecond;
                    WeaponDataSO wAt = data;
                    float lat = bonusCfg.lateralOffsetMeters;
                    float del = bonusCfg.delayUnscaledSeconds;
                    ScheduleBonusShotSpawn(wAt, del, () =>
                    {
                        SpawnBonusStandardProjectileParallel(capUseSecond, dirXZ, dmg, spd, life, pierce, lat);
                    });
                }

                return;
            }

            Debug.LogWarning("[WeaponBehavior] 발사체에서 지원 컴포넌트를 찾지 못했습니다.");
            return;
        }

        Vector3 dir = shootDir;
        if (!preserveVertical) dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();

        GameObject fallbackGO = Instantiate(projPrefab, spawnPoint.position, Quaternion.LookRotation(dir));

        if (fallbackGO.TryGetComponent(out HitBox_PC_Projectile_Sector sector2))
        {
            Vector3 fwdXZ = dir; fwdXZ.y = 0f;
            if (fwdXZ.sqrMagnitude < 0.0001f) fwdXZ = transform.forward;
            fwdXZ.Normalize();
            GameObject modRoot2 = transform.root != null ? transform.root.gameObject : gameObject;
            sector2.Initialize(this.data, fwdXZ, modRoot2);
            return;
        }

        if (fallbackGO.TryGetComponent(out HitBox_PC_Projectile p))
        {
            p.SetWeapon(this.data);

            float spd = 10f, life = 5f;
            int pierce = 0;
            if (data is WeaponDataSO_Gun g3) { spd = g3.projectileSpeed; life = g3.projectileLifetime; pierce = g3.pierceCount; }
            else if (data is WeaponDataSO_Launcher l3) { spd = l3.projectileSpeed; life = l3.projectileLifetime; }
            else if (data is WeaponDataSO_Sniper sn3) { spd = sn3.projectileSpeed; life = sn3.projectileLifetime; pierce = sn3.pierceCount; }
            else if (data is WeaponDataSO_AR ar3) { spd = ar3.projectileSpeed; life = ar3.projectileLifetime; pierce = ar3.pierceCount; }
            pierce = Mathf.Max(0, pierce + GetAdditionalPierceForWeapon(data));

            float dmgP = ScaleOutgoingDamageForWeapon(data, data.damage);
            if (data is WeaponDataSO_Sniper snInit3)
                p.InitializeSniper(dir, dmgP, spd, life, pierce, snInit3.ricochetLayers, snInit3.ricochetSpeedMultiplier);
            else if (pierce > 0) p.InitializeTowards(dir, dmgP, spd, life, pierce);
            else p.InitializeTowards(dir, dmgP, spd, life);

            if (TryRollBonusShot(out PlayerWeaponDamageModifiers.BonusShotConfig bonusCfg2))
            {
                bool capUseSecond2 = useSecond;
                WeaponDataSO wAt2 = data;
                float lat2 = bonusCfg2.lateralOffsetMeters;
                float del2 = bonusCfg2.delayUnscaledSeconds;
                ScheduleBonusShotSpawn(wAt2, del2, () =>
                {
                    SpawnBonusStandardProjectileParallel(capUseSecond2, dir, dmgP, spd, life, pierce, lat2);
                });
            }

            return;
        }

        Debug.LogWarning("[WeaponBehavior] projectile prefab does not contain supported projectile script.");
    }

    private void SpawnShotgunProjectiles(bool useSecond)
    {
        var sg = data as WeaponDataSO_Shotgun;
        if (sg == null)
        {
            Debug.LogWarning("[WeaponBehavior] SpawnShotgunProjectiles 호출되었으나 data가 Shotgun이 아님");
            return;
        }

        if (useSecond && projectileSpawnPoint2 == null)
        {
            projectileSpawnPoint2 = ResolveSecondProjectileSpawnPoint();
        }

        Transform spawnPoint = useSecond ? projectileSpawnPoint2 : projectileSpawnPoint;
        if (spawnPoint == null)
        {
            if (meleeSpawnPoint != null)
            {
                spawnPoint = meleeSpawnPoint;
            }
            else
            {
                spawnPoint = transform;
            }
        }

        var projPrefab = data.projectilePrefab;
        if (projPrefab == null)
        {
            Debug.LogWarning("[WeaponBehavior] SpawnShotgunProjectiles: projectilePrefab 미할당 (SO)");
            return;
        }

        // 탄 소비: 1 트리거(이 메서드 1회) = 1 소모
        bool allowSpawn = true;
        if (sg.usesAmmo)
        {
            var ammo = ammoRuntime != null ? ammoRuntime : GetComponent<WeaponAmmoRuntime>();
            if (ammo == null)
            {
                Debug.LogWarning("[WeaponBehavior] SpawnShotgunProjectiles: WeaponAmmoRuntime 없음 (비탄약 처리)");
            }
            else
            {
                if (!ammo.TryConsumeForShot(sg.consumePerShot))
                {
                    if (!ammo.IsReloading && sg.autoReloadOnEmpty)
                        ammo.TryStartReload();

                    if (!ammo.HasAnyReserveOrInfinite())
                    {
                        var pwcFallback = Object.FindFirstObjectByType<PlayerWeaponController>();
                        if (pwcFallback != null) pwcFallback.RequestSwitchToDefault();
                    }
                    allowSpawn = false;
                }
            }
        }

        if (!allowSpawn) return;

        var owner = GetComponentInParent<PlayerWeaponController>();
        Vector3 forward = owner != null ? owner.transform.forward : transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        int pelletCount = Mathf.Max(1, sg.pelletCount);
        bool bonusShotThisVolley = TryRollBonusShot(out PlayerWeaponDamageModifiers.BonusShotConfig bonusShotCfg);
        int bonusPelletIndex = bonusShotThisVolley ? Random.Range(0, pelletCount) : -1;
        float spread = Mathf.Clamp(sg.spreadAngle, 0f, 360f);
        float startAngle = -spread * 0.5f;
        float step = pelletCount > 1 ? spread / (pelletCount - 1) : 0f;
        float pelletDamage = ScaleOutgoingDamageForWeapon(data, sg.damagePerPellet);

        for (int i = 0; i < pelletCount; i++)
        {
            float angle = pelletCount > 1 ? (startAngle + step * i) : 0f;
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = forward;
            dir.Normalize();

            GameObject pelletGO = Instantiate(projPrefab, spawnPoint.position, Quaternion.LookRotation(dir, Vector3.up));
            if (pelletGO.TryGetComponent(out HitBox_PC_Projectile proj))
            {
                proj.SetWeapon(this.data);
                proj.InitializeShotgun(
                    dir,
                    pelletDamage,
                    sg.projectileSpeed,
                    sg.maxTravelDistance,
                    sg.falloffStartDistance,
                    sg.minDamageMultiplier,
                    Mathf.Max(0, sg.pierceCount + GetAdditionalPierceForWeapon(data)));

                if (bonusShotThisVolley && i == bonusPelletIndex)
                {
                    bool capSecond = useSecond;
                    WeaponDataSO wAt = data;
                    WeaponDataSO_Shotgun sgCap = sg;
                    float dmgCap = pelletDamage;
                    Vector3 dirCap = dir;
                    int pierceCap = Mathf.Max(0, sg.pierceCount + GetAdditionalPierceForWeapon(data));
                    float latCap = bonusShotCfg.lateralOffsetMeters;
                    float delCap = bonusShotCfg.delayUnscaledSeconds;
                    ScheduleBonusShotSpawn(wAt, delCap, () =>
                    {
                        SpawnBonusShotgunPelletParallel(capSecond, dirCap, dmgCap, sgCap, pierceCap, latCap);
                    });
                }

                continue;
            }

            Debug.LogWarning("[WeaponBehavior] SpawnShotgunProjectiles: projectilePrefab에 HitBox_PC_Projectile 컴포넌트가 없습니다.");
            Destroy(pelletGO);
        }
    }

    public void FireProjectileForced(Vector3 shootDir, bool preserveVerticalLocal = false, bool useSecond = false, float damageMultiplier = 1f)
    {
        if (data == null)
        {
            Debug.LogWarning("[WeaponBehavior] FireProjectileForced: data가 null");
            return;
        }

        var projPrefab = data.projectilePrefab;
        if (projPrefab == null)
        {
            Debug.LogWarning("[WeaponBehavior] (SO) projectilePrefab 미연결 (Forced)");
            return;
        }

        Transform spawnPoint;
        if (useSecond)
        {
            if (projectileSpawnPoint2 == null)
                projectileSpawnPoint2 = ResolveSecondProjectileSpawnPoint();

            // 왼손 스폰포인트를 못 찾으면 강제 발사를 생략(오른손 중복 발사 방지)
            if (projectileSpawnPoint2 == null)
                return;

            spawnPoint = projectileSpawnPoint2;
        }
        else
        {
            spawnPoint = projectileSpawnPoint != null ? projectileSpawnPoint
                        : (meleeSpawnPoint != null ? meleeSpawnPoint : transform);
        }

        Vector3 dir = shootDir;
        if (!preserveVerticalLocal) dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();

        GameObject bulletGO = Instantiate(projPrefab, spawnPoint.position, Quaternion.LookRotation(dir, Vector3.up));

        if (bulletGO.TryGetComponent(out HitBox_PC_Projectile_Sector sectorProj))
        {
            GameObject modRootF = transform.root != null ? transform.root.gameObject : gameObject;
            sectorProj.Initialize(this.data, dir, modRootF);
            return;
        }

        if (bulletGO.TryGetComponent(out HitBox_PC_Projectile proj))
        {
            proj.SetWeapon(this.data);

            float spd = 10f, life = 5f;
            int pierce = 0;

            if (data is WeaponDataSO_Gun g) { spd = g.projectileSpeed; life = g.projectileLifetime; pierce = g.pierceCount; }
            else if (data is WeaponDataSO_Launcher l) { spd = l.projectileSpeed; life = l.projectileLifetime; }
            else if (data is WeaponDataSO_Sniper sn) { spd = sn.projectileSpeed; life = sn.projectileLifetime; pierce = sn.pierceCount; }
            else if (data is WeaponDataSO_AR ar) { spd = ar.projectileSpeed; life = ar.projectileLifetime; pierce = ar.pierceCount; }
            pierce = Mathf.Max(0, pierce + GetAdditionalPierceForWeapon(data));

            float dmgF = ScaleOutgoingDamageForWeapon(data, data.damage) * Mathf.Max(0f, damageMultiplier);
            if (data is WeaponDataSO_Sniper snInit)
                proj.InitializeSniper(dir, dmgF, spd, life, pierce, snInit.ricochetLayers, snInit.ricochetSpeedMultiplier);
            else if (pierce > 0) proj.InitializeTowards(dir, dmgF, spd, life, pierce);
            else proj.InitializeTowards(dir, dmgF, spd, life);

            if (TryRollBonusShot(out PlayerWeaponDamageModifiers.BonusShotConfig bonusCfgF))
            {
                bool capUseSecondF = useSecond;
                WeaponDataSO wAtF = data;
                float latF = bonusCfgF.lateralOffsetMeters;
                float delF = bonusCfgF.delayUnscaledSeconds;
                ScheduleBonusShotSpawn(wAtF, delF, () =>
                {
                    SpawnBonusStandardProjectileParallel(capUseSecondF, dir, dmgF, spd, life, pierce, latF);
                });
            }

            return;
        }

        Debug.LogWarning("[WeaponBehavior] 지원 컴포넌트를 찾지 못한 발사체(Forced)");
    }

    // Preview Line and UpdatePreviewSector unchanged (omitted here for brevity in this block)
    // But in actual project please keep the EnsurePreviewLine() and UpdatePreviewSector(...) methods below.
    private void EnsurePreviewLine()
    {
        if (previewLR != null) return;

        previewLR = GetComponent<LineRenderer>();
        if (previewLR == null)
            previewLR = gameObject.AddComponent<LineRenderer>();

        previewLR.useWorldSpace = true;
        previewLR.loop = false;
        previewLR.alignment = LineAlignment.View;
        previewLR.widthMultiplier = 0.02f;

        if (previewMat == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) previewMat = new Material(shader);
            else previewMat = new Material(Shader.Find("Unlit/Color"));
        }

        previewLR.material = previewMat;
        previewLR.positionCount = 0;
        previewLR.enabled = false;
    }

    private void UpdatePreviewSector(Vector3 center, Vector3 forward, float radius, float angle, Color color)
    {
        if (previewLR == null) return;

        radius = Mathf.Max(0f, radius);
        angle = Mathf.Clamp(angle, 0f, 360f);

        if (radius <= 0.0001f || angle <= 0.0001f)
        {
            previewLR.positionCount = 0;
            previewLR.enabled = false;
            return;
        }

        previewLR.enabled = true;
        previewLR.startColor = color;
        previewLR.endColor = color;

        int seg = Mathf.Max(3, kPreviewSegments);
        int count = seg + 1;
        previewLR.positionCount = count;

        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        float half = angle * 0.5f;
        float start = -half;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / seg;
            float a = start + angle * t;
            Quaternion rot = Quaternion.AngleAxis(a, Vector3.up);
            Vector3 dir = rot * forward;

            previewLR.SetPosition(i, center + dir * radius);
        }
    }
}