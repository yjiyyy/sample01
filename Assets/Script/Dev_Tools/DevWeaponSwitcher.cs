using System.Collections.Generic;
using UnityEngine;

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
    private Texture2D selBgTex;

    void Awake()
    {
        // 빌드에서 비활성화 설정이면 컴포넌트 자체 비활성화
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
        // OnGUI에서 스타일을 lazy-init으로 생성
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
        if (InputManager.Instance == null) return;

        // 토글 키 처리
        if (InputManager.Instance.GetKeyDown(toggleKey))
        {
            overlayOpen = !overlayOpen;

            // 오버레이를 여는 순간 레지스트리가 비어있으면 로드
            if (overlayOpen && (registry == null || sorted == null))
                TryLoadRegistry();
        }

        if (!overlayOpen) return;

        int count = sorted != null ? sorted.Count : 0;
        if (count == 0) return;

        // 키 입력으로 목록 이동/선택
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
        }
        else if (InputManager.Instance.GetKeyDown(KeyCode.Escape))
        {
            overlayOpen = false;
        }
    }

    private void ScrollToSelected(int count)
    {
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

    // GUI 스타일 초기화 (OnGUI lazy-init)
    private void InitStylesIfNeeded()
    {
        if (rowStyle != null && selRowStyle != null && headerStyle != null) return;

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

            GUILayout.Label("Dev Weapon Switcher (BackQuote ` 로 닫기/열기)", hStyle);
            GUILayout.Space(4);

            if (registry == null)
            {
                GUILayout.Label("무기 레지스트리가 비어 있습니다.\n오류 해결: Dev/Build Weapon Registry 메뉴를 실행하세요.", rStyle);
                return;
            }

            int count = sorted != null ? sorted.Count : 0;
            GUILayout.Label($"무기 수: {count}  |  화살표/페이지업/다운, Enter=장착, Esc=닫기", rStyle);
            GUILayout.Space(6);

            bool scrollStarted = false;
            try
            {
                scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                scrollStarted = true;

                if (count == 0)
                {
                    GUILayout.Label("표시할 항목이 없습니다.", rStyle);
                }
                else
                {
                    for (int i = 0; i < count; i++)
                    {
                        var e = sorted[i];
                        if (e == null) continue;

                        bool selected = (i == selectedIndex);

                        GUILayout.BeginHorizontal(selected ? sStyle : rStyle);

                        if (GUILayout.Button(selected ? "선택" : " ", GUILayout.Width(48)))
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