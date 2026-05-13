using System.Collections;
using UnityEngine;

// partial class: TimeProjectileAttack 패턴 구현 (메인 EnemyAttackController와 동일한 global 네임스페이스)

public partial class EnemyAttackController : MonoBehaviour
{
    // Tick helper (메인 Update에서 호출)
    private void TickTimeProjectileUpdate()
    {
        // 현재 별도 Tick 로직 없음. 필요 시 추가.
    }

    /// <summary>
    /// TimeProjectileAttackData 공격 시작 진입점.
    /// EnemyAttackController.TryStartAttack 에서 호출됩니다.
    /// </summary>
    private void StartTimeProjectile(TimeProjectileAttackData data, Transform target, int index)
    {
        if (data == null)
        {
            Debug.LogWarning("[EnemyAttackController][TPA] TimeProjectileAttackData is null.");
            return;
        }

        // pending/hold 처리(다른 패턴과 동일한 흐름 유지)
        MarkExecuted();
        ClearHold();

        // 기존에 돌던 코루틴이 있으면 정지
        if (timeProjectileRoutine != null)
        {
            try { StopCoroutine(timeProjectileRoutine); } catch { }
            timeProjectileRoutine = null;
        }

        runningTimeProjectileIndex = index;

        // 디버그용으로 현재 공격 정보만 기록 (IsAttackExecuting은 main에서 판단)
        currentAttack = data;
        currentAttackIndex = index;

        // 적 상태를 Attack으로 바꾸어 AI의 이동/추적을 차단
        if (enemy != null)
        {
            enemy.SetState(Enemy.EnemyState.Attack);

            // 회전 고정: 타겟을 바라보게 하거나 현재 전면을 고정
            Vector3 lookDir = enemy.transform.forward;
            if (target != null)
            {
                Vector3 dirToTarget = target.position - enemy.transform.position;
                dirToTarget.y = 0f;
                if (dirToTarget.sqrMagnitude > 0.0001f) lookDir = dirToTarget.normalized;
            }

            // LockLookDirection이 Melee와 동일하게 direction + duration을 받는다고 가정
            // 약간의 여유(margin)를 주어 attackTime 동안 확실히 고정되도록 함
            float lockDuration = Mathf.Max(0f, data.attackTime);
            enemy.LockLookDirection(lookDir, lockDuration);
        }

        timeProjectileRoutine = StartCoroutine(TimeProjectileRoutine(data, target, index));
    }

    /// <summary>
    /// AttackTime / FireAtTime / 투사체 발사를 관리하는 코루틴.
    /// </summary>
    private IEnumerator TimeProjectileRoutine(TimeProjectileAttackData data, Transform target, int index)
    {
        float startTime = Time.time;
        float attackEndTime = startTime + data.attackTime;
        bool fired = false;
        bool completedSuccessfully = true;

        // FireAtTime이 AttackTime보다 크면 발사하지 않고 애니메이션만 출력
        float useFireTime = (data.fireAtTime <= data.attackTime) ? data.fireAtTime : -1f;

        // 애니메이션 시작 세팅 (clip 지정 시 재생)
        var anim = enemy != null ? enemy.animator : null;
        if (anim != null && data.clip != null)
        {
            anim.Play(data.clip.name, 0, 0f);
        }

        // 방어적으로 attack 종료/인터럽트 시 항상 정리할 수 있게 try/finally 사용
        try
        {
            while (Time.time < attackEndTime)
            {
                if (enemy == null ||
                    enemy.CurrentState != Enemy.EnemyState.Attack ||
                    enemy.CurrentState == Enemy.EnemyState.ShieldBreak)
                {
                    Log("TPA ATTACK INTERRUPT noCooldown");
                    completedSuccessfully = false;
                    yield break;
                }

                float elapsed = Time.time - startTime;

                // FireAtTime 도달 시 발사 (한 번만)
                if (!fired && useFireTime >= 0f && elapsed >= useFireTime)
                {
                    fired = true;
                    FireTimeProjectile(data, target);
                }

                yield return null;
            }
        }
        finally
        {
            // AttackTime 종료 → 정리
            // 애니메이터 속도 리셋 등 (안정성)
            if (enemy != null && enemy.animator != null)
            {
                enemy.animator.speed = 1f;
            }

            // 상태 복구: Attack 상태이면 Chase로 복귀 (단, 강제 제어 상태인 경우는 예외 처리)
            if (enemy != null)
            {
                if (enemy.CurrentState == Enemy.EnemyState.Attack && !IsHardCrowdControlled())
                {
                    enemy.SetState(Enemy.EnemyState.Chase, true);
                }

                // Unlock look direction
                try { enemy.UnlockLookDirection(); } catch { /* 방어적 처리 */ }
            }

            timeProjectileRoutine = null;
            runningTimeProjectileIndex = -1;

            currentAttack = null;
            currentAttackIndex = -1;

            if (completedSuccessfully)
            {
                ApplyPerAttackCooldown(index, data.cooldown);
                ApplyGlobalCooldown();
            }
        }

        yield break;
    }

    /// <summary>
    /// 실제 투사체를 Instantiate하고 초기화합니다.
    /// FireAtTime 시점에 호출됩니다.
    /// </summary>
    private void FireTimeProjectile(TimeProjectileAttackData data, Transform target)
    {
        if (data.projectilePrefab == null)
        {
            Debug.LogWarning("[EnemyAttackController][TPA] projectilePrefab is null.");
            return;
        }

        if (enemy == null)
        {
            Debug.LogWarning("[EnemyAttackController][TPA] Enemy reference is null.");
            return;
        }

        // 발사 위치: 커스텀 머즐 이름을 사용(빈 문자열이면 enemy root 폴백)
        Vector3 spawnPos;
        Quaternion spawnRot;

        if (string.IsNullOrEmpty(data.muzzleBoneName))
        {
            Debug.LogWarning("[EnemyAttackController][TPA] muzzleBoneName is empty. Fallback to enemy root.");
            spawnPos = enemy.transform.position;
            spawnRot = Quaternion.LookRotation(enemy.transform.forward, Vector3.up);
        }
        else
        {
            Transform muzzle = FindChildRecursive(enemy.transform, data.muzzleBoneName);
            if (muzzle != null)
            {
                spawnPos = muzzle.position;
                spawnRot = Quaternion.LookRotation(muzzle.forward, Vector3.up);
            }
            else
            {
                Debug.LogWarning($"[EnemyAttackController][TPA] muzzle bone '{data.muzzleBoneName}' not found. Fallback to enemy root.");
                spawnPos = enemy.transform.position;
                spawnRot = Quaternion.LookRotation(enemy.transform.forward, Vector3.up);
            }
        }

        GameObject go = Object.Instantiate(data.projectilePrefab, spawnPos, spawnRot);
        var tp = go.GetComponent<TimeProjectile>();
        if (tp == null)
        {
            Debug.LogWarning("[EnemyAttackController][TPA] projectile prefab does not have TimeProjectile component.");
            return;
        }

        tp.Initialize(data, enemy, target);
    }

    private void CancelTimeProjectileNoCooldown()
    {
        if (enemy != null && enemy.animator != null)
            enemy.animator.speed = 1f;

        if (enemy != null && enemy.CurrentState == Enemy.EnemyState.Attack && !IsHardCrowdControlled())
            enemy.SetState(Enemy.EnemyState.Chase, true);

        if (enemy != null)
        {
            try { enemy.UnlockLookDirection(); } catch { }
        }

        if (timeProjectileRoutine != null)
        {
            try { StopCoroutine(timeProjectileRoutine); } catch { }
            timeProjectileRoutine = null;
        }

        runningTimeProjectileIndex = -1;
        currentAttack = null;
        currentAttackIndex = -1;
    }

    private void InterruptTimeProjectileIfNeeded()
    {
        if (timeProjectileRoutine == null)
            return;

        Log("INTERRUPT timeProjectile -> cancel");
        CancelTimeProjectileNoCooldown();
    }
}