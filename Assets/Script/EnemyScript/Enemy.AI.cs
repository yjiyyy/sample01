using UnityEngine;

/// <summary>
/// NavMeshAgent 제거 버전 EnemyAI
/// - 이동/회전 직접 Transform, 실제 적용은 Enemy.FixedUpdate
/// - Backstep / Forward / Idle 로직 유지
/// - 속도 보간(signedForwardSpeed) 그대로 이용
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

    private Enemy enemy;
    private EnemyAttackController attackCtrl;

    private bool backstepping;
    private float signedForwardSpeed;
    private float forwardSpeedLerpT;
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

        if (ctx.CurrentState == Enemy.EnemyState.Dead ||
            ctx.CurrentState == Enemy.EnemyState.ShieldBreak ||
            ctx.CurrentState == Enemy.EnemyState.Stunned ||
            ctx.CurrentState == Enemy.EnemyState.Knockback)
        {
            if (backstepping) ForceClearBackstep();
            return;
        }

        if (ctx.CurrentState == Enemy.EnemyState.Attack)
        {
            HandleAttackFacing(ctx, player);
            ctx.animCtrl?.SetSignedSpeed(0f);
            return;
        }

        if (backstepping)
        {
            UpdateBackstep(ctx, player);
            return;
        }

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