using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
#endif

/// <summary>
/// DevWeaponSwitcher (수정)
/// - 라벨 영역 터치 = 선택
/// - 우측 Equip 버튼 터치 = 장착 + 오버레이 닫기
/// - 클릭 판정은 OnGUI 내부(Event.current)에서 처리하여 시뮬레이터에서 좌표 어긋남 문제 해결
/// - 텍스트/버튼/행 크기 확대(대략 2배)
/// - 오버레이가 열리면 InputManager.Instance.OverlayInputBlocked = true
/// </summary>
public class DevWeaponSwitcher : MonoBehaviour
{
    [Header("개발자용 오버레이 키")]
    [Tooltip("BackQuote(`) 키로 열기/닫기")]
    public KeyCode toggleKey = KeyCode.BackQuote;

    [Header("리소스 경로 (빌드용)")]
    [Tooltip("Resources/Dev/WeaponRegistry.asset 위치")]
    public string resourcesPath = "Dev/WeaponRegistry";

    [Header("대상 플레이어 (빌드에서 참조)")]
    public PlayerWeaponController targetPlayer;

    [Header("빌드에서 활성화 여부")]
    public bool enableInBuild = true;

    [Header("오버레이 화면 비율")]
    [Range(0.2f, 1f)]
    public float overlayWidthPercent = 0.9f;
    [Range(0.2f, 1f)]
    public float overlayHeightPercent = 0.8f;
    [Range(0f, 0.5f)]
    public float overlayTopMarginPercent = 0.05f;

    [Header("레이아웃")]
    [Tooltip("목록 행 높이 (기본값을 2배 정도 키움)")]
    public float itemHeight = 72f; // 이전 36 -> 72 (약 2배)

    [Header("디버그")]
    public bool debugTouch = false;

    // 내부 상태
    private bool overlayOpen = false;
    private Vector2 scroll;
    private int selectedIndex = 0;

    private WeaponRegistrySO registry;
    private List<WeaponRegistrySO.WeaponEntry> sorted;

    // GUI 스타일 캐시
    private GUIStyle rowStyle;
    private GUIStyle selRowStyle;
    private GUIStyle headerStyle;
    private GUIStyle buttonStyle;
    private Texture2D selBgTex;

    // OnGUI에서 수집(선택 영역/버튼 영역) - 로컬 좌표(윈도우 내부 기준) 대신 OnGUI에서 직접 Event.current로 처리하므로
    // 여기서는 디버깅용으로 유지할 수 있음(필요시 사용)
    private List<Rect> itemLabelScreenRects = new List<Rect>();
    private List<Rect> equipButtonScreenRects = new List<Rect>();

    // Public API
    public void ToggleOverlay() => SetOverlayOpen(!overlayOpen);
    public void OpenOverlay() => SetOverlayOpen(true);
    public void CloseOverlay() => SetOverlayOpen(false);
    public bool IsOverlayOpen => overlayOpen;

    private void SetOverlayOpen(bool open)
    {
        overlayOpen = open;
        if (overlayOpen && (registry == null || sorted == null))
            TryLoadRegistry();

        if (InputManager.Instance != null)
            InputManager.Instance.OverlayInputBlocked = overlayOpen;
    }

    void Awake()
    {
        if (!Application.isEditor && !enableInBuild)
        {
            enabled = false;
            return;
        }
    }

    void Start()
    {
        TryLoadRegistry();
        EnsurePlayer();

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        try { EnhancedTouchSupport.Enable(); } catch { }
#endif
    }

    void OnDestroy()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        try { EnhancedTouchSupport.Disable(); } catch { }
#endif
        if (InputManager.Instance != null)
            InputManager.Instance.OverlayInputBlocked = false;

        if (selBgTex != null)
        {
            Destroy(selBgTex);
            selBgTex = null;
        }
    }

    void Update()
    {
        if (InputManager.Instance == null) return;

        // 키 토글 (기존 유지)
        if (InputManager.Instance.GetKeyDown(toggleKey))
        {
            SetOverlayOpen(!overlayOpen);
        }

        if (!overlayOpen) return;

        int count = sorted != null ? sorted.Count : 0;
        if (count == 0) return;

        // 키보드 네비/선택 (기존)
        if (InputManager.Instance.GetKeyDown(KeyCode.DownArrow))
        {
            selectedIndex = Mathf.Clamp(selectedIndex + 1, 0, count - 1);
            ScrollToSelected(count);
        }
        else if (InputManager.Instance.GetKeyDown(KeyCode.UpArrow))
        {
            selectedIndex = Mathf.Clamp(selectedIndex - 1, 0, count - 1);
            ScrollToSelected(count);
        }
        else if (InputManager.Instance.GetKeyDown(KeyCode.PageDown))
        {
            selectedIndex = Mathf.Clamp(selectedIndex + 10, 0, count - 1);
            ScrollToSelected(count);
        }
        else if (InputManager.Instance.GetKeyDown(KeyCode.PageUp))
        {
            selectedIndex = Mathf.Clamp(selectedIndex - 10, 0, count - 1);
            ScrollToSelected(count);
        }
        else if (InputManager.Instance.GetKeyDown(KeyCode.Return) || InputManager.Instance.GetKeyDown(KeyCode.KeypadEnter))
        {
            EquipSelected();
            SetOverlayOpen(false); // Enter는 장착+닫기
        }
        else if (InputManager.Instance.GetKeyDown(KeyCode.Escape))
        {
            SetOverlayOpen(false);
        }

        // 기존 Update 기반 포인터 처리는 시뮬레이터/좌표 문제 때문에 더 이상 주된 클릭 경로가 아님.
        // OnGUI(Event.current)에서 직접 처리하므로 Update에서는 추가 처리를 하지 않음.
    }

    private void ScrollToSelected(int count)
    {
        float ih = itemHeight;
        float viewCount = Mathf.Max(1f, Mathf.Floor(((Screen.height * overlayHeightPercent) - 80f) / ih));
        float top = scroll.y / ih;
        float bottom = top + viewCount;

        if (selectedIndex < top) scroll.y = selectedIndex * ih;
        else if (selectedIndex > bottom) scroll.y = (selectedIndex - viewCount) * ih;
    }

    private void EquipSelected()
    {
        if (sorted == null || sorted.Count == 0) return;
        if (selectedIndex < 0 || selectedIndex >= sorted.Count) return;

        var entry = sorted[selectedIndex];
        if (entry == null || entry.prefab == null)
        {
            Debug.LogWarning("[DevWeaponSwitcher] 선택한 항목의 prefab이 비어 있습니다.");
            return;
        }

        EnsurePlayer();
        if (targetPlayer == null)
        {
            Debug.LogWarning("[DevWeaponSwitcher] PlayerWeaponController를 찾을 수 없습니다.");
            return;
        }

        targetPlayer.EquipWeapon(entry.prefab);
        Debug.Log($"[DevWeaponSwitcher] 장착: {entry.displayName}");
    }

    private void EnsurePlayer()
    {
        if (targetPlayer != null) return;

        if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
        {
            targetPlayer = GameManager.Instance.playerTransform.GetComponent<PlayerWeaponController>();
            if (targetPlayer != null) return;
        }

        targetPlayer = FindFirstObjectByType<PlayerWeaponController>();
    }

    private void TryLoadRegistry()
    {
        registry = Resources.Load<WeaponRegistrySO>(resourcesPath);
        if (registry == null)
        {
            Debug.LogWarning($"[DevWeaponSwitcher] 무기 레지스트리를 찾을 수 없습니다. Resources/{resourcesPath}.asset 경로를 확인하세요.");
            sorted = new List<WeaponRegistrySO.WeaponEntry>();
            return;
        }

        var ro = registry.GetSortedEntries();
        sorted = new List<WeaponRegistrySO.WeaponEntry>(ro);
        selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, sorted.Count - 1));
    }

    // 스타일 초기화
    private void InitStylesIfNeeded()
    {
        if (rowStyle != null && selRowStyle != null && headerStyle != null && buttonStyle != null) return;

        var baseLabel = GUI.skin != null ? GUI.skin.label : new GUIStyle();
        var baseBox = GUI.skin != null ? GUI.skin.box : new GUIStyle();
        var baseButton = GUI.skin != null ? GUI.skin.button : new GUIStyle();

        rowStyle = new GUIStyle(baseLabel)
        {
            fontSize = 28, // 이전 14 -> 28 (약 2배)
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(6, 6, 6, 6)
        };

        selRowStyle = new GUIStyle(rowStyle);
        selRowStyle.normal.textColor = Color.black;

        buttonStyle = new GUIStyle(baseButton)
        {
            fontSize = 22, // 버튼 폰트 키움
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(6, 6, 6, 6)
        };

        if (selBgTex == null)
        {
            selBgTex = MakeTex(1, 1, new Color(1f, 0.85f, 0.25f, 1f));
            selBgTex.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            selBgTex.wrapMode = TextureWrapMode.Clamp;
            selBgTex.filterMode = FilterMode.Point;
        }
        selRowStyle.normal.background = selBgTex;

        headerStyle = new GUIStyle(baseBox)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(8, 8, 8, 8)
        };
    }

    private Texture2D MakeTex(int w, int h, Color c)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var pix = new Color[w * h];
        for (int i = 0; i < pix.Length; i++) pix[i] = c;
        tex.SetPixels(pix);
        tex.Apply(false, false);
        return tex;
    }

    void OnGUI()
    {
        if (!overlayOpen) return;

        InitStylesIfNeeded();

        float width = Mathf.Clamp(Screen.width * overlayWidthPercent, 240f, Screen.width - 16f);
        float height = Mathf.Clamp(Screen.height * overlayHeightPercent, 160f, Screen.height - 16f);
        float left = Mathf.Round((Screen.width - width) * 0.5f);
        float top = Mathf.Round(Screen.height * overlayTopMarginPercent);
        Rect window = new Rect(left, top, width, height);

        // 매 프레임 초기화
        itemLabelScreenRects.Clear();
        equipButtonScreenRects.Clear();

        bool areaStarted = false;
        try
        {
            GUILayout.BeginArea(window, GUI.skin.window);
            areaStarted = true;

            var hStyle = headerStyle ?? GUI.skin.box;
            var rStyle = rowStyle ?? GUI.skin.label;
            var sStyle = selRowStyle ?? rStyle;

            GUILayout.Label("Dev Weapon Switcher (BackQuote ` 로 닫기/열기)", hStyle);
            GUILayout.Space(8);

            if (registry == null)
            {
                GUILayout.Label("무기 레지스트리가 비어 있습니다.\n오류 해결: Dev/Build Weapon Registry 메뉴를 실행하세요.", rStyle);
                GUILayout.EndArea();
                return;
            }

            int count = sorted != null ? sorted.Count : 0;
            GUILayout.Label($"무기 수: {count}  |  라벨=선택, Equip 버튼=장착", rStyle);
            GUILayout.Space(6);

            bool scrollStarted = false;
            try
            {
                // 스크롤 뷰 내부에서 각 로컬 rect를 얻음
                scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                scrollStarted = true;

                if (count == 0)
                {
                    GUILayout.Label("표시할 항목이 없습니다.", rStyle);
                }
                else
                {
                    Event evt = Event.current;
                    bool didConsumeEvent = false;

                    for (int i = 0; i < count; i++)
                    {
                        var e = sorted[i];
                        if (e == null) continue;

                        bool selected = (i == selectedIndex);

                        GUILayout.BeginHorizontal(selected ? sStyle : rStyle);

                        // label 지역 rect (확인용)
                        Rect labelLocalRect = GUILayoutUtility.GetRect(new GUIContent(e.displayName ?? "(no-name)"), selected ? sStyle : rStyle, GUILayout.ExpandWidth(true), GUILayout.Height(itemHeight));
                        // equip 버튼 지역 rect
                        Rect equipLocalRect = GUILayoutUtility.GetRect(120f, itemHeight, GUILayout.Width(120f), GUILayout.Height(itemHeight)); // 버튼 폭을 늘림

                        // IMGUI 버튼/라벨 그리기 (마우스/키보드로도 동작)
                        if (GUI.Button(labelLocalRect, GUIContent.none, GUIStyle.none))
                        {
                            selectedIndex = i;
                        }

                        if (GUI.Button(equipLocalRect, "Equip", buttonStyle))
                        {
                            selectedIndex = i;
                            EquipSelected();
                            SetOverlayOpen(false);
                        }

                        // 배경 표시 (선택)
                        Rect combinedRect = new Rect(labelLocalRect.x, labelLocalRect.y, labelLocalRect.width + equipLocalRect.width, labelLocalRect.height);
                        if (selected) GUI.Box(combinedRect, GUIContent.none, selRowStyle);
                        else GUI.Box(combinedRect, GUIContent.none, rowStyle);

                        // 텍스트
                        Rect textRect = new Rect(labelLocalRect.x + 8f, labelLocalRect.y, labelLocalRect.width - 8f, labelLocalRect.height);
                        GUI.Label(textRect, e.displayName ?? "(no-name)", selected ? selRowStyle : rowStyle);

                        GUILayout.EndHorizontal();

                        // OnGUI 내부: Event.current의 mousePosition은 해당 영역(현재 영역) 기준 좌표이므로
                        // 로컬 rect들과 바로 비교 가능.
                        if (!didConsumeEvent && evt != null && evt.type == EventType.MouseDown && evt.button == 0)
                        {
                            Vector2 localMouse = evt.mousePosition;
                            // Note: mousePosition is relative to the current GUILayout area (BeginArea(window)).
                            // labelLocalRect/equipLocalRect are also in that space.
                            if (equipLocalRect.Contains(localMouse))
                            {
                                // Equip 영역 클릭: 장착 + 오버레이 닫기
                                selectedIndex = i;
                                EquipSelected();
                                SetOverlayOpen(false);
                                evt.Use();
                                didConsumeEvent = true;
                                if (debugTouch) Debug.Log($"[DevWeaponSwitcher] OnGUI Equip click idx={i}");
                            }
                            else if (labelLocalRect.Contains(localMouse))
                            {
                                // 라벨 클릭: 선택만
                                selectedIndex = i;
                                ScrollToSelected(count);
                                evt.Use();
                                didConsumeEvent = true;
                                if (debugTouch) Debug.Log($"[DevWeaponSwitcher] OnGUI Label click idx={i}");
                            }
                        }

                        // (디버그용) 스크린 좌표 저장 — 필요시 Update 기반 판정에 사용
                        Rect labelScreenRect = new Rect(window.x + labelLocalRect.x, window.y + labelLocalRect.y, labelLocalRect.width, labelLocalRect.height);
                        Rect equipScreenRect = new Rect(window.x + equipLocalRect.x, window.y + equipLocalRect.y, equipLocalRect.width, equipLocalRect.height);
                        itemLabelScreenRects.Add(labelScreenRect);
                        equipButtonScreenRects.Add(equipScreenRect);
                    }
                }
            }
            finally
            {
                if (scrollStarted) GUILayout.EndScrollView();
            }
        }
        finally
        {
            if (areaStarted) GUILayout.EndArea();
        }
    }
}