using System.Collections.Generic;
using UnityEngine;

/// <summary>잔상 스폰 조건. 출시 빌드 전에는 <see cref="Normal"/> 로 두세요.</summary>
public enum SilhouetteGhostSpawnMode
{
    /// <summary>오버드라이브 또는 <see cref="ISilhouetteGhostSpawnSource"/>.</summary>
    Normal,
    /// <summary>플레이 중 조건 무시·항상 잔상(연출 확인용).</summary>
    TestAlwaysActive
}

/// <summary>
/// SkinnedMesh를 베이크해 전신 실루엣 잔상을 남깁니다. 오버드라이브 등
/// <see cref="ISilhouetteGhostSpawnSource"/> 또는 <see cref="PlayerOverdriveUpgradeRuntime"/>로 켜고 끕니다.
/// </summary>
[DisallowMultipleComponent]
public class FullBodySilhouetteGhost : MonoBehaviour
{
    [SerializeField] private SilhouetteGhostProfile profile;

    [Header("테스트")]
    [Tooltip("Test Always Active: 플레이만 하면 오버드라이브 없이 잔상이 나옵니다. 확인 후 Normal로 되돌리세요.")]
    [SerializeField] private SilhouetteGhostSpawnMode spawnMode = SilhouetteGhostSpawnMode.Normal;

    [Tooltip("테스트 모드에서 SkinnedMesh를 못 찾으면 Console에 한 번 경고.")]
    [SerializeField] private bool logMissingMeshesInTestMode = true;

    [Header("스폰 조건")]
    [Tooltip("비어 있지 않으면 이 소스가 true일 때만 잔상.")]
    [SerializeField] private MonoBehaviour spawnSourceOverride;

    [Tooltip("Override가 없을 때 계층에서 찾을 PlayerOverdriveUpgradeRuntime.")]
    [SerializeField] private PlayerOverdriveUpgradeRuntime overdriveRuntime;

    [Header("기준")]
    [Tooltip("잔상이 따라갈 모델 루트(비우면 이 오브젝트 transform).")]
    [SerializeField] private Transform modelRootOverride;

    [Tooltip("비면 modelRoot 아래에서 수집")]
    [SerializeField] private SkinnedMeshRenderer[] explicitSkinnedMeshes;

    private ISilhouetteGhostSpawnSource _spawnSource;
    private Transform _modelRoot;
    private SkinnedMeshRenderer[] _skinMeshes;

    private readonly List<GhostInstance> _active = new List<GhostInstance>();
    private readonly Stack<GhostInstance> _pool = new Stack<GhostInstance>();

    private float _spawnAccum;
    private MaterialPropertyBlock _mpb;
    private bool _loggedMissingMeshes;

    /// <summary>
    /// <see cref="PlayerOverdriveUpgradeRuntime"/> 등이 AddComponent한 경우.
    /// 컴포넌트가 있는 동안만 잔상을 남기고, 오버드라이브 조회는 하지 않습니다.
    /// </summary>
    private bool _runtimeManagedSpawn;

    private sealed class GhostPart
    {
        public GameObject GameObject;
        public MeshRenderer Renderer;
        public MeshFilter Filter;
        public Mesh Mesh;
    }

    private sealed class GhostInstance
    {
        public GameObject Root;
        public float SpawnTime;
        public readonly List<GhostPart> Parts = new List<GhostPart>();
    }

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        _modelRoot = modelRootOverride != null ? modelRootOverride : transform;

        if (spawnSourceOverride != null && spawnSourceOverride is ISilhouetteGhostSpawnSource src)
            _spawnSource = src;

        if (overdriveRuntime == null)
        {
            overdriveRuntime = GetComponent<PlayerOverdriveUpgradeRuntime>() ??
                               GetComponentInChildren<PlayerOverdriveUpgradeRuntime>(true) ??
                               GetComponentInParent<PlayerOverdriveUpgradeRuntime>();
            if (overdriveRuntime == null && transform.root != null)
                overdriveRuntime = transform.root.GetComponentInChildren<PlayerOverdriveUpgradeRuntime>(true);
        }

        ResolveSkinnedMeshes();
    }

    private void OnEnable()
    {
        ResolveSkinnedMeshes();
    }

    /// <summary>
    /// 오버드라이브 등 런타임 전용. 컴포넌트가 붙어 있는 동안 항상 잔상을 스폰합니다.
    /// </summary>
    public void ConfigureForRuntime(SilhouetteGhostProfile runtimeProfile, Transform modelRoot = null)
    {
        profile = runtimeProfile;
        _runtimeManagedSpawn = true;
        spawnMode = SilhouetteGhostSpawnMode.Normal;
        spawnSourceOverride = null;
        overdriveRuntime = null;

        if (modelRoot != null)
            modelRootOverride = modelRoot;

        _modelRoot = modelRootOverride != null ? modelRootOverride : transform;
        _spawnAccum = 0f;
        _loggedMissingMeshes = false;

        if (_mpb == null)
            _mpb = new MaterialPropertyBlock();

        ResolveSkinnedMeshes();
    }

    private void ResolveSkinnedMeshes()
    {
        if (explicitSkinnedMeshes != null && explicitSkinnedMeshes.Length > 0)
        {
            _skinMeshes = explicitSkinnedMeshes;
            return;
        }

        if (profile != null && !profile.autoCollectSkinnedMeshes)
        {
            _skinMeshes = System.Array.Empty<SkinnedMeshRenderer>();
            return;
        }

        _skinMeshes = _modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
    }

    private bool ShouldSpawnSilhouettes()
    {
        if (_runtimeManagedSpawn)
            return profile != null && profile.ghostMaterial != null;

        if (spawnMode == SilhouetteGhostSpawnMode.TestAlwaysActive)
            return true;

        if (_spawnSource != null)
            return _spawnSource.ShouldSpawnSilhouettes;

        return overdriveRuntime != null && overdriveRuntime.IsOverdriveActive;
    }

    /// <summary>플레이 중 컴포넌트 우클릭 메뉴 또는 코드에서 1회만 잔상을 찍어 봅니다.</summary>
    [ContextMenu("테스트: 잔상 1회 스폰")]
    public void TestSpawnOneSnapshot()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[FullBodySilhouetteGhost] 테스트 스폰은 플레이 모드에서만 됩니다.");
            return;
        }

        if (profile == null || profile.ghostMaterial == null)
        {
            Debug.LogWarning("[FullBodySilhouetteGhost] Profile 또는 Ghost Material이 비어 있습니다.");
            return;
        }

        ResolveSkinnedMeshes();
        if (_skinMeshes == null || _skinMeshes.Length == 0)
        {
            Debug.LogWarning(
                $"[FullBodySilhouetteGhost] SkinnedMeshRenderer를 찾지 못했습니다. modelRoot='{_modelRoot.name}'");
            return;
        }

        TrySpawnSnapshot();
    }

    private void Update()
    {
        if (profile == null || profile.ghostMaterial == null)
            return;

        float dt = GameplayTime.DeltaTime;
        if (dt <= 0f)
            return;

        UpdateGhostAlphas();

        if (_skinMeshes == null || _skinMeshes.Length == 0)
        {
            MaybeLogMissingMeshes();
            return;
        }

        if (!ShouldSpawnSilhouettes())
        {
            _spawnAccum = 0f;
            return;
        }

        float interval = Mathf.Max(0.02f, profile.snapshotIntervalSeconds);
        _spawnAccum += dt;

        if (_spawnAccum < interval)
            return;

        if (profile.limitOneSnapshotPerFrame)
        {
            _spawnAccum -= interval;
            TrySpawnSnapshot();
            return;
        }

        while (_spawnAccum >= interval)
        {
            _spawnAccum -= interval;
            TrySpawnSnapshot();
        }
    }

    private void MaybeLogMissingMeshes()
    {
        if (_loggedMissingMeshes || !logMissingMeshesInTestMode)
            return;
        if (spawnMode != SilhouetteGhostSpawnMode.TestAlwaysActive)
            return;

        _loggedMissingMeshes = true;
        Debug.LogWarning(
            $"[FullBodySilhouetteGhost] 테스트 모드인데 SkinnedMeshRenderer가 없습니다. " +
            $"Model Root='{_modelRoot.name}', Explicit Meshes={explicitSkinnedMeshes?.Length ?? 0}");
    }

    private void TrySpawnSnapshot()
    {
        if (profile.maxConcurrentGhosts <= 0)
            return;

        while (_active.Count >= profile.maxConcurrentGhosts)
            DespawnOldest();

        GhostInstance g = RentGhost();
        g.SpawnTime = Time.unscaledTime; // 시각적 페이드만; pause 시 DeltaTime 0이면 Update 자체가 안 돔
        ApplyGhostRootTransform(g.Root.transform);

        int layer = profile.ghostLayer;
        if (layer >= 0 && layer <= 31)
            SetLayerRecursive(g.Root, layer);

        int usedParts = 0;
        for (int i = 0; i < _skinMeshes.Length; i++)
        {
            SkinnedMeshRenderer smr = _skinMeshes[i];
            if (smr == null || !smr.gameObject.activeInHierarchy)
                continue;

            GhostPart part = GetOrCreatePart(g, usedParts);
            usedParts++;

            smr.BakeMesh(part.Mesh, true);

            part.Filter.sharedMesh = null;
            part.Filter.sharedMesh = part.Mesh;

            part.Renderer.sharedMaterial = profile.ghostMaterial;

            part.GameObject.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
            part.GameObject.transform.SetParent(g.Root.transform, true);
            part.GameObject.SetActive(true);

            Color c = profile.tintRgb;
            c.a = Mathf.Clamp01(profile.startAlpha);
            ApplyGhostColor(part.Renderer, c);
        }

        for (int j = usedParts; j < g.Parts.Count; j++)
            g.Parts[j].GameObject.SetActive(false);

        _active.Add(g);
    }

    private void ApplyGhostRootTransform(Transform ghostRoot)
    {
        if (profile.leaveSnapshotsInWorldSpace)
            ghostRoot.SetParent(profile.worldSpaceContainer, false);
        else
            ghostRoot.SetParent(transform, false);

        ghostRoot.SetPositionAndRotation(_modelRoot.position, _modelRoot.rotation);
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform t in go.transform)
            SetLayerRecursive(t.gameObject, layer);
    }

    private void UpdateGhostAlphas()
    {
        if (profile == null)
            return;

        float lifetime = Mathf.Max(0.05f, profile.ghostLifetimeSeconds);
        float now = Time.unscaledTime;

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            GhostInstance g = _active[i];
            float age = now - g.SpawnTime;
            float t = Mathf.Clamp01(age / lifetime);

            Color baseRgb = profile.tintRgb;
            float a = Mathf.Lerp(profile.startAlpha, profile.endAlpha, t);
            baseRgb.a = Mathf.Clamp01(a);

            for (int p = 0; p < g.Parts.Count; p++)
            {
                GhostPart part = g.Parts[p];
                if (part.GameObject.activeSelf)
                    ApplyGhostColor(part.Renderer, baseRgb);
            }

            if (age >= lifetime)
            {
                ReturnGhost(g);
                _active.RemoveAt(i);
            }
        }
    }

    private void ApplyGhostColor(MeshRenderer r, Color c)
    {
        r.GetPropertyBlock(_mpb);
        string prop = string.IsNullOrEmpty(profile.colorPropertyName) ? "_BaseColor" : profile.colorPropertyName;
        if (r.sharedMaterial != null && r.sharedMaterial.HasProperty(prop))
            _mpb.SetColor(prop, c);
        else
            _mpb.SetColor("_Color", c);
        r.SetPropertyBlock(_mpb);
    }

    private GhostPart GetOrCreatePart(GhostInstance g, int index)
    {
        while (g.Parts.Count <= index)
        {
            var go = new GameObject("SilhouettePart");
            go.transform.SetParent(g.Root.transform, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            var mesh = new Mesh { name = "SilhouetteBake" };
            var part = new GhostPart
            {
                GameObject = go,
                Mesh = mesh,
                Filter = mf,
                Renderer = mr
            };
            g.Parts.Add(part);
        }

        return g.Parts[index];
    }

    private GhostInstance RentGhost()
    {
        GhostInstance g = _pool.Count > 0 ? _pool.Pop() : CreateShellInstance();

        g.Root.SetActive(true);
        g.SpawnTime = 0f;
        return g;
    }

    private GhostInstance CreateShellInstance()
    {
        var root = new GameObject("SilhouetteGhost");
        root.transform.SetParent(transform, false);
        return new GhostInstance { Root = root };
    }

    private void ReturnGhost(GhostInstance g)
    {
        g.Root.SetActive(false);
        // 풀은 스포너 아래에 두어 씬 정리·OnDestroy 시 같이 정리되게 합니다.
        g.Root.transform.SetParent(transform, false);
        _pool.Push(g);
    }

    private void DespawnOldest()
    {
        if (_active.Count == 0)
            return;
        GhostInstance g = _active[0];
        _active.RemoveAt(0);
        ReturnGhost(g);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _active.Count; i++)
            DestroyGhostMeshes(_active[i]);
        while (_pool.Count > 0)
            DestroyGhostMeshes(_pool.Pop());
    }

    private static void DestroyGhostMeshes(GhostInstance g)
    {
        for (int i = 0; i < g.Parts.Count; i++)
        {
            if (g.Parts[i].Mesh != null)
                Destroy(g.Parts[i].Mesh);
            if (g.Parts[i].GameObject != null)
                Destroy(g.Parts[i].GameObject);
        }

        g.Parts.Clear();
        if (g.Root != null)
            Destroy(g.Root);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (profile != null && profile.snapshotIntervalSeconds < 0.02f)
            profile.snapshotIntervalSeconds = 0.02f;
    }
#endif
}
