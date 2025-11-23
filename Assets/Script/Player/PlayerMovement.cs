// PlayerMovement - Step & obstacle/floor/head masks separated
// - Implements: (1) absolute block for spaces narrower than player capsule (obstacles),
//               (2) configurable auto-step onto floor surfaces up to maxStepHeight,
//               (3) strict headroom checks so you can't step/enter if head area collides.
// - Uses binary-search step finder for performance/precision.
// - Unity 6 (6000.0.42f1) compatible.
// - Do NOT push to git automatically; apply into project file as requested.

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

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

    [Header("Headroom (머리) 검사")]
    [Tooltip("머리 충돌 검사에 사용할 레이어 (예: ceilings)")]
    [SerializeField] private LayerMask headMask;
    [Tooltip("머리 검사 영역 비율(상단 cylindrical 부분의 비율)")]
    [SerializeField, Range(0.2f, 0.6f)] private float headPortion = 0.4f;
    [Tooltip("머리 캡슐 반경 감소량 - 안전 여유")]
    [SerializeField, Range(0f, 0.05f)] private float headMargin = 0.02f;
    [Tooltip("머리 클램프(이진 탐색) 반복 횟수")]
    [SerializeField, Range(1, 4)] private int headClampIterations = 2;

    [Header("Collision (분리된 레이어 마스크)")]
    [Tooltip("장애물(벽/좁은 물체) - 진입 차단 대상")]
    [SerializeField] private LayerMask obstacleMask;
    [Tooltip("바닥(올라탈 수 있는 표면) - 스텝 판정용")]
    [SerializeField] private LayerMask floorMask;
    [Tooltip("이동(캐스트)에서 검사할 레이어 마스크(레거시). 사용하지 말고 obstacleMask/floorMask/headMask 사용 권장.")]
    [SerializeField] private LayerMask movementBlockMask_fallback;

    [Header("슬라이딩 & 스텝")]
    [Tooltip("충돌 스킨(히트 거리에서 빼는 여유)")]
    [SerializeField] private float collisionSkin = 0.03f;
    [Tooltip("바닥 판정 임계값 (normal.y >= 이면 바닥으로 간주)")]
    [SerializeField] private float floorThreshold = 0.75f;
    [Tooltip("슬라이드 재시도 횟수")]
    [SerializeField, Range(0, 2)] private int slideIterations = 1;
    [Tooltip("아주 작은 이동 무시 임계값 (m)")]
    [SerializeField] private float tinyDispThreshold = 0.002f;

    [Header("스텝(자동 올라타기) 설정")]
    [Tooltip("최대 올라탈 수 있는 높이 (m)")]
    [SerializeField] private float maxStepHeight = 0.35f;
    [Tooltip("스텝 높이 찾기 시 이진 탐색 반복 횟수 (정밀도)")]
    [SerializeField, Range(1, 8)] private int stepSearchIterations = 5;
    [Tooltip("스텝을 허용할 때 바닥을 체크하는 수직 거리(버퍼, m)")]
    [SerializeField] private float floorCheckDepth = 0.15f;

    [Header("Headroom Strict Block")]
    [Tooltip("머리 영역이 막히면 이동 전체를 차단")]
    [SerializeField] private bool strictHeadroomBlock = true;

    [Header("Gizmos & debug")]
    [SerializeField] private bool enableGizmos = false;

    // 내부
    private Rigidbody rb;
    private CapsuleCollider capsule;
    private Camera mainCam;
    private PlayerWeaponController weaponCtrl;
    private PlayerAnimationController anim;

    private bool isKnockbacked = false;
    private Coroutine knockbackRoutine;

    private Vector3 lastInput = Vector3.zero;
    private bool backStepActive = false;
    private Vector3 _lastLookDirection;
    private float currentMoveSpeed = 0f;

    private bool suspendFalling = false;

    private StageManager stageManager;
    public Action onPlayerFellOutOfStage;

    // debug state
    private Vector3 lastAttemptedDisp = Vector3.zero;
    private bool lastAttemptedBlocked = false;
    private float lastAttemptedStepH = 0f;

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

        // Fallbacks for older inspector fields
        if (obstacleMask == 0 && movementBlockMask_fallback != 0)
            obstacleMask = movementBlockMask_fallback;
        if (headMask == 0)
        {
            int g = LayerMask.NameToLayer("Ground");
            if (g >= 0) headMask = 1 << g;
        }
        if (floorMask == 0)
        {
            int f = LayerMask.NameToLayer("Floor");
            if (f >= 0) floorMask = 1 << f;
            else
            {
                int g = LayerMask.NameToLayer("Ground");
                if (g >= 0) floorMask = 1 << g;
            }
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

        MoveFilteredDisplacement(desiredMove);

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

    // 주요: 실제 물리 이동 적용 전 검사/클램프/스텝/엄격 차단 등을 처리
    public void MovePhysicsDisplacement(Vector3 disp)
    {
        lastAttemptedDisp = disp;
        lastAttemptedBlocked = false;
        lastAttemptedStepH = 0f;

        if (rb == null || disp.sqrMagnitude <= EPS) return;

        // 1) strict headroom block: 현재 머리는 안 닿고 목표에서 머리만 닿으면 이동 차단
        if (capsule != null && strictHeadroomBlock && headClampIterations > 0 && headPortion > 0f && headMask != 0)
        {
            Transform t = capsule.transform;

            Vector3 worldCenterNow = t.TransformPoint(capsule.center) + (rb.position - t.position);

            float radius = capsule.radius;
            float height = capsule.height;
            float cylLen = Mathf.Max(height - 2f * radius, 0f);
            float headCylLen = cylLen * Mathf.Clamp01(headPortion);
            float topLine = (height * 0.5f) - radius;
            float usedRadius = Mathf.Max(radius - headMargin, radius * 0.5f);
            Vector3 up = t.up;

            Vector3 topSphereNow = worldCenterNow + up * topLine;
            Vector3 bottomHeadNow = topSphereNow - up * headCylLen;

            Collider[] nowHits = Physics.OverlapCapsule(
                bottomHeadNow,
                topSphereNow,
                usedRadius,
                headMask,
                QueryTriggerInteraction.Ignore);

            bool currentHeadOverlap = nowHits != null && nowHits.Length > 0;

            Vector3 targetOrigin = rb.position + disp;
            Vector3 worldCenterAtTarget = t.TransformPoint(capsule.center) + (targetOrigin - t.position);

            Vector3 topSphereTarget = worldCenterAtTarget + up * topLine;
            Vector3 bottomHeadTarget = topSphereTarget - up * headCylLen;

            Collider[] targetHits = Physics.OverlapCapsule(
                bottomHeadTarget,
                topSphereTarget,
                usedRadius,
                headMask,
                QueryTriggerInteraction.Ignore);

            bool targetHeadOverlap = targetHits != null && targetHits.Length > 0;

            if (!currentHeadOverlap && targetHeadOverlap)
            {
                lastAttemptedBlocked = true;
                if (debugLogs) Debug.Log("[PlayerMovement] Movement blocked by strict headroom (target head overlap).");
                return;
            }
        }

        // 2) headroom clamp (부분 허용)
        if (capsule != null && headClampIterations > 0 && headPortion > 0f)
        {
            disp = NarrowSpaceUtil.ClampHeadroomHorizontal(
                capsule,
                rb.position,
                disp,
                headMask,
                headClampIterations,
                headPortion,
                headMargin
            );
        }

        // 3) 최종 안전 검사: 목표 위치에서 capsule이 장애물과 겹치는지 검사 (겹치면 step 시도, 없으면 적용)
        if (capsule != null)
        {
            LayerMask obsMask = obstacleMask != 0 ? obstacleMask : movementBlockMask_fallback;
            if (obsMask != 0)
            {
                Transform t = capsule.transform;
                Vector3 targetOrigin = rb.position + disp;
                Vector3 worldCenterAtTarget = t.TransformPoint(capsule.center) + (targetOrigin - t.position);

                float radius = capsule.radius;
                float height = capsule.height;
                float halfLine = Mathf.Max(height * 0.5f - radius, 0f);
                Vector3 up = t.up;

                Vector3 topTarget = worldCenterAtTarget + up * halfLine;
                Vector3 bottomTarget = worldCenterAtTarget - up * halfLine;

                Collider[] hits = Physics.OverlapCapsule(
                    bottomTarget,
                    topTarget,
                    radius,
                    obsMask,
                    QueryTriggerInteraction.Ignore);

                if (hits != null && hits.Length > 0)
                {
                    var myCols = new HashSet<Collider>(GetComponentsInChildren<Collider>());
                    bool foundExternal = false;
                    for (int i = 0; i < hits.Length; ++i)
                    {
                        Collider c = hits[i];
                        if (c == null) continue;
                        if (myCols.Contains(c)) continue;
                        foundExternal = true;
                        break;
                    }

                    if (foundExternal)
                    {
                        // 시도: 자동 스텝(올라타기) 가능한지 검사
                        float foundStep = FindValidStepHeight(targetOrigin, radius, height, t.rotation, out bool canStep);
                        if (canStep && foundStep > EPS)
                        {
                            // 추가 조건: 올라탄 위치에서 머리 공간 및 오버랩 재확인(머리 검사)
                            Vector3 steppedOrigin = targetOrigin + Vector3.up * foundStep;
                            if (!WouldCapsuleOverlap(steppedOrigin, radius, height, t.rotation, obsMask | headMask))
                            {
                                // 스텝 후 아래에 floor가 있는지 확인 (floorMask)
                                if (floorMask != 0)
                                {
                                    Vector3 steppedCenter = t.TransformPoint(capsule.center) + (steppedOrigin - t.position);
                                    Vector3 steppedBottom = steppedCenter - up * halfLine;
                                    RaycastHit floorHit;
                                    if (Physics.Raycast(steppedBottom + up * 0.01f, Vector3.down, out floorHit, floorCheckDepth + 0.01f, floorMask, QueryTriggerInteraction.Ignore))
                                    {
                                        if (floorHit.normal.y >= floorThreshold)
                                        {
                                            // 허용: 스텝을 적용
                                            lastAttemptedStepH = foundStep;
                                            MoveCapsuleDirect(steppedOrigin);
                                            return;
                                        }
                                        else
                                        {
                                            if (debugLogs) Debug.Log($"[PlayerMovement] Step denied: floor normal too shallow {floorHit.normal.y:F3}");
                                        }
                                    }
                                    else
                                    {
                                        if (debugLogs) Debug.Log("[PlayerMovement] Step denied: no floor found under stepped position");
                                    }
                                }
                                else
                                {
                                    // floorMask 비설정: 보수적으로 스텝 차단
                                    if (debugLogs) Debug.Log("[PlayerMovement] Step denied: floorMask not set");
                                }
                            }
                            else
                            {
                                if (debugLogs) Debug.Log("[PlayerMovement] Step denied: overlap after stepping (head/obstacle)");
                            }
                        }

                        // 스텝 불가 또는 실패 -> 차단 (플레이어 콜라이더보다 좁음)
                        lastAttemptedBlocked = true;
                        if (debugLogs) Debug.Log("[PlayerMovement] Movement blocked: obstacle overlap and cannot step.");
                        return;
                    }
                }
            }
        }

        // 최종 이동 적용
        if (disp.sqrMagnitude <= EPS) return;
        MoveCapsuleDirect(rb.position + disp);
    }

    // capsule 이동 적용(공통)
    private void MoveCapsuleDirect(Vector3 newPosition)
    {
        rb.MovePosition(newPosition);
    }

    // MoveFilteredDisplacement: capsule cast + slide (uses obstacleMask for hits)
    public void MoveFilteredDisplacement(Vector3 disp)
    {
        lastAttemptedDisp = disp;
        lastAttemptedBlocked = false;
        lastAttemptedStepH = 0f;

        if (rb == null || disp.sqrMagnitude <= EPS) return;

        LayerMask obsMask = obstacleMask != 0 ? obstacleMask : movementBlockMask_fallback;

        if (disp.sqrMagnitude <= tinyDispThreshold * tinyDispThreshold)
        {
            MovePhysicsDisplacement(disp);
            return;
        }

        if (capsule == null)
        {
            MovePhysicsDisplacement(disp);
            return;
        }

        Vector3 remaining = disp;
        Vector3 totalMove = Vector3.zero;

        int maxIters = Mathf.Max(0, slideIterations) + 1;
        for (int iter = 0; iter < maxIters; ++iter)
        {
            if (remaining.sqrMagnitude <= tinyDispThreshold * tinyDispThreshold)
                break;

            Vector3 origin = rb.position;
            Vector3 dir = remaining.normalized;
            float dist = remaining.magnitude;

            Transform t = capsule.transform;
            Vector3 worldCenterNow = t.TransformPoint(capsule.center) + (origin - t.position);

            float radius = capsule.radius;
            float height = capsule.height;
            float halfLine = Mathf.Max(height * 0.5f - radius, 0f);
            Vector3 up = t.up;
            Vector3 top = worldCenterNow + up * halfLine;
            Vector3 bottom = worldCenterNow - up * halfLine;

            RaycastHit hit;
            bool h = Physics.CapsuleCast(
                bottom,
                top,
                radius,
                dir,
                out hit,
                dist + collisionSkin,
                obsMask,
                QueryTriggerInteraction.Ignore);

            if (!h)
            {
                totalMove += remaining;
                remaining = Vector3.zero;
                break;
            }

            // 바닥 판정: 경사가 완만하면 바닥으로 간주하여 통과
            if (hit.normal.y >= floorThreshold)
            {
                totalMove += remaining;
                remaining = Vector3.zero;
                break;
            }

            // 벽: 허용 거리만큼 전진, 남은 이동은 법선에 수직으로 투영(슬라이드)
            float allowed = Mathf.Max(hit.distance - collisionSkin, 0f);
            Vector3 allowedPart = dir * allowed;
            totalMove += allowedPart;

            float leftover = dist - allowed;
            if (leftover <= tinyDispThreshold)
            {
                remaining = Vector3.zero;
                break;
            }

            Vector3 remainingAfter = remaining - allowedPart;
            Vector3 slide = Vector3.ProjectOnPlane(remainingAfter, hit.normal);

            if (slide.sqrMagnitude <= tinyDispThreshold * tinyDispThreshold)
            {
                remaining = Vector3.zero;
                break;
            }

            remaining = slide;
        }

        if (totalMove.sqrMagnitude > EPS)
        {
            MovePhysicsDisplacement(totalMove);
        }
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
            bool arAllowMove = weaponCtrl != null && weaponCtrl.ARAllowMoveWhileFiring;
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

    // -------- helper: step search & overlap checks --------

    // Binary-search for minimal step height in [0, maxStepHeight] that resolves obstacle overlap.
    // Returns foundHeight in meters and sets out bool canStep.
    private float FindValidStepHeight(Vector3 targetOrigin, float radius, float height, Quaternion rot, out bool canStep)
    {
        canStep = false;
        if (maxStepHeight <= EPS) return 0f;

        float low = 0f;
        float high = maxStepHeight;
        float valid = 0f;
        bool foundAny = false;

        for (int i = 0; i < Mathf.Max(1, stepSearchIterations); ++i)
        {
            float mid = (low + high) * 0.5f;
            Vector3 testOrigin = targetOrigin + Vector3.up * mid;
            if (!WouldCapsuleOverlap(testOrigin, radius, height, rot, obstacleMask | headMask))
            {
                // no overlap at mid -> try lower to find minimal
                high = mid;
                valid = mid;
                foundAny = true;
            }
            else
            {
                // overlap -> need more height
                low = mid;
            }
        }

        if (foundAny)
        {
            canStep = true;
            return valid;
        }

        canStep = false;
        return 0f;
    }

    // Checks if capsule at given origin would overlap any colliders in given mask (excluding self colliders)
    private bool WouldCapsuleOverlap(Vector3 targetOrigin, float radius, float height, Quaternion rot, LayerMask mask)
    {
        if (mask == 0) return false;

        Transform t = capsule.transform;
        Vector3 worldCenterAtTarget = t.TransformPoint(capsule.center) + (targetOrigin - t.position);
        float halfLine = Mathf.Max(height * 0.5f - radius, 0f);
        Vector3 up = t.up;
        Vector3 top = worldCenterAtTarget + up * halfLine;
        Vector3 bottom = worldCenterAtTarget - up * halfLine;

        Collider[] hits = Physics.OverlapCapsule(bottom, top, radius, mask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return false;

        var myCols = new HashSet<Collider>(GetComponentsInChildren<Collider>());
        for (int i = 0; i < hits.Length; ++i)
        {
            if (hits[i] == null) continue;
            if (myCols.Contains(hits[i])) continue;
            return true;
        }

        return false;
    }

    // Public helper APIs (compatibility)
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

    // Knockback (uses MovePhysicsDisplacement)
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

    // Debug gizmos
    void OnDrawGizmosSelected()
    {
        if (!enableGizmos || capsule == null) return;

        Transform t = capsule.transform;
        Vector3 curCenter = t.TransformPoint(capsule.center) + (Application.isPlaying && rb != null ? (rb.position - t.position) : (t.position - t.position));
        float radius = capsule.radius;
        float height = capsule.height;
        Vector3 up = t.up;
        float halfLine = Mathf.Max(height * 0.5f - radius, 0f);

        Vector3 top = curCenter + up * halfLine;
        Vector3 bottom = curCenter - up * halfLine;

        // current capsule
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(top, radius);
        Gizmos.DrawWireSphere(bottom, radius);
        Gizmos.DrawLine(top + (t.right * radius), bottom + (t.right * radius));
        Gizmos.DrawLine(top - (t.right * radius), bottom - (t.right * radius));
        Gizmos.DrawLine(top + (t.forward * radius), bottom + (t.forward * radius));
        Gizmos.DrawLine(top - (t.forward * radius), bottom - (t.forward * radius));

        if (Application.isPlaying)
        {
            Vector3 targetCenter = curCenter + lastAttemptedDisp;
            Vector3 topT = targetCenter + up * halfLine;
            Vector3 bottomT = targetCenter - up * halfLine;

            Gizmos.color = lastAttemptedBlocked ? new Color(1f, 0.3f, 0.3f, 0.9f) : new Color(0.3f, 1f, 0.3f, 0.6f);
            Gizmos.DrawWireSphere(topT, radius);
            Gizmos.DrawWireSphere(bottomT, radius);
            Gizmos.DrawLine(topT + (t.right * radius), bottomT + (t.right * radius));
            Gizmos.DrawLine(topT - (t.right * radius), bottomT - (t.right * radius));
            Gizmos.DrawLine(topT + (t.forward * radius), bottomT + (t.forward * radius));
            Gizmos.DrawLine(topT - (t.forward * radius), bottomT - (t.forward * radius));

            if (lastAttemptedStepH > 0f)
            {
                // show a small line indicating step height
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(targetCenter, targetCenter + Vector3.up * lastAttemptedStepH);
            }
        }
    }

    // Evade/Knockback 중 낙하 보류 제어 외부 재사용 위해 노출
    public void SetSuspendFalling(bool suspend)
    {
        suspendFalling = suspend;
        if (rb == null) return;
        if (suspend)
        {
            rb.useGravity = false;
            Vector3 v = rb.linearVelocity;
            v.y = 0f;
            rb.linearVelocity = v;
        }
        else
        {
            rb.useGravity = true;
        }
    }

    public bool IsSuspendingFalling() => suspendFalling;
}