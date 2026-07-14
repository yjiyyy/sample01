using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카메라와 추적 대상 사이 Occluder를 2-side 반투명 머티리얼로 즉시 전환합니다.
/// (페이드 인/아웃 없이 Occlusion Min Alpha를 바로 적용)
/// </summary>
public sealed class DiabloStyleCameraOcclusionFade
{
    private static readonly int FadeAmountId = Shader.PropertyToID("_FadeAmount");

    private sealed class FadeEntry
    {
        public Renderer Renderer;
        public Material[] OriginalSharedMaterials;
        public Material[] FadeMaterials;
        public float FadeAmount = 1f;
    }

    private readonly Dictionary<Renderer, FadeEntry> activeEntries = new();
    private readonly HashSet<Renderer> frameHits = new();
    private readonly HashSet<Renderer> priorityFrameHits = new();
    private readonly HashSet<Renderer> rayFrameHits = new();
    private readonly List<Renderer> staleKeys = new();
    private readonly RaycastHit[] raycastHits = new RaycastHit[32];
    private readonly Collider[] overlapHits = new Collider[24];

    private Shader fadeShader;
    private float minAlpha = 0.25f;
    private LayerMask occluderMask;
    private float raycastRadius;
    private float castStopBeforeTarget;
    private float adjacentDistance;
    private float insideCheckRadius;
    private float nearCheckRadius;
    private int maxOccluders;
    private bool useRayOcclusion;
    private bool useInsideColliderOcclusion;
    private bool useNearCameraOcclusion;
    private bool useBuildingVolumeOcclusion;
    private Transform ignoreRoot;

    public void Configure(
        LayerMask mask,
        float minFadeAlpha,
        float castRadius,
        float stopBeforeTarget,
        float adjacentDist,
        float insideRadius,
        float nearRadius,
        int maxCount,
        bool rayOcclusion,
        bool insideColliderOcclusion,
        bool nearCameraOcclusion,
        bool buildingVolumeOcclusion)
    {
        occluderMask = mask;
        minAlpha = Mathf.Clamp01(minFadeAlpha);
        raycastRadius = Mathf.Max(0f, castRadius);
        castStopBeforeTarget = Mathf.Max(0f, stopBeforeTarget);
        adjacentDistance = Mathf.Max(0f, adjacentDist);
        insideCheckRadius = Mathf.Max(0.01f, insideRadius);
        nearCheckRadius = Mathf.Max(0.05f, nearRadius);
        maxOccluders = Mathf.Max(1, maxCount);
        useRayOcclusion = rayOcclusion;
        useInsideColliderOcclusion = insideColliderOcclusion;
        useNearCameraOcclusion = nearCameraOcclusion;
        useBuildingVolumeOcclusion = buildingVolumeOcclusion;
    }

    public void SetIgnoreRoot(Transform root) => ignoreRoot = root;

    public void Update(Vector3 cameraPosition, Vector3 followPosition)
    {
        EnsureShader();
        if (fadeShader == null)
            return;

        priorityFrameHits.Clear();
        rayFrameHits.Clear();
        frameHits.Clear();

        if (useInsideColliderOcclusion)
            CollectInsideColliderRenderers(cameraPosition, priorityFrameHits);

        if (useNearCameraOcclusion)
            CollectNearCameraOccludingRenderers(cameraPosition, followPosition, priorityFrameHits);

        if (useBuildingVolumeOcclusion)
            CollectBuildingVolumeRenderers(cameraPosition, priorityFrameHits);

        if (useRayOcclusion)
            CollectRayOccludingRenderers(cameraPosition, followPosition, rayFrameHits);

        foreach (Renderer renderer in priorityFrameHits)
            frameHits.Add(renderer);

        foreach (Renderer renderer in rayFrameHits)
            frameHits.Add(renderer);

        foreach (Renderer renderer in frameHits)
        {
            if (renderer == null)
                continue;

            if (!activeEntries.TryGetValue(renderer, out FadeEntry entry))
            {
                bool isPriority = priorityFrameHits.Contains(renderer);
                if (!isPriority && activeEntries.Count >= maxOccluders)
                    continue;

                if (!TryBeginFade(renderer, out entry))
                    continue;
            }

            // 페이드 없이 목표 알파를 즉시 적용
            if (!Mathf.Approximately(entry.FadeAmount, minAlpha))
            {
                entry.FadeAmount = minAlpha;
                ApplyFadeAmount(entry);
            }
        }

        staleKeys.Clear();
        foreach (KeyValuePair<Renderer, FadeEntry> pair in activeEntries)
        {
            Renderer renderer = pair.Key;
            FadeEntry entry = pair.Value;

            if (renderer == null)
            {
                DestroyFadeMaterials(entry);
                staleKeys.Add(renderer);
                continue;
            }

            if (frameHits.Contains(renderer))
                continue;

            // 가림이 끝나면 페이드 아웃 없이 즉시 원본 복구
            staleKeys.Add(renderer);
        }

        for (int i = 0; i < staleKeys.Count; i++)
            EndFade(staleKeys[i]);
    }

    public void RestoreAll()
    {
        staleKeys.Clear();
        foreach (Renderer renderer in activeEntries.Keys)
            staleKeys.Add(renderer);

        for (int i = 0; i < staleKeys.Count; i++)
            EndFade(staleKeys[i]);
    }

    private void EnsureShader()
    {
        if (fadeShader != null)
            return;

        fadeShader = Shader.Find("Custom/OccluderFade");
    }

    private void CollectInsideColliderRenderers(Vector3 cameraPosition, HashSet<Renderer> results)
    {
        int count = Physics.OverlapSphereNonAlloc(
            cameraPosition,
            insideCheckRadius,
            overlapHits,
            occluderMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider collider = overlapHits[i];
            if (collider == null || collider.isTrigger || ShouldIgnoreCollider(collider))
                continue;

            if (!IsPointInsideCollider(collider, cameraPosition))
                continue;

            if (!TryGetOccluderRenderer(collider, out Renderer renderer))
                continue;

            results.Add(renderer);
        }
    }

    private static bool IsPointInsideCollider(Collider collider, Vector3 worldPoint)
    {
        Vector3 closest = collider.ClosestPoint(worldPoint);
        return (closest - worldPoint).sqrMagnitude < 0.0001f;
    }

    /// <summary>
    /// 카메라에 거의 붙은 Occluder를 잡습니다.
    /// SphereCast는 시작 위치와 겹친 Collider를 감지하지 않아서, 바닥/천장이 카메라에 붙으면 레이만으로는 투명화가 안 됩니다.
    /// 옆 벽 오탐을 막기 위해 "카메라 근처"가 아니라 "시선(카메라→주인공)을 가로막는지"로 판정합니다.
    /// </summary>
    private void CollectNearCameraOccludingRenderers(
        Vector3 cameraPosition,
        Vector3 followPosition,
        HashSet<Renderer> results)
    {
        int count = Physics.OverlapSphereNonAlloc(
            cameraPosition,
            nearCheckRadius,
            overlapHits,
            occluderMask,
            QueryTriggerInteraction.Ignore);

        Vector3 delta = followPosition - cameraPosition;
        float distance = delta.magnitude;
        if (distance < 0.01f)
            return;

        Vector3 direction = delta / distance;
        float maxAlong = Mathf.Max(0.01f, distance - castStopBeforeTarget);

        for (int i = 0; i < count; i++)
        {
            Collider collider = overlapHits[i];
            if (collider == null || collider.isTrigger || ShouldIgnoreCollider(collider))
                continue;

            if (!TryGetNearOcclusionPoint(
                    cameraPosition,
                    direction,
                    maxAlong,
                    collider,
                    out Vector3 occlusionPoint))
                continue;

            // 카메라가 Collider 안이 아니고, 캐릭터가 옆면에 붙어 있으며,
            // 가림 지점도 캐릭터 근처일 때만 엄폐로 보고 제외.
            // (카메라 위 바닥/천장은 캐릭터에 붙어 있어도 페이드해야 함)
            bool cameraInside = IsPointInsideCollider(collider, cameraPosition);
            if (!cameraInside &&
                ShouldSkipAsCharacterSideCover(cameraPosition, followPosition, occlusionPoint, collider))
                continue;

            if (!IsPointBetweenCameraAndTarget(cameraPosition, followPosition, direction, maxAlong, occlusionPoint))
                continue;

            if (!TryGetOccluderRenderer(collider, out Renderer renderer))
                continue;

            if (!renderer.enabled)
                continue;

            results.Add(renderer);
        }
    }

    /// <summary>
    /// 시선 선분 위에서 Collider와 가장 가까운 지점을 찾고, 그 지점이 시선에 충분히 가까울 때만 가림으로 인정합니다.
    /// </summary>
    private bool TryGetNearOcclusionPoint(
        Vector3 cameraPosition,
        Vector3 direction,
        float maxAlong,
        Collider collider,
        out Vector3 occlusionPoint)
    {
        occlusionPoint = cameraPosition;

        float bestDistSq = float.MaxValue;
        Vector3 bestPointOnCollider = cameraPosition;
        bool found = false;

        // 카메라 근처를 더 촘촘히 샘플링 (붙은 바닥 감지용)
        const int nearSamples = 6;
        const int farSamples = 6;
        float nearSpan = Mathf.Min(maxAlong, nearCheckRadius * 2f);

        for (int s = 0; s <= nearSamples; s++)
        {
            float along = nearSpan * (s / (float)nearSamples);
            ConsiderSample(cameraPosition + direction * along);
        }

        if (maxAlong > nearSpan + 0.01f)
        {
            for (int s = 1; s <= farSamples; s++)
            {
                float along = Mathf.Lerp(nearSpan, maxAlong, s / (float)farSamples);
                ConsiderSample(cameraPosition + direction * along);
            }
        }

        if (!found)
            return false;

        occlusionPoint = bestPointOnCollider;
        return true;

        void ConsiderSample(Vector3 pointOnSegment)
        {
            Vector3 closest = collider.ClosestPoint(pointOnSegment);
            float distSq = (closest - pointOnSegment).sqrMagnitude;

            // 시선에 실제로 걸친 면만 허용. 옆 벽이 카메라 근처에 있어도 시선과 멀면 제외.
            float along = Vector3.Dot(pointOnSegment - cameraPosition, direction);
            float accept = along <= nearCheckRadius
                ? Mathf.Max(0.08f, nearCheckRadius * 0.35f)
                : 0.08f;

            if (distSq > accept * accept)
                return;

            if (distSq >= bestDistSq)
                return;

            bestDistSq = distSq;
            bestPointOnCollider = closest;
            found = true;
        }
    }

    private bool IsPointBetweenCameraAndTarget(
        Vector3 cameraPosition,
        Vector3 followPosition,
        Vector3 direction,
        float castDistance,
        Vector3 point)
    {
        float alongFromCamera = Vector3.Dot(point - cameraPosition, direction);
        if (alongFromCamera < -0.01f || alongFromCamera > castDistance + nearCheckRadius)
            return false;

        float alongFromFollow = Vector3.Dot(point - followPosition, direction);
        if (alongFromFollow >= -0.01f)
            return false;

        float inFrontOfCharacter = -alongFromFollow;
        Vector3 sideFromCharacter = Vector3.ProjectOnPlane(point - followPosition, direction);
        if (sideFromCharacter.magnitude > inFrontOfCharacter + 0.15f)
            return false;

        return true;
    }

    private void CollectBuildingVolumeRenderers(Vector3 cameraPosition, HashSet<Renderer> results)
    {
        IReadOnlyList<OccluderBuildingVolume> volumes = OccluderBuildingVolume.Instances;
        for (int i = 0; i < volumes.Count; i++)
        {
            OccluderBuildingVolume volume = volumes[i];
            if (volume == null || !volume.isActiveAndEnabled)
                continue;

            if (!volume.ContainsPoint(cameraPosition))
                continue;

            volume.CollectRenderers(results);
        }
    }

    private void CollectRayOccludingRenderers(Vector3 cameraPosition, Vector3 followPosition, HashSet<Renderer> results)
    {
        int remainingSlots = maxOccluders - activeEntries.Count;
        if (remainingSlots <= 0)
            return;

        Vector3 delta = followPosition - cameraPosition;
        float distance = delta.magnitude;
        if (distance < 0.01f)
            return;

        // 주인공 직전에서 캐스트를 끊어 옆·뒤 벽이 투명해지는 걸 줄입니다.
        float castDistance = distance - castStopBeforeTarget;
        if (castDistance < 0.01f)
            return;

        Vector3 direction = delta / distance;
        int hitCount = raycastRadius > 0.001f
            ? Physics.SphereCastNonAlloc(cameraPosition, raycastRadius, direction, raycastHits, castDistance, occluderMask, QueryTriggerInteraction.Ignore)
            : Physics.RaycastNonAlloc(cameraPosition, direction, raycastHits, castDistance, occluderMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount && remainingSlots > 0; i++)
        {
            RaycastHit hit = raycastHits[i];
            Collider collider = hit.collider;
            if (collider == null || ShouldIgnoreCollider(collider))
                continue;

            if (!IsHitBetweenCameraAndTarget(cameraPosition, followPosition, direction, castDistance, hit))
                continue;

            // 옆면 엄폐만 스킵. 카메라 근처 바닥/천장은 페이드 유지.
            if (ShouldSkipAsCharacterSideCover(cameraPosition, followPosition, hit.point, collider))
                continue;

            if (!TryGetOccluderRenderer(collider, out Renderer renderer))
                continue;

            if (!renderer.enabled)
                continue;

            if (results.Add(renderer))
                remainingSlots--;
        }
    }

    /// <summary>
    /// hit가 카메라→주인공 사이(정면 가림)인지 판정합니다.
    /// 캐릭터 옆·뒤에 붙은 벽은 제외합니다.
    /// </summary>
    private bool IsHitBetweenCameraAndTarget(
        Vector3 cameraPosition,
        Vector3 followPosition,
        Vector3 direction,
        float castDistance,
        in RaycastHit hit)
    {
        Vector3 toHit = hit.point - cameraPosition;
        float alongFromCamera = Vector3.Dot(toHit, direction);

        // 카메라 뒤이거나 캐스트 구간 밖이면 제외
        if (alongFromCamera < 0.01f || alongFromCamera > castDistance + 0.01f)
            return false;

        // 캐릭터보다 앞(카메라 쪽)에 있어야 함. 같거나 뒤면 옆·뒤 벽.
        float alongFromFollow = Vector3.Dot(hit.point - followPosition, direction);
        if (alongFromFollow >= -0.01f)
            return false;

        // 캐릭터 기준: 옆으로 더 치우쳐 있으면 옆면으로 보고 제외
        float inFrontOfCharacter = -alongFromFollow;
        Vector3 sideFromCharacter = Vector3.ProjectOnPlane(hit.point - followPosition, direction);
        if (sideFromCharacter.magnitude > inFrontOfCharacter + 0.15f)
            return false;

        return true;
    }

    private bool IsCharacterAdjacentToCollider(Vector3 followPosition, Collider collider)
    {
        if (adjacentDistance <= 0.0001f || collider == null)
            return false;

        Vector3 closest = collider.ClosestPoint(followPosition);
        return (closest - followPosition).sqrMagnitude <= adjacentDistance * adjacentDistance;
    }

    /// <summary>
    /// 캐릭터가 건물에 붙어 있고, 가림 지점도 캐릭터 쪽(옆면)일 때만 페이드를 건너뜁니다.
    /// 가림 지점이 카메라에 더 가까우면 바닥/천장으로 보고 페이드를 허용합니다.
    /// </summary>
    private bool ShouldSkipAsCharacterSideCover(
        Vector3 cameraPosition,
        Vector3 followPosition,
        Vector3 occlusionPoint,
        Collider collider)
    {
        if (!IsCharacterAdjacentToCollider(followPosition, collider))
            return false;

        float distToCharacterSq = (occlusionPoint - followPosition).sqrMagnitude;
        float distToCameraSq = (occlusionPoint - cameraPosition).sqrMagnitude;

        // hit/가림점이 카메라보다 캐릭터에 가까우면 옆면 엄폐
        return distToCharacterSq <= distToCameraSq;
    }

    private static bool TryGetOccluderRenderer(Collider collider, out Renderer renderer)
    {
        renderer = collider.GetComponent<Renderer>();
        if (renderer != null)
            return true;

        renderer = collider.GetComponentInParent<Renderer>();
        return renderer != null;
    }

    private bool ShouldIgnoreCollider(Collider collider)
    {
        if (ignoreRoot == null)
            return false;

        Transform hitTransform = collider.transform;
        return hitTransform == ignoreRoot || hitTransform.IsChildOf(ignoreRoot);
    }

    private bool TryBeginFade(Renderer renderer, out FadeEntry entry)
    {
        entry = null;
        Material[] sharedMaterials = renderer.sharedMaterials;
        if (sharedMaterials == null || sharedMaterials.Length == 0)
            return false;

        var fadeMaterials = new Material[sharedMaterials.Length];
        for (int i = 0; i < sharedMaterials.Length; i++)
        {
            Material source = sharedMaterials[i];
            if (source == null)
                return false;

            Material fadeMaterial = new Material(fadeShader);
            CopyMaterialProperties(source, fadeMaterial);
            fadeMaterials[i] = fadeMaterial;
        }

        entry = new FadeEntry
        {
            Renderer = renderer,
            OriginalSharedMaterials = sharedMaterials,
            FadeMaterials = fadeMaterials,
            FadeAmount = minAlpha
        };

        renderer.materials = fadeMaterials;
        activeEntries[renderer] = entry;
        ApplyFadeAmount(entry);
        return true;
    }

    private void ApplyFadeAmount(FadeEntry entry)
    {
        if (entry.FadeMaterials == null)
            return;

        for (int i = 0; i < entry.FadeMaterials.Length; i++)
        {
            Material fadeMaterial = entry.FadeMaterials[i];
            if (fadeMaterial != null)
                fadeMaterial.SetFloat(FadeAmountId, entry.FadeAmount);
        }
    }

    private void EndFade(Renderer renderer)
    {
        if (!activeEntries.TryGetValue(renderer, out FadeEntry entry))
            return;

        if (renderer != null && entry.OriginalSharedMaterials != null)
            renderer.sharedMaterials = entry.OriginalSharedMaterials;

        DestroyFadeMaterials(entry);
        activeEntries.Remove(renderer);
    }

    private static void DestroyFadeMaterials(FadeEntry entry)
    {
        if (entry.FadeMaterials == null)
            return;

        for (int i = 0; i < entry.FadeMaterials.Length; i++)
        {
            if (entry.FadeMaterials[i] != null)
                Object.Destroy(entry.FadeMaterials[i]);
        }

        entry.FadeMaterials = null;
    }

    private static void CopyMaterialProperties(Material source, Material destination)
    {
        if (source.HasProperty("_BaseMap"))
            destination.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
        else if (source.HasProperty("_MainTex"))
            destination.SetTexture("_BaseMap", source.GetTexture("_MainTex"));

        if (source.HasProperty("_BaseColor"))
            destination.SetColor("_BaseColor", source.GetColor("_BaseColor"));
        else if (source.HasProperty("_Color"))
            destination.SetColor("_BaseColor", source.GetColor("_Color"));
    }
}
