using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
#endif

/// <summary>
/// 개발용 업그레이드 전환 오버레이.
/// 인스펙터에 등록한 UpgradeEffectSO 목록을 표시하고, 선택한 슬롯에 즉시 장착/해제합니다.
/// </summary>
public class DevUpgradeSwitcher : MonoBehaviour
{
    private const int DefaultColumns = 4;

    [Header("업그레이드 목록 (인스펙터 순서)")]
    [Tooltip("표시·장착에 사용할 UpgradeEffectSO. null 슬롯은 빈 칸으로 표시됩니다.")]
    public List<UpgradeEffectSO> upgrades = new List<UpgradeEffectSO>();

    [Header("개발자용 오버레이 키")]
    [Tooltip("F3 키로 열기/닫기")]
    public KeyCode toggleKey = KeyCode.F3;

    [Header("대상 플레이어 Upgrade")]
    public Upgrade targetUpgrade;

    [Header("빌드에서 활성화 여부")]
    public bool enableInBuild = true;

    [Header("오버레이 화면 비율")]
    [Range(0.2f, 1f)] public float overlayWidthPercent = 0.9f;
    [Range(0.2f, 1f)] public float overlayHeightPercent = 0.8f;
    [Range(0f, 0.5f)] public float overlayTopMarginPercent = 0.05f;

    [Header("그리드")]
    [Min(1)] public int columns = DefaultColumns;
    public float cellSize = 100f;
    public float cellSpacing = 8f;

    [Header("디버그")]
    public bool debugLog = false;

    private bool overlayOpen;
    private int selectedSlotIndex;
    private Vector2 scroll;
    private GUIStyle headerStyle;
    private GUIStyle rowStyle;
    private GUIStyle cellTextStyle;
    private EventSystem blockedEventSystem;
    private bool blockedEventSystemWasEnabled;
    private bool pendingUiEventSystemRestore;
    private float uiEventSystemRestoreEarliestTime;
    private float overlayReopenBlockedUntil;

    public bool IsOverlayOpen => overlayOpen;

    public void ToggleOverlay()
    {
        if (overlayOpen)
        {
            SetOverlayOpen(false);
            return;
        }

        if (!CanOpenOverlayNow())
            return;

        SetOverlayOpen(true);
    }

    public void OpenOverlay()
    {
        if (!CanOpenOverlayNow())
            return;

        SetOverlayOpen(true);
    }

    public void CloseOverlay() => SetOverlayOpen(false);

    public void ToggleOverlayForSlot(int slotIndex)
    {
        selectedSlotIndex = Mathf.Clamp(slotIndex, 0, Upgrade.SlotCount - 1);
        if (overlayOpen)
        {
            SetOverlayOpen(false);
            return;
        }

        if (!CanOpenOverlayNow())
            return;

        SetOverlayOpen(true);
    }

    public void OpenOverlayForSlot(int slotIndex)
    {
        selectedSlotIndex = Mathf.Clamp(slotIndex, 0, Upgrade.SlotCount - 1);
        if (!CanOpenOverlayNow())
            return;

        SetOverlayOpen(true);
    }

    private void SetOverlayOpen(bool open)
    {
        if (overlayOpen == open)
            return;

        overlayOpen = open;
        if (InputManager.Instance != null)
            InputManager.Instance.OverlayInputBlocked = overlayOpen;

        if (overlayOpen)
        {
            pendingUiEventSystemRestore = false;
            SetUiEventSystemBlocked(true);
        }
        else
        {
            // 닫히는 클릭이 뒤 UI로 전달되지 않게, 포인터가 완전히 떨어진 뒤 복구
            pendingUiEventSystemRestore = true;
            uiEventSystemRestoreEarliestTime = Time.unscaledTime + 0.12f;
            overlayReopenBlockedUntil = Time.unscaledTime + 0.25f;
        }
    }

    private void Awake()
    {
        if (!Application.isEditor && !enableInBuild)
        {
            enabled = false;
            return;
        }
    }

    private void Start()
    {
        EnsureUpgrade();
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        try { EnhancedTouchSupport.Enable(); } catch { }
#endif
    }

    private void OnDestroy()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        try { EnhancedTouchSupport.Disable(); } catch { }
#endif
        if (InputManager.Instance != null)
            InputManager.Instance.OverlayInputBlocked = false;
        SetUiEventSystemBlocked(false);
    }

    private void SetUiEventSystemBlocked(bool blocked)
    {
        if (blocked)
        {
            if (blockedEventSystem != null)
                return;

            blockedEventSystem = EventSystem.current;
            if (blockedEventSystem == null)
                return;

            blockedEventSystemWasEnabled = blockedEventSystem.enabled;
            if (blockedEventSystemWasEnabled)
                blockedEventSystem.enabled = false;
            return;
        }

        if (blockedEventSystem == null)
            return;

        blockedEventSystem.enabled = blockedEventSystemWasEnabled;
        blockedEventSystem = null;
    }

    private void Update()
    {
        if (InputManager.Instance == null)
            return;

        if (InputManager.Instance.GetKeyDown(toggleKey))
            SetOverlayOpen(!overlayOpen);

        if (!overlayOpen)
        {
            TryRestoreUiEventSystemIfSafe();
            return;
        }

        if (InputManager.Instance.GetKeyDown(KeyCode.Escape))
            SetOverlayOpen(false);
    }

    private void TryRestoreUiEventSystemIfSafe()
    {
        if (!pendingUiEventSystemRestore)
            return;

        if (Time.unscaledTime < uiEventSystemRestoreEarliestTime)
            return;

        if (IsAnyPointerPressed())
            return;

        pendingUiEventSystemRestore = false;
        SetUiEventSystemBlocked(false);
    }

    private static bool IsAnyPointerPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed)
            return true;

        var touch = Touchscreen.current;
        if (touch != null)
        {
            for (int i = 0; i < touch.touches.Count; i++)
            {
                if (touch.touches[i].isInProgress)
                    return true;
            }
        }

        return false;
#else
        if (Input.GetMouseButton(0))
            return true;
        return Input.touchCount > 0;
#endif
    }

    private bool CanOpenOverlayNow()
    {
        return Time.unscaledTime >= overlayReopenBlockedUntil;
    }

    private void EnsureUpgrade()
    {
        if (targetUpgrade != null)
            return;

        if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
        {
            targetUpgrade = GameManager.Instance.playerTransform.GetComponent<Upgrade>();
            if (targetUpgrade != null)
                return;
        }

        targetUpgrade = Object.FindFirstObjectByType<Upgrade>();
    }

    private bool TryEquipAtIndex(int effectIndex)
    {
        EnsureUpgrade();
        if (targetUpgrade == null)
        {
            Debug.LogWarning("[DevUpgradeSwitcher] Upgrade 컴포넌트를 찾을 수 없습니다.");
            return false;
        }

        if (selectedSlotIndex < 0 || selectedSlotIndex >= Upgrade.SlotCount)
            selectedSlotIndex = 0;

        if (upgrades == null || effectIndex < 0 || effectIndex >= upgrades.Count)
            return false;

        UpgradeEffectSO effect = upgrades[effectIndex];
        bool ok = targetUpgrade.TrySetSlot(selectedSlotIndex, effect);

        if (ok)
            SetOverlayOpen(false);

        if (debugLog)
        {
            string effectName = effect != null ? effect.name : "(null)";
            Debug.Log($"[DevUpgradeSwitcher] slot:{selectedSlotIndex} equip:{effectName} result:{ok}");
        }

        return ok;
    }

    private bool TryClearSelectedSlot()
    {
        EnsureUpgrade();
        if (targetUpgrade == null)
            return false;

        bool ok = targetUpgrade.TryClearSlot(selectedSlotIndex);
        if (debugLog)
            Debug.Log($"[DevUpgradeSwitcher] slot:{selectedSlotIndex} clear result:{ok}");
        return ok;
    }

    private void InitStylesIfNeeded()
    {
        if (headerStyle != null && rowStyle != null && cellTextStyle != null)
            return;

        var baseLabel = GUI.skin != null ? GUI.skin.label : new GUIStyle();
        var baseBox = GUI.skin != null ? GUI.skin.box : new GUIStyle();

        headerStyle = new GUIStyle(baseBox)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(8, 8, 8, 8)
        };

        rowStyle = new GUIStyle(baseLabel)
        {
            fontSize = 18,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(6, 6, 6, 6)
        };

        cellTextStyle = new GUIStyle(baseLabel)
        {
            fontSize = 15,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            clipping = TextClipping.Clip
        };
    }

    private static void DrawSpriteInRect(Rect rect, Sprite sprite)
    {
        if (sprite == null || sprite.texture == null) return;

        Texture2D tex = sprite.texture;
        Rect tr = sprite.textureRect;
        float tw = tex.width;
        float th = tex.height;
        GUI.DrawTextureWithTexCoords(rect, tex,
            new Rect(tr.x / tw, tr.y / th, tr.width / tw, tr.height / th));
    }

    private void OnGUI()
    {
        if (!overlayOpen)
            return;

        InitStylesIfNeeded();
        EnsureUpgrade();

        float width = Mathf.Clamp(Screen.width * overlayWidthPercent, 260f, Screen.width - 16f);
        float height = Mathf.Clamp(Screen.height * overlayHeightPercent, 180f, Screen.height - 16f);
        float left = Mathf.Round((Screen.width - width) * 0.5f);
        float top = Mathf.Round(Screen.height * overlayTopMarginPercent);
        Rect window = new Rect(left, top, width, height);

        int colCount = Mathf.Max(1, columns);
        int count = upgrades != null ? upgrades.Count : 0;
        float cs = Mathf.Max(40f, cellSize);
        float gap = Mathf.Max(0f, cellSpacing);

        bool areaStarted = false;
        try
        {
            GUILayout.BeginArea(window, GUI.skin.window);
            areaStarted = true;

            GUILayout.Label("Dev Upgrade Switcher (ESC/` 닫기)", headerStyle);
            GUILayout.Space(6);
            GUILayout.Label($"대상 슬롯: {selectedSlotIndex}", rowStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("선택 슬롯 비우기", GUILayout.Height(34)))
            {
                TryClearSelectedSlot();
                SetOverlayOpen(false);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            if (count == 0)
            {
                GUILayout.Label("upgrades 리스트에 UpgradeEffectSO를 등록하세요.", rowStyle);
                return;
            }

            scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            for (int rowStart = 0; rowStart < count; rowStart += colCount)
            {
                GUILayout.BeginHorizontal(GUILayout.Height(cs));

                for (int c = 0; c < colCount; c++)
                {
                    int i = rowStart + c;
                    if (i >= count) break;

                    GUILayout.Space(gap * 0.5f);
                    Rect cellRect = GUILayoutUtility.GetRect(cs, cs, GUILayout.Width(cs), GUILayout.Height(cs));
                    UpgradeEffectSO so = upgrades[i];

                    if (GUI.Button(cellRect, GUIContent.none, GUIStyle.none))
                    {
                        TryEquipAtIndex(i);
                    }

                    if (Event.current.type == EventType.Repaint)
                    {
                        GUI.Box(cellRect, GUIContent.none);
                        if (so == null)
                        {
                            GUI.Label(cellRect, "—", cellTextStyle);
                        }
                        else if (so.icon != null)
                        {
                            const float pad = 6f;
                            Rect iconRect = new Rect(cellRect.x + pad, cellRect.y + pad, cellRect.width - pad * 2f, cellRect.height - pad * 2f);
                            DrawSpriteInRect(iconRect, so.icon);
                        }
                        else
                        {
                            string label = !string.IsNullOrEmpty(so.upgradeName) ? so.upgradeName : so.name;
                            GUI.Label(cellRect, label, cellTextStyle);
                        }
                    }

                    GUILayout.Space(gap * 0.5f);
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Space(gap);
            }

            GUILayout.EndScrollView();
        }
        finally
        {
            if (areaStarted) GUILayout.EndArea();
        }
    }
}
