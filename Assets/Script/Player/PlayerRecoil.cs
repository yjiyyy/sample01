using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Recoil helper: 기존 호출 시그니처와 호환성 유지 (StartRecoil overloads, Cancel).
/// 실제 이동은 PlayerMovement.MoveFilteredDisplacement 또는 MovePhysicsDisplacement 를 통해 수행.
/// </summary>
public class PlayerRecoil : MonoBehaviour
{
    [SerializeField] private Transform owner;
    [SerializeField] private float recoilDistance = 0.3f;
    [SerializeField] private float recoilDuration = 0.15f;

    private Coroutine routine;
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

        recoilDistance = data.recoilPower;
        recoilDuration = data.recoilDuration;

        if (ownerTransform != null)
        {
            owner = ownerTransform;
            movement = owner.GetComponent<PlayerMovement>();
        }

        Vector3 dir = owner != null ? -owner.forward : -transform.forward;
        dir.y = 0f;

        TriggerRecoil(dir, keep);
    }

    // Cancel existing recoil
    public void Cancel()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
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

    private IEnumerator RecoilRoutine(Vector3 n)
    {
        float elapsed = 0f;
        float dur = Mathf.Max(recoilDuration, EPS);

        while (elapsed < dur)
        {
            if (keepCondition != null && !keepCondition())
                break;

            float t = Mathf.Clamp01(elapsed / dur);
            float speedMul = 4f * t * (1f - t);
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