// 수정: evade 중 gravity 일시중지 동작을 옵션화(evadeSuspendFalling).
using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerEvadeController : MonoBehaviour
{
    private EvadeDataSO data;
    private PlayerAnimationController anim;
    private PlayerMovement movement;
    private PlayerStats playerStats;
    private Func<PlayerState> getState;
    private Action<PlayerState> changeState;

    private Coroutine evadeRoutine;
    private bool isInvincible;

    [Header("디버그")]
    public bool debugLogs = false;

    [Header("충돌(회피 차단) 설정")]
    [Tooltip("Evade 이동을 막을 레이어(현재 Ground).")]
    [SerializeField] private LayerMask evadeBlockMask;
    [Tooltip("짧은 프리캐스트 및 메인 캐스트에서 사용하는 skin 거리")]
    [SerializeField] private float collisionSkin = 0.02f;
    [Tooltip("벽에 겹치거나 너무 붙어 있을 때 살짝 밀어낼 거리")]
    [SerializeField] private float smallPushDistance = 0.01f;

    [Header("Evade 동작 옵션")]
    [Tooltip("체크하면 회피 시작 시 중력을 일시적으로 끕니다(기존 동작). 기본적으로는 중력을 유지합니다(권장).")]
    [SerializeField] private bool evadeSuspendFalling = false;

    // 분류 임계값(경사 바닥과 벽 구분)
    private const float floorThreshold = 0.75f;      // penDir.y 또는 hit.normal.y가 이 이상이면 바닥으로 간주
    private const float horizThreshold = 0.2f;       // 침투 방향 수평 성분 크기(벽 판단 최소값)
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
        playerStats = GetComponent<PlayerStats>() ?? gameObject.AddComponent<PlayerStats>();
        getState = getStateFunc;
        changeState = changeStateAction;

        if (debugLogs) Debug.Log($"[Evade SETUP] maxStamina={playerStats.maxStamina}, minInputMag={data?.minInputMagnitude}");
    }

    private void OnValidate()
    {
        if (collisionSkin < 0f) collisionSkin = 0f;
        if (smallPushDistance < 0f) smallPushDistance = 0f;

        if (evadeBlockMask == 0)
        {
            int g = LayerMask.NameToLayer("Ground");
            if (g >= 0) evadeBlockMask = 1 << g;
        }
    }

    public void TickRecharge(float dt)
    {
        if (playerStats == null) return;
        playerStats.TickStaminaRecharge(dt);
    }

    public bool CanEvade()
    {
        if (data == null || playerStats == null) return false;
        return playerStats.CanUseStamina(data.evadeCost);
    }

    public float GetEvadeGauge() => playerStats != null ? playerStats.currentStamina : 0f;
    public float GetMaxEvadeGauge() => playerStats != null ? playerStats.maxStamina : 100f;
    public bool IsInvincible() => isInvincible;

    /// <summary>
    /// 현재 회피 게이지를 지정한 양만큼 소모합니다.
    /// </summary>
    public void ConsumeGauge(float amount)
    {
        if (amount <= 0f) return;
        if (playerStats == null) return;
        playerStats.ConsumeStamina(amount);
        if (debugLogs) Debug.Log($"[Evade] Gauge -{amount:F1} => {playerStats.currentStamina:F1}");
    }

    public void PerformEvade(Vector2 moveInput, Action preEvadeCleanup)
    {
        if (data == null || !CanEvade()) return;

        preEvadeCleanup?.Invoke();
        if (!playerStats.UseStamina(data.evadeCost))
            return;

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
        if (movement != null) movement.SetSuspendFalling(false);
        anim?.EndEvade();
        if (debugLogs) Debug.Log("[Evade] CancelEvade called");
    }

    // ─────────────────────────────────────────────────────────
    // 고정 방향 Evade
    // ─────────────────────────────────────────────────────────
    private IEnumerator FixedEvadeRoutine(Vector3 fixedDirection)
    {
        changeState?.Invoke(PlayerState.Evade);
        // 이전: if (movement != null) movement.SetSuspendFalling(true);
        // 옵션에 따라 회피 중 중력 일시정지 여부 결정
        if (evadeSuspendFalling && movement != null) movement.SetSuspendFalling(true);

        float elapsed = 0f;
        Vector3 dir = fixedDirection.normalized; dir.y = 0f;
        isInvincible = true;

        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

        float dur = Mathf.Max(0f, data.evadeDuration);

        while (elapsed < dur)
        {
            float t = dur > 0f ? elapsed / dur : 1f;
            float speedMul = data.speedCurve != null ? data.speedCurve.Evaluate(t) : 1f;
            Vector3 disp = dir * (data.evadeSpeed * speedMul) * Time.fixedDeltaTime;

            disp = CapsuleCastEvadeAdjustment(disp, out Vector3 pushOut);

            if (movement != null)
            {
                if (pushOut.sqrMagnitude > 0f) movement.MovePhysicsDisplacement(pushOut);
                if (disp.sqrMagnitude > 0f) movement.MovePhysicsDisplacement(disp);
            }
            else
            {
                if (pushOut.sqrMagnitude > 0f) transform.position += pushOut;
                if (disp.sqrMagnitude > 0f) transform.position += disp;
            }

            if (elapsed >= data.invincibilityDuration) isInvincible = false;

            if (getState != null)
            {
                var s = getState();
                if (s == PlayerState.Knockback || s == PlayerState.Stun || s == PlayerState.Dead)
                {
                    if (movement != null) movement.SetSuspendFalling(false);
                    yield break;
                }
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        if (movement != null) movement.SetSuspendFalling(false);
        FinishEvade();
    }

    // ─────────────────────────────────────────────────────────
    // 방향 변경 허용 Evade
    // ─────────────────────────────────────────────────────────
    private IEnumerator DynamicEvadeRoutine(Vector3 initialDirection)
    {
        changeState?.Invoke(PlayerState.Evade);
        // 이전: if (movement != null) movement.SetSuspendFalling(true);
        if (evadeSuspendFalling && movement != null) movement.SetSuspendFalling(true);

        float elapsed = 0f;
        float dur = Mathf.Max(0f, data.evadeDuration);

        Vector3 currentDir = initialDirection.normalized; currentDir.y = 0f;
        Vector3 lastValidDir = currentDir;
        isInvincible = true;

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
                }
            }
            else
            {
                currentDir = lastValidDir;
            }

            if (currentDir.sqrMagnitude > 0.01f)
            {
                Quaternion target = Quaternion.LookRotation(currentDir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, data.angularSpeed * Time.fixedDeltaTime);
            }

            float speedMul = data.speedCurve != null ? data.speedCurve.Evaluate(t) : 1f;
            Vector3 disp = currentDir * (data.evadeSpeed * speedMul) * Time.fixedDeltaTime;

            disp = CapsuleCastEvadeAdjustment(disp, out Vector3 pushOut);

            if (movement != null)
            {
                if (pushOut.sqrMagnitude > 0f) movement.MovePhysicsDisplacement(pushOut);
                if (disp.sqrMagnitude > 0f) movement.MovePhysicsDisplacement(disp);
            }
            else
            {
                if (pushOut.sqrMagnitude > 0f) transform.position += pushOut;
                if (disp.sqrMagnitude > 0f) transform.position += disp;
            }

            if (elapsed >= data.invincibilityDuration) isInvincible = false;

            if (getState != null)
            {
                var s = getState();
                if (s == PlayerState.Knockback || s == PlayerState.Stun || s == PlayerState.Dead)
                {
                    if (movement != null) movement.SetSuspendFalling(false);
                    yield break;
                }
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        if (movement != null) movement.SetSuspendFalling(false);
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

    // 이동 조정: desiredDisp → (전진 이동) / pushOut(겹침 해소)
    private Vector3 CapsuleCastEvadeAdjustment(Vector3 desiredDisp, out Vector3 pushOut)
    {
        pushOut = Vector3.zero;
        if (movement == null || desiredDisp.sqrMagnitude <= 0f || evadeBlockMask == 0)
            return desiredDisp;

        CapsuleCollider cap = movement.GetComponent<CapsuleCollider>();
        if (cap == null) return desiredDisp;

        Vector3 dir = desiredDisp.normalized;
        float dist = desiredDisp.magnitude;

        Vector3 centerWorld = cap.transform.TransformPoint(cap.center);
        float radius = cap.radius;
        float halfLine = Mathf.Max(cap.height * 0.5f - radius, 0f);
        Vector3 p0 = centerWorld + Vector3.up * halfLine;
        Vector3 p1 = centerWorld - Vector3.up * halfLine;

        // 1) Overlap 검사 (벽만 차단)
        Collider[] overlaps = Physics.OverlapCapsule(p0, p1, radius, evadeBlockMask, QueryTriggerInteraction.Ignore);
        if (overlaps != null && overlaps.Length > 0)
        {
            foreach (var other in overlaps)
            {
                if (Physics.ComputePenetration(
                        cap, cap.transform.position, cap.transform.rotation,
                        other, other.transform.position, other.transform.rotation,
                        out Vector3 penDir, out float penDist))
                {
                    float penUp = penDir.y;
                    float horizMag = new Vector2(penDir.x, penDir.z).magnitude;

                    // 바닥이면 무시
                    if (penUp >= floorThreshold) continue;

                    // 벽 판단
                    if (horizMag >= horizThreshold)
                    {
                        float resolve = Mathf.Min(penDist + collisionSkin, smallPushDistance);
                        pushOut = penDir * resolve;
                        if (debugLogs)
                            Debug.Log($"[EvadeCollision] Overlap WALL: push={resolve:F3}, penDir={penDir}");
                        return Vector3.zero;
                    }
                }
            }
        }

        // 2) 프리캐스트 (벽만 차단)
        if (Physics.CapsuleCast(p0, p1, radius, dir, out RaycastHit preHit, collisionSkin, evadeBlockMask, QueryTriggerInteraction.Ignore))
        {
            if (preHit.normal.y < floorThreshold) // 벽으로 간주
            {
                pushOut = preHit.normal * smallPushDistance;
                if (debugLogs)
                    Debug.Log($"[EvadeCollision] PreCast WALL @ {preHit.collider.name}, push={smallPushDistance:F3}");
                return Vector3.zero;
            }
            // 바닥이면 통과
        }

        // 3) 메인 캐스트 (벽만 제한)
        float castDistance = dist + collisionSkin;
        if (Physics.CapsuleCast(p0, p1, radius, dir, out RaycastHit mainHit, castDistance, evadeBlockMask, QueryTriggerInteraction.Ignore))
        {
            if (mainHit.normal.y < floorThreshold)
            {
                float allowed = mainHit.distance - collisionSkin;
                if (allowed < 0f) allowed = 0f;
                if (allowed < dist && debugLogs)
                    Debug.Log($"[EvadeCollision] MainCast clip: allowed={allowed:F3}/{dist:F3} hit={mainHit.collider.name}");
                return dir * allowed;
            }
            // floor hit → 무시
        }

        // 정상 이동
        return desiredDisp;
    }
}