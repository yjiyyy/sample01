using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// EnemyAttackController partial: AoE 패턴 통합 (디버그 마커 primitive 사용, spawn-delay = hitboxActivationDelay 적용)
/// 변경 요약:
/// - prepareDuration: 첫 지연(기존)
/// - 각 히트박스마다 플레이어 위치를 다시 조회해서 디버그 마커 생성
/// - hitboxActivationDelay 는 "Instantiate 전 대기"로 동작 (Instantiate 후 비활성화/재활성화 없음)
/// - SpawnHitboxesSequence는 AoESequence와 분리되어 AoESequence가 끝나도 예정된 스폰은 계속 수행됨
/// </summary>
public partial class EnemyAttackController
{
    public bool IsAoEExecuting => aoeCoroutine != null;
    private Coroutine aoeCoroutine;
    private int runningAoEIndex = -1;
    private float aoeStartTime = 0f;
    private bool animFrozenByAoE = false;

    private void StartAoE(AoEAttackData data, Transform target, int index)
    {
        if (data == null) return;

        MarkExecuted();
        ClearHold();

        StopAoECoroutine();

        runningAoEIndex = index;
        enemy.SetState(Enemy.EnemyState.Attack);

        aoeStartTime = Time.time;
        animFrozenByAoE = false;

        // 애니메이션 재생 (즉시)
        if (enemy.animator != null)
        {
            if (data.attackClip != null)
            {
                enemy.animator.speed = 1f;
                enemy.animator.Play(data.attackClip.name, 0, 0f);
            }
            else if (!string.IsNullOrEmpty(data.attackStateName))
            {
                SafeSetBool("IsAttackPrepare", true);
                enemy.animator.Play(data.attackStateName);
            }

            if (data.attackClip != null)
            {
                float clipLen = data.attackClip.length;
                if (data.attackDuration > clipLen)
                    StartCoroutine(ScheduleAnimFreeze(data, clipLen));
            }
        }

        // AoE main sequence: prepareDuration 후 spawn 코루틴 시작, 본체는 attackDuration 기준으로 종료
        aoeCoroutine = StartCoroutine(AoESequence(data));
        Log($"AOE START idx={index} name={data.attackName}");
    }

    private IEnumerator ScheduleAnimFreeze(AoEAttackData data, float clipLen)
    {
        float elapsed = Time.time - aoeStartTime;
        float toWait = clipLen - elapsed;
        if (toWait > 0f) yield return new WaitForSeconds(toWait);

        float elapsedTotal = Time.time - aoeStartTime;
        if (elapsedTotal < data.attackDuration && enemy != null && enemy.animator != null)
        {
            enemy.animator.speed = 0f;
            animFrozenByAoE = true;
            Log("[EnemyAttackController] Animation frozen to hold last frame for AoE duration");
        }
    }

    private IEnumerator AoESequence(AoEAttackData data)
    {
        // 1) Prepare (prepareDuration)
        float prep = Mathf.Max(0f, data.prepareDuration);
        if (prep > 0f) yield return new WaitForSeconds(prep);

        // 2) spawn base 결정 (플레이어 위치는 SpawnHitboxesSequence에서 매 스폰마다 재조회)
        Vector3 spawnBase = transform.position;
        if (data.spawnMode == AoEAttackData.SpawnMode.SpawnAtPlayerPosition)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                spawnBase = player.transform.position;
        }

        // 3) SO에 따라 디버그 마커 생성 (primitive sphere) — 최초 기준 위치(보이는 기준)
        if (data.spawnDebugMarker && debugDecisionLogs)
        {
            SpawnDebugMarkerAt(data, spawnBase);
        }

        // 4) 스폰 시퀀트를 별도 코루틴으로 시작 (비동기). 이 코루틴은 AoESequence가 끝나도 계속 실행됨.
        StartCoroutine(SpawnHitboxesSequence(data, spawnBase));

        // 5) AoE 본체는 attackDuration 기준으로 종료 (몬스터는 이 시점에 상태 전환됨)
        float totalElapsed = Time.time - aoeStartTime;
        float remaining = data.attackDuration - totalElapsed;
        if (remaining > 0f)
        {
            Log($"AOE waiting remaining attackDuration: {remaining:F2}s");
            yield return new WaitForSeconds(remaining);
        }

        // 애니메이터/상태 정리
        SafeSetBool("IsAttackPrepare", false);

        if (animFrozenByAoE && enemy != null && enemy.animator != null)
        {
            enemy.animator.speed = 1f;
            animFrozenByAoE = false;
        }

        if (enemy != null)
        {
            enemy.SetState(Enemy.EnemyState.Chase, true);
            if (enemy.animator != null) enemy.animator.speed = 1f;
        }

        ApplyPerAttackCooldown(runningAoEIndex, data.cooldown);
        ApplyGlobalCooldown();

        Log($"AOE END idx={runningAoEIndex} name={data.attackName}");

        runningAoEIndex = -1;
        aoeCoroutine = null;
        yield break;
    }

    // SpawnHitboxesSequence: 매 반복마다 (SpawnAtPlayerPosition 모드면) 플레이어 위치 재조회,
    // 해당 위치에 Debug sphere 생성 -> hitboxActivationDelay 대기 -> 히트박스 Instantiate 및 Initialize 수행
    private IEnumerator SpawnHitboxesSequence(AoEAttackData data, Vector3 initialSpawnBase)
    {
        for (int i = 0; i < data.spawnCount; i++)
        {
            Vector3 spawnBase = initialSpawnBase;

            // 매 스폰마다 플레이어 위치를 재조회해서 추적(요청에 따름)
            if (data.spawnMode == AoEAttackData.SpawnMode.SpawnAtPlayerPosition)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    spawnBase = player.transform.position;
            }

            // 스폰 위치 결정 (플레이어 기준 또는 몬스터 기준)
            Vector3 pos;
            if (data.spawnMode == AoEAttackData.SpawnMode.RandomAroundEnemy && initialSpawnBase == transform.position)
            {
                Vector2 rnd = Random.insideUnitCircle * data.spawnRadius;
                pos = transform.position + new Vector3(rnd.x, 0f, rnd.y);
            }
            else if (data.spawnMode == AoEAttackData.SpawnMode.RandomAroundEnemy)
            {
                // If initialSpawnBase isn't transform (defensive), still use transform for randomness
                Vector2 rnd = Random.insideUnitCircle * data.spawnRadius;
                pos = transform.position + new Vector3(rnd.x, 0f, rnd.y);
            }
            else
            {
                Vector2 ornd = Random.insideUnitCircle * data.spawnAroundPlayerRadius;
                pos = spawnBase + new Vector3(ornd.x, 0f, ornd.y);
            }

            pos = ApplyGroundSnap(pos, data.groundMask);

            // 디버그 마커는 매 스폰 시마다 현재 pos에 생성 (primitive sphere)
            if (data.spawnDebugMarker && debugDecisionLogs)
            {
                SpawnDebugSphereAt(pos, data);
            }

            // hitboxActivationDelay 만큼 기다린 뒤에 실제 히트박스 Instantiate
            if (data.hitboxActivationDelay > 0f)
            {
                // 클램프는 하지 않음(사용자 의도: delay 동안 스폰을 미룸). 단, 너무 길면 공격이 끝난 뒤에도 계속 스폰됨.
                yield return new WaitForSeconds(data.hitboxActivationDelay);
            }
            else
            {
                yield return null; // 한 프레임 대기해서 안정화
            }

            // 실제 히트박스 스폰 (Instantiate + Initialize)
            var hb = SpawnAoEHitbox(data, pos);
            if (hb != null)
            {
                Debug.Log($"[EnemyAttackController] Hitbox INSTANCED at {pos} (index {i})");
            }
            else
            {
                Debug.LogWarning($"[EnemyAttackController] Failed to instantiate hitbox prefab for AoE at {pos} (index {i})");
            }

            // spawnInterval 대기 (다음 스폰 위치/마커를 위한 간격)
            if (data.spawnInterval > 0f)
                yield return new WaitForSeconds(data.spawnInterval);
            else
                yield return null;
        }

        yield break;
    }

    private void SpawnDebugSphereAt(Vector3 pos, AoEAttackData data)
    {
        GameObject dbg = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var dbgCol = dbg.GetComponent<Collider>();
        if (dbgCol != null) GameObject.Destroy(dbgCol);
        dbg.name = $"AoE_DebugMarker";
        dbg.transform.position = pos;
        dbg.transform.localScale = Vector3.one * 0.35f;
        var rend = dbg.GetComponent<Renderer>();
        if (rend != null)
        {
            var mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0f, 0.6f, 1f, 0.45f);
            rend.material = mat;
        }

        float dbgLife = Mathf.Max(0.5f, data.attackDuration); // 보이는 시간: 공격 전체 길이 기준
        GameObject.Destroy(dbg, dbgLife);
        Debug.Log($"[EnemyAttackController] Debug sphere spawned at {pos} (life {dbgLife:F2}s)");
    }

    private Vector3 ApplyGroundSnap(Vector3 pos, LayerMask groundMask)
    {
        if (groundMask == 0) return pos;
        Vector3 castOrigin = pos + Vector3.up * 1.5f;
        if (Physics.Raycast(castOrigin, Vector3.down, out RaycastHit hit, 5f, groundMask, QueryTriggerInteraction.Ignore))
            pos.y = hit.point.y + 0.01f;
        return pos;
    }

    // SpawnAoEHitbox: 이제 activation delay는 여기서 처리하지 않음.
    // 즉시 Instantiate 하고 Initialize를 호출한다.
    private GameObject SpawnAoEHitbox(AoEAttackData data, Vector3 atPosition)
    {
        if (data.hitBoxPrefab == null)
        {
            Debug.LogWarning("[EnemyAttackController] AoE hitBoxPrefab not assigned.");
            return null;
        }

        GameObject go = Instantiate(data.hitBoxPrefab, atPosition, Quaternion.identity);
        go.SetActive(true);
        if (data.attachHitboxToEnemy) go.transform.SetParent(transform, true);

        string parentInfo = go.transform.parent != null ? go.transform.parent.name : "null";
        Debug.Log($"[EnemyAttackController] Spawned AoE prefab '{data.hitBoxPrefab.name}' -> instance '{go.name}' at {atPosition} active:{go.activeSelf} parent:{parentInfo}");

        // ensure colliders exist / enabled (prefab이 항상 활성화된다고 가정하나 안전성 보완)
        var childColliders = go.GetComponentsInChildren<Collider>(true);
        if (childColliders == null || childColliders.Length == 0)
        {
            var sc = go.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 0.5f;
            Debug.Log($"[EnemyAttackController] No collider found on prefab instance '{go.name}'. Added debug SphereCollider (isTrigger=true, radius=0.5).");
        }
        else
        {
            foreach (var c in childColliders)
            {
                if (c == null) continue;
                if (!c.enabled) c.enabled = true;
                if (!c.isTrigger) c.isTrigger = true;
            }
        }

        // Try initialize HitBox component if present (immediately)
        Component foundHitBoxComp = null;
        foreach (var comp in go.GetComponentsInChildren<Component>(true))
        {
            if (comp == null) continue;
            var t = comp.GetType();
            if (t.Name.ToLower().Contains("hitbox"))
            {
                foundHitBoxComp = comp;
                Debug.Log($"[EnemyAttackController] Found hitbox component: {t.Name} on {go.name}");
                break;
            }
        }

        if (foundHitBoxComp != null)
        {
            bool initOk = false;
            var methods = foundHitBoxComp.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);
            foreach (var m in methods)
            {
                if (m.Name != "Initialize") continue;
                var ps = m.GetParameters();
                var args = BuildArgsForInitialize(ps, data);
                if (args == null) continue;
                try
                {
                    m.Invoke(foundHitBoxComp, args);
                    initOk = true;
                    Debug.Log($"[EnemyAttackController] Called Initialize on {foundHitBoxComp.GetType().Name} (args count {args.Length})");
                    break;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[EnemyAttackController] Initialize invoke failed on {foundHitBoxComp.GetType().Name}: {ex.Message}");
                }
            }

            if (!initOk)
            {
                Debug.Log($"[EnemyAttackController] Initialize not invoked or not found on component, using fallback destroy scheduling for {go.name}");
                Destroy(go, data.hitBoxLifetime);
            }
        }
        else
        {
            Debug.Log($"[EnemyAttackController] No HitBox component found on prefab '{data.hitBoxPrefab.name}'. Scheduling destroy for {go.name}");
            Destroy(go, data.hitBoxLifetime);
        }

        return go;
    }

    private object[] BuildArgsForInitialize(System.Reflection.ParameterInfo[] ps, AoEAttackData data)
    {
        if (ps == null) return null;

        // HitBox_Enemy.Initialize signature:
        // Initialize(float dmg, float rng, float kbPower, float kbDuration, float lifetime, float stun = 0f, bool allowDup = false, float dupInterval = 0f)
        var list = new List<object>();

        for (int i = 0; i < ps.Length; i++)
        {
            var ptype = ps[i].ParameterType;

            if (ptype == typeof(float))
            {
                if (i == 0) list.Add(data.damage);                // dmg
                else if (i == 1) list.Add(data.spawnRadius);     // rng
                else if (i == 2) list.Add(data.knockbackPower);  // kbPower
                else if (i == 3) list.Add(data.knockbackDuration); // kbDuration
                else if (i == 4) list.Add(data.hitBoxLifetime);  // lifetime
                else if (i == 5) list.Add(data.stunDuration);    // stun
                else list.Add(0f);
            }
            else if (ptype == typeof(bool))
            {
                list.Add(data.allowDuplicateHit);
            }
            else if (ptype == typeof(int))
            {
                list.Add(0);
            }
            else if (ptype == typeof(WeaponDataSO))
            {
                list.Add(null); // AoE: 처치 연출 없음이면 Animation 죽음
            }
            else
            {
                return null;
            }
        }

        return list.ToArray();
    }

    private void StopAoECoroutine()
    {
        if (aoeCoroutine != null)
        {
            StopCoroutine(aoeCoroutine);
            aoeCoroutine = null;
            runningAoEIndex = -1;
        }
    }

    private void InterruptAoEIfNeeded()
    {
        if (aoeCoroutine != null)
        {
            StopAoECoroutine();
            SafeSetBool("IsAttackPrepare", false);
            if (animFrozenByAoE && enemy != null && enemy.animator != null)
            {
                enemy.animator.speed = 1f;
                animFrozenByAoE = false;
            }
            Log("AOE INTERRUPTED (note: spawned hitboxes may continue until their lifetime ends)");
        }
    }
    private void SpawnDebugMarkerAt(AoEAttackData data, Vector3 pos)
    {
        // 기존에 구현한 SpawnDebugSphereAt(pos, data)를 재사용합니다.
        // 만약 SpawnDebugSphereAt가 없다면 아래에 직접 Sphere 생성 코드를 넣어도 됩니다.
        SpawnDebugSphereAt(pos, data);
    }
}