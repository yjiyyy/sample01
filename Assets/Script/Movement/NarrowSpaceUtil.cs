using UnityEngine;

/// <summary>
/// 낮아지는 천장(Headroom) 진입 시 캡슐 머리 부분이 천장에 박혀 바닥을 뚫거나 관통하는 문제를 줄이기 위한 간단한 수평 이동 클램프.
/// 조건:
/// - 현재 머리 부분은 겹치지 않는데 목표 위치 머리 부분은 겹치는 경우만 이동량 축소(반감 탐색).
/// - 이미 머리 부분 겹침 상태라면 제한 없이 이동(좁은 곳 탈출 허용).
/// 성능:
/// - 최대 검사 횟수: 현재(1) + 목표(1) + iterations(2) = 4회 OverlapCapsule.
/// - 모바일에서도 부담 미미.
/// </summary>
public static class NarrowSpaceUtil
{
    /// <param name="cap">플레이어/적 CapsuleCollider</param>
    /// <param name="origin">현재 기준 위치(rb.position)</param>
    /// <param name="disp">원하는 수평 이동량</param>
    /// <param name="mask">머리 공간을 막는 레이어(Ground 등)</param>
    /// <param name="iterations">반감 탐색 횟수(2 추천)</param>
    /// <param name="headPortion">상단 cylindrical 영역 비율(0.4 → 윗부분 40%)</param>
    /// <param name="headMargin">머리 부분 캡슐 반경 감소(오검출 완화)</param>
    public static Vector3 ClampHeadroomHorizontal(
        CapsuleCollider cap,
        Vector3 origin,
        Vector3 disp,
        LayerMask mask,
        int iterations,
        float headPortion,
        float headMargin)
    {
        if (cap == null) return disp;
        if (disp.sqrMagnitude <= 0f) return disp;
        if (mask == 0) return disp;

        // 캡슐 기본 데이터
        float height = cap.height;
        float radius = cap.radius;
        if (height < radius * 2f)
        {
            // 비정상 설정 경고 (1회만 표시 가능하도록 원한다면 static flag)
            Debug.LogWarning($"[NarrowSpaceUtil] Capsule height({height}) < 2*radius({radius}) : headroom 검사 신뢰도 낮음.");
        }

        // 월드 기준 중심(현재 위치)
        // transform.TransformPoint(center) + (origin - transform.position) 로 가상 위치 캡슐 중심 보정
        Transform t = cap.transform;
        Vector3 worldCenterAtCurrent = t.TransformPoint(cap.center);
        Vector3 worldCenterAtOrigin = worldCenterAtCurrent + (origin - t.position);

        // 수평 이동이므로 Up 방향(transform.up) 이용
        Vector3 up = t.up;

        // 캡슐 cylindrical 부분 길이
        float cylLen = Mathf.Max(height - 2f * radius, 0f);
        float headCylLen = cylLen * Mathf.Clamp01(headPortion);
        // 상단 구 중심 local (height/2 - radius) 위
        float topLine = (height * 0.5f) - radius;

        float usedRadius = Mathf.Max(radius - headMargin, radius * 0.5f);

        // 현재 위치 머리 부분 Overlap 검사
        bool currentHeadOverlap = CheckHeadOverlap(
            worldCenterAtOrigin, up,
            topLine, headCylLen, usedRadius, mask);

        // 목표 위치 머리 부분 Overlap 검사
        Vector3 targetOrigin = origin + disp;
        Vector3 worldCenterAtTarget = worldCenterAtCurrent + (targetOrigin - t.position);

        bool targetHeadOverlap = CheckHeadOverlap(
            worldCenterAtTarget, up,
            topLine, headCylLen, usedRadius, mask);

        // 새로 겹치려는 상황에서만 제한 (현재 미겹침 -> 목표 겹침)
        if (!currentHeadOverlap && targetHeadOverlap)
        {
            float low = 0f;
            float high = 1f;
            for (int i = 0; i < iterations; ++i)
            {
                float mid = (low + high) * 0.5f;
                Vector3 midPos = origin + disp * mid;
                Vector3 worldCenterAtMid = worldCenterAtCurrent + (midPos - t.position);

                bool midOverlap = CheckHeadOverlap(
                    worldCenterAtMid, up,
                    topLine, headCylLen, usedRadius, mask);

                if (midOverlap)
                {
                    // 줄여야 함
                    high = mid;
                }
                else
                {
                    // 아직 안 겹침 → 더 전진 가능
                    low = mid;
                }
            }

            // 허용 비율 low
            if (low < 0.05f)
            {
                // 사실상 이동 불가
                return Vector3.zero;
            }
            return disp * low;
        }

        // 이미 내부거나 애초에 겹침 없음 → 그대로 이동
        return disp;
    }

    /// <summary>
    /// 머리 영역 OverlapCapsule 검사
    /// </summary>
    private static bool CheckHeadOverlap(
        Vector3 worldCenter,
        Vector3 up,
        float topLine,
        float headCylLen,
        float radius,
        LayerMask mask)
    {
        // 상단 구 중심
        Vector3 topSphere = worldCenter + up * topLine;
        // 머리 cylindrical 부분 시작점(아래쪽) : topSphere - up * headCylLen
        Vector3 bottomHead = topSphere - up * headCylLen;

        // QueryTriggerInteraction.Ignore 로 트리거 무시
        Collider[] hits = Physics.OverlapCapsule(
            bottomHead,
            topSphere,
            radius,
            mask,
            QueryTriggerInteraction.Ignore);

        return hits != null && hits.Length > 0;
    }
}