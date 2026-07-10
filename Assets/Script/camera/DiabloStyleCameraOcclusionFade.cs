using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카메라와 추적 대상 사이 Occluder를 2-side 반투명 머티리얼로 페이드합니다.
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
    private float fadeInSpeed = 8f;
    private float fadeOutSpeed = 6f;
    private LayerMask occluderMask;
    private float raycastRadius;
    private float insideCheckRadius;
    private int maxOccluders;
    private bool useRayOcclusion;
    private bool useInsideColliderOcclusion;
    private bool useBuildingVolumeOcclusion;
    private Transform ignoreRoot;

    public void Configure(
        LayerMask mask,
        float minFadeAlpha,
        float fadeIn,
        float fadeOut,
        float castRadius,
        float insideRadius,
        int maxCount,
        bool rayOcclusion,
        bool insideColliderOcclusion,
        bool buildingVolumeOcclusion)
    {
        occluderMask = mask;
        minAlpha = Mathf.Clamp01(minFadeAlpha);
        fadeInSpeed = Mathf.Max(0.01f, fadeIn);
        fadeOutSpeed = Mathf.Max(0.01f, fadeOut);
        raycastRadius = Mathf.Max(0f, castRadius);
        insideCheckRadius = Mathf.Max(0.01f, insideRadius);
        maxOccluders = Mathf.Max(1, maxCount);
        useRayOcclusion = rayOcclusion;
        useInsideColliderOcclusion = insideColliderOcclusion;
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

            entry.FadeAmount = Mathf.MoveTowards(entry.FadeAmount, minAlpha, fadeInSpeed * Time.deltaTime);
            ApplyFadeAmount(entry);
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

            entry.FadeAmount = Mathf.MoveTowards(entry.FadeAmount, 1f, fadeOutSpeed * Time.deltaTime);
            ApplyFadeAmount(entry);

            if (entry.FadeAmount >= 0.999f)
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

        Vector3 direction = delta / distance;
        int hitCount = raycastRadius > 0.001f
            ? Physics.SphereCastNonAlloc(cameraPosition, raycastRadius, direction, raycastHits, distance, occluderMask, QueryTriggerInteraction.Ignore)
            : Physics.RaycastNonAlloc(cameraPosition, direction, raycastHits, distance, occluderMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount && remainingSlots > 0; i++)
        {
            Collider collider = raycastHits[i].collider;
            if (collider == null || ShouldIgnoreCollider(collider))
                continue;

            Renderer[] renderers = collider.GetComponentsInChildren<Renderer>(false);
            for (int r = 0; r < renderers.Length && remainingSlots > 0; r++)
            {
                Renderer renderer = renderers[r];
                if (renderer == null || !renderer.enabled)
                    continue;

                if (results.Add(renderer))
                    remainingSlots--;
            }
        }
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
            FadeAmount = 1f
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
