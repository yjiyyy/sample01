using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 배경(Ground/Wall/Prop)과의 캡슐 이동·슬라이드·스텝 해결.
/// 캐릭터끼리 충돌은 포함하지 않음.
/// </summary>
public static class MovementCollisionSolver
{
    private const float Eps = 0.0001f;

    public struct Result
    {
        public bool moved;
        public bool blocked;
        public Vector3 finalPosition;
        public float stepHeight;
    }

    public static Result Solve(
        Rigidbody rb,
        CapsuleCollider capsule,
        Vector3 displacement,
        MovementSettings settings,
        Collider[] overlapBuffer,
        HashSet<int> selfColliderIds)
    {
        var fail = new Result
        {
            moved = false,
            blocked = true,
            finalPosition = rb != null ? rb.position : Vector3.zero,
            stepHeight = 0f
        };

        if (rb == null || settings == null)
            return fail;

        if (displacement.sqrMagnitude <= Eps)
        {
            return new Result
            {
                moved = false,
                blocked = false,
                finalPosition = rb.position,
                stepHeight = 0f
            };
        }

        if (capsule == null)
        {
            return new Result
            {
                moved = true,
                blocked = false,
                finalPosition = rb.position + displacement,
                stepHeight = 0f
            };
        }

        bool horizontalIntent = IsHorizontalIntent(displacement, settings);
        Vector3 workingDisp = displacement;

        if (horizontalIntent && settings.enableHeadroomClamp && settings.SolidMask != 0)
        {
            workingDisp = StepChecker.ClampHeadroomHorizontal(
                capsule,
                rb.position,
                workingDisp,
                settings.SolidMask,
                settings.headroomSearchIterations,
                settings.headPortion,
                settings.collisionSkin,
                overlapBuffer,
                selfColliderIds);

            if (workingDisp.sqrMagnitude <= Eps)
            {
                return new Result
                {
                    moved = false,
                    blocked = false,
                    finalPosition = rb.position,
                    stepHeight = 0f
                };
            }
        }

        Vector3 slideDisp = ComputeSlideDisplacement(
            rb.position,
            workingDisp,
            horizontalIntent,
            capsule,
            settings,
            overlapBuffer,
            selfColliderIds);

        return TryResolvePosition(
            rb.position,
            slideDisp,
            horizontalIntent,
            capsule,
            settings,
            overlapBuffer,
            selfColliderIds);
    }

    public static Vector3 ComputeSlideDisplacement(
        Vector3 originPosition,
        Vector3 displacement,
        CapsuleCollider capsule,
        MovementSettings settings,
        Collider[] overlapBuffer,
        HashSet<int> selfColliderIds)
    {
        bool horizontalIntent = IsHorizontalIntent(displacement, settings);
        return ComputeSlideDisplacement(
            originPosition,
            displacement,
            horizontalIntent,
            capsule,
            settings,
            overlapBuffer,
            selfColliderIds);
    }

    public static Vector3 ComputeSlideDisplacement(
        Vector3 originPosition,
        Vector3 displacement,
        bool horizontalIntent,
        CapsuleCollider capsule,
        MovementSettings settings,
        Collider[] overlapBuffer,
        HashSet<int> selfColliderIds)
    {
        if (capsule == null || settings == null || displacement.sqrMagnitude <= Eps)
            return displacement;

        LayerMask castMask = settings.SolidMask;
        if (castMask == 0)
            return displacement;

        float tiny = settings.tinyMoveThreshold;
        if (displacement.sqrMagnitude <= tiny * tiny)
            return displacement;

        Vector3 remaining = displacement;
        Vector3 totalMove = Vector3.zero;
        // slideIterations=0 → 루프 2회(접촉+미끄러짐 1회). 대각선 슬라이드는 2 이상 권장.
        int maxIters = Mathf.Clamp(settings.slideIterations, 0, 4) + 2;

        for (int iter = 0; iter < maxIters; iter++)
        {
            if (remaining.sqrMagnitude <= tiny * tiny)
                break;

            GetCapsulePoints(capsule, originPosition + totalMove, out Vector3 bottom, out Vector3 top, out float radius);

            Vector3 dir = remaining.normalized;
            float dist = remaining.magnitude;

            bool hitSomething = Physics.CapsuleCast(
                bottom,
                top,
                radius,
                dir,
                out RaycastHit hit,
                dist + settings.collisionSkin,
                castMask,
                QueryTriggerInteraction.Ignore);

            if (!hitSomething)
            {
                totalMove += remaining;
                break;
            }

            float allowed = Mathf.Max(hit.distance - settings.collisionSkin, 0f);
            totalMove += dir * allowed;

            float leftover = dist - allowed;
            if (leftover <= tiny)
                break;

            Vector3 leftoverMove = dir * leftover;

            if (hit.normal.y >= settings.floorSlopeThreshold)
            {
                // 완만한 바닥/경사: 남은 이동을 경사면에 투영해 미끄러짐
                remaining = Vector3.ProjectOnPlane(leftoverMove, hit.normal);
            }
            else
            {
                if (TryStepMove(
                        originPosition,
                        displacement,
                        capsule,
                        settings,
                        overlapBuffer,
                        selfColliderIds,
                        hit,
                        dir,
                        out Vector3 steppedPosition,
                        out _))
                {
                    return steppedPosition - originPosition;
                }

                // 벽·급경사·천장: 벽면을 따라 미끄러짐
                remaining = Vector3.ProjectOnPlane(leftoverMove, hit.normal);
            }

            if (horizontalIntent)
                remaining.y = Mathf.Max(remaining.y, 0f);

            if (remaining.sqrMagnitude <= tiny * tiny)
                break;
        }

        if (horizontalIntent && totalMove.y < 0f)
            totalMove.y = 0f;

        return totalMove;
    }

    public static Result TryResolvePosition(
        Vector3 originPosition,
        Vector3 displacement,
        CapsuleCollider capsule,
        MovementSettings settings,
        Collider[] overlapBuffer,
        HashSet<int> selfColliderIds)
    {
        bool horizontalIntent = IsHorizontalIntent(displacement, settings);
        return TryResolvePosition(
            originPosition,
            displacement,
            horizontalIntent,
            capsule,
            settings,
            overlapBuffer,
            selfColliderIds);
    }

    public static Result TryResolvePosition(
        Vector3 originPosition,
        Vector3 displacement,
        bool horizontalIntent,
        CapsuleCollider capsule,
        MovementSettings settings,
        Collider[] overlapBuffer,
        HashSet<int> selfColliderIds)
    {
        var blocked = new Result
        {
            moved = false,
            blocked = true,
            finalPosition = originPosition,
            stepHeight = 0f
        };

        if (settings == null || displacement.sqrMagnitude <= Eps)
        {
            return new Result
            {
                moved = false,
                blocked = false,
                finalPosition = originPosition,
                stepHeight = 0f
            };
        }

        Vector3 target = originPosition + displacement;

        if (capsule == null)
        {
            return new Result
            {
                moved = true,
                blocked = false,
                finalPosition = target,
                stepHeight = 0f
            };
        }

        if (horizontalIntent && target.y < originPosition.y - settings.tinyMoveThreshold)
            target.y = originPosition.y;

        LayerMask blockMask = settings.blockMask;
        if (blockMask != 0 && StepChecker.WouldCapsuleOverlap(capsule, target, blockMask, overlapBuffer, selfColliderIds))
        {
            if (TryStepUp(originPosition, displacement, capsule, settings, overlapBuffer, selfColliderIds, out Vector3 stepped, out float stepH))
            {
                return new Result
                {
                    moved = true,
                    blocked = false,
                    finalPosition = stepped,
                    stepHeight = stepH
                };
            }

            return blocked;
        }

        if (settings.SolidMask != 0 && HasHeadSolidOverlap(capsule, target, settings, overlapBuffer, selfColliderIds))
            return blocked;

        if (horizontalIntent &&
            HasGroundSupport(capsule, originPosition, settings) &&
            !HasGroundSupport(capsule, target, settings))
        {
            return blocked;
        }

        return new Result
        {
            moved = true,
            blocked = false,
            finalPosition = target,
            stepHeight = 0f
        };
    }

    private static bool IsHorizontalIntent(Vector3 displacement, MovementSettings settings)
    {
        float tiny = settings != null ? settings.tinyMoveThreshold : Eps;
        return Mathf.Abs(displacement.y) <= tiny;
    }

    private static bool HasHeadSolidOverlap(
        CapsuleCollider capsule,
        Vector3 bodyPosition,
        MovementSettings settings,
        Collider[] overlapBuffer,
        HashSet<int> selfColliderIds)
    {
        Transform t = capsule.transform;
        Vector3 worldCenter = t.TransformPoint(capsule.center) + (bodyPosition - t.position);
        Vector3 up = t.up;
        float radius = capsule.radius;
        float height = capsule.height;
        float cylLen = Mathf.Max(height - 2f * radius, 0f);
        float headCylLen = cylLen * Mathf.Clamp01(settings.headPortion);
        float topLine = (height * 0.5f) - radius;
        float usedRadius = Mathf.Max(radius - settings.collisionSkin, radius * 0.5f);

        Vector3 topSphere = worldCenter + up * topLine;
        Vector3 bottomHead = topSphere - up * headCylLen;

        return NarrowSpaceUtil.CheckHeadOverlap(
            topSphere,
            bottomHead,
            usedRadius,
            settings.SolidMask,
            overlapBuffer,
            selfColliderIds);
    }

    public static bool HasGroundSupport(CapsuleCollider capsule, Vector3 bodyPosition, MovementSettings settings)
    {
        if (capsule == null || settings == null || settings.groundMask == 0)
            return false;

        GetCapsulePoints(capsule, bodyPosition, out Vector3 bottom, out _, out _);
        Vector3 up = capsule.transform.up;
        return Physics.Raycast(
            bottom + up * 0.01f,
            Vector3.down,
            out RaycastHit hit,
            settings.floorCheckDepth + 0.05f,
            settings.groundMask,
            QueryTriggerInteraction.Ignore) && hit.normal.y >= settings.floorSlopeThreshold;
    }

    private static bool TryStepMove(
        Vector3 originPosition,
        Vector3 fullDisplacement,
        CapsuleCollider capsule,
        MovementSettings settings,
        Collider[] overlapBuffer,
        HashSet<int> selfColliderIds,
        RaycastHit hit,
        Vector3 dir,
        out Vector3 steppedPosition,
        out float stepHeight)
    {
        steppedPosition = originPosition;
        stepHeight = 0f;

        if (settings.maxStepHeight <= Eps)
            return false;

        Vector3 probeOrigin = hit.point - dir * 0.02f + Vector3.up * 0.02f;
        Vector3 probeCandidate = originPosition + dir * Mathf.Max(settings.minStepProbeDistance, 0.02f);

        if (!TryFindStepOrigin(capsule, probeOrigin, settings, overlapBuffer, selfColliderIds, out float foundStep) &&
            !TryFindStepOrigin(capsule, probeCandidate, settings, overlapBuffer, selfColliderIds, out foundStep))
        {
            return false;
        }

        Vector3 target = originPosition + fullDisplacement;
        Vector3 stepped = target + Vector3.up * foundStep;
        if (!IsValidSteppedPosition(capsule, stepped, settings, overlapBuffer, selfColliderIds))
            return false;

        steppedPosition = stepped;
        stepHeight = foundStep;
        return true;
    }

    private static bool TryStepUp(
        Vector3 originPosition,
        Vector3 displacement,
        CapsuleCollider capsule,
        MovementSettings settings,
        Collider[] overlapBuffer,
        HashSet<int> selfColliderIds,
        out Vector3 steppedPosition,
        out float stepHeight)
    {
        steppedPosition = originPosition;
        stepHeight = 0f;

        if (settings.maxStepHeight <= Eps)
            return false;

        Vector3 target = originPosition + displacement;
        Vector3 probeOrigin = target;
        if (displacement.sqrMagnitude > Eps)
        {
            Vector3 dir = displacement.normalized;
            float probeDist = Mathf.Max(displacement.magnitude, settings.minStepProbeDistance);
            probeOrigin = originPosition + dir * probeDist;
        }

        if (!TryFindStepOrigin(capsule, probeOrigin, settings, overlapBuffer, selfColliderIds, out float foundStep))
            return false;

        Vector3 stepped = target + Vector3.up * foundStep;
        if (!IsValidSteppedPosition(capsule, stepped, settings, overlapBuffer, selfColliderIds))
            return false;

        steppedPosition = stepped;
        stepHeight = foundStep;
        return true;
    }

    private static bool TryFindStepOrigin(
        CapsuleCollider capsule,
        Vector3 probeOrigin,
        MovementSettings settings,
        Collider[] overlapBuffer,
        HashSet<int> selfColliderIds,
        out float foundStep)
    {
        foundStep = StepChecker.FindValidStepHeight(
            capsule,
            probeOrigin,
            settings.maxStepHeight,
            Mathf.Max(1, settings.stepSearchIterations),
            overlapBuffer,
            selfColliderIds,
            settings.blockMask,
            settings.blockMask,
            out bool canStep);

        return canStep && foundStep > Eps;
    }

    private static bool IsValidSteppedPosition(
        CapsuleCollider capsule,
        Vector3 steppedBodyPosition,
        MovementSettings settings,
        Collider[] overlapBuffer,
        HashSet<int> selfColliderIds)
    {
        if (settings.blockMask != 0 &&
            StepChecker.WouldCapsuleOverlap(capsule, steppedBodyPosition, settings.blockMask, overlapBuffer, selfColliderIds))
        {
            return false;
        }

        if (settings.SolidMask != 0 &&
            HasHeadSolidOverlap(capsule, steppedBodyPosition, settings, overlapBuffer, selfColliderIds))
        {
            return false;
        }

        return HasGroundSupport(capsule, steppedBodyPosition, settings);
    }

    private static void GetCapsulePoints(CapsuleCollider capsule, Vector3 bodyPosition, out Vector3 bottom, out Vector3 top, out float radius)
    {
        Transform t = capsule.transform;
        Vector3 worldCenter = t.TransformPoint(capsule.center) + (bodyPosition - t.position);
        radius = capsule.radius;
        float halfLine = Mathf.Max(capsule.height * 0.5f - radius, 0f);
        Vector3 up = t.up;
        top = worldCenter + up * halfLine;
        bottom = worldCenter - up * halfLine;
    }
}
