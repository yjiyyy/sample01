using UnityEngine;

/// <summary>
/// Backstep + 거리 유지 + 공격 선택
/// 우선순위: Dead/ShieldBreak/Stunned/Knockback (외부) > Backstep > Attack 진행/선택 > Chase
/// </summary>
[DisallowMultipleComponent]
public class EnemyAI : MonoBehaviour
{
    [Header("백스텝 중심 거리 (±1 밴드)")]
    [Tooltip("예: 5 → 4~6 사이면 Idle 유지, 4 미만이면 Backstep, 6 초과면 전진")]
    public float backstepDistance = 5f;

    private Enemy enemy;
    private EnemyAttackController attackCtrl;

    // Backstep 상태 플래그
    private bool backstepping;
    private Vector3 cachedBackDir;  // 시작시(또는 매 프레임) 플레이어 반대 방향

    // Attack 진행 중 여부(플래그만 유지)
    private bool inAttackAnim;

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

        // 1. 치명적/우선 상태
        if (ctx.CurrentState == Enemy.EnemyState.Dead ||
            ctx.CurrentState == Enemy.EnemyState.ShieldBreak ||
            ctx.CurrentState == Enemy.EnemyState.Stunned ||
            ctx.CurrentState == Enemy.EnemyState.Knockback)
        {
            // Backstep 플래그 정리
            if (backstepping) CancelBackstep();
            return;
        }

        // 2. Backstep 상태 업데이트
        if (ctx.CurrentState == Enemy.EnemyState.Backstep)
        {
            UpdateBackstep(ctx, player);
            return;
        }

        // 3. (Attack 진행 중) 단순 회전 유지 (Rush, Melee 등 AttackController가 주도)
        if (ctx.CurrentState == Enemy.EnemyState.Attack)
        {
            HandleAttackFacing(ctx, player);
            return;
        }

        // 4. Chase + 선택 로직
        if (ctx.CurrentState == Enemy.EnemyState.Chase)
        {
            DriveDecision(ctx, player);
        }
    }

    /// <summary>
    /// 거리 유지 / Backstep / Attack 전환 결정
    /// </summary>
    private void DriveDecision(Enemy ctx, Transform player)
    {
        if (attackCtrl == null)
        {
            ChaseForward(ctx, player);
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

        // 거리 유지 모드 (Backstep/Idle/전진) 조건
        bool distanceMaintenanceMode = globalCooling || !anyPatternOffCooldown;

        if (distanceMaintenanceMode)
        {
            HandleDistanceMaintenance(ctx, player, distance);
            return;
        }

        // 공격 가능 모드 → 패턴 선택
        int idx = attackCtrl.SelectAttackIndex(distance);
        if (idx >= 0)
        {
            // 사거리 안이면 시작
            float attackRange = attackCtrl.GetAttackRange(idx);
            if (distance <= attackRange)
            {
                if (attackCtrl.TryStartAttack(idx, player))
                    return; // Attack 상태 전환
            }
        }

        // 선택 실패 or 사거리 밖 → 추격
        ChaseForward(ctx, player);
    }

    #region Distance Maintenance

    private void HandleDistanceMaintenance(Enemy ctx, Transform player, float distance)
    {
        // 거리 밴드 안 → Idle + 회전만
        if (!backstepping && distance >= LowerBand && distance <= UpperBand)
        {
            IdleFace(ctx, player);
            return;
        }

        // 너무 멀다 → 앞으로 전진해서 밴드 중심 접근
        if (!backstepping && distance > UpperBand)
        {
            MoveForwardToBand(ctx, player);
            return;
        }

        // 너무 가깝다 → Backstep 시작
        if (!backstepping && distance < LowerBand)
        {
            StartBackstep(ctx, player);
            return;
        }

        // 방금 종료 후 등 예외 → Idle
        if (!backstepping)
        {
            IdleFace(ctx, player);
        }
    }

    private void StartBackstep(Enemy ctx, Transform player)
    {
        backstepping = true;

        Vector3 away = (ctx.transform.position - player.position);
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f)
            away = -ctx.transform.forward;

        cachedBackDir = away.normalized;

        ctx.SetState(Enemy.EnemyState.Backstep);
        if (ctx.animator != null)
            ctx.animator.Play("Backstep", 0, 0f);

        if (attackCtrl?.debugDecisionLogs == true)
            Debug.Log($"[Backstep] START dist={(player.position - ctx.transform.position).magnitude:F2} target={backstepDistance:F2}", ctx);
    }

    private void UpdateBackstep(Enemy ctx, Transform player)
    {
        if (!backstepping)
        {
            // 비정상 → Chase 복귀
            ctx.SetState(Enemy.EnemyState.Chase);
            return;
        }

        // 아직 공격 불가 조건 유지 되는지 재평가
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
            // 유지 모드 해제 → Backstep 즉시 끝 + 공격 재평가
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
            // 실패 시 Chase
            ctx.SetState(Enemy.EnemyState.Chase);
            return;
        }

        // 플레이어 방향으로 회전
        Vector3 face = player.position - ctx.transform.position;
        face.y = 0f;
        if (face.sqrMagnitude > 0.0001f)
            ctx.transform.rotation = Quaternion.LookRotation(face.normalized);

        float distCurrent = face.magnitude;

        // 뒤로 이동 (RootMotion 없음)
        float speed = ctx.moveSpeed;
        Vector3 moveDir = (-face).normalized;
        ctx.transform.position += moveDir * speed * Time.deltaTime;

        // 종료 조건: 목표 거리 이상 or 밴드 재진입
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

        if (attackCtrl?.debugDecisionLogs == true)
            Debug.Log($"[Backstep] END success={success}", ctx);

        if (ctx.CurrentState == Enemy.EnemyState.Backstep)
            ctx.SetState(Enemy.EnemyState.Chase);
    }

    private void CancelBackstep()
    {
        backstepping = false;
        if (enemy != null && enemy.CurrentState == Enemy.EnemyState.Backstep)
            enemy.SetState(Enemy.EnemyState.Chase);
    }

    #endregion

    #region Movement Helpers & Attack Facing

    private void ChaseForward(Enemy ctx, Transform player)
    {
        Vector3 dir = player.position - ctx.transform.position;
        dir.y = 0f;

        if (ctx.agent && ctx.agent.isOnNavMesh)
        {
            ctx.agent.isStopped = false;
            ctx.agent.SetDestination(player.position);
            ctx.animCtrl.UpdateMovement(ctx.agent.velocity.magnitude);
        }

        if (dir.sqrMagnitude > 0.0001f)
            ctx.transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    private void MoveForwardToBand(Enemy ctx, Transform player)
    {
        Vector3 dir = player.position - ctx.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            ctx.transform.rotation = Quaternion.LookRotation(dir.normalized);

        if (ctx.agent && ctx.agent.isOnNavMesh)
        {
            ctx.agent.isStopped = false;
            // 목표: 플레이어로부터 backstepDistance 떨어진 중심점
            Vector3 target = player.position - dir.normalized * backstepDistance;
            ctx.agent.SetDestination(target);
            ctx.animCtrl.UpdateMovement(ctx.agent.velocity.magnitude);
        }
    }

    private void IdleFace(Enemy ctx, Transform player)
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
        ctx.animCtrl.UpdateMovement(0f);
    }

    private void HandleAttackFacing(Enemy ctx, Transform player)
    {
        if (attackCtrl != null && attackCtrl.IsRushing)
            return;

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
    }

    public void InterruptAttack()
    {
        inAttackAnim = false;
        if (backstepping)
            CancelBackstep();
    }

    #endregion
}