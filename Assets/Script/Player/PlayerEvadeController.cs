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

    private NavMeshAgent navAgent;

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

        navAgent = null;
        if (movement != null)
            navAgent = movement.GetComponent<NavMeshAgent>();
        if (navAgent == null)
            navAgent = GetComponent<NavMeshAgent>();

        if (data != null) currentGauge = data.maxGauge;

        if (debugLogs) Debug.Log($"[Evade SETUP] maxGauge={currentGauge}, navAgentPresent={(navAgent != null)}");
    }

    public void TickRecharge(float dt)
    {
        if (data == null) return;
        if (currentGauge < data.maxGauge)
        {
            currentGauge += data.rechargeRate * dt;
            currentGauge = Mathf.Min(currentGauge, data.maxGauge);
            if (debugLogs && Time.frameCount % 30 == 0)
                Debug.Log($"[Evade RECHARGE] gauge={currentGauge:F3}/{data.maxGauge}");
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
        if (!CanEvade())
        {
            if (debugLogs) Debug.Log("[Evade] Cannot evade: insufficient gauge");
            return;
        }

        preEvadeCleanup?.Invoke();
        currentGauge -= data.evadeCost;

        Vector3 initialDir;
        if (moveInput.magnitude > data.minInputMagnitude)
            initialDir = new Vector3(moveInput.x, 0f, moveInput.y);
        else
        {
            // InputManager에 최근 유효 입력을 캐시해두었다면 그것을 우선 사용
            Vector2 lastMove = InputManager.Instance.GetMoveInput(); // 이미 0인 경우가 많음 — 대안: InputManager가 최근NonZero를 제공
            if (lastMove.magnitude > data.minInputMagnitude)
                initialDir = new Vector3(lastMove.x, 0f, lastMove.y);
            else
                initialDir = transform.forward;
        }

        if (Camera.main != null)
        {
            Vector3 camF = Camera.main.transform.forward;
            Vector3 camR = Camera.main.transform.right;
            camF.y = 0f; camR.y = 0f;
            camF.Normalize(); camR.Normalize();
            initialDir = (camF * initialDir.z + camR * initialDir.x).normalized;
        }

        if (debugLogs)
        {
            Debug.Log($"[Evade] PerformEvade called. input={moveInput}, initialDir={initialDir}, gaugeAfter={currentGauge:F3}");
            if (Camera.main != null)
                Debug.Log($"[Evade] Camera main forward={Camera.main.transform.forward}, right={Camera.main.transform.right}");
        }

        if (evadeRoutine != null) { StopCoroutine(evadeRoutine); evadeRoutine = null; }

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
            Vector3 delta = dir * (data.evadeSpeed * speedMul) * Time.deltaTime;

            if (navAgent != null && navAgent.isOnNavMesh)
                navAgent.Move(delta);
            else
                transform.position += delta;

            if (elapsed >= data.invincibilityDuration) isInvincible = false;

            if (getState != null)
            {
                var s = getState();
                if (s == PlayerState.Knockback || s == PlayerState.Stun || s == PlayerState.Dead)
                {
                    if (debugLogs) Debug.Log($"[Evade] Interrupted by state {s}");
                    evadeRoutine = null;
                    yield break;
                }
            }

            if (debugLogs && Time.frameCount % 6 == 0)
            {
                Debug.Log($"[Evade LOOP - Fixed] elapsed={elapsed:F3}, dir={dir}, pos={transform.position}, isInv={isInvincible}");
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
                Vector3 newDir = new Vector3(input.x, 0f, input.y);
                if (Camera.main != null)
                {
                    Vector3 camF = Camera.main.transform.forward;
                    Vector3 camR = Camera.main.transform.right;
                    camF.y = 0f; camR.y = 0f;
                    camF.Normalize(); camR.Normalize();
                    newDir = (camF * newDir.z + camR * newDir.x).normalized;
                    newDir.y = 0f;
                }

                float lerp = data.directionChangeSensitivity * Time.deltaTime;
                currentDir = Vector3.Lerp(currentDir, newDir, lerp).normalized;

                if (currentDir.sqrMagnitude > 0.01f)
                {
                    Quaternion target = Quaternion.LookRotation(currentDir, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, target, (navAgent != null ? navAgent.angularSpeed : 720f) * Time.deltaTime);
                }
            }

            float speedMul = data.speedCurve != null ? data.speedCurve.Evaluate(t) : 1f;
            Vector3 delta = currentDir * (data.evadeSpeed * speedMul) * Time.deltaTime;

            if (navAgent != null && navAgent.isOnNavMesh)
                navAgent.Move(delta);
            else
                transform.position += delta;

            if (elapsed >= data.invincibilityDuration) isInvincible = false;

            if (getState != null)
            {
                var s = getState();
                if (s == PlayerState.Knockback || s == PlayerState.Stun || s == PlayerState.Dead)
                {
                    if (debugLogs) Debug.Log($"[Evade] Interrupted by state {s}");
                    evadeRoutine = null;
                    yield break;
                }
            }

            if (debugLogs && Time.frameCount % 6 == 0)
            {
                Debug.Log($"[Evade LOOP - Dynamic] elapsed={elapsed:F3}, currentDir={currentDir}, pos={transform.position}, isInv={isInvincible}");
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
        if (debugLogs) Debug.Log("[Evade] End");

        if (movement != null && movement.GetVelocityMagnitude() > 0.1f)
            changeState?.Invoke(PlayerState.Move);
        else
            changeState?.Invoke(PlayerState.Idle);
    }
}