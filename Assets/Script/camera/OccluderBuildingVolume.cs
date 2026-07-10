using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카메라가 이 볼륨 안에 들어오면 건물 Renderer 전체를 Occluder Fade 대상으로 등록합니다.
/// Mesh Collider(벽)와 별도로 Trigger BoxCollider가 필요합니다.
/// </summary>
[DisallowMultipleComponent]
public class OccluderBuildingVolume : MonoBehaviour
{
    private static readonly List<OccluderBuildingVolume> instances = new();

    [SerializeField] private BoxCollider volumeCollider;
    [SerializeField] private Transform renderersRoot;

    private Renderer[] cachedRenderers;

    public static IReadOnlyList<OccluderBuildingVolume> Instances => instances;

    private void Reset()
    {
        volumeCollider = GetComponent<BoxCollider>();
        if (volumeCollider == null)
            volumeCollider = gameObject.AddComponent<BoxCollider>();

        volumeCollider.isTrigger = true;
        renderersRoot = transform;
    }

    private void Awake()
    {
        if (volumeCollider == null)
            volumeCollider = GetComponent<BoxCollider>();

        if (renderersRoot == null)
            renderersRoot = transform;

        CacheRenderers();
    }

    private void OnEnable() => instances.Add(this);

    private void OnDisable() => instances.Remove(this);

    private void OnValidate()
    {
        if (renderersRoot == null)
            renderersRoot = transform;

        if (volumeCollider != null)
            volumeCollider.isTrigger = true;

        if (isActiveAndEnabled)
            CacheRenderers();
    }

    public bool ContainsPoint(Vector3 worldPoint)
    {
        if (volumeCollider == null || !volumeCollider.enabled)
            return false;

        Vector3 localPoint = volumeCollider.transform.InverseTransformPoint(worldPoint);
        Vector3 offset = localPoint - volumeCollider.center;
        Vector3 halfExtents = volumeCollider.size * 0.5f;

        return Mathf.Abs(offset.x) <= halfExtents.x
            && Mathf.Abs(offset.y) <= halfExtents.y
            && Mathf.Abs(offset.z) <= halfExtents.z;
    }

    public void CollectRenderers(HashSet<Renderer> results)
    {
        if (results == null || cachedRenderers == null)
            return;

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer renderer = cachedRenderers[i];
            if (renderer != null && renderer.enabled)
                results.Add(renderer);
        }
    }

    public void RefreshRenderers() => CacheRenderers();

    private void CacheRenderers()
    {
        if (renderersRoot == null)
            cachedRenderers = System.Array.Empty<Renderer>();
        else
            cachedRenderers = renderersRoot.GetComponentsInChildren<Renderer>(true);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (volumeCollider == null)
            volumeCollider = GetComponent<BoxCollider>();

        if (volumeCollider == null)
            return;

        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.25f);
        Gizmos.matrix = volumeCollider.transform.localToWorldMatrix;
        Gizmos.DrawCube(volumeCollider.center, volumeCollider.size);

        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
        Gizmos.DrawWireCube(volumeCollider.center, volumeCollider.size);
    }
#endif
}
