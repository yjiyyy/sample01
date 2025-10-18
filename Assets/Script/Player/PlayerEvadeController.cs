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
        if (data == null) return;
        if (!CanEvade()) return;

        // 사전 정리(공격/넉백/리코일/리로드)
        preEvadeCleanup?.Invoke();

        currentGauge -= data.evadeCost;

        // 초기 방향 계산
        Vector3 initialDir;
        if (moveInput.magnitude > 0.1f)
            initialDir = new Vector3(moveInput.x, 0, moveInput.y);
        else
            initialDir = transform.forward;

        // 카메라 기준 보정
        if (Camera.main != null)
        {
            Vector3 camF = Camera.main.transform.forward;
            Vector3 camR = Camera.main.transform.right;
            camF.y = 0; camR.y = 0;
            camF.Normalize(); camR.Normalize();
            initialDir = (camF * initialDir.z + camR * initialDir.x).normalized;
        }

        // 기존 루틴 중단
        if (evadeRoutine != null) { StopCoroutine(evadeRoutine); evadeRoutine = null; }

        // 실행
        if (data.allowDirectionChangeWhileEvading)
            evadeRoutine = StartCoroutine(DynamicEvadeRoutine(initialDir));
        else
            evadeRoutine = StartCoroutine(FixedEvadeRoutine(initialDir));
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

    private IEnumerator FixedEvadeRoutine(Vector3 fixedDirection)
    {
        changeState?.Invoke(PlayerState.Evade);

        float elapsed = 0f;
        Vector3 dir = fixedDirection.normalized;
        dir.y = 0f;
        isInvincible = true;

        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(dir);

        float dur = Mathf.Max(0f, data.evadeDuration);
        while (elapsed < dur)
        {
            float t = dur > 0f ? (elapsed / dur) : 1f;
            float speedMul = data.speedCurve != null ? data.speedCurve.Evaluate(t) : 1f;
            transform.position += dir * (data.evadeSpeed * speedMul) * Time.deltaTime;

            if (elapsed >= data.invincibilityDuration)
                isInvincible = false;

            // CC/죽음 등으로 상태가 바뀌면 즉시 종료
            if (getState != null)
            {
                var s = getState();
                if (s == PlayerState.Knockback || s == PlayerState.Stun || s == PlayerState.Dead)
                {
                    evadeRoutine = null;
                    yield break;
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        FinishEvade();
        evadeRoutine = null;
    }

    private IEnumerator DynamicEvadeRoutine(Vector3 initialDirection)
    {
        changeState?.Invoke(PlayerState.Evade);

        float elapsed = 0f;
        Vector3 currentDir = initialDirection.normalized;
        currentDir.y = 0f;
        isInvincible = true;

        float dur = Mathf.Max(0f, data.evadeDuration);
        while (elapsed < dur)
        {
            float t = dur > 0f ? (elapsed / dur) : 1f;

            // 입력에 따른 방향 변경
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

            float speedMul = data.speedCurve != null ? data.speedCurve.Evaluate(t) : 1f;
            transform.position += currentDir * (data.evadeSpeed * speedMul) * Time.deltaTime;

            if (elapsed >= data.invincibilityDuration)
                isInvincible = false;

            // CC/죽음 등으로 상태가 바뀌면 즉시 종료
            if (getState != null)
            {
                var s = getState();
                if (s == PlayerState.Knockback || s == PlayerState.Stun || s == PlayerState.Dead)
                {
                    evadeRoutine = null;
                    yield break;
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        FinishEvade();
        evadeRoutine = null;
    }

    private void FinishEvade()
    {
        isInvincible = false;
        anim?.EndEvade();

        // 속도 기반으로 Idle/Move 결정
        if (movement != null && movement.GetVelocityMagnitude() > 0.1f)
            changeState?.Invoke(PlayerState.Move);
        else
            changeState?.Invoke(PlayerState.Idle);
    }
}