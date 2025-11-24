using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Step and headroom checks (FindValidStepHeight, WouldCapsuleOverlap, ClampHeadroomHorizontal helpers).
/// - Uses caller-provided overlapBuffer and selfIds to minimize allocations.
/// - Compatible with Unity6 and with existing PlayerMovement usage.
/// </summary>
public static class StepChecker
{
    private const float EPS = 0.0001f;

    /// <summary>
    /// Binary-search for minimal step height in [0, maxStepHeight] that resolves obstacle overlap.
    /// Returns foundHeight and sets out bool canStep.
    /// Uses overlapBuffer/selfIds to minimize GC.
    /// </summary>
    public static float FindValidStepHeight(
        CapsuleCollider cap,
        Vector3 targetOrigin,
        float maxStepHeight,
        int stepSearchIterations,
        Collider[] overlapBuffer,
        HashSet<int> selfIds,
        LayerMask obstacleMask,
        LayerMask headMask,
        out bool canStep)
    {
        canStep = false;
        if (cap == null || maxStepHeight <= EPS) return 0f;

        float low = 0f;
        float high = maxStepHeight;
        float valid = 0f;
        bool foundAny = false;

        float radius = cap.radius;
        float height = cap.height;
        Quaternion rot = cap.transform.rotation;

        for (int i = 0; i < Mathf.Max(1, stepSearchIterations); ++i)
        {
            float mid = (low + high) * 0.5f;
            Vector3 testOrigin = targetOrigin + Vector3.up * mid;
            if (!WouldCapsuleOverlap(cap, testOrigin, obstacleMask | headMask, overlapBuffer, selfIds))
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

    /// <summary>
    /// Returns true if capsule at targetOrigin overlaps any collider in mask excluding self.
    /// Uses overlapBuffer for NonAlloc queries.
    /// </summary>
    public static bool WouldCapsuleOverlap(
        CapsuleCollider cap,
        Vector3 targetOrigin,
        LayerMask mask,
        Collider[] overlapBuffer,
        HashSet<int> selfIds)
    {
        if (cap == null || mask == 0) return false;

        Transform t = cap.transform;
        Vector3 worldCenterAtTarget = t.TransformPoint(cap.center) + (targetOrigin - t.position);
        float halfLine = Mathf.Max(cap.height * 0.5f - cap.radius, 0f);
        Vector3 up = t.up;
        Vector3 top = worldCenterAtTarget + up * halfLine;
        Vector3 bottom = worldCenterAtTarget - up * halfLine;

        if (overlapBuffer == null || overlapBuffer.Length == 0)
        {
            Collider[] hits = Physics.OverlapCapsule(bottom, top, cap.radius, mask, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return false;
            if (selfIds == null || selfIds.Count == 0) return hits.Length > 0;
            for (int i = 0; i < hits.Length; ++i)
            {
                if (hits[i] == null) continue;
                if (selfIds.Contains(hits[i].GetInstanceID())) continue;
                return true;
            }
            return false;
        }

        int cnt = Physics.OverlapCapsuleNonAlloc(bottom, top, cap.radius, overlapBuffer, mask, QueryTriggerInteraction.Ignore);
        if (cnt == overlapBuffer.Length)
        {
            // fallback
            Collider[] hits = Physics.OverlapCapsule(bottom, top, cap.radius, mask, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return false;
            if (selfIds == null || selfIds.Count == 0) return hits.Length > 0;
            for (int i = 0; i < hits.Length; ++i)
            {
                if (hits[i] == null) continue;
                if (selfIds.Contains(hits[i].GetInstanceID())) continue;
                return true;
            }
            return false;
        }

        if (cnt == 0) return false;
        if (selfIds == null || selfIds.Count == 0) return cnt > 0;
        for (int i = 0; i < cnt; ++i)
        {
            var h = overlapBuffer[i];
            if (h == null) continue;
            if (selfIds.Contains(h.GetInstanceID())) continue;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Headroom (top cylinder portion) overlap test with NonAlloc support.
    /// topSphere and bottomHead are world-space points defining the head region.
    /// </summary>
    public static bool CheckHeadOverlap(
        Vector3 topSphere,
        Vector3 bottomHead,
        float radius,
        LayerMask mask,
        Collider[] overlapBuffer,
        HashSet<int> selfIds)
    {
        if (mask == 0) return false;

        if (overlapBuffer != null && overlapBuffer.Length > 0)
        {
            int cnt = Physics.OverlapCapsuleNonAlloc(bottomHead, topSphere, radius, overlapBuffer, mask, QueryTriggerInteraction.Ignore);
            if (cnt == overlapBuffer.Length)
            {
                Collider[] hits = Physics.OverlapCapsule(bottomHead, topSphere, radius, mask, QueryTriggerInteraction.Ignore);
                if (hits == null || hits.Length == 0) return false;
                if (selfIds == null || selfIds.Count == 0) return hits.Length > 0;
                for (int i = 0; i < hits.Length; ++i)
                {
                    if (hits[i] == null) continue;
                    if (selfIds.Contains(hits[i].GetInstanceID())) continue;
                    return true;
                }
                return false;
            }

            if (cnt == 0) return false;
            if (selfIds == null || selfIds.Count == 0) return cnt > 0;
            for (int i = 0; i < cnt; ++i)
            {
                var c = overlapBuffer[i];
                if (c == null) continue;
                if (selfIds.Contains(c.GetInstanceID())) continue;
                return true;
            }
            return false;
        }
        else
        {
            Collider[] hits = Physics.OverlapCapsule(bottomHead, topSphere, radius, mask, QueryTriggerInteraction.Ignore);
            return hits != null && hits.Length > 0;
        }
    }

    /// <summary>
    /// Helper: Clamp headroom using the existing NarrowSpaceUtil.ClampHeadroomHorizontal signature,
    /// but exposing overload that accepts overlapBuffer/selfIds for non-alloc behavior.
    /// This simply forwards to NarrowSpaceUtil but supplies provided buffers.
    /// </summary>
    public static Vector3 ClampHeadroomHorizontal(
        CapsuleCollider cap,
        Vector3 origin,
        Vector3 disp,
        LayerMask mask,
        int iterations,
        float headPortion,
        float headMargin,
        Collider[] overlapBuffer,
        HashSet<int> selfIds)
    {
        // Use NarrowSpaceUtil's implementation which supports tempBuffer & selfIds if present.
        // If NarrowSpaceUtil doesn't support those parameters in your version, we would inline logic here.
        return NarrowSpaceUtil.ClampHeadroomHorizontal(
            cap,
            origin,
            disp,
            mask,
            iterations,
            headPortion,
            headMargin,
            overlapBuffer,
            selfIds
        );
    }
}