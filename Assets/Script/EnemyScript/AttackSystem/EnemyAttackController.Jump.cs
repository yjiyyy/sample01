using System.Collections;
using UnityEngine;

public partial class EnemyAttackController
{
    /* Jump (점프 공격) */
    public bool IsJumping { get; private set; } = false;
    private Coroutine jumpPrepareCoroutine;
    private Coroutine jumpCoroutine;
    private GameObject spawnedJumpHitbox;
    private int runningJumpIndex = -1;
    private Transform jumpTarget;

    private bool debugJumpTrajectory = true;

    // 우리가 걸어놓은 prepare/jump/end용 LookLock 추적용 플래그
    private bool weSetPrepareLookLock = false;
    private bool weSetJumpLookLock = false;
    private bool weSetEndLookLock = false;

    // 모바일 권장값(변경 원하면 여기서 조정)
    private const int trajectorySamples = 8;
    private const int visibilitySteps = 8;
    private const float clearance = 0.02f; // 2cm 여유
    private const float landingYMinOffset = -1.0f; // startPos.y + landingYMinOffset (즉 startY - 1.0f)
    private const int maxAdjustIterations = 3;
    private readonly int trajectoryLayerMask = ~0; // 모든 레이어 (원하면 환경 레이어로 제한 권장)

    private void StartJump(JumpAttackData data, Transform target, int index)
    {
        MarkExecuted();
        ClearHold();

        StopJumpCoroutines();
        runningJumpIndex = index;
        jumpTarget = target;

        enemy.SetState(Enemy.EnemyState.Attack);
        if (data.grantSuperArmor) enemy.AddSuperArmor(SuperArmorSource.Attack);
        else enemy.RemoveSuperArmor(SuperArmorSource.Attack);

        // 변경: 준비 구간 동안만 고정(prepare). 기존처럼 전체 시퀀스 고정은 하지 않음.
        // 단, 이미 외부에서 Lock 되어 있으면 덮어쓰지 않고 weSetPrepareLookLock=false 로 둠.
        if (enemy != null && !enemy.IsLookLocked)
        {
            float prepareLockDuration = Mathf.Max(0f, data.prepareDuration) + 0.05f;
            Vector3 lockDir = transform.forward;
            lockDir.y = 0f;
            if (lockDir.sqrMagnitude < 1e-6f) lockDir = Vector3.forward;
            enemy.LockLookDirection(lockDir, prepareLockDuration);
            weSetPrepareLookLock = true;
        }
        else
        {
            weSetPrepareLookLock = false;
        }

        jumpPrepareCoroutine = StartCoroutine(JumpPrepareRoutine(data));
        Log($"JUMP PREPARE START idx={index} prep={data.prepareDuration:F2}");
    }

    private IEnumerator JumpPrepareRoutine(JumpAttackData data)
    {
        if (enemy.animator)
        {
            if (data.prepareClip != null)
            {
                enemy.animator.speed = 1f;
                enemy.animator.Play(data.prepareClip.name, 0, 0f);
            }
            else
            {
                // 폴백: "JumpPrepare"
                SafeSetBool("IsJumpPrepare", true);
                SafeSetBool("IsJump", false);
                enemy.animator.Play("JumpPrepare");
            }
        }

        float elapsed = 0f;
        while (elapsed < data.prepareDuration)
        {
            // 준비 구간에서는 더 이상 매 프레임 플레이어를 바라보지 않습니다.
            if (enemy.CurrentState != Enemy.EnemyState.Attack ||
                enemy.CurrentState == Enemy.EnemyState.ShieldBreak)
            {
                Log("JUMP PREPARE INTERRUPT noCooldown");
                CancelJumpNoCooldown();
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        jumpPrepareCoroutine = null;
        jumpCoroutine = StartCoroutine(JumpAttackRoutine(data));
    }

    private IEnumerator JumpAttackRoutine(JumpAttackData data)
    {
        IsJumping = true;

        // start / target positions (landingPos captured at jump start == prepare 끝 시점의 플레이어 위치)
        Vector3 startPos = transform.position;
        Vector3 landingPos = startPos;
        if (jumpTarget != null)
        {
            landingPos = jumpTarget.position;
        }

        // Components
        CapsuleCollider cap = GetComponent<CapsuleCollider>();
        Rigidbody rb = GetComponent<Rigidbody>();
        float capScaleY = 1f;
        if (cap != null) capScaleY = cap.transform.lossyScale.y;

        // --- SAMPLE playerGroundY (플레이어 XZ 아래 지면 높이), 플레이어 콜라이더는 무시 ---
        float playerGroundY = transform.position.y;
        Collider[] playerColliders = null;
        if (jumpTarget != null)
        {
            float rayStartOffset = 2.0f;
            float maxRay = 20f;
            Vector3 p = jumpTarget.position;
            Vector3 rayStart = new Vector3(p.x, p.y + rayStartOffset, p.z);

            // 플레이어(및 자식)의 콜라이더 수집
            playerColliders = jumpTarget.GetComponentsInChildren<Collider>(true);

            // 모든 히트 수집 (트리거 무시)
            RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, rayStartOffset + maxRay, trajectoryLayerMask, QueryTriggerInteraction.Ignore);

            // 높이 기준(descending) 정렬: 가장 위 표면부터 검사
            System.Array.Sort(hits, (a, b) => b.point.y.CompareTo(a.point.y));

            bool found = false;
            float maxSlopeAngle = 75f; // 가파른 표면(벽 등) 제외용, 필요시 조절
            foreach (var h in hits)
            {
                if (h.collider == null) continue;

                // 플레이어 자신의 콜라이더는 무시
                bool hitPlayer = false;
                if (playerColliders != null)
                {
                    foreach (var pc in playerColliders)
                    {
                        if (pc == null) continue;
                        if (pc == h.collider || h.collider.transform.IsChildOf(pc.transform))
                        {
                            hitPlayer = true;
                            break;
                        }
                    }
                }
                if (hitPlayer) continue;

                // 너무 가파른 표면(거의 벽)은 제외
                if (Vector3.Angle(h.normal, Vector3.up) > maxSlopeAngle) continue;

                // 유효한 지면으로 판단
                playerGroundY = h.point.y;
                found = true;
                break;
            }

            if (!found)
            {
                // 폴백 A: ray 실패 시 플레이어의 위치 Y 사용
                playerGroundY = jumpTarget.position.y;
            }
        }

        // According to request: use ONLY playerGroundY as the landing transform Y.
        float landingTransformY = playerGroundY;

        // 먼저: 플레이어가 벽 뒤에 숨었는지 간단 검사 (라인캐스트).
        // 통과 불가하면 landingPos.xz를 단계적으로 당겨서 가능한 지점 찾음.
        Vector3 startXZ = new Vector3(startPos.x, 0f, startPos.z);
        Vector3 landingXZ = new Vector3(landingPos.x, 0f, landingPos.z);
        bool foundVisible = false;
        // Use a small height for visibility test
        Vector3 visFrom = startPos + Vector3.up * 0.5f;
        Vector3 visTo = landingPos + Vector3.up * 0.5f;

        // function to test line between two points while ignoring player/self colliders
        System.Func<Vector3, Vector3, bool> IsLineBlocked = (from, to) =>
        {
            RaycastHit[] rHits = Physics.RaycastAll(from, (to - from).normalized, Vector3.Distance(from, to), trajectoryLayerMask, QueryTriggerInteraction.Ignore);
            if (rHits == null || rHits.Length == 0) return false;
            foreach (var rh in rHits)
            {
                if (rh.collider == null) continue;
                // ignore player's colliders
                if (playerColliders != null)
                {
                    bool isPlayer = false;
                    foreach (var pc in playerColliders)
                    {
                        if (pc == null) continue;
                        if (pc == rh.collider || rh.collider.transform.IsChildOf(pc.transform))
                        {
                            isPlayer = true;
                            break;
                        }
                    }
                    if (isPlayer) continue;
                }
                // ignore self colliders
                Collider[] selfCols = GetComponentsInChildren<Collider>(true);
                bool isSelf = false;
                foreach (var sc in selfCols)
                {
                    if (sc == null) continue;
                    if (sc == rh.collider || rh.collider.transform.IsChildOf(sc.transform))
                    {
                        isSelf = true;
                        break;
                    }
                }
                if (isSelf) continue;

                // otherwise blocked by something else
                return true;
            }
            return false;
        };

        // Initial direct visibility check
        if (!IsLineBlocked(visFrom, visTo))
        {
            foundVisible = true;
        }
        else
        {
            // step toward startPos to find first reachable point
            for (int s = 1; s <= visibilitySteps; s++)
            {
                float t = 1f - (s / (float)visibilitySteps); // interpolation from landing->start
                Vector3 candidateXZ = Vector3.Lerp(landingXZ, startXZ, t);
                Vector3 candidateWorld = new Vector3(candidateXZ.x, landingPos.y, candidateXZ.z);
                if (!IsLineBlocked(visFrom, candidateWorld + Vector3.up * 0.5f))
                {
                    landingPos.x = candidateXZ.x;
                    landingPos.z = candidateXZ.z;
                    foundVisible = true;
                    if (debugJumpTrajectory) Debug.Log($"[JumpVisibility] adjusted landing XZ to {landingPos.x:F2},{landingPos.z:F2} (step {s})");
                    break;
                }
            }
        }

        bool usedInPlaceJump = false;

        if (!foundVisible)
        {
            // cannot reach landing XZ (player hidden) -> do in-place jump instead of cancel
            if (debugJumpTrajectory) Debug.Log("[JumpVisibility] landing XZ blocked - doing in-place jump");

            // set landing XZ to start (in-place)
            landingPos.x = startPos.x;
            landingPos.z = startPos.z;

            // sample ground under startPos
            float inPlaceGroundY = startPos.y;
            {
                float rayStartOffset = 2.0f;
                float maxRay = 20f;
                Vector3 rayStart = startPos + Vector3.up * rayStartOffset;
                RaycastHit gr;
                if (Physics.Raycast(rayStart, Vector3.down, out gr, rayStartOffset + maxRay, trajectoryLayerMask, QueryTriggerInteraction.Ignore))
                {
                    // ignore self collider check isn't necessary here in most cases, but keep safe
                    Collider[] selfCols = GetComponentsInChildren<Collider>(true);
                    bool isSelf = false;
                    foreach (var sc in selfCols)
                    {
                        if (sc == null) continue;
                        if (sc == gr.collider || gr.collider.transform.IsChildOf(sc.transform))
                        {
                            isSelf = true;
                            break;
                        }
                    }
                    if (!isSelf)
                        inPlaceGroundY = gr.point.y;
                }
            }

            landingTransformY = inPlaceGroundY;
            usedInPlaceJump = true;
        }

        // Now perform trajectory collision checks and adjust landingTransformY downward if needed.
        // Iterate a few times because arcH depends on landingTransformY.
        bool cancelled = false;
        for (int iter = 0; iter < maxAdjustIterations; iter++)
        {
            // vertical difference and arc height according to requested rule:
            float verticalDiff = landingTransformY - startPos.y;
            float arcH = data.height;
            if (verticalDiff > 0f) arcH += verticalDiff / 3f;

            float minAllowedLandingY = startPos.y + landingYMinOffset; // startY - 1.0f

            float newLandingY = landingTransformY; // candidate (we will lower it if needed)

            // sample points along the parabola
            for (int i = 1; i < trajectorySamples; i++) // skip i=0 (start)
            {
                float u = i / (float)(trajectorySamples - 1);
                Vector3 posXZ = Vector3.Lerp(startXZ, new Vector3(landingPos.x, 0f, landingPos.z), u);
                float baseY = Mathf.Lerp(startPos.y, newLandingY, u);
                float arcTerm = 4f * arcH * u * (1f - u);
                float sampleY = baseY + arcTerm;

                // cast a ray from above down to find any obstacle top at that XZ
                float rayStartY = Mathf.Max(startPos.y, newLandingY) + arcH + 2f;
                Vector3 rayStart = new Vector3(posXZ.x, rayStartY, posXZ.z);
                RaycastHit rh;
                if (Physics.Raycast(rayStart, Vector3.down, out rh, rayStartY + 10f, trajectoryLayerMask, QueryTriggerInteraction.Ignore))
                {
                    if (rh.collider != null)
                    {
                        // ignore player's colliders and self colliders
                        bool isPlayer = false;
                        if (playerColliders != null)
                        {
                            foreach (var pc in playerColliders)
                            {
                                if (pc == null) continue;
                                if (pc == rh.collider || rh.collider.transform.IsChildOf(pc.transform))
                                {
                                    isPlayer = true;
                                    break;
                                }
                            }
                        }
                        if (isPlayer) continue;

                        Collider[] selfCols = GetComponentsInChildren<Collider>(true);
                        bool isSelf = false;
                        foreach (var sc in selfCols)
                        {
                            if (sc == null) continue;
                            if (sc == rh.collider || rh.collider.transform.IsChildOf(sc.transform))
                            {
                                isSelf = true;
                                break;
                            }
                        }
                        if (isSelf) continue;

                        // if obstacle top is above the sample point (with clearance), we need to lower landingTransformY
                        if (rh.point.y > sampleY + clearance)
                        {
                            // solve for landingTransformY so sampleY <= rh.point.y - clearance
                            // sampleY = startY*(1-u) + landingY*u + arcTerm
                            // landingY <= (rhY - clearance - startY*(1-u) - arcTerm) / u
                            if (u > 0f)
                            {
                                float candidateLandingY = (rh.point.y - clearance - startPos.y * (1f - u) - arcTerm) / u;
                                if (candidateLandingY < newLandingY)
                                {
                                    if (debugJumpTrajectory)
                                        Debug.Log($"[JumpAdjust] sample u={u:F2} rhY={rh.point.y:F3} arcTerm={arcTerm:F3} candidateLandingY={candidateLandingY:F3}");
                                    newLandingY = candidateLandingY;
                                }
                            }
                        }
                    }
                }
            } // end sample loop

            // clamp or cancel if newLandingY too low
            if (newLandingY < minAllowedLandingY)
            {
                // originally would cancel; now fallback to in-place jump if not already using it
                if (!usedInPlaceJump)
                {
                    if (debugJumpTrajectory) Debug.Log($"[JumpAdjust] landingY {newLandingY:F3} below allowed min {minAllowedLandingY:F3} -> switching to in-place jump");

                    // in-place: set landing XZ to start, sample ground under start
                    landingPos.x = startPos.x;
                    landingPos.z = startPos.z;

                    float inPlaceGroundY = startPos.y;
                    {
                        float rayStartOffset = 2.0f;
                        float maxRay = 20f;
                        Vector3 rayStart = startPos + Vector3.up * rayStartOffset;
                        RaycastHit gr;
                        if (Physics.Raycast(rayStart, Vector3.down, out gr, rayStartOffset + maxRay, trajectoryLayerMask, QueryTriggerInteraction.Ignore))
                        {
                            Collider[] selfCols = GetComponentsInChildren<Collider>(true);
                            bool isSelf = false;
                            foreach (var sc in selfCols)
                            {
                                if (sc == null) continue;
                                if (sc == gr.collider || gr.collider.transform.IsChildOf(sc.transform))
                                {
                                    isSelf = true;
                                    break;
                                }
                            }
                            if (!isSelf)
                                inPlaceGroundY = gr.point.y;
                        }
                    }

                    landingTransformY = inPlaceGroundY;
                    usedInPlaceJump = true;

                    // continue outer iter loop to recompute arc with new landingTransformY
                    continue;
                }
                else
                {
                    // already using in-place but still invalid -> cancel as last resort
                    if (debugJumpTrajectory) Debug.Log($"[JumpAdjust] in-place landingY still below min -> cancel");
                    cancelled = true;
                    break;
                }
            }

            // if nothing changed, break
            if (newLandingY >= landingTransformY - 1e-5f) break;

            // otherwise update and loop again (recompute arcH in next iter)
            landingTransformY = newLandingY;
        } // end iter adjustments

        if (cancelled)
        {
            // final resort cancel
            CancelJumpNoCooldown();
            yield break;
        }

        // Play loop/jump animation (after landingY adjusted)
        if (enemy.animator)
        {
            enemy.animator.speed = 1f;
            if (data.loopClip != null)
                enemy.animator.Play(data.loopClip.name, 0, 0f);
            else if (!string.IsNullOrEmpty(data.attackName))
                enemy.animator.Play(data.attackName, 0, 0f);
            else
                enemy.animator.Play("JumpLoop", 0, 0f);
        }

        // Recompute final arc parameters after any adjustments
        float finalTTotal = Mathf.Max(0.0001f, data.duration);
        float finalVerticalDiff = landingTransformY - startPos.y;
        float finalArcH = data.height;
        if (finalVerticalDiff > 0f) finalArcH += finalVerticalDiff / 3f;

        // If we did in-place jump, shorten arc and duration for a snappier small hop
        if (usedInPlaceJump)
        {
            finalArcH = Mathf.Max(0.3f, finalArcH * 0.5f);
            finalTTotal = Mathf.Max(0.18f, finalTTotal * 0.6f);
        }

        // Trajectory loop variables
        float tTotal = finalTTotal;
        float flightElapsed = 0f;
        Vector3 simulatedPos = transform.position;

        if (debugJumpTrajectory)
        {
            Debug.Log($"[JumpFinal] startY={startPos.y:F3} landingY={landingTransformY:F3} arcH={finalArcH:F3} duration={tTotal:F3} inPlace={usedInPlaceJump}");
        }

        // --- 준비용 Lock을 우리가 걸었다면 해제해서 '점프 시작 시' 회전 스냅이 가능하게 만듦 ---
        if (weSetPrepareLookLock && enemy != null)
        {
            enemy.UnlockLookDirection();
            weSetPrepareLookLock = false;
        }

        // 준비 끝(=점프 시작) 시, 착지 지점(landingPos)의 XZ 방향으로 1회 스냅 회전
        Quaternion desiredRotation = transform.rotation;
        bool shouldApplyJumpRotation = false;
        Vector3 jumpLockDir = Vector3.zero;
        if (enemy != null)
        {
            Vector3 dirToLanding = new Vector3(landingPos.x - transform.position.x, 0f, landingPos.z - transform.position.z);
            if (dirToLanding.sqrMagnitude > 0.0001f)
            {
                jumpLockDir = dirToLanding.normalized;
                desiredRotation = Quaternion.LookRotation(jumpLockDir, Vector3.up);
                shouldApplyJumpRotation = true;
            }
        }

        bool rotationApplied = false;

        // Precompute capsule params needed during flight (for overlap push-up later)
        float capRadiusWorld = 0f;
        float capHalfHeightWorld = 0f;
        if (cap != null)
        {
            capRadiusWorld = cap.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
            capHalfHeightWorld = (cap.height * 0.5f) * capScaleY;
        }

        // Use FixedUpdate sync for physics-consistent movement
        while (flightElapsed < tTotal)
        {
            if (enemy.CurrentState != Enemy.EnemyState.Attack ||
                enemy.CurrentState == Enemy.EnemyState.ShieldBreak)
            {
                Log("JUMP ATTACK INTERRUPT");
                CancelJumpNoCooldown();
                yield break;
            }

            // FixedUpdate 동기
            yield return new WaitForFixedUpdate();
            float dt = Time.fixedDeltaTime;
            flightElapsed += dt;
            float u = Mathf.Clamp01(flightElapsed / tTotal);

            // First FixedUpdate: apply the snap rotation once (rb.MoveRotation if possible)
            if (!rotationApplied && shouldApplyJumpRotation)
            {
                if (rb != null)
                    rb.MoveRotation(desiredRotation);
                else
                    transform.rotation = desiredRotation;

                rotationApplied = true;

                // 추가: 스냅된 방향이 다른 시스템에 의해 덮어써지지 않도록 jump용 Lock 설정
                // (lock 기간은 비행 시간 tTotal + 여유 0.05s)
                if (enemy != null && !enemy.IsLookLocked)
                {
                    float jumpLockDuration = tTotal + 0.05f;
                    enemy.LockLookDirection(jumpLockDir, jumpLockDuration);
                    weSetJumpLookLock = true;
                }
                else
                {
                    weSetJumpLookLock = false;
                }
            }

            // horizontal (XZ) linear interpolation (use adjusted landingPos.x,z)
            Vector3 targetXZ = new Vector3(landingPos.x, 0f, landingPos.z);
            Vector3 posXZ = Vector3.Lerp(startXZ, targetXZ, u);

            // vertical (Y): base lerp plus arc term (peak at u=0.5)
            float baseY = Mathf.Lerp(startPos.y, landingTransformY, u);
            float arcTerm = 4f * finalArcH * u * (1f - u); // parabolic arc
            float posY = baseY + arcTerm;

            Vector3 nextPos = new Vector3(posXZ.x, posY, posXZ.z);

            // Move using Rigidbody.MovePosition if we have a Rigidbody (FixedUpdate context),
            // otherwise fallback to enemy.MoveFilteredDisplacement or transform.position.
            if (rb != null)
            {
                rb.MovePosition(nextPos);
                simulatedPos = nextPos;
            }
            else
            {
                try
                {
                    Vector3 disp = nextPos - simulatedPos;
                    enemy.MoveFilteredDisplacement(disp);
                    simulatedPos += disp;
                }
                catch
                {
                    transform.position = nextPos;
                    simulatedPos = nextPos;
                }
            }
        }

        // Final snap to landing position (exact playerGroundY at player's XZ)
        Vector3 desiredPos = new Vector3(landingPos.x, landingTransformY, landingPos.z);

        if (rb != null)
        {
            // zero velocity and snap via MovePosition in Fixed context
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.MovePosition(desiredPos);
        }
        else
        {
            try
            {
                Vector3 finalDisp = desiredPos - simulatedPos;
                enemy.MoveFilteredDisplacement(finalDisp);
            }
            catch
            {
                transform.position = desiredPos;
            }
        }

        // 착지 직후: jump용 잠금이 있으면 해제하고(end 구간용 잠금은 별도로 설정)
        if (weSetJumpLookLock && enemy != null)
        {
            enemy.UnlockLookDirection();
            weSetJumpLookLock = false;
        }

        // 착지 직후: end 구간 동안 방향 고정 (단, 이미 외부에서 Lock 되어 있으면 건드리지 않음)
        if (enemy != null && !enemy.IsLookLocked)
        {
            Vector3 endLockDir = transform.forward;
            endLockDir.y = 0f;
            if (endLockDir.sqrMagnitude < 1e-6f) endLockDir = Vector3.forward;
            float endLockDuration = Mathf.Max(0f, data.endDuration) + 0.05f;
            enemy.LockLookDirection(endLockDir, endLockDuration);
            weSetEndLookLock = true;
        }
        else
        {
            weSetEndLookLock = false;
        }

        // OverlapCapsule check & small lift to avoid penetration (if capsule exists)
        if (cap != null)
        {
            int maxIter = 8;
            float pushUpStep = 0.02f; // 2cm
            for (int iter = 0; iter < maxIter; iter++)
            {
                Vector3 capCenterWorld = transform.TransformPoint(cap.center);
                Vector3 bottom = capCenterWorld - Vector3.up * capHalfHeightWorld;
                Vector3 top = capCenterWorld + Vector3.up * capHalfHeightWorld;
                Collider[] hits = Physics.OverlapCapsule(bottom, top, capRadiusWorld, trajectoryLayerMask, QueryTriggerInteraction.Ignore);
                // ignore self-colliders
                bool overlapping = false;
                if (hits != null && hits.Length > 0)
                {
                    foreach (var c in hits)
                    {
                        if (c == null) continue;
                        if (c.transform.IsChildOf(transform)) continue;
                        overlapping = true;
                        break;
                    }
                }
                if (!overlapping) break;

                // nudge up
                desiredPos.y += pushUpStep;
                if (rb != null) rb.MovePosition(desiredPos);
                else transform.position = desiredPos;
            }
        }

        // END: play end animation & spawn hitbox at landing position
        if (enemy.animator)
        {
            enemy.animator.speed = 1f;
            if (data.endClip != null)
                enemy.animator.Play(data.endClip.name, 0, 0f);
            else
                enemy.animator.Play("JumpEnd", 0, 0f);
        }

        // Camera shake
        if (data.cameraShake != null)
        {
            var shaker = CameraShakePlayer.Instance;
            if (shaker != null)
                shaker.PlayShake(data.cameraShake, 1f);
        }

        // Spawn hitbox at landing location
        SpawnJumpHitboxAtPosition(data, desiredPos);

        // Wait for endDuration (allow animation to play)
        float waited = 0f;
        while (waited < data.endDuration)
        {
            if (enemy.CurrentState != Enemy.EnemyState.Attack ||
                enemy.CurrentState == Enemy.EnemyState.ShieldBreak)
            {
                break;
            }
            yield return null;
            waited += Time.deltaTime;
        }

        DespawnJumpHitbox();

        // finalize: 우리가 걸어놓은 end lock은 자동 만료되도록 두고,
        // 인터럽트 경로에서 해제하도록 처리함.

        if (enemy.animator && !IsHardCrowdControlled())
        {
            SafeSetBool("IsJump", false);
            SafeSetBool("IsJumpPrepare", false);
        }

        IsJumping = false;
        runningJumpIndex = -1;
        jumpCoroutine = null;

        enemy.RemoveSuperArmor(SuperArmorSource.Attack);

        if (enemy.CurrentState == Enemy.EnemyState.Attack && !IsHardCrowdControlled())
            enemy.SetState(Enemy.EnemyState.Chase);
    }

    // Helper that spawns the hitbox at a specific world position (landing)
    private void SpawnJumpHitboxAtPosition(JumpAttackData data, Vector3 worldPos)
    {
        if (data.hitBoxPrefab == null) return;
        if (spawnedJumpHitbox != null) return;

        if (data.attachHitboxToEnemy)
        {
            // attach to enemy, but still place at landing world pos (localization)
            spawnedJumpHitbox = Instantiate(data.hitBoxPrefab, worldPos, transform.rotation, transform);
        }
        else
        {
            spawnedJumpHitbox = Instantiate(data.hitBoxPrefab, worldPos, Quaternion.identity);
        }

        if (spawnedJumpHitbox.TryGetComponent<HitBox_Enemy>(out var hb))
        {
            float life = data.hitBoxLifetime > 0f ? data.hitBoxLifetime : data.endDuration;
            hb.Initialize(
                data.damage,
                data.range,
                data.knockbackPower,
                data.knockbackDuration,
                life,
                data.stunDuration,
                data.allowDuplicateHit,
                data.duplicateHitInterval,
                null
            );
        }
    }

    private void SpawnJumpHitbox(JumpAttackData data)
    {
        // kept for backward compatibility in case other code expects this name
        // it will simply spawn at current transform position and follow attachHitboxToEnemy flag
        if (data == null || data.hitBoxPrefab == null) return;
        if (spawnedJumpHitbox != null) return;

        Transform parent = data.attachHitboxToEnemy ? transform : null;
        Vector3 spawnPos = transform.position;
        Quaternion spawnRot = transform.rotation;

        spawnedJumpHitbox = parent != null
            ? Instantiate(data.hitBoxPrefab, spawnPos, spawnRot, parent)
            : Instantiate(data.hitBoxPrefab, spawnPos, spawnRot);

        if (spawnedJumpHitbox.TryGetComponent<HitBox_Enemy>(out var hb))
        {
            float life = data.hitBoxLifetime > 0f ? data.hitBoxLifetime : data.endDuration;
            hb.Initialize(
                data.damage,
                data.range,
                data.knockbackPower,
                data.knockbackDuration,
                life,
                data.stunDuration,
                data.allowDuplicateHit,
                data.duplicateHitInterval,
                null
            );
        }
    }

    private void DespawnJumpHitbox()
    {
        if (spawnedJumpHitbox != null)
            Destroy(spawnedJumpHitbox);
        spawnedJumpHitbox = null;
    }

    private void CancelJumpNoCooldown()
    {
        // 인터럽트/취소 경로: 우리가 걸어놓은 잠금이 있으면 해제
        if (weSetPrepareLookLock && enemy != null)
        {
            enemy.UnlockLookDirection();
            weSetPrepareLookLock = false;
        }
        if (weSetJumpLookLock && enemy != null)
        {
            enemy.UnlockLookDirection();
            weSetJumpLookLock = false;
        }
        if (weSetEndLookLock && enemy != null)
        {
            enemy.UnlockLookDirection();
            weSetEndLookLock = false;
        }

        enemy.RemoveSuperArmor(SuperArmorSource.Attack);
        if (enemy.animator && !IsHardCrowdControlled())
        {
            SafeSetBool("IsJump", false);
            SafeSetBool("IsJumpPrepare", false);
        }
        DespawnJumpHitbox();
        runningJumpIndex = -1;

        if (enemy.CurrentState == Enemy.EnemyState.Attack && !IsHardCrowdControlled())
            enemy.SetState(Enemy.EnemyState.Chase);
    }

    private void StopJumpCoroutines()
    {
        if (jumpPrepareCoroutine != null) StopCoroutine(jumpPrepareCoroutine);
        if (jumpCoroutine != null) StopCoroutine(jumpCoroutine);
        jumpPrepareCoroutine = null;
        jumpCoroutine = null;
    }

    private void InterruptJumpIfNeeded()
    {
        if (jumpPrepareCoroutine != null || IsJumping)
        {
            Log("INTERRUPT jump -> cancel");

            // 인터럽트시 우리가 걸어놓은 잠금 해제
            if (weSetPrepareLookLock && enemy != null)
            {
                enemy.UnlockLookDirection();
                weSetPrepareLookLock = false;
            }
            if (weSetJumpLookLock && enemy != null)
            {
                enemy.UnlockLookDirection();
                weSetJumpLookLock = false;
            }
            if (weSetEndLookLock && enemy != null)
            {
                enemy.UnlockLookDirection();
                weSetEndLookLock = false;
            }

            StopJumpCoroutines();
            IsJumping = false;
            CancelJumpNoCooldown();
        }
    }
}