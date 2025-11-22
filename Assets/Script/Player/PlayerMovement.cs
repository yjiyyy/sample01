// (기존 PlayerMovement 파일을 유지하되 하단에 MoveFilteredDisplacement 추가 - 전체 파일은 사용자 제공 버전을 기본으로 유지)
// 아래는 기존 파일에서 변경/추가된 부분을 포함한 전체 파일 복사본입니다.

using UnityEngine;
using System.Collections;
using System;

/*
 * PlayerMovement + Headroom 클램프 (원본 유지)
 * - Rigidbody(useGravity=true)
 * - 수평 이동: rb.MovePosition
 * - Evade/Knockback 중 중력 보류는 기존 유지
 * - 기존 MovePhysicsDisplacement를 유지하고, MoveFilteredDisplacement를 추가하여 외부가 호출할 API를 통일
 */

[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class PlayerMovement : MonoBehaviour
{
    private const float BACKSTEP_ENTER_ANGLE = 120f;
    private const float BACKSTEP_EXIT_ANGLE = 100f;
    private const float EPS = 0.0001f;

    [Header("이동 옵션")]
    [SerializeField, Tooltip("기본 이동 속도 (m/s)")]
    private float baseMoveSpeed = 10f;

    [Tooltip("입력이 없을 때 멈춤 여부")]
    public bool stopWhenNoInput = true;

    [Header("회전 옵션")]
    [SerializeField, Tooltip("초당 회전 가능한 최대 각도(deg)")]
    public float rotationSpeedDegPerSec = 720f;

    [Header("디버그")]
    public bool debugLogs = false;

    [Header("Headroom(낮은 천장) 충돌")]
    [Tooltip("머리 공간을 막는 레이어 (Ground 레이어 할당)")]
    [SerializeField] private LayerMask headBlockMask;
    [Tooltip("머리 검사 영역 비율(상단 cylindrical 40%)")]
    [SerializeField, Range(0.2f, 0.6f)] private float headPortion = 0.4f;
    [Tooltip("머리 캡슐 반경 감소량")]
    [SerializeField, Range(0f, 0.05f)] private float headMargin = 0.01f;
    [Tooltip("이진(반감) 탐색 횟수")]
    [SerializeField, Range(1, 3)] private int headClampIterations = 2;

    private Rigidbody rb;
    private CapsuleCollider capsule; // 머리 검사용
    private Camera mainCam;
    private PlayerWeaponController weaponCtrl;
    private PlayerAnimationController anim;

    private bool isKnockbacked = false;
    private Coroutine knockbackRoutine;

    private Vector3 lastInput = Vector3.zero;
    private bool backStepActive = false;
    private Vector3 _lastLookDirection;
    private float currentMoveSpeed = 0f;

    // Evade/Knockback 중 중력 보류 플래그 (기존 유지)
    private bool suspendFalling = false;

    private StageManager stageManager;
    public Action onPlayerFellOutOfStage;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        mainCam = Camera.main;
        weaponCtrl = GetComponent<PlayerWeaponController>();
        anim = GetComponent<PlayerAnimationController>();

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        _lastLookDirection = transform.forward;

#if UNITY_6000_0_OR_NEWER
        stageManager = UnityEngine.Object.FindFirstObjectByType<StageManager>();
        if (stageManager == null)
            stageManager = UnityEngine.Object.FindFirstObjectByType<StageManager>(UnityEngine.FindObjectsInactive.Include);
#else
        stageManager = FindObjectOfType<StageManager>();
#endif

        if (stageManager == null)
            Debug.LogWarning("[PlayerMovement] StageManager not found. KillZone uses default 0.");

        // 기본 Ground 레이어 자동 할당 (비어 있으면)
        if (headBlockMask == 0)
        {
            int g = LayerMask.NameToLayer("Ground");
            if (g >= 0) headBlockMask = 1 << g;
        }
    }

    void Update()
    {
        if (isKnockbacked) return;
        Vector2 raw = InputManager.Instance.GetMoveInput();
        lastInput = new Vector3(raw.x, 0f, raw.y);
    }

    void FixedUpdate()
    {
        bool isARFiring = weaponCtrl != null && weaponCtrl.IsARFiring;
        bool arAllowMove = weaponCtrl != null && weaponCtrl.ARAllowMoveWhileFiring;
        currentMoveSpeed = ComputeCurrentMoveSpeed(isARFiring, arAllowMove);

        bool isEvading = weaponCtrl != null && weaponCtrl.CurrentState == PlayerState.Evade;
        if (!isKnockbacked && !isEvading)
        {
            HandleHorizontal();
            HandleRotation(isARFiring);
        }

        CheckKillZone();

        if (!isEvading)
            HandleBackStep(lastInput.sqrMagnitude > EPS, IsMovementBlocked(), lastInput);
    }

    private void HandleHorizontal()
    {
        Vector3 desiredMove = ComputeHorizontalDisplacement();
        if (desiredMove.sqrMagnitude <= EPS) return;

        MovePhysicsDisplacement(desiredMove);

        if (desiredMove.sqrMagnitude > EPS)
            _lastLookDirection = desiredMove.normalized;
    }

    private Vector3 ComputeHorizontalDisplacement()
    {
        if (IsMovementBlocked() || lastInput.sqrMagnitude <= EPS)
            return Vector3.zero;

        Vector3 camRel = CameraRelative(lastInput);
        float speed = currentMoveSpeed;
        if (speed <= EPS) return Vector3.zero;

        float inputMag = Mathf.Clamp01(lastInput.magnitude);
        return camRel.normalized * speed * inputMag * Time.fixedDeltaTime;
    }

    private bool IsMovementBlocked()
    {
        if (weaponCtrl == null) return false;
        PlayerState state = weaponCtrl.CurrentState;
        bool isARFiring = weaponCtrl.IsARFiring;
        bool arAllowMove = weaponCtrl.ARAllowMoveWhileFiring;
        bool attackBlocking = state == PlayerState.Attack && !(isARFiring && arAllowMove);

        if (attackBlocking ||
            state == PlayerState.Knockback ||
            state == PlayerState.Stun ||
            state == PlayerState.Dead ||
            state == PlayerState.Evade)
            return true;

        return false;
    }

    // 재사용: Evade/Knockback 등에서 호출
    public void MovePhysicsDisplacement(Vector3 disp)
    {
        if (rb == null || disp.sqrMagnitude <= EPS) return;

        // 낮은 천장 진입 클램프 (머리 부분 검사)
        if (capsule != null && headClampIterations > 0 && headPortion > 0f)
        {
            disp = NarrowSpaceUtil.ClampHeadroomHorizontal(
                capsule,
                rb.position,
                disp,
                headBlockMask,
                headClampIterations,
                headPortion,
                headMargin
            );
        }

        if (disp.sqrMagnitude <= EPS) return;
        rb.MovePosition(rb.position + disp);
    }

    // ----------------------------------------------------------------
    // 새로 추가한 메서드: 외부에서 MoveFilteredDisplacement로 호출할 수 있게 함.
    // 기존 MovePhysicsDisplacement를 내부에서 호출하여 호환성 유지.
    // ----------------------------------------------------------------
    public void MoveFilteredDisplacement(Vector3 disp)
    {
        MovePhysicsDisplacement(disp);
    }

    private void HandleRotation(bool isARFiring)
    {
        if (weaponCtrl != null && weaponCtrl.CurrentState == PlayerState.Evade)
            return;

        Vector3 desiredDir = _lastLookDirection;
        bool arRotationLocked = weaponCtrl != null && weaponCtrl.ARIsRotationLocked;

        if (arRotationLocked && isARFiring && weaponCtrl != null)
        {
            Vector3 lockedF = weaponCtrl.ARLockedForward;
            if (lockedF.sqrMagnitude > EPS)
                desiredDir = lockedF.normalized;
        }

        if (desiredDir.sqrMagnitude > EPS)
        {
            Quaternion targetRot = Quaternion.LookRotation(desiredDir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRot,
                rotationSpeedDegPerSec * Time.fixedDeltaTime
            );
        }
    }

    private void HandleBackStep(bool hasInput, bool movementBlocked, Vector3 moveInput)
    {
        Vector3 currentMoveDir = moveInput.sqrMagnitude > EPS ? moveInput.normalized : Vector3.zero;
        if (hasInput && !movementBlocked && currentMoveDir.sqrMagnitude > EPS)
        {
            float absAngle = Vector3.Angle(transform.forward, currentMoveDir);
            if (!backStepActive && absAngle >= BACKSTEP_ENTER_ANGLE)
            {
                backStepActive = true;
                anim?.SetBackStep(true);
            }
            else if (backStepActive && absAngle <= BACKSTEP_EXIT_ANGLE)
            {
                backStepActive = false;
                anim?.SetBackStep(false);
            }
        }
        else
        {
            if (backStepActive)
            {
                backStepActive = false;
                anim?.SetBackStep(false);
            }
        }
    }

    private void CheckKillZone()
    {
        float limit = stageManager != null ? stageManager.killY : 0f;
        if (transform.position.y <= limit)
        {
            if (stageManager != null)
                stageManager.HandlePlayerFall(gameObject);
            onPlayerFellOutOfStage?.Invoke();
        }
    }

    void LateUpdate()
    {
        if (anim != null && weaponCtrl != null)
        {
            bool isARFiring = weaponCtrl.IsARFiring;
            bool arAllowMove = weaponCtrl.ARAllowMoveWhileFiring;
            float lowerSpeed = 1f;

            if (isARFiring && arAllowMove)
            {
                var arData = weaponCtrl.GetCurrentWeaponData() as WeaponDataSO_AR;
                if (arData != null)
                    lowerSpeed = Mathf.Max(0f, arData.animPlaybackSpeedWhileFiring);
            }
            anim.SetLowerBodyPlaybackSpeed(lowerSpeed);
        }
    }

    private float ComputeCurrentMoveSpeed(bool isARFiring, bool arAllowMove)
    {
        float speed = baseMoveSpeed;
        if (isARFiring && arAllowMove && weaponCtrl != null)
        {
            var arData = weaponCtrl.GetCurrentWeaponData() as WeaponDataSO_AR;
            if (arData != null)
                speed *= Mathf.Max(0f, arData.moveSpeedWhileFiring);
        }
        return speed;
    }

    // Knockback (원본 유지: 필요 시 MovePhysicsDisplacement로 교체 가능)
    public void ApplyKnockback(Vector3 dir, float force, float duration, Transform attacker = null)
    {
        if (knockbackRoutine != null) StopCoroutine(knockbackRoutine);
        if (debugLogs) Debug.Log($"[PM KNOCK] start dir={dir}, force={force}, dur={duration}");
        knockbackRoutine = StartCoroutine(KnockbackRoutine(dir, force, duration, attacker));
    }

    private IEnumerator KnockbackRoutine(Vector3 dir, float force, float duration, Transform attacker)
    {
        isKnockbacked = true;
        Vector3 knockDir = dir.normalized; knockDir.y = 0f;
        float elapsed = 0f;

        if (attacker != null)
        {
            Vector3 lookDir = attacker.position - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > EPS)
                _lastLookDirection = lookDir.normalized;
        }

        while (elapsed < duration)
        {
            float t = 1f - Mathf.Clamp01(elapsed / Mathf.Max(duration, EPS));
            float currentSpeed = force * t;
            Vector3 disp = knockDir * currentSpeed * Time.fixedDeltaTime;
            MovePhysicsDisplacement(disp);

            if (_lastLookDirection.sqrMagnitude > EPS)
            {
                Quaternion target = Quaternion.LookRotation(_lastLookDirection, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    target,
                    rotationSpeedDegPerSec * Time.fixedDeltaTime
                );
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        isKnockbacked = false;
        knockbackRoutine = null;
        if (debugLogs) Debug.Log("[PM KNOCK] finished");
    }

    public void CancelKnockback()
    {
        if (knockbackRoutine != null) StopCoroutine(knockbackRoutine);
        isKnockbacked = false;
        if (debugLogs) Debug.Log("[PM KNOCK] cancelled");
    }

    public bool IsCurrentlyKnockbacked() => isKnockbacked;

    public float GetAnimatorSpeedEstimate()
    {
        float inputMag = Mathf.Clamp01(lastInput.magnitude);
        if (baseMoveSpeed <= EPS) return inputMag > EPS ? 1f : 0f;
        return Mathf.Clamp01(inputMag * (currentMoveSpeed / baseMoveSpeed));
    }

    public float GetVelocityMagnitude()
    {
        float inputMag = Mathf.Clamp01(lastInput.magnitude);
        if (IsMovementBlocked()) return 0f;
        return inputMag * currentMoveSpeed;
    }

    public Vector3 CameraRelative(Vector3 input)
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return new Vector3(input.x, 0f, input.z);

        Vector3 camF = mainCam.transform.forward;
        Vector3 camR = mainCam.transform.right;
        camF.y = 0f; camR.y = 0f;
        camF.Normalize(); camR.Normalize();
        return camF * input.z + camR * input.x;
    }

    // Evade/Knockback 중 낙하 보류 제어 외부 재사용 위해 노출
    public void SetSuspendFalling(bool suspend)
    {
        suspendFalling = suspend;
        if (rb == null) return;
        if (suspend)
        {
            rb.useGravity = false;
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }
        else
        {
            rb.useGravity = true;
        }
    }

    public bool IsSuspendingFalling() => suspendFalling;
}