using UnityEngine;

/// <summary>
/// 1D SignedSpeed 방식:
///  - Forward(양수) / Idle(0) / Backstep(음수 -1) 전환
///  - Backstep은 EnemyState로 분리하지 않고 내부 플래그 backstepping 으로만 유지
///  - Animator: BlendTree (Speed -1 / 0 / +1)
///
/// 2025-10-07 개선:
///  - 거리 유지(distanceMaintenance) 판정 로직 재설계
///    요구사항: 글로벌 쿨다운 뿐 아니라 "개별 쿨다운이 모두 진행 중" 이면서 이미 전투 링( backstepDistance ±1 ) 안에 있으면 유지.
///    또한 사거리 안에 들어왔지만 '그 사거리 커버 공격' 이 아직 준비되지 않았을 때도 유지.
///  - 로직 개요:
///      1) 글로벌 쿨다운이면 → 유지 (reason=GLOBAL)
///      2) (글로벌 아님) 아직 어떤 공격도 Ready 아님 → 링 안( distance <= UpperBand )이면 유지 (reason=ALL_COOLDOWN_WAIT_RING)
///      3) (글로벌 아님, 하나 이상 Ready) 현재 거리에서 커버 가능한 공격 사거리들 중 Ready 가 하나도 없으면 유지 (reason=IN_RANGE_WAIT)
///      4) 그 외 → 유지 해제
///  - 로그: ON/OFF 전환 시 한 번만 원인(reason) 포함 출력
/// </summary>
[DisallowMultipleComponent]
public class EnemyAI : MonoBehaviour
{
    [Header("백스텝 중심 거리 (±1 밴드)")]
    [Tooltip("예: 5 → 4~6 사이면 Idle, 4 미만 Backstep, 6 초과 Forward")]
    public float backstepDistance = 5f;

    [Header("백스텝 속도 계수 (기본 moveSpeed * multiplier)")]
    public float backstepSpeedMultiplier = 1.0f;

    [Header("서서히 Forward 속도 정규화(1=즉시 1로 세팅)")]
    [Range(0.1f, 2f)] public float forwardSpeedNormalizeTime = 0.25f;

    private Enemy enemy;
    private EnemyAttackController attackCtrl;

    // Backstep 플래그
    private bool backstepping;

    private float signedForwardSpeed; // +0~+1 로 서서히 보간
    private float forwardSpeedLerpT;

    // 유지모드 전환 감지용
    private bool distanceMaintenanceModeLast = false;

    private float LowerBand => backstepDistance - 1f;
    private float UpperBand => backstepDistance + 1f;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        attackCtrl = GetComponent<EnemyAttackController>();
    }

    public void Tick(Enemy ctx, Transform player)
    {
        if (ctx == null || player == null) return;

        // 1. 하드 CC / 사망 / 실드브레이크 / 넉백 / 스턴 상태에서는 이동/공격 결정 정지
        if (ctx.CurrentState == Enemy.EnemyState.Dead ||
            ctx.CurrentState == Enemy.EnemyState.ShieldBreak ||
            ctx.CurrentState == Enemy.EnemyState.Stunned ||
            ctx.CurrentState == Enemy.EnemyState.Knockback)
        {
            if (backstepping) ForceClearBackstep();
            return;
        }

        // 2. 공격 상태: 방향만 맞추고 속도 0
        if (ctx.CurrentState == Enemy.EnemyState.Attack)
        {
            HandleAttackFacing(ctx, player);
            ctx.animCtrl?.SetSignedSpeed(0f);
            return;
        }

        // 3. Backstep 중일 때
        if (backstepping)
        {
            UpdateBackstep(ctx, player);
            return;
        }

        // 4. 일반 추적/의사결정
        DriveDecision(ctx, player);
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

        // 공격 시도
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

        // 적절한 공격 없음 → 추격
        ForwardChase(ctx, player);
    }

    /// <summary>
    /// 거리 유지 조건 계산
    /// </summary>
    /// <param name="distance">플레이어와 거리</param>
    /// <param name="reason">디버그용 이유 문자열</param>
    private bool ShouldDistanceMaintain(float distance, out string reason)
    {
        reason = "NONE";
        if (attackCtrl == null) return false;

        // (1) 글로벌 쿨다운이면 무조건 유지
        if (attackCtrl.IsGlobalCooling())
        {
            reason = "GLOBAL";
            return true;
        }

        // 공격 준비 여부 스캔
        bool anyReadyOverall = false;
        int count = attackCtrl.AttackCount;
        for (int i = 0; i < count; i++)
        {
            if (attackCtrl.IsOffCooldown(i))
            {
                anyReadyOverall = true;
                break;
            }
        }

        // (2) 어떤 공격도 Ready 아님 → 링 내부면 유지
        if (!anyReadyOverall)
        {
            if (distance <= UpperBand)
            {
                reason = "ALL_COOLDOWN_WAIT_RING";
                return true;
            }
            // 링 밖이면 접근해서 링 안으로 진입 시까지 유지 모드 아님
            reason = "APPROACH_RING";
            return false;
        }

        // (3) 하나 이상 Ready overall → "현재 거리" 에서 커버되는 Ready 없음이면 유지
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

        if (!withinAnyAttackRange)
        {
            reason = "NO_RANGE";
            return false;
        }

        if (!anyReadyWithinDistance)
        {
            reason = "IN_RANGE_WAIT";
            return true;
        }

        reason = "READY_IN_RANGE";
        return false;
    }

    #region Distance Maintenance

    private void HandleDistanceMaintenance(Enemy ctx, Transform player, float distance)
    {
        // 밴드 안 → Idle
        if (!backstepping && distance >= LowerBand && distance <= UpperBand)
        {
            IdleFacing(ctx, player);
            return;
        }

        // 너무 멀다 → Forward (링으로 접근)
        if (!backstepping && distance > UpperBand)
        {
            ForwardChase(ctx, player);
            return;
        }

        // 너무 가깝다 → Backstep
        if (!backstepping && distance < LowerBand)
        {
            StartBackstep(ctx, player);
            return;
        }

        // 예외 → Idle
        IdleFacing(ctx, player);
    }

    private void StartBackstep(Enemy ctx, Transform player)
    {
        backstepping = true;

        if (ctx.agent && ctx.agent.isOnNavMesh)
        {
            ctx.agent.isStopped = true;
            ctx.agent.velocity = Vector3.zero;
            ctx.agent.ResetPath();
        }

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

        // 유지 종료 → Backstep 해제 후 재평가
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

        // 플레이어 바라보기
        Vector3 face = player.position - ctx.transform.position;
        face.y = 0f;
        if (face.sqrMagnitude > 0.0001f)
            ctx.transform.rotation = Quaternion.LookRotation(face.normalized);

        // 수동 후퇴
        float backSpeed = ctx.moveSpeed * backstepSpeedMultiplier;
        if (face.sqrMagnitude > 0.0001f)
            ctx.transform.position += (-face.normalized) * backSpeed * Time.deltaTime;

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

        if (ctx.agent && ctx.agent.isOnNavMesh)
        {
            ctx.agent.Warp(ctx.transform.position);
            ctx.agent.isStopped = false;
        }

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

    private void CancelBackstep()
    {
        if (!backstepping) return;
        backstepping = false;
        enemy?.animCtrl?.SetSignedSpeed(0f);
    }

    #endregion

    #region Movement Helpers & Facing

    private void ForwardChase(Enemy ctx, Transform player)
    {
        if (backstepping) return;

        Vector3 dir = player.position - ctx.transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.0001f)
            ctx.transform.rotation = Quaternion.LookRotation(dir.normalized);

        if (ctx.agent && ctx.agent.isOnNavMesh)
        {
            ctx.agent.isStopped = false;
            ctx.agent.SetDestination(player.position);
        }

        forwardSpeedLerpT += Time.deltaTime / Mathf.Max(0.0001f, forwardSpeedNormalizeTime);
        signedForwardSpeed = Mathf.Lerp(signedForwardSpeed, 1f, forwardSpeedLerpT);
        signedForwardSpeed = Mathf.Clamp01(signedForwardSpeed);

        ctx.animCtrl?.SetSignedSpeed(signedForwardSpeed);
    }

    private void IdleFacing(Enemy ctx, Transform player)
    {
        if (ctx.agent && ctx.agent.isOnNavMesh)
        {
            ctx.agent.isStopped = true;
            ctx.agent.velocity = Vector3.zero;
        }

        Vector3 look = player.position - ctx.transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.0001f)
            ctx.transform.rotation = Quaternion.LookRotation(look.normalized);

        signedForwardSpeed = 0f;
        forwardSpeedLerpT = 0f;
        ctx.animCtrl?.SetSignedSpeed(0f);
    }

    private void HandleAttackFacing(Enemy ctx, Transform player)
    {
        if (attackCtrl != null && attackCtrl.IsRushing)
            return; // Rush 중엔 Rush 로직이 방향 결정

        Vector3 dir = player.position - ctx.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            ctx.transform.rotation = Quaternion.LookRotation(dir);
    }

    #endregion

    #region Interface Hooks

    public void OnAttackStarted(Enemy ctx)
    {
        ctx.animCtrl?.SetSignedSpeed(0f);
        signedForwardSpeed = 0f;
        forwardSpeedLerpT = 0f;
    }

    public void InterruptAttack()
    {
        if (backstepping)
            CancelBackstep();
    }

    #endregion
}