using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 플레이어 자기 반동(리코일) 전담 컴포넌트
/// - WeaponDataSO.recoilStartDelay / recoilPower(+뒤, -앞) / recoilDuration 사용
/// - 속도 프로파일: v(t) = 4t(1−t)
/// - Attack 상태 콜백이 false가 되면 즉시 중단
/// - FixedUpdate 타이밍과 동기화하여 PC/모바일 일관성 보장
/// </summary>
[DisallowMultipleComponent]
public class PlayerRecoil : MonoBehaviour
{
    private Coroutine routine;

    public bool IsActive => routine != null;

    public void StartRecoil(WeaponDataSO data, Func<bool> isAttackState, Transform owner)
    {
        if (data == null) return;
        if (data.recoilDuration <= 0f) return;
        if (Mathf.Approximately(data.recoilPower, 0f)) return;

        Cancel();
        routine = StartCoroutine(RecoilRoutine(data, isAttackState, owner ? owner : transform));
    }

    public void Cancel()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    private IEnumerator RecoilRoutine(WeaponDataSO data, Func<bool> isAttackState, Transform owner)
    {
        // ─────────────────────────────────────────────────────────
        // Start delay (고정 타임스텝 사용)
        // ─────────────────────────────────────────────────────────
        float delay = Mathf.Max(0f, data.recoilStartDelay);
        float waited = 0f;
        while (waited < delay)
        {
            if (isAttackState != null && !isAttackState())
            {
                routine = null; yield break;
            }

            waited += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        if (isAttackState != null && !isAttackState())
        {
            routine = null; yield break;
        }

        // ─────────────────────────────────────────────────────────
        // 리코일 실행 (고정 타임스텝으로 일관된 거리 보장)
        // ─────────────────────────────────────────────────────────
        Vector3 forward = owner.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 dir = (data.recoilPower >= 0f ? -forward : forward);
        float speedAbs = Mathf.Abs(data.recoilPower);
        float duration = Mathf.Max(0f, data.recoilDuration);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (isAttackState != null && !isAttackState())
            {
                routine = null; yield break;
            }

            float t = duration > 0f ? (elapsed / duration) : 1f;
            float speedMul = 4f * t * (1f - t);  // 0→최대→0
            float currentSpeed = speedAbs * Mathf.Max(0f, speedMul);

            // 고정 타임스텝으로 일관된 리코일 거리 보장
            owner.position += dir * currentSpeed * Time.fixedDeltaTime;

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        routine = null;
    }
}