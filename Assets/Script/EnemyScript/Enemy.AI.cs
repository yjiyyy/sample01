// 전체 파일(원본에 기능 추가한 버전)
using System.Collections;
using UnityEngine;

/// <summary>
/// NavMeshAgent 제거 버전 EnemyAI
/// - 이동/회전 직접 Transform, 실제 적용은 Enemy.FixedUpdate
/// - Backstep / Forward / Idle 로직 유지
/// - 속도 보간(signedForwardSpeed) 그대로 이용
/// 
/// 추가된 기능:
/// - AIState: Peace(배회), Finding(Find 애니 실행 대기), Combat(플레이어 인식 후 기존 동작)
/// - 평화 모드에서 스폰 지점 기준 랜덤 배회
/// - 플레이어가 detectionRadius 이내로 들어오면 Find 트리거 재생, findDuration 후 전투 모드로 전환
/// </summary>
[DisallowMultipleComponent]
public class EnemyAI : MonoBehaviour
{
    [Header("백스텝 중심 거리 (±1 밴드)")]
    public float backstepDistance = 5f;

    [Header("백스텝 속도 계수")]
    public float backstepSpeedMultiplier = 1.0f;

    [Header("Forward 속도 정규화 시간")]
    [Range(0.1f, 2f)] public float forwardSpeedNormalizeTime = 0.25f;

    [Header("Peace / Detection")]
    [Tooltip("플레이어를 발견하는 반경 (유닛)")]
    public float detectionRadius = 8f;
    [Tooltip("Find 애니메이션 재생 대기 시간(초) - Animator와 동일하게 설정하세요.")]
    public float findDuration = 0.8f;
    [Tooltip("스폰 지점 기준 배회 반경")]
    public float roamRadius = 3f;
    [Range(0.05f, 1f), Tooltip("평화 모드에서 이동할 때의 속도(기본은 기본 moveSpeed의 비율)")]
    public float peaceMoveSpeedMultiplier = 0.6f;
    [Tooltip("평화 모드에서 Idle 최소/최대 대기시간")]
    public float idleMin = 1f;
    public float idleMax = 3f;

    [Header("Chase tracking")]
    [Tooltip("전투 추적 이동 시 플레이어 위치 갱신 주기. 발견·공격은 실시간.")]
    public EnemyChaseTrackingMode chaseTrackingMode = EnemyChaseTrackingMode.ThreeSeconds;

    private Enemy enemy;
    private EnemyAttackController attackCtrl;

    private bool backstepping;
    private float signedForwardSpeed;
    private float forwardSpeedLerpT;
    private bool distanceMaintenanceModeLast = false;

    private float LowerBand => backstepDistance - 1f;
    private float UpperBand => backstepDistance + 1f;

    private enum AIState { Peace, Finding, Combat }
    private AIState aiState = AIState.Peace;

    /// <summary>표정 시스템용 AI 페이즈 (Animator와 별개).</summary>
    public enum FacePhase { Peace, Finding, Combat }

    public FacePhase CurrentFacePhase { get; private set; } = FacePhase.Peace;

    private Vector3 spawnPosition;
    private Vector3 roamTarget;
    private bool hasRoamTarget = false;
    private float idleTimer = 0f;
    private Coroutine findingCoroutine;

    private Vector3 cachedChasePosition;
    private bool chaseCacheInitialized;
    private float nextChaseRefreshTime;
    private const float CHASE_TARGET_REACHED_EPS = 0.05f;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        attackCtrl = GetComponent<EnemyAttackController>();
        spawnPosition = transform.position;
        aiState = AIState.Peace;
        SetFacePhase(FacePhase.Peace);
    }

    private void SetFacePhase(FacePhase phase)
    {
        CurrentFacePhase = phase;
    }

    public void Tick(Enemy ctx, Transform player)
    {
        if (ctx == null || player == null) return;

        if (ctx.CurrentState == Enemy.EnemyState.Dead ||
            ctx.CurrentState == Enemy.EnemyState.ShieldBreak ||
            ctx.CurrentState == Enemy.EnemyState.Stunned ||
            ctx.CurrentState == Enemy.EnemyState.Knockback)
        {
            if (backstepping) ForceClearBackstep();
            return;
        }

        if (aiState == AIState.Combat)
            UpdateChaseTargetCache(false);

        if (ctx.CurrentState == Enemy.EnemyState.Attack)
        {
            HandleAttackFacing(ctx, player);
            ctx.animCtrl?.SetSignedSpeed(0f);
            return;
        }

        switch (aiState)
        {
            case AIState.Peace:
                PeaceTick(ctx, player);
                break;
            case AIState.Finding:
                IdleFacing(ctx, player);
                break;
            case AIState.Combat:
                if (backstepping) UpdateBackstep(ctx, player);
                else DriveDecision(ctx, player);
                break;
        }
    }

    private void PeaceTick(Enemy ctx, Transform player)
    {
        Vector3 livePlayerPos = GetLivePlayerPosition(player);
        float sqrDist = (livePlayerPos - ctx.transform.position).sqrMagnitude;
        if (sqrDist <= detectionRadius * detectionRadius)
        {
            StartFinding(ctx, player);
            return;
        }

        if (hasRoamTarget)
        {
            Vector3 toTarget = roamTarget - ctx.transform.position;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;
            if (dist <= 0.25f)
            {
                hasRoamTarget = false;
                idleTimer = Random.Range(idleMin, idleMax);
                ctx.animCtrl?.SetSignedSpeed(0f);
            }
            else
            {
                Vector3 dir = toTarget.normalized;
                ctx.RequestLook(dir);
                ctx.RequestMove(dir, Mathf.Clamp01(peaceMoveSpeedMultiplier));
                ctx.animCtrl?.SetSignedSpeed(Mathf.Clamp01(peaceMoveSpeedMultiplier));
            }
        }
        else
        {
            if (idleTimer > 0f)
            {
                idleTimer -= Time.deltaTime;
                ctx.animCtrl?.SetSignedSpeed(0f);
            }
            else
            {
                Vector2 rnd2 = Random.insideUnitCircle * roamRadius;
                roamTarget = new Vector3(spawnPosition.x + rnd2.x, ctx.transform.position.y, spawnPosition.z + rnd2.y);
                hasRoamTarget = true;
                ctx.animCtrl?.SetSignedSpeed(Mathf.Clamp01(peaceMoveSpeedMultiplier));
            }
        }
    }

    private void StartFinding(Enemy ctx, Transform player)
    {
        if (aiState == AIState.Finding) return;
        aiState = AIState.Finding;
        SetFacePhase(FacePhase.Finding);

        signedForwardSpeed = 0f;
        forwardSpeedLerpT = 0f;

        if (findingCoroutine != null) StopCoroutine(findingCoroutine);
        findingCoroutine = StartCoroutine(FindingCoroutine(ctx, player));
    }

    private IEnumerator FindingCoroutine(Enemy ctx, Transform player)
    {
        ctx.animCtrl?.PlayFind();

        float t = 0f;
        while (t < findDuration)
        {
            if (ctx.CurrentState == Enemy.EnemyState.Dead ||
                ctx.CurrentState == Enemy.EnemyState.ShieldBreak ||
                ctx.CurrentState == Enemy.EnemyState.Stunned ||
                ctx.CurrentState == Enemy.EnemyState.Knockback)
            {
                aiState = AIState.Peace;
                SetFacePhase(FacePhase.Peace);
                findingCoroutine = null;
                yield break;
            }

            t += Time.deltaTime;
            yield return null;
        }

        aiState = AIState.Combat;
        SetFacePhase(FacePhase.Combat);
        findingCoroutine = null;

        signedForwardSpeed = 0f;
        forwardSpeedLerpT = 0f;
        ForceRefreshChaseTarget();
    }

    private void DriveDecision(Enemy ctx, Transform player)
    {
        if (attackCtrl == null)
        {
            ForwardChase(ctx, player);
            return;
        }

        float distance = Vector3.Distance(GetLivePlayerPosition(player), ctx.transform.position);
        string reason;
        bool distanceMaintenanceMode = ShouldDistanceMaintain(distance, out reason);

        if (attackCtrl.debugDecisionLogs && distanceMaintenanceMode != distanceMaintenanceModeLast)
        {
            Debug.Log($"[AI] DistanceMaintenance {(distanceMaintenanceMode ? "ON" : "OFF")} (reason={reason}, dist={distance:F2})");
            distanceMaintenanceModeLast = distanceMaintenanceMode;
        }

        if (distanceMaintenanceMode)
        {
            HandleDistanceMaintenance(ctx, player, distance);
            return;
        }

        int idx = attackCtrl.SelectAttackIndex(distance);
        if (idx >= 0)
        {
            float attackRange = attackCtrl.GetAttackRange(idx);
            if (distance <= attackRange)
            {
                if (attackCtrl.TryStartAttack(idx, player))
                    return;
            }
        }

        ForwardChase(ctx, player);
    }

    private bool ShouldDistanceMaintain(float distance, out string reason)
    {
        reason = "NONE";
        if (attackCtrl == null) return false;

        if (attackCtrl.IsGlobalCooling())
        {
            reason = "GLOBAL";
            return true;
        }

        // ✅ 최적화: 패턴 1개면 루프 없이 0번만 검사
        if (attackCtrl.AttackCount == 1)
        {
            const int idx = 0;

            bool ready = attackCtrl.IsOffCooldown(idx);
            float r = attackCtrl.GetAttackRange(idx);

            if (!ready)
            {
                if (distance <= UpperBand)
                {
                    reason = "ALL_COOLDOWN_WAIT_RING";
                    return true;
                }
                reason = "APPROACH_RING";
                return false;
            }

            // ready인데 사거리 밖이면 그냥 추적
            if (distance > r)
            {
                reason = "NO_RANGE";
                return false;
            }

            // ready인데 사거리 안인데(=지금 당장 공격 가능 거리)
            // 유지모드 할 이유 없음(바로 공격 시도)
            reason = "READY_IN_RANGE";
            return false;
        }

        bool anyReadyOverall = false;
        int count = attackCtrl.AttackCount;
        for (int i = 0; i < count; i++)
        {
            if (attackCtrl.IsOffCooldown(i)) { anyReadyOverall = true; break; }
        }

        if (!anyReadyOverall)
        {
            if (distance <= UpperBand)
            {
                reason = "ALL_COOLDOWN_WAIT_RING";
                return true;
            }
            reason = "APPROACH_RING";
            return false;
        }

        bool withinAnyAttackRange = false;
        bool anyReadyWithinDistance = false;
        for (int i = 0; i < count; i++)
        {
            float r = attackCtrl.GetAttackRange(i);
            if (distance <= r)
            {
                withinAnyAttackRange = true;
                if (attackCtrl.IsOffCooldown(i))
                {
                    anyReadyWithinDistance = true;
                    break;
                }
            }
        }

        if (!withinAnyAttackRange) { reason = "NO_RANGE"; return false; }
        if (!anyReadyWithinDistance) { reason = "IN_RANGE_WAIT"; return true; }

        reason = "READY_IN_RANGE";
        return false;
    }

    private void HandleDistanceMaintenance(Enemy ctx, Transform player, float distance)
    {
        if (!backstepping && distance >= LowerBand && distance <= UpperBand)
        {
            IdleFacing(ctx, player);
            return;
        }
        if (!backstepping && distance > UpperBand)
        {
            ForwardChase(ctx, player);
            return;
        }
        if (!backstepping && distance < LowerBand)
        {
            StartBackstep(ctx, player);
            return;
        }
        IdleFacing(ctx, player);
    }

    private void StartBackstep(Enemy ctx, Transform player)
    {
        backstepping = true;
        ctx.animCtrl?.SetSignedSpeed(-1f);
        if (attackCtrl?.debugDecisionLogs == true)
            Debug.Log($"[AI] Backstep START dist={Vector3.Distance(GetLivePlayerPosition(player), ctx.transform.position):F2}");
    }

    private void UpdateBackstep(Enemy ctx, Transform player)
    {
        if (!backstepping) return;

        Vector3 livePlayerPos = GetLivePlayerPosition(player);
        float dist = Vector3.Distance(livePlayerPos, ctx.transform.position);
        string reason;
        bool distanceMaintenanceMode = ShouldDistanceMaintain(dist, out reason);

        if (!distanceMaintenanceMode)
        {
            EndBackstep(ctx, true);

            if (attackCtrl != null)
            {
                int idx = attackCtrl.SelectAttackIndex(dist);
                if (idx >= 0)
                {
                    float rng = attackCtrl.GetAttackRange(idx);
                    if (dist <= rng && attackCtrl.TryStartAttack(idx, player))
                        return;
                }
            }

            ForwardChase(ctx, player);
            return;
        }

        Vector3 face = GetChaseTargetPosition(livePlayerPos) - ctx.transform.position;
        face.y = 0f;
        if (face.sqrMagnitude > 0.0001f)
            ctx.RequestLook(face.normalized);

        float backSpeed = ctx.moveSpeed * backstepSpeedMultiplier;
        if (face.sqrMagnitude > 0.0001f)
        {
            float speed01 = Mathf.Clamp01(backSpeed / Mathf.Max(ctx.moveSpeed, 0.0001f));
            ctx.RequestMove(-face.normalized, speed01);
        }

        ctx.animCtrl?.SetSignedSpeed(-1f);

        float distCurrent = face.magnitude;
        if (distCurrent >= backstepDistance ||
            (distCurrent >= LowerBand && distCurrent <= UpperBand))
        {
            EndBackstep(ctx, true);
        }
    }

    private void EndBackstep(Enemy ctx, bool success)
    {
        if (!backstepping) return;
        backstepping = false;
        ctx.animCtrl?.SetSignedSpeed(0f);
        if (attackCtrl?.debugDecisionLogs == true)
            Debug.Log($"[AI] Backstep END success={success}");
    }

    public void ForceClearBackstep()
    {
        if (!backstepping) return;
        backstepping = false;
        enemy?.animCtrl?.SetSignedSpeed(0f);
    }

    private void ForwardChase(Enemy ctx, Transform player)
    {
        if (backstepping) return;

        Vector3 livePlayerPos = GetLivePlayerPosition(player);
        Vector3 targetPos = GetChaseTargetPosition(livePlayerPos);
        Vector3 dir = targetPos - ctx.transform.position;
        dir.y = 0f;

        // 틱 모드에서 마지막 목표 지점에 도착하면 즉시 1회 재갱신해 제자리 걸음을 줄인다.
        if (chaseTrackingMode != EnemyChaseTrackingMode.Realtime &&
            dir.sqrMagnitude <= CHASE_TARGET_REACHED_EPS * CHASE_TARGET_REACHED_EPS)
        {
            ForceRefreshChaseTarget();
            targetPos = GetChaseTargetPosition(livePlayerPos);
            dir = targetPos - ctx.transform.position;
            dir.y = 0f;
        }

        if (dir.sqrMagnitude > 0.0001f)
            ctx.RequestLook(dir.normalized);

        forwardSpeedLerpT += Time.deltaTime / Mathf.Max(0.0001f, forwardSpeedNormalizeTime);
        signedForwardSpeed = Mathf.Lerp(signedForwardSpeed, 1f, forwardSpeedLerpT);
        signedForwardSpeed = Mathf.Clamp01(signedForwardSpeed);

        ctx.animCtrl?.SetSignedSpeed(signedForwardSpeed);
        if (dir.sqrMagnitude > 0.0001f)
            ctx.RequestMove(dir.normalized, signedForwardSpeed);
    }

    private void IdleFacing(Enemy ctx, Transform player)
    {
        Vector3 look = GetLivePlayerPosition(player) - ctx.transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.0001f)
            ctx.RequestLook(look.normalized);

        signedForwardSpeed = 0f;
        forwardSpeedLerpT = 0f;
        ctx.animCtrl?.SetSignedSpeed(0f);
    }

    private void HandleAttackFacing(Enemy ctx, Transform player)
    {
        if (attackCtrl != null && attackCtrl.IsRushing)
            return;
        Vector3 dir = GetLivePlayerPosition(player) - ctx.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            ctx.RequestLook(dir);
    }

    public void OnAttackStarted(Enemy ctx)
    {
        ctx.animCtrl?.SetSignedSpeed(0f);
        signedForwardSpeed = 0f;
        forwardSpeedLerpT = 0f;
    }

    public void OnAttackEnded(Transform player)
    {
        RefreshChaseTargetNow(player);
    }

    public void InterruptAttack()
    {
        if (backstepping)
            ForceClearBackstep();
    }

    /// <summary>
    /// Find를 건너뛰고 바로 Combat 모드로 전환. (Peace 중 피격 시 호출)
    /// </summary>
    public void SkipFindGoToCombat()
    {
        if (findingCoroutine != null)
        {
            StopCoroutine(findingCoroutine);
            findingCoroutine = null;
        }
        aiState = AIState.Combat;
        SetFacePhase(FacePhase.Combat);
        signedForwardSpeed = 0f;
        forwardSpeedLerpT = 0f;
        ForceRefreshChaseTarget();
    }

    private Vector3 GetLivePlayerPosition(Transform player)
    {
        if (PlayerTargetCache.TryGetPosition(out Vector3 cachedPos))
            return cachedPos;
        return player != null ? player.position : transform.position;
    }

    private Vector3 GetChaseTargetPosition(Vector3 liveFallback)
    {
        return chaseCacheInitialized ? cachedChasePosition : liveFallback;
    }

    private float GetChaseIntervalSeconds()
    {
        switch (chaseTrackingMode)
        {
            case EnemyChaseTrackingMode.Realtime:
                return Time.fixedDeltaTime;
            case EnemyChaseTrackingMode.OneSecond:
                return 1f;
            case EnemyChaseTrackingMode.TwoSeconds:
                return 2f;
            default:
                return 3f;
        }
    }

    private void ForceRefreshChaseTarget()
    {
        UpdateChaseTargetCache(true);
    }

    private void RefreshChaseTargetNow(Transform player)
    {
        if (PlayerTargetCache.TryGetPosition(out Vector3 livePos))
        {
            cachedChasePosition = livePos;
            chaseCacheInitialized = true;
            nextChaseRefreshTime = Time.time + GetChaseIntervalSeconds();
            return;
        }

        if (player != null)
        {
            cachedChasePosition = player.position;
            chaseCacheInitialized = true;
            nextChaseRefreshTime = Time.time + GetChaseIntervalSeconds();
            return;
        }

        chaseCacheInitialized = false;
    }

    private void UpdateChaseTargetCache(bool force)
    {
        if (!PlayerTargetCache.TryGetPosition(out Vector3 livePos))
        {
            chaseCacheInitialized = false;
            return;
        }

        if (chaseTrackingMode == EnemyChaseTrackingMode.Realtime)
        {
            cachedChasePosition = livePos;
            chaseCacheInitialized = true;
            return;
        }

        if (force || !chaseCacheInitialized)
        {
            cachedChasePosition = livePos;
            chaseCacheInitialized = true;
            nextChaseRefreshTime = Time.time + GetChaseIntervalSeconds();
            return;
        }

        if (Time.time >= nextChaseRefreshTime)
        {
            cachedChasePosition = livePos;
            nextChaseRefreshTime = Time.time + GetChaseIntervalSeconds();
        }
    }
}