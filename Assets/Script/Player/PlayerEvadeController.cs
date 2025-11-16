using System;
using System.Collections;
using UnityEngine;

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

    [Header("디버그")]
    public bool debugLogs = false;

    private const float TinyInputThreshold = 0.05f;

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

        if (data != null) currentGauge = data.maxGauge;
        if (debugLogs) Debug.Log($"[Evade SETUP] maxGauge={currentGauge}, minInputMag={data?.minInputMagnitude}");
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

    public bool CanEvade()
    {
        if (data == null) return false;
        return currentGauge >= data.evadeCost;
    }

    public float GetEvadeGauge() => currentGauge;
    public float GetMaxEvadeGauge() => data != null ? data.maxGauge : 100f;
    public bool IsInvincible() => isInvincible;

    public void PerformEvade(Vector2 moveInput, Action preEvadeCleanup)
    {
        if (data == null || !CanEvade()) return;

        preEvadeCleanup?.Invoke();
        currentGauge -= data.evadeCost;

        Vector3 evadeDir;
        if (moveInput.magnitude > TinyInputThreshold)
        {
            evadeDir = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
            if (movement != null) evadeDir = movement.CameraRelative(evadeDir);
        }
        else
        {
            evadeDir = transform.forward;
        }

        if (evadeRoutine != null) StopCoroutine(evadeRoutine);

        if (data.allowDirectionChangeWhileEvading)
            evadeRoutine = StartCoroutine(DynamicEvadeRoutine(evadeDir));
        else
            evadeRoutine = StartCoroutine(FixedEvadeRoutine(evadeDir));
    }

    public void CancelEvade()
    {
        if (evadeRoutine != null) StopCoroutine(evadeRoutine);
        isInvincible = false;
        anim?.EndEvade();
        if (debugLogs) Debug.Log("[Evade] CancelEvade called");
    }

    private IEnumerator FixedEvadeRoutine(Vector3 fixedDirection)
    {
        changeState?.Invoke(PlayerState.Evade);
        float elapsed = 0f;
        Vector3 dir = fixedDirection.normalized;
        dir.y = 0f;

        isInvincible = true;

        if (dir.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        float dur = Mathf.Max(0f, data.evadeDuration);
        Vector3 startPos = transform.position;

        while (elapsed < dur)
        {
            float t = dur > 0f ? elapsed / dur : 1f;
            float speedMul = data.speedCurve != null ? data.speedCurve.Evaluate(t) : 1f;
            Vector3 disp = dir * (data.evadeSpeed * speedMul) * Time.fixedDeltaTime;
            transform.position += disp;

            if (elapsed >= data.invincibilityDuration) isInvincible = false;

            // 인터럽트
            if (getState != null)
            {
                var s = getState();
                if (s == PlayerState.Knockback || s == PlayerState.Stun || s == PlayerState.Dead)
                    yield break;
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        FinishEvade();
    }

    private IEnumerator DynamicEvadeRoutine(Vector3 initialDirection)
    {
        changeState?.Invoke(PlayerState.Evade);
        float elapsed = 0f;
        float dur = Mathf.Max(0f, data.evadeDuration);

        Vector3 currentDir = initialDirection.normalized;
        currentDir.y = 0f;
        Vector3 lastValidDir = currentDir;

        isInvincible = true;
        Vector3 startPos = transform.position;

        while (elapsed < dur)
        {
            float t = dur > 0f ? elapsed / dur : 1f;
            Vector2 input = InputManager.Instance.GetMoveInput();

            bool hasInput = input.magnitude >= TinyInputThreshold;
            if (hasInput)
            {
                Vector3 raw = new Vector3(input.x, 0f, input.y);
                Vector3 camDir = movement != null ? movement.CameraRelative(raw).normalized : raw.normalized;

                if (input.magnitude >= data.minInputMagnitude)
                {
                    float factor = Mathf.Clamp01(input.magnitude / data.minInputMagnitude);
                    float lerp = data.directionChangeSensitivity * factor * Time.fixedDeltaTime;
                    currentDir = Vector3.Lerp(currentDir, camDir, lerp).normalized;
                    lastValidDir = currentDir;
                }
                else
                {
                    float lerp = (data.directionChangeSensitivity * 0.25f) * Time.fixedDeltaTime;
                    currentDir = Vector3.Lerp(currentDir, camDir, lerp).normalized;
                    // lastValidDir 갱신 안 함
                }
            }
            else
            {
                currentDir = lastValidDir; // 입력 없음 → 유지
            }

            // 회전(항상)
            if (currentDir.sqrMagnitude > 0.01f)
            {
                Quaternion target = Quaternion.LookRotation(currentDir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, data.angularSpeed * Time.fixedDeltaTime);
            }

            float speedMul = data.speedCurve != null ? data.speedCurve.Evaluate(t) : 1f;
            Vector3 disp = currentDir * (data.evadeSpeed * speedMul) * Time.fixedDeltaTime;
            transform.position += disp;

            if (elapsed >= data.invincibilityDuration) isInvincible = false;

            if (getState != null)
            {
                var s = getState();
                if (s == PlayerState.Knockback || s == PlayerState.Stun || s == PlayerState.Dead)
                    yield break;
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        FinishEvade();
    }

    private void FinishEvade()
    {
        isInvincible = false;
        anim?.EndEvade();
        evadeRoutine = null;

        if (movement != null && movement.GetVelocityMagnitude() > 0.1f)
            changeState?.Invoke(PlayerState.Move);
        else
            changeState?.Invoke(PlayerState.Idle);
    }
}