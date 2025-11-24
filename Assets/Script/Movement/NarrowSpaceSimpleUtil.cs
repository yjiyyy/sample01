using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Thin backward-compat shim / simple narrow-space filter.
/// - Provides FilterCapsuleDisplacement(...) which Enemy/other scripts call.
/// - NonAlloc 버퍼와 self-id 필터링을 지원하도록 확장.
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
        return FilterCapsuleDisplacement(cap, origin, disp, mask, iterations, minFactorThreshold, tinyDispThreshold, null, null);
    }

    public static Vector3 FilterCapsuleDisplacement(
        CapsuleCollider cap,
        Vector3 origin,
        Vector3 disp,
        LayerMask mask,
        int iterations,
        float minFactorThreshold,
        float tinyDispThreshold,
        Collider[] tempBuffer,
        HashSet<int> selfIds)
    {
        if (cap == null) return disp;
        if (mask == 0) return disp;
        if (disp.sqrMagnitude <= tinyDispThreshold * tinyDispThreshold) return disp;

        Transform t = cap.transform;

        Vector3 worldCenterNow = t.TransformPoint(cap.center) + (origin - t.position);

        float radius = cap.radius;
        float height = cap.height;
        float halfLine = Mathf.Max(height * 0.5f - radius, 0f);
        Vector3 up = t.up;

        bool currentOverlap = CheckCapsuleOverlap(worldCenterNow, up, halfLine, radius, mask, tempBuffer, selfIds);

        Vector3 targetOrigin = origin + disp;
        Vector3 worldCenterTarget = t.TransformPoint(cap.center) + (targetOrigin - t.position);
        bool targetOverlap = CheckCapsuleOverlap(worldCenterTarget, up, halfLine, radius, mask, tempBuffer, selfIds);

        if (!currentOverlap && targetOverlap)
        {
            float low = 0f;
            float high = 1f;
            for (int i = 0; i < Mathf.Max(1, iterations); i++)
            {
                float mid = (low + high) * 0.5f;
                Vector3 midPos = origin + disp * mid;
                Vector3 midCenter = t.TransformPoint(cap.center) + (midPos - t.position);
                bool midOverlap = CheckCapsuleOverlap(midCenter, up, halfLine, radius, mask, tempBuffer, selfIds);
                if (midOverlap) high = mid;
                else low = mid;
            }

            if (low < minFactorThreshold) return Vector3.zero;
            return disp * low;
        }

        if (currentOverlap && targetOverlap)
        {
            return Vector3.zero;
        }

        return disp;
    }

    private static bool CheckCapsuleOverlap(
        Vector3 worldCenter,
        Vector3 up,
        float halfLine,
        float radius,
        LayerMask mask,
        Collider[] tempBuffer,
        HashSet<int> selfIds)
    {
        Vector3 top = worldCenter + up * halfLine;
        Vector3 bottom = worldCenter - up * halfLine;

        if (tempBuffer != null && tempBuffer.Length > 0)
        {
            int cnt = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, tempBuffer, mask, QueryTriggerInteraction.Ignore);
            if (cnt == tempBuffer.Length)
            {
                Collider[] hits = Physics.OverlapCapsule(bottom, top, radius, mask, QueryTriggerInteraction.Ignore);
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
                var c = tempBuffer[i];
                if (c == null) continue;
                if (selfIds.Contains(c.GetInstanceID())) continue;
                return true;
            }
            return false;
        }
        else
        {
            Collider[] hits = Physics.OverlapCapsule(bottom, top, radius, mask, QueryTriggerInteraction.Ignore);
            return hits != null && hits.Length > 0;
        }
    }
}