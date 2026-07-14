using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 배경 파괴 오브젝트.
/// 플레이어 무기 HitBox에 맞을 때마다 힛수 1 감소 → Hit Pulse →
/// 0이 되면 보상 1회 드랍 + Rigidbody 활성화 → despawnDelay 후 제거.
/// </summary>
[DisallowMultipleComponent]
public class DestructibleObject : MonoBehaviour
{
    private const string DebrisLayerName = "D_Prop";

    private static int cachedDebrisLayer = int.MinValue;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionColorSyntyId = Shader.PropertyToID("_Emission_Color");
    private static readonly int EnableEmissionSyntyId = Shader.PropertyToID("_Enable_Emission");

    private enum EmissionMode : byte
    {
        None = 0,
        Standard = 1,
        Synty = 2,
    }

    [Header("내구도 (피격 횟수)")]
    [Tooltip("깨질 때까지 필요한 피격 횟수. 데미지 수치가 아니라 맞은 횟수입니다.")]
    [SerializeField] private int hitsToBreak = 3;

    [Header("피격 판정")]
    [Tooltip("타격 판정에 쓸 Collider. 비어 있으면 이 오브젝트(자식 포함) Collider에 반응합니다.")]
    [SerializeField] private Collider hitCollider;

    [Header("파괴 후")]
    [Tooltip("Rigidbody 켠 뒤 제거까지 대기 시간(초).")]
    [SerializeField] private float despawnDelay = 3f;

    [Tooltip("깨질 때 가할 직선 임펄스 세기. 0이면 밀지 않습니다.")]
    [SerializeField] private float breakImpulse = 8f;

    [Tooltip("깨질 때 가할 회전(토크) 세기. 타격 방향으로 굴러가게 만듭니다. 0이면 회전 없음.")]
    [SerializeField] private float breakTorque = 0.5f;

    [Tooltip("임펄스 방향에 더할 위쪽 비율.")]
    [SerializeField] private float breakImpulseUpBias = 2f;

    [Header("분리 파츠 (Break 시)")]
    [Tooltip("파괴 순간 부모에서 분리해 물리 연출할 자식 Transform. 각 파츠에 Rigidbody + Collider 필요.")]
    [SerializeField] private Transform[] detachParts = new Transform[0];

    [Tooltip("분리 파츠에 가할 직선 임펄스 배율 (본체 breakImpulse × 이 값).")]
    [SerializeField] private float partBreakImpulseScale = 0.1f;

    [Tooltip("분리 파츠에 가할 회전 토크 배율 (본체 breakTorque × 이 값).")]
    [SerializeField] private float partBreakTorqueScale = 1f;

    [Header("Hit Pulse (저비용)")]
    [Tooltip("펄스 때 섞일 색. Synty처럼 BaseColor가 흰색인 머티리얼은 아래 Brighten이 보여줍니다.")]
    [SerializeField] private Color pulseColor = Color.white;
    [Tooltip("0~1. 펄스 세기(색 혼합 비율).")]
    [SerializeField] private float pulseIntensity = 1f;
    [Tooltip("베이스 색을 이 배수만큼 더 밝게(HDR). 흰색 BaseColor 텍스처도 반짝이게 합니다.")]
    [SerializeField] private float pulseBrighten = 5f;
    [Tooltip("맞는 순간 바로 밝아진 뒤, 원래 색으로 돌아오는 시간(초). In 구간 없음.")]
    [SerializeField] private float pulseOutDuration = 0.2f;

    public enum HitShakeMode
    {
        [Tooltip("로컬 위치 Z축만 흔듭니다.")]
        Position = 0,
        [Tooltip("로컬 회전 Z축만 흔듭니다.")]
        Rotation = 1,
    }

    [Header("Hit Shake (깨지기 전만)")]
    [SerializeField] private HitShakeMode shakeMode = HitShakeMode.Rotation;
    [Tooltip("Position이면 미터, Rotation이면 도(°). 로컬 Z축만 사용. 0이면 쉐이크 없음.")]
    [SerializeField] private float shakeIntensity = 5f;
    [Tooltip("맞는 순간 바로 최대 흔들림 → 이 시간(초) 동안 0으로. In 구간 없음.")]
    [SerializeField] private float shakeDuration = 0.5f;
    [Tooltip("초당 진동 횟수. 클수록 더 빠르게 떨립니다.")]
    [SerializeField] private float shakeFrequency = 10f;

    [Header("보상 드랍 (Break 시 1회)")]
    [SerializeField] private int totalDropCountMin = 2;
    [SerializeField] private int totalDropCountMax = 5;
    [SerializeField] private ItemDropEntry[] dropEntries = new ItemDropEntry[0];
    // Ground(10), Wall(12), Building(19) — Prop 제외
    [SerializeField] private LayerMask dropGroundLayerMask = (1 << 10) | (1 << 12) | (1 << 19);

    [Header("근접 외곽선 (테스트용)")]
    [Tooltip("켜면 플레이어가 가까이 올 때 외곽선을 표시합니다. 기본 OFF.")]
    [SerializeField] private bool enableProximityOutline = false;

    [Tooltip("플레이어와의 수평(XZ) 거리. 이 값 이내면 외곽선 ON.")]
    [SerializeField] private float proximityOutlineRadius = 5f;

    [Tooltip("거리 검사 주기(초).")]
    [SerializeField] private float proximityOutlineCheckInterval = 0.15f;

    [SerializeField] private Color outlineColor = Color.yellow;
    [SerializeField] private float outlineWidth = 0.1f;

    private int remainingHits;
    private bool isBroken;
    private Rigidbody cachedBody;
    private Renderer[] pulseRenderers;
    private MaterialPropertyBlock mpb;
    private readonly List<Color> baseColors = new List<Color>();
    private readonly List<int> colorPropertyIds = new List<int>();
    private readonly List<EmissionMode> emissionModes = new List<EmissionMode>();
    private readonly HashSet<int> activeHitBoxIds = new HashSet<int>();
    private Coroutine pulseRoutine;
    private Coroutine shakeRoutine;
    private Coroutine despawnRoutine;
    private Vector3 restLocalPosition;
    private Quaternion restLocalRotation;

    private sealed class CachedDetachPart
    {
        public Transform partTransform;
        public Rigidbody body;
        public Collider[] colliders;
    }

    private readonly List<CachedDetachPart> cachedDetachParts = new List<CachedDetachPart>();
    private readonly List<GameObject> detachedPartRoots = new List<GameObject>();

    private readonly List<Renderer> outlineRenderers = new List<Renderer>();
    private Material outlineMaterialInstance;
    private Coroutine proximityOutlineRoutine;
    private Transform playerTransform;
    private float proximityOutlineRadiusSqr;
    private static Material sharedOutlineMaterial;

    private void Awake()
    {
        remainingHits = Mathf.Max(1, hitsToBreak);
        cachedBody = GetComponent<Rigidbody>();
        if (cachedBody == null)
            cachedBody = GetComponentInChildren<Rigidbody>(true);

        mpb = new MaterialPropertyBlock();
        restLocalPosition = transform.localPosition;
        restLocalRotation = transform.localRotation;
        CachePulseRenderers();
        CacheDetachParts();
        PrepareIdlePhysics();
        PrepareDetachPartsIdle();

        proximityOutlineRadiusSqr = proximityOutlineRadius * proximityOutlineRadius;
        if (enableProximityOutline)
            BuildOutlineLayers();
    }

    private void Start()
    {
        if (enableProximityOutline && !isBroken)
            proximityOutlineRoutine = StartCoroutine(ProximityOutlineRoutine());
    }

    private void OnDestroy()
    {
        if (outlineMaterialInstance != null)
            Destroy(outlineMaterialInstance);
    }

#if UNITY_EDITOR
    /// <summary>컴포넌트를 새로 붙일 때 드랍 프리팹 기본 참조를 채웁니다.</summary>
    private void Reset()
    {
        hitsToBreak = 3;
        hitCollider = null;
        despawnDelay = 3f;
        breakImpulse = 8f;
        breakTorque = 0.5f;
        breakImpulseUpBias = 2f;
        detachParts = new Transform[0];
        partBreakImpulseScale = 0.1f;
        partBreakTorqueScale = 1f;
        pulseColor = Color.white;
        pulseIntensity = 1f;
        pulseBrighten = 5f;
        pulseOutDuration = 0.2f;
        shakeMode = HitShakeMode.Rotation;
        shakeIntensity = 5f;
        shakeDuration = 0.5f;
        shakeFrequency = 10f;
        totalDropCountMin = 2;
        totalDropCountMax = 5;
        dropGroundLayerMask = (1 << 10) | (1 << 12) | (1 << 19);
        enableProximityOutline = false;
        proximityOutlineRadius = 5f;
        proximityOutlineCheckInterval = 0.15f;
        outlineColor = Color.yellow;
        outlineWidth = 0.1f;

        var money = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Arts/DropItem/DropItem_Money.prefab");
        var cuboid = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Arts/DropItem/DropItem_Cuboid.prefab");

        dropEntries = new[]
        {
            new ItemDropEntry { itemPrefab = money, dropChance = 0.5f },
            new ItemDropEntry { itemPrefab = cuboid, dropChance = 0.5f },
        };
    }
#endif

    private void OnValidate()
    {
        hitsToBreak = Mathf.Max(1, hitsToBreak);
        despawnDelay = Mathf.Max(0f, despawnDelay);
        breakImpulse = Mathf.Max(0f, breakImpulse);
        breakTorque = Mathf.Max(0f, breakTorque);
        partBreakImpulseScale = Mathf.Max(0f, partBreakImpulseScale);
        partBreakTorqueScale = Mathf.Max(0f, partBreakTorqueScale);
        pulseOutDuration = Mathf.Max(0.01f, pulseOutDuration);
        pulseIntensity = Mathf.Clamp01(pulseIntensity);
        pulseBrighten = Mathf.Max(0f, pulseBrighten);
        shakeIntensity = Mathf.Max(0f, shakeIntensity);
        shakeDuration = Mathf.Max(0.01f, shakeDuration);
        shakeFrequency = Mathf.Max(0.1f, shakeFrequency);
        totalDropCountMin = Mathf.Max(0, totalDropCountMin);
        totalDropCountMax = Mathf.Max(totalDropCountMin, totalDropCountMax);
        proximityOutlineRadius = Mathf.Max(0.1f, proximityOutlineRadius);
        proximityOutlineCheckInterval = Mathf.Max(0.05f, proximityOutlineCheckInterval);
        outlineWidth = Mathf.Max(0.0001f, outlineWidth);
        proximityOutlineRadiusSqr = proximityOutlineRadius * proximityOutlineRadius;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isBroken || other == null)
            return;

        if (!TryGetPlayerHitBoxId(other, out int hitBoxId))
            return;

        if (hitCollider != null && !IsHitColliderInvolved(other))
            return;

        // 같은 HitBox가 여러 Collider에 겹쳐도 1힛. Exit 후 다시 들어오면 재카운트.
        if (!activeHitBoxIds.Add(hitBoxId))
            return;

        RegisterHit(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null)
            return;

        if (TryGetPlayerHitBoxId(other, out int hitBoxId))
            activeHitBoxIds.Remove(hitBoxId);
    }

    /// <summary>외부·디버그용. 피격 1회와 동일하게 처리합니다.</summary>
    public void RegisterHit()
    {
        RegisterHit(null);
    }

    private void RegisterHit(Collider sourceCollider)
    {
        if (isBroken)
            return;

        remainingHits--;
        PlayHitPulse();
        PlayHitShake();

        if (remainingHits > 0)
            return;

        Break(sourceCollider);
    }

    private void Break(Collider sourceCollider)
    {
        if (isBroken)
            return;

        isBroken = true;
        activeHitBoxIds.Clear();
        StopHitShakeAndRestore();
        StopProximityOutline();

        DropRewardsOnce();
        ApplyBrokenLayer();

        Vector3 flatDir = ResolveHitFlatDirection(sourceCollider);
        ActivatePhysics(flatDir);
        DetachAndActivateParts(flatDir);

        if (despawnRoutine != null)
            StopCoroutine(despawnRoutine);
        despawnRoutine = StartCoroutine(DespawnRoutine());
    }

    private void ApplyBrokenLayer()
    {
        if (cachedDebrisLayer == int.MinValue)
            cachedDebrisLayer = LayerMask.NameToLayer(DebrisLayerName);

        int layer = cachedDebrisLayer;
        if (layer < 0)
        {
            Debug.LogWarning(
                $"[DestructibleObject] '{DebrisLayerName}' 레이어가 없습니다. Edit → Project Settings → Tags and Layers에 추가해 주세요.",
                this);
            return;
        }

        // 분리 파츠는 아직 자식이므로 루트부터 재귀 적용하면 본체·파츠 모두 D_Prop이 됩니다.
        DieColliderUtility.SetLayerRecursively(transform, layer);
    }

    private void DropRewardsOnce()
    {
        if (dropEntries == null || dropEntries.Length == 0)
            return;
        if (totalDropCountMax <= 0)
            return;

        ItemDropSpawner.DropItemsForItemBox(
            transform.position,
            totalDropCountMin,
            totalDropCountMax,
            dropEntries,
            dropGroundLayerMask);
    }

    private void ActivatePhysics(Vector3 flatDir)
    {
        if (cachedBody == null)
        {
            Debug.LogWarning(
                $"[DestructibleObject] '{name}'에 Rigidbody가 없습니다. Break 시 물리 연출을 건너뜁니다.",
                this);
            return;
        }

        if (!cachedBody.gameObject.activeInHierarchy)
            cachedBody.gameObject.SetActive(true);

        cachedBody.detectCollisions = true;
        cachedBody.isKinematic = false;
        cachedBody.WakeUp();

        if (breakImpulse <= 0f && breakTorque <= 0f)
            return;

        if (breakImpulse > 0f)
        {
            Vector3 forceDir = (flatDir + Vector3.up * breakImpulseUpBias).normalized;
            cachedBody.AddForce(forceDir * breakImpulse, ForceMode.Impulse);
        }

        if (breakTorque > 0f)
        {
            // 수평 타격 방향으로 굴러가도록: 축 = up × 타격방향
            Vector3 rollAxis = Vector3.Cross(Vector3.up, flatDir);
            if (rollAxis.sqrMagnitude < 0.0001f)
                rollAxis = Vector3.right;
            else
                rollAxis.Normalize();

            cachedBody.AddTorque(rollAxis * breakTorque, ForceMode.Impulse);

            // 옆 회전(사이드 텀블): 타격 방향을 축으로, 세기 = 직선 토크와 동일
            cachedBody.AddTorque(flatDir * breakTorque, ForceMode.Impulse);
        }
    }

    private void CacheDetachParts()
    {
        cachedDetachParts.Clear();

        if (detachParts == null || detachParts.Length == 0)
            return;

        for (int i = 0; i < detachParts.Length; i++)
        {
            Transform part = detachParts[i];
            if (part == null)
                continue;

            if (part == transform)
            {
                Debug.LogWarning(
                    $"[DestructibleObject] '{name}' detachParts[{i}]에 루트 자신은 넣을 수 없습니다.",
                    this);
                continue;
            }

            if (!part.IsChildOf(transform))
            {
                Debug.LogWarning(
                    $"[DestructibleObject] '{name}' detachParts[{i}] '{part.name}'는 이 오브젝트의 자식이 아닙니다.",
                    this);
                continue;
            }

            var body = part.GetComponent<Rigidbody>();
            if (body == null)
            {
                Debug.LogWarning(
                    $"[DestructibleObject] '{name}' detachParts[{i}] '{part.name}'에 Rigidbody가 없습니다. 분리 파츠를 건너뜁니다.",
                    part);
                continue;
            }

            cachedDetachParts.Add(new CachedDetachPart
            {
                partTransform = part,
                body = body,
                colliders = part.GetComponentsInChildren<Collider>(true),
            });
        }
    }

    private void PrepareDetachPartsIdle()
    {
        for (int i = 0; i < cachedDetachParts.Count; i++)
        {
            var entry = cachedDetachParts[i];
            if (entry.body == null)
                continue;

            entry.body.isKinematic = true;
            entry.body.useGravity = true;
            entry.body.Sleep();

            if (entry.colliders == null)
                continue;

            for (int c = 0; c < entry.colliders.Length; c++)
            {
                if (entry.colliders[c] != null)
                    entry.colliders[c].enabled = false;
            }
        }
    }

    private void DetachAndActivateParts(Vector3 flatDir)
    {
        if (cachedDetachParts.Count == 0)
            return;

        float partImpulse = breakImpulse * partBreakImpulseScale;
        float partTorque = breakTorque * partBreakTorqueScale;

        for (int i = 0; i < cachedDetachParts.Count; i++)
        {
            var entry = cachedDetachParts[i];
            Transform part = entry.partTransform;
            Rigidbody body = entry.body;
            if (part == null || body == null)
                continue;

            Vector3 worldPos = part.position;
            Quaternion worldRot = part.rotation;

            part.SetParent(null, worldPositionStays: true);
            part.position = worldPos;
            part.rotation = worldRot;

            detachedPartRoots.Add(part.gameObject);

            if (entry.colliders != null)
            {
                for (int c = 0; c < entry.colliders.Length; c++)
                {
                    if (entry.colliders[c] != null)
                        entry.colliders[c].enabled = true;
                }
            }

            body.isKinematic = false;
            body.useGravity = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.WakeUp();

            if (partImpulse > 0f)
            {
                Vector3 forceDir = (flatDir + Vector3.up * breakImpulseUpBias).normalized;
                body.AddForce(forceDir * partImpulse, ForceMode.Impulse);
            }

            if (partTorque > 0f)
            {
                Vector3 rollAxis = Vector3.Cross(Vector3.up, flatDir);
                if (rollAxis.sqrMagnitude < 0.0001f)
                    rollAxis = Vector3.right;
                else
                    rollAxis.Normalize();

                body.AddTorque(rollAxis * partTorque, ForceMode.Impulse);
                body.AddTorque(flatDir * partTorque, ForceMode.Impulse);
            }
        }
    }

    private void DestroyDetachedParts()
    {
        for (int i = 0; i < detachedPartRoots.Count; i++)
        {
            if (detachedPartRoots[i] != null)
                Destroy(detachedPartRoots[i]);
        }

        detachedPartRoots.Clear();
    }

    private void BuildOutlineLayers()
    {
        outlineRenderers.Clear();

        Material baseMat = GetOrCreateSharedOutlineMaterial();
        if (baseMat == null)
            return;

        if (outlineMaterialInstance == null)
            outlineMaterialInstance = new Material(baseMat);
        outlineMaterialInstance.SetColor("_OutlineColor", outlineColor);
        outlineMaterialInstance.SetFloat("_OutlineWidth", outlineWidth);

        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter source = meshFilters[i];
            if (source == null || source.sharedMesh == null)
                continue;

            if (source.name.EndsWith("_Outline"))
                continue;

            var outlineGo = new GameObject(source.name + "_Outline");
            outlineGo.layer = source.gameObject.layer;
            outlineGo.transform.SetParent(source.transform, false);

            var outlineFilter = outlineGo.AddComponent<MeshFilter>();
            outlineFilter.sharedMesh = source.sharedMesh;

            var outlineRenderer = outlineGo.AddComponent<MeshRenderer>();
            outlineRenderer.sharedMaterial = outlineMaterialInstance;
            outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;
            outlineRenderer.enabled = false;

            outlineRenderers.Add(outlineRenderer);
        }
    }

    private static Material GetOrCreateSharedOutlineMaterial()
    {
        if (sharedOutlineMaterial != null)
            return sharedOutlineMaterial;

        Shader shader = Shader.Find("Custom/SimpleObjectOutline");
        if (shader == null)
        {
            Debug.LogWarning("[DestructibleObject] Custom/SimpleObjectOutline 셰이더를 찾지 못했습니다.");
            return null;
        }

        sharedOutlineMaterial = new Material(shader);
        return sharedOutlineMaterial;
    }

    private IEnumerator ProximityOutlineRoutine()
    {
        var wait = new WaitForSeconds(proximityOutlineCheckInterval);

        while (!isBroken)
        {
            CachePlayerIfNeeded();

            bool shouldShow = playerTransform != null
                && GetFlatDistanceSqr(transform.position, playerTransform.position) <= proximityOutlineRadiusSqr;

            SetOutlineVisible(shouldShow);
            yield return wait;
        }

        SetOutlineVisible(false);
        proximityOutlineRoutine = null;
    }

    private void StopProximityOutline()
    {
        if (proximityOutlineRoutine != null)
        {
            StopCoroutine(proximityOutlineRoutine);
            proximityOutlineRoutine = null;
        }

        SetOutlineVisible(false);
    }

    private void SetOutlineVisible(bool visible)
    {
        for (int i = 0; i < outlineRenderers.Count; i++)
        {
            if (outlineRenderers[i] != null)
                outlineRenderers[i].enabled = visible;
        }
    }

    private void CachePlayerIfNeeded()
    {
        if (playerTransform != null)
            return;

        if (PlayerResources.Instance != null)
        {
            playerTransform = PlayerResources.Instance.transform;
            return;
        }

        var go = GameObject.FindGameObjectWithTag("Player");
        if (go != null)
            playerTransform = go.transform;
    }

    private static float GetFlatDistanceSqr(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!enableProximityOutline)
            return;

        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, proximityOutlineRadius);
    }
#endif

    private Vector3 ResolveHitFlatDirection(Collider sourceCollider)
    {
        if (sourceCollider == null)
            return Vector3.forward;

        Vector3 dir = transform.position - sourceCollider.bounds.center;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            return Vector3.forward;

        return dir.normalized;
    }

    private IEnumerator DespawnRoutine()
    {
        if (despawnDelay > 0f)
            yield return new WaitForSeconds(despawnDelay);

        DestroyDetachedParts();
        Destroy(gameObject);
    }

    private void PrepareIdlePhysics()
    {
        if (cachedBody == null)
            return;

        // 깨지기 전에는 물리 비용이 없도록 kinematic으로 둡니다.
        cachedBody.isKinematic = true;
        cachedBody.Sleep();
    }

    private void CachePulseRenderers()
    {
        pulseRenderers = GetComponentsInChildren<Renderer>(true);
        baseColors.Clear();
        colorPropertyIds.Clear();
        emissionModes.Clear();

        if (pulseRenderers == null)
            return;

        for (int i = 0; i < pulseRenderers.Length; i++)
        {
            var r = pulseRenderers[i];
            if (r == null)
            {
                baseColors.Add(Color.white);
                colorPropertyIds.Add(0);
                emissionModes.Add(EmissionMode.None);
                continue;
            }

            var mat = r.sharedMaterial;
            int propId = 0;
            Color c = Color.white;
            EmissionMode emission = EmissionMode.None;

            if (mat != null)
            {
                if (mat.HasProperty(BaseColorId))
                {
                    propId = BaseColorId;
                    c = mat.GetColor(BaseColorId);
                }
                else if (mat.HasProperty(ColorId))
                {
                    propId = ColorId;
                    c = mat.GetColor(ColorId);
                }

                // Synty Polygon 셰이더는 _Emission_Color + _Enable_Emission 사용
                if (mat.HasProperty(EmissionColorSyntyId) && mat.HasProperty(EnableEmissionSyntyId))
                    emission = EmissionMode.Synty;
                else if (mat.HasProperty(EmissionColorId))
                    emission = EmissionMode.Standard;
            }

            baseColors.Add(c);
            colorPropertyIds.Add(propId);
            emissionModes.Add(emission);
        }
    }

    private void PlayHitShake()
    {
        if (isBroken || shakeIntensity <= 0f)
            return;

        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(HitShakeRoutine());
    }

    private void StopHitShakeAndRestore()
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }

        transform.localPosition = restLocalPosition;
        transform.localRotation = restLocalRotation;
    }

    private IEnumerator HitShakeRoutine()
    {
        // In 없음: t=0에서 바로 최대(sin 위상 π/2) → Duration 동안 envelope 0으로
        float t = 0f;
        while (t < shakeDuration && !isBroken)
        {
            t += Time.deltaTime;
            float envelope = 1f - Mathf.Clamp01(t / shakeDuration);
            float wave = Mathf.Sin((Mathf.PI * 0.5f) + (t * shakeFrequency * Mathf.PI * 2f));
            float signed = wave * shakeIntensity * envelope;

            if (shakeMode == HitShakeMode.Position)
            {
                transform.localRotation = restLocalRotation;
                transform.localPosition = restLocalPosition + new Vector3(0f, 0f, signed);
            }
            else
            {
                transform.localPosition = restLocalPosition;
                transform.localRotation = restLocalRotation * Quaternion.Euler(0f, 0f, signed);
            }

            yield return null;
        }

        transform.localPosition = restLocalPosition;
        transform.localRotation = restLocalRotation;
        shakeRoutine = null;
    }

    private void PlayHitPulse()
    {
        if (pulseRenderers == null || pulseRenderers.Length == 0)
            return;

        if (pulseRoutine != null)
            StopCoroutine(pulseRoutine);
        pulseRoutine = StartCoroutine(HitPulseRoutine());
    }

    private IEnumerator HitPulseRoutine()
    {
        // In 없음: 즉시 펄스 색 → Out 동안만 원래 색으로 복귀
        ApplyPulseBlend(pulseIntensity);

        float t = 0f;
        while (t < pulseOutDuration)
        {
            t += Time.deltaTime;
            float u = 1f - Mathf.Clamp01(t / pulseOutDuration);
            ApplyPulseBlend(u * pulseIntensity);
            yield return null;
        }

        ApplyPulseBlend(0f);
        pulseRoutine = null;
    }

    private void ApplyPulseBlend(float amount)
    {
        if (pulseRenderers == null)
            return;

        amount = Mathf.Clamp01(amount);
        Color emissionColor = pulseColor * (amount * (1f + pulseBrighten));

        for (int i = 0; i < pulseRenderers.Length; i++)
        {
            var r = pulseRenderers[i];
            if (r == null)
                continue;

            r.GetPropertyBlock(mpb);

            int propId = colorPropertyIds[i];
            if (propId != 0)
            {
                // Synty 등 BaseColor=흰색+텍스처면 흰색→흰색 Lerp는 안 보임 → HDR로 밝게
                Color target = pulseColor * (1f + pulseBrighten);
                Color blended = Color.Lerp(baseColors[i], target, amount);
                mpb.SetColor(propId, blended);
            }

            switch (emissionModes[i])
            {
                case EmissionMode.Synty:
                    mpb.SetColor(EmissionColorSyntyId, emissionColor);
                    mpb.SetFloat(EnableEmissionSyntyId, amount > 0.001f ? 1f : 0f);
                    break;
                case EmissionMode.Standard:
                    mpb.SetColor(EmissionColorId, emissionColor);
                    break;
            }

            r.SetPropertyBlock(mpb);
        }
    }

    private bool IsHitColliderInvolved(Collider other)
    {
        if (hitCollider == null || other == null)
            return true;

        Vector3 closest = hitCollider.ClosestPoint(other.transform.position);
        return (closest - other.transform.position).sqrMagnitude < 0.25f
            || hitCollider.bounds.Intersects(other.bounds);
    }

    private static bool TryGetPlayerHitBoxId(Collider other, out int hitBoxId)
    {
        hitBoxId = 0;
        if (other == null)
            return false;

        var melee = other.GetComponentInParent<HitBox_PC>();
        if (melee != null)
        {
            hitBoxId = melee.GetInstanceID();
            return true;
        }

        var proj = other.GetComponentInParent<HitBox_PC_Projectile>();
        if (proj != null)
        {
            hitBoxId = proj.GetInstanceID();
            return true;
        }

        var sector = other.GetComponentInParent<HitBox_PC_Sector>();
        if (sector != null)
        {
            hitBoxId = sector.GetInstanceID();
            return true;
        }

        var boom = other.GetComponentInParent<HitBox_PC_Projectile_Sector>();
        if (boom != null)
        {
            hitBoxId = boom.GetInstanceID();
            return true;
        }

        return false;
    }
}
