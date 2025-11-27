using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Recoil helper: 기존 호출 시그니처와 호환성 유지 (StartRecoil overloads, Cancel).
/// 실제 이동은 PlayerMovement.MoveFilteredDisplacement 또는 MovePhysicsDisplacement 를 통해 수행.
/// 변경사항(옵션 A):
/// - WeaponDataSO.recoilStartDelay 를 반영하여 지연 후 리코일 시작(지연 도중 keep()이 false가 되면 자동 취소)
/// - 감쇠 프로파일 정규화: 기존 4*t*(1-t) -> 6*t*(1-t) 을 사용하여 전체 이동량이 recoilDistance 가 되도록 보정
/// </summary>
public class PlayerRecoil : MonoBehaviour
{
    [SerializeField] private Transform owner;
    [SerializeField] private float recoilDistance = 0.3f;
    [SerializeField] private float recoilDuration = 0.15f;

    private Coroutine routine;
    private Coroutine delayRoutine;
    private PlayerMovement movement;
    private Func<bool> keepCondition;
    private const float EPS = 0.0001f;

    void Awake()
    {
        if (owner == null) owner = transform;
        movement = owner.GetComponent<PlayerMovement>();
    }

    // 기존 호출: StartRecoil(Vector3 dir)
    public void StartRecoil(Vector3 dir)
    {
        TriggerRecoil(dir, null);
    }

    // 기존 호출: StartRecoil(Vector3 dir, float distance, float duration)
    public void StartRecoil(Vector3 dir, float distance, float duration)
    {
        if (distance > EPS) recoilDistance = distance;
        if (duration > EPS) recoilDuration = duration;
        TriggerRecoil(dir, null);
    }

    // 기존 호출(현재 PlayerWeaponController에서 사용하는 시그니처):
    // StartRecoil(WeaponDataSO data, Func<bool> keep, Transform ownerTransform)
    public void StartRecoil(WeaponDataSO data, Func<bool> keep, Transform ownerTransform)
    {
        if (data == null) return;
        if (Mathf.Approximately(data.recoilDuration, 0f)) return;
        if (Mathf.Approximately(data.recoilPower, 0f)) return;

        // Apply configured values
        recoilDistance = data.recoilPower;
        recoilDuration = data.recoilDuration;

        if (ownerTransform != null)
        {
            owner = ownerTransform;
            movement = owner.GetComponent<PlayerMovement>();
        }

        Vector3 dir = owner != null ? -owner.forward : -transform.forward;
        dir.y = 0f;
        dir.Normalize();

        // Cancel any existing routines (both active recoil and pending delay)
        Cancel();

        // If a start delay is configured, wait but obey the keep() predicate during the wait.
        if (data.recoilStartDelay > EPS)
        {
            delayRoutine = StartCoroutine(DelayedStartRecoil(data.recoilStartDelay, dir, keep));
        }
        else
        {
            TriggerRecoil(dir, keep);
        }
    }

    // Cancel existing recoil and any pending delayed starts
    public void Cancel()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        if (delayRoutine != null)
        {
            StopCoroutine(delayRoutine);
            delayRoutine = null;
        }
        keepCondition = null;
    }

    // Internal trigger
    private void TriggerRecoil(Vector3 dir, Func<bool> keep)
    {
        if (dir.sqrMagnitude <= EPS) return;
        keepCondition = keep;

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(RecoilRoutine(dir.normalized));
    }

    private IEnumerator DelayedStartRecoil(float delaySeconds, Vector3 dir, Func<bool> keep)
    {
        // Poll keep() while waiting to allow early-cancel if the firing/attack state ends.
        float waited = 0f;
        while (waited < delaySeconds)
        {
            // If keep predicate is provided and becomes false, cancel start.
            if (keep != null && !keep())
            {
                delayRoutine = null;
                yield break;
            }

            // Wait one frame (use scaled time to keep in-game timing)
            yield return null;
            waited += Time.deltaTime;
        }

        delayRoutine = null;

        // Re-check keep once more before starting
        if (keep != null && !keep()) yield break;

        TriggerRecoil(dir, keep);
    }

    private IEnumerator RecoilRoutine(Vector3 n)
    {
        float elapsed = 0f;
        float dur = Mathf.Max(recoilDuration, EPS);

        // Use normalized damping profile so total displacement equals recoilDistance.
        // speedMulProfile(t) = 6 * t * (1 - t)  (integral 0..1 == 1)
        while (elapsed < dur)
        {
            if (keepCondition != null && !keepCondition())
                break;

            float t = Mathf.Clamp01(elapsed / dur);
            float speedMul = 6f * t * (1f - t); // normalized profile
            float currentSpeed = recoilDistance * speedMul / dur;
            Vector3 disp = n * currentSpeed * Time.fixedDeltaTime;

            if (movement != null)
            {
                // prefer MoveFilteredDisplacement if available; if not, fallback to MovePhysicsDisplacement
                try
                {
                    movement.MoveFilteredDisplacement(disp);
                }
                catch (MissingMethodException)
                {
                    movement.MovePhysicsDisplacement(disp);
                }
            }
            else
            {
                owner.position += disp;
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        routine = null;
        keepCondition = null;
    }
}