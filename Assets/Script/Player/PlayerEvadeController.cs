using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

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

    // NavMeshAgent는 더 이상 직접 제어하지 않음

    [Header("디버그")]
    [Tooltip("에비드 관련 디버그 로그 켜기 (프레임 샘플링 사용)")]
    public bool debugLogs = false;

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

        if (debugLogs) Debug.Log($"[Evade SETUP] maxGauge={currentGauge}");
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
        // InputManager에서 이미 정규화되었으므로, .normalized 호출은 이중 안전장치
        if (moveInput.magnitude > data.minInputMagnitude)
            evadeDir = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
        else
            evadeDir = transform.forward; // 이미 정규화된 단위 벡터

        // PlayerMovement에 있는 CameraRelative를 직접 호출하여 방향 변환
        if (Camera.main != null && movement != null)
        {
            evadeDir = movement.CameraRelative(evadeDir);
        }

        if (evadeRoutine != null) { StopCoroutine(evadeRoutine); }

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
        if (debugLogs) Debug.Log("[Evade] Fixed Start");

        float elapsed = 0f;
        Vector3 dir = fixedDirection.normalized;
        dir.y = 0f;
        isInvincible = true;

        if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(dir);

        float dur = Mathf.Max(0f, data.evadeDuration);
        while (elapsed < dur)
        {
            float t = dur > 0f ? (elapsed / dur) : 1f;
            float speedMul = data.speedCurve != null ? data.speedCurve.Evaluate(t) : 1f;
            // 고정 타임스텝을 사용하여 회피 이동량 계산
            Vector3 evadeDisplacement = dir * (data.evadeSpeed * speedMul) * Time.fixedDeltaTime;

            // 회피 이동량을 transform.position에 직접 적용
            transform.position += evadeDisplacement;

            if (elapsed >= data.invincibilityDuration) isInvincible = false;

            if (getState != null)
            {
                var s = getState();
                if (s == PlayerState.Knockback || s == PlayerState.Stun || s == PlayerState.Dead)
                {
                    if (debugLogs) Debug.Log($"[Evade] Interrupted by state {s}");
                    yield break;
                }
            }
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        FinishEvade();
    }

    private IEnumerator DynamicEvadeRoutine(Vector3 initialDirection)
    {
        changeState?.Invoke(PlayerState.Evade);
        if (debugLogs) Debug.Log("[Evade] Dynamic Start");

        float elapsed = 0f;
        Vector3 currentDir = initialDirection.normalized;
        currentDir.y = 0f;
        isInvincible = true;

        float dur = Mathf.Max(0f, data.evadeDuration);
        while (elapsed < dur)
        {
            float t = dur > 0f ? (elapsed / dur) : 1f;

            Vector2 input = InputManager.Instance.GetMoveInput();
            if (input.magnitude >= data.minInputMagnitude)
            {
                Vector3 newDir = movement.CameraRelative(new Vector3(input.x, 0, input.y));

                float lerp = data.directionChangeSensitivity * Time.fixedDeltaTime;
                currentDir = Vector3.Lerp(currentDir, newDir, lerp).normalized;

                if (currentDir.sqrMagnitude > 0.01f)
                {
                    var agent = movement.GetComponent<NavMeshAgent>();
                    Quaternion target = Quaternion.LookRotation(currentDir, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, target, (agent != null ? agent.angularSpeed : 720f) * Time.fixedDeltaTime);
                }
            }

            float speedMul = data.speedCurve != null ? data.speedCurve.Evaluate(t) : 1f;
            // 고정 타임스텝을 사용하여 회피 이동량 계산
            Vector3 evadeDisplacement = currentDir * (data.evadeSpeed * speedMul) * Time.fixedDeltaTime;

            // 회피 이동량을 transform.position에 직접 적용
            transform.position += evadeDisplacement;

            if (elapsed >= data.invincibilityDuration) isInvincible = false;

            if (getState != null)
            {
                var s = getState();
                if (s == PlayerState.Knockback || s == PlayerState.Stun || s == PlayerState.Dead)
                {
                    if (debugLogs) Debug.Log($"[Evade] Interrupted by state {s}");
                    yield break;
                }
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
        if (debugLogs) Debug.Log("[Evade] End");
        evadeRoutine = null;

        if (movement != null && movement.GetVelocityMagnitude() > 0.1f)
            changeState?.Invoke(PlayerState.Move);
        else
            changeState?.Invoke(PlayerState.Idle);
    }
}