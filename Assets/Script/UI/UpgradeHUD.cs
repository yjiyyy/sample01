using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어의 <see cref="Upgrade"/> 슬롯 데이터를 읽어 HUD 아이콘을 갱신합니다.
/// </summary>
[DisallowMultipleComponent]
public class UpgradeHUD : MonoBehaviour
{
    /// <summary>슬롯 커스텀 FX는 이 이름의 자식(또는 하위) Transform에 붙습니다.</summary>
    public const string SlotFxChildName = "FX_Slot";

    [Header("UI (아이콘 표시용 Image 5개)")]
    [SerializeField] private Image[] slotImages = new Image[Upgrade.SlotCount];
    [SerializeField] private Sprite[] defaultSlotSprites = new Sprite[Upgrade.SlotCount];

    [Header("데이터 소스")]
    [Tooltip("비워두면 씬에서 Upgrade 컴포넌트를 자동 검색합니다.")]
    [SerializeField] private Upgrade upgrade;

    /// <summary>현재 바인딩된 <see cref="Upgrade"/> (없으면 null).</summary>
    public Upgrade DataSource => upgrade;

    /// <summary>인스펙터/자동 탐색으로 슬롯 Image가 몇 칸 채워졌는지 (HUD 선택용).</summary>
    public int CountAssignedSlotImages()
    {
        EnsureSlotImages();
        if (slotImages == null)
            return 0;

        int n = 0;
        for (int i = 0; i < slotImages.Length && i < Upgrade.SlotCount; i++)
        {
            if (slotImages[i] != null)
                n++;
        }

        return n;
    }

    /// <summary>부활 등 런타임에서 플레이어와 동일한 Upgrade를 쓰도록 강제 연결합니다.</summary>
    public void EnsureDataSource(Upgrade playerUpgrade)
    {
        if (playerUpgrade == null)
            return;

        if (upgrade != playerUpgrade)
        {
            UnbindUpgrade();
            upgrade = playerUpgrade;
            BindUpgrade();
            Refresh();
            return;
        }

        BindUpgrade();
    }

    private void OnEnable()
    {
        EnsureSlotImages();
        CaptureDefaultSlotSpritesIfNeeded();
        BindUpgrade();
        Refresh();
    }

    private void Start()
    {
        EnsureSlotImages();
        CaptureDefaultSlotSpritesIfNeeded();
        BindUpgrade();
        Refresh();
    }

    private void OnDisable()
    {
        UnbindUpgrade();
    }

    private void OnValidate()
    {
        if (slotImages == null || slotImages.Length != Upgrade.SlotCount)
        {
            System.Array.Resize(ref slotImages, Upgrade.SlotCount);
        }
        if (defaultSlotSprites == null || defaultSlotSprites.Length != Upgrade.SlotCount)
        {
            System.Array.Resize(ref defaultSlotSprites, Upgrade.SlotCount);
        }

        EnsureSlotImages();
        CaptureDefaultSlotSpritesIfNeeded();
        Refresh();
    }

    public void Refresh()
    {
        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            var img = slotImages != null && i < slotImages.Length ? slotImages[i] : null;
            if (img == null)
                continue;

            UpgradeEffectSO slotData = upgrade != null ? upgrade.GetSlot(i) : null;
            if (slotData != null && slotData.icon != null)
            {
                img.sprite = slotData.icon;
                img.enabled = true;
                img.gameObject.SetActive(true);
            }
            else
            {
                // 빈 슬롯이어도 슬롯 오브젝트(박스)는 항상 보이도록 유지합니다.
                // 기본 슬롯 스프라이트(에디터에서 넣은 박스 이미지)는 지우지 않습니다.
                if (defaultSlotSprites != null && i < defaultSlotSprites.Length)
                    img.sprite = defaultSlotSprites[i];
                img.gameObject.SetActive(true);
                img.enabled = true;
            }
        }
    }

    public bool TryGetSlotTransform(int index, out Transform slotTransform)
    {
        slotTransform = null;
        if (slotImages == null || index < 0 || index >= slotImages.Length)
            return false;

        Image img = slotImages[index];
        if (img == null)
            return false;

        slotTransform = img.transform;
        return slotTransform != null;
    }

    public bool TryPlaySlotFx(int index, GameObject fxPrefab, float autoDestroySeconds, bool verboseLog = false)
    {
        if (fxPrefab == null)
        {
            if (verboseLog)
                Debug.LogWarning($"[ReviveSlotFx/UpgradeHUD] '{name}' TryPlaySlotFx: fxPrefab null (index:{index})");
            return false;
        }

        EnsureSlotImages();

        if (slotImages == null)
        {
            if (verboseLog)
                Debug.LogWarning($"[ReviveSlotFx/UpgradeHUD] '{name}' slotImages 배열 null");
            return false;
        }

        if (index < 0 || index >= slotImages.Length)
        {
            if (verboseLog)
                Debug.LogWarning($"[ReviveSlotFx/UpgradeHUD] '{name}' index 범위 밖: {index} (length:{slotImages.Length})");
            return false;
        }

        // 데이터 슬롯 인덱스 ↔ UI: 이름 UpgradeSlot_01 … 과 매칭 (자식 순서·slotImages 배열 순서와 무관)
        Transform namedSlotRoot = FindUpgradeSlotRootByDataIndex(index);
        Image imgAt = null;
        if (namedSlotRoot != null)
        {
            imgAt = namedSlotRoot.GetComponent<Image>();
            if (imgAt == null)
                imgAt = namedSlotRoot.GetComponentInChildren<Image>(true);
        }

        if (imgAt == null && index >= 0 && index < slotImages.Length)
            imgAt = slotImages[index];

        if (imgAt == null)
        {
            if (verboseLog)
            {
                int filled = CountAssignedSlotImages();
                Debug.LogWarning(
                    $"[ReviveSlotFx/UpgradeHUD] '{name}' 슬롯 {index}에 해당하는 Image를 찾지 못했습니다. " +
                    $"연결된 슬롯 수:{filled}, HUD path:{DebugHierarchyPath(transform)}");
            }
            return false;
        }

        Transform slotTransform = imgAt.transform;
        if (verboseLog)
        {
            Debug.Log(
                $"[ReviveSlotFx/UpgradeHUD] 슬롯 Image — dataIndex:{index}, path:{DebugHierarchyPath(slotTransform)}, " +
                $"namedSlotMatch:{(namedSlotRoot != null ? namedSlotRoot.name : "fallback slotImages")}");
        }

        Transform parent = FindSlotFxParent(slotTransform);
        if (verboseLog)
        {
            bool underFxSlot = parent != null && parent.name == SlotFxChildName;
            Debug.Log(
                $"[ReviveSlotFx/UpgradeHUD] FX 부모 Transform — name:'{(parent != null ? parent.name : "?")}', " +
                $"path:{DebugHierarchyPath(parent)}, FX_Slot으로 붙임:{underFxSlot}");
        }

        // Instantiate(…, parent, bool)은 Unity 버전에 따라 월드좌표 유지/로컬 해석이 달라,
        // 부모는 FX_Slot인데 월드 위치만 1번 슬롯에 남는 현상이 날 수 있음 → 부모 없이 생성 후 SetParent(false)로 고정.
        GameObject fx = Object.Instantiate(fxPrefab);
        Transform tr = fx.transform;
        tr.SetParent(parent, false);
        tr.localPosition = Vector3.zero;
        tr.localRotation = Quaternion.identity;
        tr.localScale = Vector3.one;

        // 월드 파티클 프리팹은 RectTransform이 없음 — GetComponent<RectTransform>()는 예외/MissingComponent가 날 수 있어 TryGetComponent만 사용
        if (parent is RectTransform && fx.TryGetComponent<RectTransform>(out RectTransform fxRt))
        {
            fxRt.anchorMin = Vector2.zero;
            fxRt.anchorMax = Vector2.one;
            fxRt.pivot = new Vector2(0.5f, 0.5f);
            fxRt.offsetMin = Vector2.zero;
            fxRt.offsetMax = Vector2.zero;
            fxRt.anchoredPosition3D = Vector3.zero;
            fxRt.localRotation = Quaternion.identity;
            fxRt.localScale = Vector3.one;
        }

        if (verboseLog)
        {
            Debug.Log(
                $"[ReviveSlotFx/UpgradeHUD] Instantiate+SetParent — instance:'{fx.name}', " +
                $"parent:{DebugHierarchyPath(tr.parent)}, worldPos:{tr.position}, localPos:{tr.localPosition}");
        }

        // 월드 단위 파티클이 UI Rect 아래에 붙으면 캔버스 스케일 때문에 극도로 작게 보이는 경우가 많음
        ApplySlotFxScaleToParentRect(fx, parent as RectTransform, verboseLog);

        // Overlay UI 안에서 월드 파티클이 슬롯 Image 뒤에 깔리거나 안 보이는 경우가 많아 보정합니다.
        ApplySlotFxOverlayVisibilityFix(fx, parent, index, verboseLog);

        if (autoDestroySeconds > 0f)
            Destroy(fx, autoDestroySeconds);

        return true;
    }

    /// <summary>FX_Slot Rect 크기에 맞춰 루트 스케일을 키웁니다.</summary>
    private static void ApplySlotFxScaleToParentRect(GameObject fxInstance, RectTransform fxSlotRect, bool verboseLog)
    {
        if (fxInstance == null || fxSlotRect == null)
            return;

        float w = Mathf.Max(1f, fxSlotRect.rect.width);
        float h = Mathf.Max(1f, fxSlotRect.rect.height);
        float slotSpan = Mathf.Max(w, h);
        // 대략 150px 슬롯 기준으로 1배 — 더 작은 슬롯은 축소, 큰 슬롯은 확대 (파티클이 너무 미세한 문제 완화)
        const float refSpan = 150f;
        float mul = Mathf.Clamp(slotSpan / refSpan, 0.35f, 14f);
        fxInstance.transform.localScale = Vector3.one * mul;

        if (verboseLog)
            Debug.Log($"[ReviveSlotFx/UpgradeHUD] FX 스케일 보정 — slotRect:{w:F0}x{h:F0}, mul:{mul:F2}");
    }

    /// <summary>Overlay에서 슬롯 아이콘(Image)보다 FX가 위에 그려지도록 정렬합니다.</summary>
    private static void ApplySlotFxOverlayVisibilityFix(GameObject fxInstance, Transform fxSlotParent, int slotDataIndex, bool verboseLog)
    {
        if (fxInstance == null)
            return;

        // 슬롯 안에서 FX_Slot(또는 부모)을 형제 중 맨 뒤로 → 같은 깊이에서 다른 Image보다 나중에 그림
        if (fxSlotParent != null)
            fxSlotParent.SetAsLastSibling();

        fxInstance.transform.SetAsLastSibling();

        int uiLayer = fxSlotParent != null ? fxSlotParent.gameObject.layer : 5;
        SetLayerRecursively(fxInstance.transform, uiLayer);

        Canvas canvas = fxSlotParent != null ? fxSlotParent.GetComponentInParent<Canvas>() : null;
        int sortLayerId = SortingLayer.NameToID("Default");
        int sortOrder = 100;
        if (canvas != null)
        {
            var root = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
            sortLayerId = root.sortingLayerID;
            // 부모 슬롯 루트에 Image가 있으면 같은 캔버스 배치에서 자식 파티클이 아이콘 뒤로 깔리는 경우가 많음.
            // FX_Slot에 overrideSorting Canvas를 두면 슬롯 단위로 루트 캔버스보다 나중에 합성되도록 분리합니다.
            int nestedCanvasOrder = root.sortingOrder + 2000 + Mathf.Clamp(slotDataIndex, 0, 31) * 10;
            if (fxSlotParent != null &&
                fxSlotParent is RectTransform &&
                string.Equals(fxSlotParent.name, SlotFxChildName, System.StringComparison.Ordinal))
            {
                EnsureSlotFxNestedOverlayCanvas(fxSlotParent.gameObject, root, nestedCanvasOrder);
                sortOrder = nestedCanvasOrder + 50;
            }
            else
            {
                sortOrder = root.sortingOrder + 500;
            }
        }

        var renderers = fxInstance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null)
                continue;
            r.sortingLayerID = sortLayerId;
            r.sortingOrder = sortOrder;
        }

        var systems = fxInstance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] == null)
                continue;
            systems[i].scalingMode = ParticleSystemScalingMode.Hierarchy;
        }

        if (verboseLog)
        {
            Debug.Log(
                $"[ReviveSlotFx/UpgradeHUD] FX 가시성 보정 — Renderer:{renderers.Length}, layer:{uiLayer}, " +
                $"sortLayerId:{sortLayerId}, sortOrder:{sortOrder}, fxSlot siblingIndex:{(fxSlotParent != null ? fxSlotParent.GetSiblingIndex() : -1)}");
        }
    }

    /// <summary>
    /// 슬롯 전용 FX_Slot에 중첩 Canvas를 두어, 루트 슬롯 Image보다 나중에 그려지게 합니다.
    /// (동일 GameObject에 Canvas가 이미 있으면 덮어씁니다.)
    /// </summary>
    private static void EnsureSlotFxNestedOverlayCanvas(GameObject fxSlotRoot, Canvas rootCanvas, int sortingOrder)
    {
        if (fxSlotRoot == null || rootCanvas == null)
            return;

        Canvas c = fxSlotRoot.GetComponent<Canvas>();
        if (c == null)
            c = fxSlotRoot.AddComponent<Canvas>();

        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.overrideSorting = true;
        c.sortingLayerID = rootCanvas.sortingLayerID;
        c.sortingOrder = sortingOrder;
    }

    private static void SetLayerRecursively(Transform t, int layer)
    {
        if (t == null)
            return;
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i), layer);
    }

    private static string DebugHierarchyPath(Transform t)
    {
        if (t == null)
            return "(null)";

        System.Text.StringBuilder sb = new System.Text.StringBuilder(128);
        Transform walk = t;
        while (walk != null)
        {
            if (sb.Length > 0)
                sb.Insert(0, '/');
            sb.Insert(0, walk.name);
            walk = walk.parent;
        }

        return sb.ToString();
    }


    /// <summary>
    /// 슬롯 Image 아래에서 <see cref="SlotFxChildName"/>을 찾고, 없으면 바로 위 부모(슬롯 루트) 범위에서만 찾습니다.
    /// (HUD 루트까지 올리면 다른 슬롯의 FX_Slot을 잘못 고를 수 있음)
    /// </summary>
    private static Transform FindSlotFxParent(Transform slotImageTransform)
    {
        if (slotImageTransform == null)
            return null;

        Transform fxSlot = FindChildByNameRecursive(slotImageTransform, SlotFxChildName);
        if (fxSlot != null)
            return fxSlot;

        Transform parent = slotImageTransform.parent;
        if (parent == null)
            return slotImageTransform;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform c = parent.GetChild(i);
            if (c.name == SlotFxChildName)
                return c;
        }

        fxSlot = FindChildByNameRecursive(parent, SlotFxChildName);
        return fxSlot != null ? fxSlot : slotImageTransform;
    }

    private static Transform FindChildByNameRecursive(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform c = root.GetChild(i);
            if (c.name == childName)
                return c;

            Transform nested = FindChildByNameRecursive(c, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private void BindUpgrade()
    {
        if (upgrade == null)
            upgrade = Object.FindFirstObjectByType<Upgrade>();

        if (upgrade == null)
            return;

        upgrade.OnSlotsChanged -= Refresh;
        upgrade.OnSlotsChanged += Refresh;
    }

    private void UnbindUpgrade()
    {
        if (upgrade == null)
            return;

        upgrade.OnSlotsChanged -= Refresh;
    }

    private void EnsureSlotImages()
    {
        if (slotImages == null || slotImages.Length != Upgrade.SlotCount)
            System.Array.Resize(ref slotImages, Upgrade.SlotCount);

        bool hasEmpty = false;
        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            if (slotImages[i] == null)
            {
                hasEmpty = true;
                break;
            }
        }

        if (!hasEmpty)
            return;

        // 1) 이름 기준 UpgradeSlot_01 … (데이터 인덱스 0 = 01)
        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            if (slotImages[i] != null)
                continue;

            Transform slotRoot = FindUpgradeSlotRootByDataIndex(i);
            if (slotRoot == null)
                continue;

            Image img = slotRoot.GetComponent<Image>();
            if (img == null)
                img = slotRoot.GetComponentInChildren<Image>(true);

            slotImages[i] = img;
        }

        // 2) 여전히 비면: 직계 자식 순서(레거시)
        int childCount = Mathf.Min(transform.childCount, Upgrade.SlotCount);
        for (int i = 0; i < childCount; i++)
        {
            if (slotImages[i] != null)
                continue;

            Transform child = transform.GetChild(i);
            if (child == null)
                continue;

            Image img = child.GetComponent<Image>();
            if (img == null)
                img = child.GetComponentInChildren<Image>(true);

            slotImages[i] = img;
        }
    }

    /// <summary>데이터 슬롯 인덱스(0~4)에 대응하는 `UpgradeSlot_01` 형태 오브젝트를 찾습니다.</summary>
    private Transform FindUpgradeSlotRootByDataIndex(int dataIndex)
    {
        if (dataIndex < 0 || dataIndex >= Upgrade.SlotCount)
            return null;

        string wantTwo = $"UpgradeSlot_{dataIndex + 1:D2}";
        string wantOne = $"UpgradeSlot_{dataIndex + 1}";

        Transform found = FindDirectOrDeepChildBySlotName(transform, wantTwo);
        if (found != null)
            return found;

        return FindDirectOrDeepChildBySlotName(transform, wantOne);
    }

    private static Transform FindDirectOrDeepChildBySlotName(Transform root, string exactName)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform c = root.GetChild(i);
            if (c == null)
                continue;
            if (string.Equals(c.name, exactName, System.StringComparison.Ordinal))
                return c;
            if (c.name.StartsWith(exactName + " (", System.StringComparison.Ordinal))
                return c;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform c = root.GetChild(i);
            if (c == null)
                continue;
            Transform nested = FindDirectOrDeepChildBySlotName(c, exactName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private void CaptureDefaultSlotSpritesIfNeeded()
    {
        if (slotImages == null)
            return;

        if (defaultSlotSprites == null || defaultSlotSprites.Length != Upgrade.SlotCount)
            System.Array.Resize(ref defaultSlotSprites, Upgrade.SlotCount);

        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            if (slotImages[i] == null)
                continue;

            if (defaultSlotSprites[i] == null)
                defaultSlotSprites[i] = slotImages[i].sprite;
        }
    }
}
