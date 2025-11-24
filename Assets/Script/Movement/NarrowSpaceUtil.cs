using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 머리 공간(headroom) 검사 유틸리티 (향상: NonAlloc 사용 및 외부 버퍼/self id 필터 지원)
/// - 목적: capsule 의 상단(머리) 부분만 별도 검사하여 좁은 머리공간에서 이동을 제한 또는 클램프
/// - 변경: CheckHeadOverlap 및 ClampHeadroomHorizontal에 optional tempBuffer/selfIds 파라미터를 추가하여
///         호출측(예: PlayerMovement)에서 재사용 버퍼를 전달하도록 지원.
/// </summary>
public static class NarrowSpaceUtil
{
    /// <param name="cap">체크 대상 CapsuleCollider</param>
    /// <param name="origin">현재 원점(rb.position)</param>
    /// <param name="disp">시도할 이동량</param>
    /// <param name="mask">머리 충돌 검사 레이어</param>
    /// <param name="iterations">이진탐색 반복 횟수</param>
    /// <param name="headPortion">머리 원통 부분 비율</param>
    /// <param name="headMargin">머리 반경 마진</param>
    /// <param name="tempBuffer">(optional) 외부에서 할당한 Collider[] 버퍼 (NonAlloc 용)</param>
    /// <param name="selfIds">(optional) 자기 콜라이더 인스턴스ID 집합(필터링용)</param>
    public static Vector3 ClampHeadroomHorizontal(
        CapsuleCollider cap,
        Vector3 origin,
        Vector3 disp,
        LayerMask mask,
        int iterations,
        float headPortion,
        float headMargin,
        Collider[] tempBuffer = null,
        HashSet<int> selfIds = null)
    {
        if (cap == null) return disp;
        if (disp.sqrMagnitude <= 0f) return disp;
        if (mask == 0) return disp;

        float height = cap.height;
        float radius = cap.radius;
        if (height < radius * 2f)
        {
            Debug.LogWarning($"[NarrowSpaceUtil] Capsule height({height}) < 2*radius({radius}) : headroom check may be incorrect.");
        }

        Transform t = cap.transform;
        Vector3 worldCenterAtCurrent = t.TransformPoint(cap.center) + (origin - t.position);

        Vector3 up = t.up;

        float cylLen = Mathf.Max(height - 2f * radius, 0f);
        float headCylLen = cylLen * Mathf.Clamp01(headPortion);
        float topLine = (height * 0.5f) - radius;

        float usedRadius = Mathf.Max(radius - headMargin, radius * 0.5f);

        // current
        Vector3 topSphereNow = worldCenterAtCurrent + up * topLine;
        Vector3 bottomHeadNow = topSphereNow - up * headCylLen;
        bool currentHeadOverlap = CheckHeadOverlap(topSphereNow, bottomHeadNow, usedRadius, mask, tempBuffer, selfIds);

        // target
        Vector3 targetOrigin = origin + disp;
        Vector3 worldCenterAtTarget = worldCenterAtCurrent + (targetOrigin - t.position);
        Vector3 topSphereTarget = worldCenterAtTarget + up * topLine;
        Vector3 bottomHeadTarget = topSphereTarget - up * headCylLen;
        bool targetHeadOverlap = CheckHeadOverlap(topSphereTarget, bottomHeadTarget, usedRadius, mask, tempBuffer, selfIds);

        if (!currentHeadOverlap && targetHeadOverlap)
        {
            float low = 0f;
            float high = 1f;
            for (int i = 0; i < iterations; ++i)
            {
                float mid = (low + high) * 0.5f;
                Vector3 midPos = origin + disp * mid;
                Vector3 worldCenterAtMid = worldCenterAtCurrent + (midPos - t.position);

                Vector3 topSphereMid = worldCenterAtMid + up * topLine;
                Vector3 bottomHeadMid = topSphereMid - up * headCylLen;

                bool midOverlap = CheckHeadOverlap(topSphereMid, bottomHeadMid, usedRadius, mask, tempBuffer, selfIds);

                if (midOverlap)
                {
                    high = mid;
                }
                else
                {
                    low = mid;
                }
            }

            if (low < 0.05f)
            {
                return Vector3.zero;
            }
            return disp * low;
        }

        return disp;
    }

    /// <summary>
    /// topSphere / bottomHead 를 주고 Overlap 검사 (NonAlloc 옵션 지원)
    /// </summary>
    public static bool CheckHeadOverlap(
        Vector3 topSphere,
        Vector3 bottomHead,
        float radius,
        LayerMask mask,
        Collider[] tempBuffer = null,
        HashSet<int> selfIds = null)
    {
        if (tempBuffer != null && tempBuffer.Length > 0)
        {
            int cnt = Physics.OverlapCapsuleNonAlloc(bottomHead, topSphere, radius, tempBuffer, mask, QueryTriggerInteraction.Ignore);
            if (cnt == tempBuffer.Length)
            {
                // buffer full -> safe fallback to allocating API
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
                var c = tempBuffer[i];
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
}