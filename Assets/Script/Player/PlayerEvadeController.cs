using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 회피 전담(게이지/실행/무적/종료)
/// - 단일 루틴 구조로 GC/분기 최소화
/// - CC/죽음 발생 시 즉시 종료
/// </summary>
[DisallowMultipleComponent]
public class PlayerEvadeController : MonoBehaviour
{
    private EvadeDataSO data;
    private PlayerAnimationController anim;
    private PlayerMovement movement;
    private Func<PlayerState> getState;
    private Action<PlayerState> changeState;

    private Coroutine evadeRoutine;
    private float currentGauge;
    private bool isInvincible;

    public void Setup(
        EvadeDataSO evadeData,
        PlayerAnimationController animCtrl,
        PlayerMovement move,
        Func<PlayerState> getStateFunc,
        Action<PlayerState> changeStateAction)
    {
        data = evadeData;
        anim = animCtrl;
        movement = move;
        getState = getStateFunc;
        changeState = changeStateAction;

        if (data != null)
            currentGauge = data.maxGauge;
    }

    public void TickRecharge(float dt)
    {
        if (data == null) return;
        if (currentGauge < data.maxGauge)
        {
            currentGauge += data.rechargeRate * dt;
            currentGauge = Mathf.Min(currentGauge, data.maxGauge);
        }
    }

    public bool CanEvade() => data != null && currentGauge >= data.evadeCost;
    public float GetEvadeGauge() => currentGauge;
    public float GetMaxEvadeGauge() => data != null ? data.maxGauge : 100f;
    public bool IsInvincible() => isInvincible;

    public void PerformEvade(Vector2 moveInput, Action preEvadeCleanup)
    {
        if (data == null || !CanEvade()) return;

        preEvadeCleanup?.Invoke();
        currentGauge -= data.evadeCost;

        Vector3 initialDir;
        if (moveInput.magnitude > 0.1f)
            initialDir = new Vector3(moveInput.x, 0, moveInput.y);
        else
            initialDir = transform.forward;

        if (Camera.main != null)
        {
            Vector3 camF = Camera.main.transform.forward;
            Vector3 camR = Camera.main.transform.right;
            camF.y = 0; camR.y = 0;
            camF.Normalize(); camR.Normalize();
            initialDir = (camF * initialDir.z + camR * initialDir.x).normalized;
        }

        if (evadeRoutine != null) { StopCoroutine(evadeRoutine); evadeRoutine = null; }
        evadeRoutine = StartCoroutine(EvadeRoutine(initialDir));
    }

    public void CancelEvade()
    {
        if (evadeRoutine != null)
        {
            StopCoroutine(evadeRoutine);
            evadeRoutine = null;
        }
        isInvincible = false;
        anim?.EndEvade();
    }

    private IEnumerator EvadeRoutine(Vector3 initialDir)
    {
        changeState?.Invoke(PlayerState.Evade);

        float elapsed = 0f;
        Vector3 currentDir = initialDir.normalized;
        currentDir.y = 0f;
        isInvincible = true;

        float dur = Mathf.Max(0f, data.evadeDuration);
        while (elapsed < dur)
        {
            float t = dur > 0f ? (elapsed / dur) : 1f;

            // 방향 고정/동적
            if (data.allowDirectionChangeWhileEvading)
            {
                Vector2 input = InputManager.Instance.GetMoveInput();
                if (input.magnitude >= data.minInputMagnitude)
                {
                    Vector3 newDir = new Vector3(input.x, 0, input.y);
                    if (Camera.main != null)
                    {
                        Vector3 camF = Camera.main.transform.forward;
                        Vector3 camR = Camera.main.transform.right;
                        camF.y = 0; camR.y = 0;
                        camF.Normalize(); camR.Normalize();
                        newDir = (camF * newDir.z + camR * newDir.x).normalized;
                        newDir.y = 0f;
                    }

                    float lerp = data.directionChangeSensitivity * Time.deltaTime;
                    currentDir = Vector3.Lerp(currentDir, newDir, lerp).normalized;

                    if (currentDir.sqrMagnitude > 0.01f)
                    {
                        Quaternion target = Quaternion.LookRotation(currentDir, Vector3.up);
                        transform.rotation = Quaternion.Slerp(transform.rotation, target, lerp);
                    }
                }
            }
            else
            {
                if (currentDir.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(currentDir);
            }

            float speedMul = data.speedCurve != null ? data.speedCurve.Evaluate(t) : 1f;
            transform.position += currentDir * (data.evadeSpeed * speedMul) * Time.deltaTime;

            if (elapsed >= data.invincibilityDuration)
                isInvincible = false;

            // CC/죽음으로 상태 바뀌면 중단
            var s = getState != null ? getState() : PlayerState.Idle;
            if (s == PlayerState.Knockback || s == PlayerState.Stun || s == PlayerState.Dead)
            {
                evadeRoutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        isInvincible = false;
        anim?.EndEvade();

        changeState?.Invoke(movement != null && movement.GetVelocityMagnitude() > 0.1f
            ? PlayerState.Move
            : PlayerState.Idle);

        evadeRoutine = null;
    }
}