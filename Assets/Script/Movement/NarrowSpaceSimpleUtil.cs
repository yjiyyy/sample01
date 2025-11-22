using UnityEngine;

/// <summary>
/// Thin backward-compat shim / simple narrow-space filter.
/// - Provides FilterCapsuleDisplacement(...) which Enemy/other scripts call.
/// - Checks capsule overlap at origin and target. If target overlaps while current does not,
///   performs a small binary search (iterations) to find maximum allowed fraction of movement.
/// - If already overlapping and target also overlaps, blocks movement (returns Vector3.zero).
/// - If already overlapping and target does NOT overlap, allows (escape).
/// </summary>
public static class NarrowSpaceSimpleUtil
{
    public static Vector3 FilterCapsuleDisplacement(
        CapsuleCollider cap,
        Vector3 origin,
        Vector3 disp,
        LayerMask mask,
        int iterations = 2,
        float minFactorThreshold = 0.05f,
        float tinyDispThreshold = 0.001f)
    {
        if (cap == null) return disp;
        if (mask == 0) return disp;
        if (disp.sqrMagnitude <= tinyDispThreshold * tinyDispThreshold) return disp;

        Transform t = cap.transform;

        // world center at origin
        Vector3 worldCenterNow = t.TransformPoint(cap.center) + (origin - t.position);

        float radius = cap.radius;
        float height = cap.height;
        float halfLine = Mathf.Max(height * 0.5f - radius, 0f);
        Vector3 up = t.up;

        bool currentOverlap = CheckCapsuleOverlap(worldCenterNow, up, halfLine, radius, mask);

        // target
        Vector3 targetOrigin = origin + disp;
        Vector3 worldCenterTarget = t.TransformPoint(cap.center) + (targetOrigin - t.position);
        bool targetOverlap = CheckCapsuleOverlap(worldCenterTarget, up, halfLine, radius, mask);

        // Case: currently not overlapping, but target overlaps -> binary search to boundary
        if (!currentOverlap && targetOverlap)
        {
            float low = 0f;
            float high = 1f;
            for (int i = 0; i < Mathf.Max(1, iterations); i++)
            {
                float mid = (low + high) * 0.5f;
                Vector3 midPos = origin + disp * mid;
                Vector3 midCenter = t.TransformPoint(cap.center) + (midPos - t.position);
                bool midOverlap = CheckCapsuleOverlap(midCenter, up, halfLine, radius, mask);
                if (midOverlap) high = mid;
                else low = mid;
            }

            if (low < minFactorThreshold) return Vector3.zero;
            return disp * low;
        }

        // Case: currently overlapping and target overlapping -> block
        if (currentOverlap && targetOverlap)
        {
            return Vector3.zero;
        }

        // Case: currently overlapping but target not overlapping -> allow (escape)
        // Case: both not overlapping -> allow
        return disp;
    }

    private static bool CheckCapsuleOverlap(
        Vector3 worldCenter,
        Vector3 up,
        float halfLine,
        float radius,
        LayerMask mask)
    {
        Vector3 top = worldCenter + up * halfLine;
        Vector3 bottom = worldCenter - up * halfLine;

        Collider[] hits = Physics.OverlapCapsule(
            bottom,
            top,
            radius,
            mask,
            QueryTriggerInteraction.Ignore);

        return hits != null && hits.Length > 0;
    }
}