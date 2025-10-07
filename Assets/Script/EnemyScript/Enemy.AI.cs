using UnityEngine;

/// <summary>
/// 1D SignedSpeed 방식:
///  - Forward(양수) / Idle(0) / Backstep(음수 -1) 전환
///  - Backstep은 EnemyState로 분리하지 않고 내부 플래그 backstepping 으로만 유지
///  - Animator: BlendTree (Speed -1 / 0 / +1)
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
    // 공격 중 여부 플래그
    private bool inAttackAnim;

    private float signedForwardSpeed; // +0~+1 로 서서히 보간
    private float forwardSpeedLerpT;

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

        // 1. 하드 CC / 사망 / 그로기
        if (ctx.CurrentState == Enemy.EnemyState.Dead ||
            ctx.CurrentState == Enemy.EnemyState.ShieldBreak ||
            ctx.CurrentState == Enemy.EnemyState.Stunned ||
            ctx.CurrentState == Enemy.EnemyState.Knockback)
        {
            if (backstepping) ForceClearBackstep();
            return;
        }

        // 2. 공격 상태: 방향만 맞추고 Speed=0
        if (ctx.CurrentState == Enemy.EnemyState.Attack)
        {
            HandleAttackFacing(ctx, player);
            ctx.animCtrl?.SetSignedSpeed(0f);
            return;
        }

        // 3. Backstepping 중이면 우선 처리
        if (backstepping)
        {
            UpdateBackstep(ctx, player);
            return;
        }

        // 4. 일반 Chase 로직
        DriveDecision(ctx, player);
    }

    private void DriveDecision(Enemy ctx, Transform player)
    {
        if (attackCtrl == null)
        {
            ForwardChase(ctx, player);
            return;
        }

        Vector3 toPlayer = player.position - ctx.transform.position;
        float distance = toPlayer.magnitude;

        bool globalCooling = attackCtrl.IsGlobalCooling();
        bool anyPatternOffCooldown = false;
        for (int i = 0; i < attackCtrl.AttackCount; i++)
        {
            if (attackCtrl.IsOffCooldown(i))
            {
                anyPatternOffCooldown = true;
                break;
            }
        }

        bool distanceMaintenanceMode = globalCooling || !anyPatternOffCooldown;

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

        // 사거리 안 아님 → 추격
        ForwardChase(ctx, player);
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

        // 너무 멀다 → Forward
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

        // 즉시 음수 속도
        ctx.animCtrl?.SetSignedSpeed(-1f);

        if (attackCtrl?.debugDecisionLogs == true)
            Debug.Log($"[AI] Backstep START dist={Vector3.Distance(player.position, ctx.transform.position):F2}");
    }

    private void UpdateBackstep(Enemy ctx, Transform player)
    {
        if (!backstepping)
            return;

        // 공격 가능 상태로 전환되면 Backstep 중단 후 공격 재평가
        bool globalCooling = attackCtrl != null && attackCtrl.IsGlobalCooling();
        bool anyPatternOffCooldown = false;
        if (attackCtrl != null)
        {
            for (int i = 0; i < attackCtrl.AttackCount; i++)
            {
                if (attackCtrl.IsOffCooldown(i))
                {
                    anyPatternOffCooldown = true;
                    break;
                }
            }
        }
        bool distanceMaintenanceMode = globalCooling || !anyPatternOffCooldown;

        if (!distanceMaintenanceMode)
        {
            EndBackstep(ctx, true);

            if (attackCtrl != null)
            {
                float dist = Vector3.Distance(ctx.transform.position, player.position);
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

        // 수동 후퇴 (RootMotion 없음)
        float backSpeed = ctx.moveSpeed * backstepSpeedMultiplier;
        ctx.transform.position += (-face.normalized) * backSpeed * Time.deltaTime;

        // 애니 파라미터 유지 (-1)
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

        // Idle로 초기화 → 다음 프레임 재평가
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

        // Forward 속도를 0→1로 부드럽게 (forwardSpeedNormalizeTime)
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
            return; // Rush는 별도 이동/방향 로직

        Vector3 dir = player.position - ctx.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            ctx.transform.rotation = Quaternion.LookRotation(dir);
    }

    #endregion

    #region Interface Hooks

    public void OnAttackStarted(Enemy ctx)
    {
        inAttackAnim = true;
        // 공격 시작 시 이동 정지
        ctx.animCtrl?.SetSignedSpeed(0f);
        // Forward 보간 리셋
        signedForwardSpeed = 0f;
        forwardSpeedLerpT = 0f;
    }

    public void InterruptAttack()
    {
        inAttackAnim = false;
        if (backstepping)
            CancelBackstep();
    }

    #endregion
}