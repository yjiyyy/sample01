using System.Collections.Generic;
using UnityEngine;

public class DevWeaponSwitcher : MonoBehaviour
{
    [Header("Dev 모드 토글 키")]
    [Tooltip("BackQuote(`)로 열고/닫기")]
    public KeyCode toggleKey = KeyCode.BackQuote;

    [Header("런타임 로드 (빌드 포함)")]
    [Tooltip("Resources/Dev/WeaponRegistry.asset 를 자동 로드")]
    public string resourcesPath = "Dev/WeaponRegistry";

    [Header("대상 플레이어 (비워두면 자동 탐색)")]
    public PlayerWeaponController targetPlayer;

    [Header("빌드에서도 활성화")]
    public bool enableInBuild = true;

    // 내부 상태
    private bool overlayOpen = false;
    private Vector2 scroll;
    private int selectedIndex = 0;

    private WeaponRegistrySO registry;
    private List<WeaponRegistrySO.WeaponEntry> sorted;

    // GUI 스타일 (OnGUI에서만 초기화)
    private GUIStyle rowStyle;
    private GUIStyle selRowStyle;
    private GUIStyle headerStyle;
    private Texture2D selBgTex;

    void Awake()
    {
        // 에디터가 아니고 빌드에서 비활성 옵션이면 꺼두기
        if (!Application.isEditor && !enableInBuild)
        {
            enabled = false;
            return;
        }
    }

    void Start()
    {
        // GUI API 사용 금지: 단순 데이터 로드/참조만
        TryLoadRegistry();
        EnsurePlayer();
        // 스타일 초기화는 OnGUI에서만 수행(lazy-init)
    }

    void OnDestroy()
    {
        if (selBgTex != null)
        {
            Destroy(selBgTex);
            selBgTex = null;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            overlayOpen = !overlayOpen;

            // 오픈하는 순간 레지스트리 재시도 (처음에 못 불러왔던 경우)
            if (overlayOpen && (registry == null || sorted == null))
                TryLoadRegistry();
        }

        if (!overlayOpen) return;

        // 리스트 내 키보드 탐색
        int count = sorted != null ? sorted.Count : 0;
        if (count == 0) return;

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            selectedIndex = Mathf.Clamp(selectedIndex + 1, 0, count - 1);
            ScrollToSelected(count);
        }
        else if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            selectedIndex = Mathf.Clamp(selectedIndex - 1, 0, count - 1);
            ScrollToSelected(count);
        }
        else if (Input.GetKeyDown(KeyCode.PageDown))
        {
            selectedIndex = Mathf.Clamp(selectedIndex + 10, 0, count - 1);
            ScrollToSelected(count);
        }
        else if (Input.GetKeyDown(KeyCode.PageUp))
        {
            selectedIndex = Mathf.Clamp(selectedIndex - 10, 0, count - 1);
            ScrollToSelected(count);
        }
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            EquipSelected();
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            overlayOpen = false;
        }
    }

    private void ScrollToSelected(int count)
    {
        // 간단 스크롤 보정: 선택 이동 시 약간 내려줌
        float itemHeight = 24f;
        float viewCount = 16f;
        float top = scroll.y / itemHeight;
        float bottom = top + viewCount;

        if (selectedIndex < top) scroll.y = selectedIndex * itemHeight;
        else if (selectedIndex > bottom) scroll.y = (selectedIndex - viewCount) * itemHeight;
    }

    private void EquipSelected()
    {
        if (sorted == null || sorted.Count == 0) return;
        if (selectedIndex < 0 || selectedIndex >= sorted.Count) return;

        var entry = sorted[selectedIndex];
        if (entry == null || entry.prefab == null)
        {
            Debug.LogWarning("[DevWeaponSwitcher] 선택한 항목의 prefab이 비어있습니다.");
            return;
        }

        EnsurePlayer();
        if (targetPlayer == null)
        {
            Debug.LogWarning("[DevWeaponSwitcher] PlayerWeaponController를 찾지 못했습니다.");
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
            Debug.LogWarning($"[DevWeaponSwitcher] 레지스트리를 찾지 못했습니다. Resources/{resourcesPath}.asset 를 생성하세요. (Dev/Build Weapon Registry 메뉴)");
            sorted = new List<WeaponRegistrySO.WeaponEntry>();
            return;
        }

        var ro = registry.GetSortedEntries();
        sorted = new List<WeaponRegistrySO.WeaponEntry>(ro);
        selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, sorted.Count - 1));
    }

    // GUI 관련 초기화: 반드시 OnGUI 내부에서만 호출!
    private void InitStylesIfNeeded()
    {
        if (rowStyle != null && selRowStyle != null && headerStyle != null) return;

        // 기본 스타일 폴백
        var baseLabel = GUI.skin != null ? GUI.skin.label : new GUIStyle();
        var baseBox = GUI.skin != null ? GUI.skin.box : new GUIStyle();

        rowStyle = new GUIStyle(baseLabel)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleLeft
        };

        selRowStyle = new GUIStyle(rowStyle);
        selRowStyle.normal.textColor = Color.black;

        if (selBgTex == null)
        {
            selBgTex = MakeTex(1, 1, new Color(1f, 0.8f, 0.2f, 1f));
            selBgTex.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            selBgTex.wrapMode = TextureWrapMode.Clamp;
            selBgTex.filterMode = FilterMode.Point;
        }
        selRowStyle.normal.background = selBgTex;

        headerStyle = new GUIStyle(baseBox)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(8, 8, 4, 4)
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

        // 안전: 스타일 lazy-init (OnGUI 내부에서만 호출)
        InitStylesIfNeeded();

        const float width = 420f;
        const float height = 520f;
        Rect window = new Rect(16, 16, width, height);

        bool areaStarted = false;
        try
        {
            GUILayout.BeginArea(window, GUI.skin.window);
            areaStarted = true;

            var hStyle = headerStyle ?? GUI.skin.box;
            var rStyle = rowStyle ?? GUI.skin.label;
            var sStyle = selRowStyle ?? rStyle;

            GUILayout.Label("Dev Weapon Switcher (BackQuote ` to close)", hStyle);
            GUILayout.Space(4);

            if (registry == null)
            {
                GUILayout.Label("레지스트리가 없습니다.\n메뉴: Dev/Build Weapon Registry 실행 후 다시 시도하세요.", rStyle);
                return;
            }

            int count = sorted != null ? sorted.Count : 0;
            GUILayout.Label($"무기 수: {count}  |  ↑/↓, PageUp/Down, Enter=장착, Esc=닫기", rStyle);
            GUILayout.Space(6);

            bool scrollStarted = false;
            try
            {
                scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                scrollStarted = true;

                if (count == 0)
                {
                    GUILayout.Label("등록된 무기가 없습니다.", rStyle);
                }
                else
                {
                    for (int i = 0; i < count; i++)
                    {
                        var e = sorted[i];
                        if (e == null) continue;

                        bool selected = (i == selectedIndex);

                        GUILayout.BeginHorizontal(selected ? sStyle : rStyle);

                        if (GUILayout.Button(selected ? "▶" : " ", GUILayout.Width(24)))
                        {
                            selectedIndex = i;
                            EquipSelected();
                        }

                        GUILayout.Label(e.displayName ?? "(no-name)", GUILayout.ExpandWidth(true));

                        if (GUILayout.Button("Equip", GUILayout.Width(64)))
                        {
                            selectedIndex = i;
                            EquipSelected();
                        }

                        GUILayout.EndHorizontal();
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