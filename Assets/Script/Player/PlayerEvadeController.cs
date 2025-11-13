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

    [Header("디버그")]
    [Tooltip("회피 관련 디버그 로그 켜기")]
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

        // ✅ 디버그: 입력 확인
        if (debugLogs) Debug.Log($"[Evade] moveInput: {moveInput}, magnitude: {moveInput.magnitude:F3}, minInputMagnitude: {data.minInputMagnitude}");

        if (moveInput.magnitude > data.minInputMagnitude)
            evadeDir = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
        else
            evadeDir = transform.forward;

        // ✅ 디버그: 카메라 변환 전 방향
        if (debugLogs) Debug.Log($"[Evade] evadeDir BEFORE CameraRelative: {evadeDir}");

        if (Camera.main != null && movement != null)
        {
            evadeDir = movement.CameraRelative(evadeDir);
        }

        // ✅ 디버그: 카메라 변환 후 방향
        if (debugLogs) Debug.Log($"[Evade] evadeDir AFTER CameraRelative: {evadeDir}");

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

        // ✅ 디버그: 정규화 전후 방향
        if (debugLogs) Debug.Log($"[Evade] fixedDirection BEFORE normalize: {fixedDirection}");

        dir.y = 0f;

        // ✅ 디버그: y=0 후 방향과 크기
        if (debugLogs) Debug.Log($"[Evade] dir AFTER y=0: {dir}, magnitude: {dir.magnitude:F3}");

        isInvincible = true;

        if (dir.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(dir);
            if (debugLogs) Debug.Log($"[Evade] Rotation applied to direction: {dir}");
        }
        else
        {
            Debug.LogWarning($"[Evade] dir too small! sqrMagnitude: {dir.sqrMagnitude:F6} - NO MOVEMENT WILL OCCUR!");
        }

        float dur = Mathf.Max(0f, data.evadeDuration);

        // ✅ 디버그: 회피 시간과 속도
        if (debugLogs) Debug.Log($"[Evade] Duration: {dur}, Speed: {data.evadeSpeed}, Time.fixedDeltaTime: {Time.fixedDeltaTime:F4}");

        int frameCount = 0;
        Vector3 startPos = transform.position;

        while (elapsed < dur)
        {
            frameCount++;
            float t = dur > 0f ? (elapsed / dur) : 1f;
            float speedMul = data.speedCurve != null ? data.speedCurve.Evaluate(t) : 1f;

            Vector3 evadeDisplacement = dir * (data.evadeSpeed * speedMul) * Time.fixedDeltaTime;

            // ✅ 디버그: 처음 3프레임만 상세 로그
            if (debugLogs && frameCount <= 3)
            {
                Debug.Log($"[Evade Frame {frameCount}] t={t:F3}, speedMul={speedMul:F3}, " +
                         $"evadeSpeed={data.evadeSpeed}, fixedDeltaTime={Time.fixedDeltaTime:F4}, " +
                         $"dir={dir}, displacement={evadeDisplacement}, " +
                         $"currentPos={transform.position}");
            }

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

        Vector3 endPos = transform.position;
        float totalDistance = Vector3.Distance(startPos, endPos);

        // ✅ 디버그: 최종 결과
        if (debugLogs) Debug.Log($"[Evade] End - Total frames: {frameCount}, Total distance moved: {totalDistance:F2}, StartPos: {startPos}, EndPos: {endPos}");

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

        // PC 회피 문제 해결: 마지막 유효 입력 방향 저장
        Vector3 lastValidDir = currentDir;

        float dur = Mathf.Max(0f, data.evadeDuration);

        int frameCount = 0;
        Vector3 startPos = transform.position;

        while (elapsed < dur)
        {
            frameCount++;
            float t = dur > 0f ? (elapsed / dur) : 1f;

            Vector2 input = InputManager.Instance.GetMoveInput();
            if (input.magnitude >= data.minInputMagnitude)
            {
                Vector3 newDir = movement.CameraRelative(new Vector3(input.x, 0, input.y));

                float lerp = data.directionChangeSensitivity * Time.fixedDeltaTime;
                currentDir = Vector3.Lerp(currentDir, newDir, lerp).normalized;

                // 유효한 입력이 있으면 저장
                lastValidDir = currentDir;

                if (currentDir.sqrMagnitude > 0.01f)
                {
                    var agent = movement.GetComponent<NavMeshAgent>();
                    Quaternion target = Quaternion.LookRotation(currentDir, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation,
                        target,
                        (agent != null ? agent.angularSpeed : 720f) * Time.fixedDeltaTime
                    );
                }
            }
            else
            {
                // 입력이 없으면 마지막 유효 방향 유지 (PC 키보드 대응)
                currentDir = lastValidDir;
            }

            float speedMul = data.speedCurve != null ? data.speedCurve.Evaluate(t) : 1f;

            Vector3 evadeDisplacement = currentDir * (data.evadeSpeed * speedMul) * Time.fixedDeltaTime;

            // ✅ 디버그: 처음 3프레임만 상세 로그
            if (debugLogs && frameCount <= 3)
            {
                Debug.Log($"[Evade Dynamic Frame {frameCount}] t={t:F3}, speedMul={speedMul:F3}, " +
                         $"currentDir={currentDir}, displacement={evadeDisplacement}");
            }

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

        Vector3 endPos = transform.position;
        float totalDistance = Vector3.Distance(startPos, endPos);

        // ✅ 디버그: 최종 결과
        if (debugLogs) Debug.Log($"[Evade Dynamic] End - Total frames: {frameCount}, Total distance moved: {totalDistance:F2}");

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