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

    // --- Peace / Detection 설정 (인스펙터에서 조정 가능)
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

    private Enemy enemy;
    private EnemyAttackController attackCtrl;

    private bool backstepping;
    private float signedForwardSpeed;
    private float forwardSpeedLerpT;
    private bool distanceMaintenanceModeLast = false;

    private float LowerBand => backstepDistance - 1f;
    private float UpperBand => backstepDistance + 1f;

    // --- AI 모드 상태
    private enum AIState { Peace, Finding, Combat }
    private AIState aiState = AIState.Peace;
    private Vector3 spawnPosition;
    private Vector3 roamTarget;
    private bool hasRoamTarget = false;
    private float idleTimer = 0f;
    private Coroutine findingCoroutine;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        attackCtrl = GetComponent<EnemyAttackController>();

        // 기록해둔 스폰 위치를 배회 기준으로 사용
        spawnPosition = transform.position;

        // 기본 모드는 Peace (스폰 직후 배회)
        aiState = AIState.Peace;
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

        // 공격 애니를 실행 중이면 공격 관련 처리 우선 (기존 동작 유지)
        if (ctx.CurrentState == Enemy.EnemyState.Attack)
        {
            HandleAttackFacing(ctx, player);
            ctx.animCtrl?.SetSignedSpeed(0f);
            return;
        }

        // AI 모드에 따른 분기
        switch (aiState)
        {
            case AIState.Peace:
                PeaceTick(ctx, player);
                break;
            case AIState.Finding:
                // Finding 중에도 플레이어를 바라보게 함
                IdleFacing(ctx, player);
                break;
            case AIState.Combat:
                // 기존 DriveDecision 로직 유지
                if (backstepping)
                {
                    UpdateBackstep(ctx, player);
                }
                else
                {
                    DriveDecision(ctx, player);
                }
                break;
        }
    }

    private void PeaceTick(Enemy ctx, Transform player)
    {
        // 플레이어 탐지(거리 기반, 성능을 위해 sqr사용)
        float sqrDist = (player.position - ctx.transform.position).sqrMagnitude;
        if (sqrDist <= detectionRadius * detectionRadius)
        {
            StartFinding(ctx, player);
            return;
        }

        // 평화 배회 행동: 타겟이 있으면 이동, 없으면 대기(Idle) 후 랜덤 타겟 선택
        if (hasRoamTarget)
        {
            Vector3 toTarget = roamTarget - ctx.transform.position;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;
            if (dist <= 0.25f)
            {
                // 도착: 대기 시간
                hasRoamTarget = false;
                idleTimer = Random.Range(idleMin, idleMax);
                ctx.animCtrl?.SetSignedSpeed(0f);
            }
            else
            {
                // 이동
                Vector3 dir = toTarget.normalized;
                ctx.RequestLook(dir);
                ctx.RequestMove(dir, Mathf.Clamp01(peaceMoveSpeedMultiplier));
                // 애니메이터에도 속도 전달 (0~1)
                ctx.animCtrl?.SetSignedSpeed(Mathf.Clamp01(peaceMoveSpeedMultiplier));
            }
        }
        else
        {
            // idle 중인지 체크
            if (idleTimer > 0f)
            {
                idleTimer -= Time.deltaTime;
                ctx.animCtrl?.SetSignedSpeed(0f);
                // 바라보기 정도만 유지
            }
            else
            {
                // 새로운 배회 타겟 생성 (spawnPosition 기준)
                Vector2 rnd2 = Random.insideUnitCircle * roamRadius;
                roamTarget = new Vector3(spawnPosition.x + rnd2.x, ctx.transform.position.y, spawnPosition.z + rnd2.y);
                hasRoamTarget = true;
                // 준비: 애니에서 걷기(느리게)
                ctx.animCtrl?.SetSignedSpeed(Mathf.Clamp01(peaceMoveSpeedMultiplier));
            }
        }
    }

    private void StartFinding(Enemy ctx, Transform player)
    {
        if (aiState == AIState.Finding) return;
        aiState = AIState.Finding;

        // 이동/속도 보간 초기화
        signedForwardSpeed = 0f;
        forwardSpeedLerpT = 0f;

        if (findingCoroutine != null) StopCoroutine(findingCoroutine);
        findingCoroutine = StartCoroutine(FindingCoroutine(ctx, player));
    }

    private IEnumerator FindingCoroutine(Enemy ctx, Transform player)
    {
        // Find 애니 재생(Animator에 "Find" trigger 추가 필요)
        ctx.animCtrl?.PlayFind();

        float t = 0f;
        while (t < findDuration)
        {
            // 만약 도중에 상태가 Dead/Stunned/Knockback이면 Finding 취소하고 Peace로 복귀
            if (ctx.CurrentState == Enemy.EnemyState.Dead ||
                ctx.CurrentState == Enemy.EnemyState.ShieldBreak ||
                ctx.CurrentState == Enemy.EnemyState.Stunned ||
                ctx.CurrentState == Enemy.EnemyState.Knockback)
            {
                aiState = AIState.Peace;
                findingCoroutine = null;
                yield break;
            }

            t += Time.deltaTime;
            yield return null;
        }

        // 애니 재생 후 전투 모드로 전환
        aiState = AIState.Combat;
        findingCoroutine = null;

        // 전투 모드로 전환하면 기존 AI가 플레이어를 추적하도록 함
        // signed speed 초기화
        signedForwardSpeed = 0f;
        forwardSpeedLerpT = 0f;
    }

    private void DriveDecision(Enemy ctx, Transform player)
    {
        if (attackCtrl == null)
        {
            ForwardChase(ctx, player);
            return;
        }

        float distance = (player.position - ctx.transform.position).magnitude;
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
            Debug.Log($"[AI] Backstep START dist={Vector3.Distance(player.position, ctx.transform.position):F2}");
    }

    private void UpdateBackstep(Enemy ctx, Transform player)
    {
        if (!backstepping) return;

        float dist = Vector3.Distance(player.position, ctx.transform.position);
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

        Vector3 face = player.position - ctx.transform.position;
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
        Vector3 dir = player.position - ctx.transform.position;
        dir.y = 0f;

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
        Vector3 look = player.position - ctx.transform.position;
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
        Vector3 dir = player.position - ctx.transform.position;
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

    public void InterruptAttack()
    {
        if (backstepping)
            ForceClearBackstep();
    }
}