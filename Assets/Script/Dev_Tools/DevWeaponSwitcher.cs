using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
#endif

/// <summary>
/// 개발용 무기 전환 오버레이 — 인스펙터에 등록한 WeaponDataSO 순서대로 5열 그리드 표시.
/// 셀 터치 시 장착 후 창 닫기. 아이콘 없으면 weaponName 표시.
/// </summary>
public class DevWeaponSwitcher : MonoBehaviour
{
    const int DefaultColumns = 5;

    [Header("무기 목록 (인스펙터 순서)")]
    [Tooltip("표시·장착에 사용할 WeaponDataSO. null 슬롯은 빈 칸으로 표시.")]
    public List<WeaponDataSO> weapons = new List<WeaponDataSO>();

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

    [Header("그리드")]
    [Min(1)]
    public int columns = DefaultColumns;
    [Tooltip("각 칸 크기(정사각형, 픽셀)")]
    public float cellSize = 100f;
    [Tooltip("칸 사이 간격")]
    public float cellSpacing = 8f;

    [Header("디버그")]
    public bool debugTouch = false;

    private bool overlayOpen = false;
    private Vector2 scroll;

    private GUIStyle rowStyle;
    private GUIStyle headerStyle;
    private GUIStyle cellTextStyle;

    public void ToggleOverlay() => SetOverlayOpen(!overlayOpen);
    public void OpenOverlay() => SetOverlayOpen(true);
    public void CloseOverlay() => SetOverlayOpen(false);
    public bool IsOverlayOpen => overlayOpen;

    private void SetOverlayOpen(bool open)
    {
        overlayOpen = open;
        if (InputManager.Instance != null)
            InputManager.Instance.SetOverlayInputBlocked(overlayOpen);
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
            InputManager.Instance.SetOverlayInputBlocked(false);
    }

    private bool IsWeaponInInactiveSlot(WeaponDataSO so)
    {
        EnsurePlayer();
        if (so == null || targetPlayer == null)
            return false;

        var pec = targetPlayer.GetComponent<PlayerEquipmentController>();
        if (pec == null || pec.IsUnarmed(so))
            return false;

        return pec.IsSameWeapon(so, pec.InactiveWeaponData);
    }

    /// <summary>성공 시 true — 오버레이 닫기에 사용.</summary>
    private bool TryEquipAtIndex(int index)
    {
        if (weapons == null || index < 0 || index >= weapons.Count)
            return false;

        var so = weapons[index];
        if (so == null)
        {
            Debug.LogWarning("[DevWeaponSwitcher] 빈 슬롯입니다.");
            return false;
        }

        if (so.weaponPrefab == null)
        {
            Debug.LogWarning($"[DevWeaponSwitcher] weaponPrefab이 비어 있습니다: {so.weaponName}");
            return false;
        }

        EnsurePlayer();
        if (targetPlayer == null)
        {
            Debug.LogWarning("[DevWeaponSwitcher] PlayerWeaponController를 찾을 수 없습니다.");
            return false;
        }

        if (!targetPlayer.TryEquipWeaponToActiveSlot(so, out var failReason))
        {
            if (failReason == WeaponAssignFailReason.DuplicateInOtherSlot)
                Debug.LogWarning($"[DevWeaponSwitcher] 다른 슬롯에 이미 있는 무기입니다: {so.weaponName}");
            else if (failReason == WeaponAssignFailReason.InsufficientStrength)
                Debug.LogWarning($"[DevWeaponSwitcher] 근력 부족으로 장착 불가: {so.weaponName}");
            else
                Debug.LogWarning($"[DevWeaponSwitcher] 장착 실패: {so.weaponName}");
            return false;
        }

        Debug.Log($"[DevWeaponSwitcher] 장착(활성 슬롯): {so.weaponName}");
        return true;
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

    private void InitStylesIfNeeded()
    {
        if (rowStyle != null && headerStyle != null && cellTextStyle != null) return;

        var baseLabel = GUI.skin != null ? GUI.skin.label : new GUIStyle();
        var baseBox = GUI.skin != null ? GUI.skin.box : new GUIStyle();

        rowStyle = new GUIStyle(baseLabel)
        {
            fontSize = 22,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(6, 6, 6, 6)
        };

        headerStyle = new GUIStyle(baseBox)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(8, 8, 8, 8)
        };

        cellTextStyle = new GUIStyle(baseLabel)
        {
            fontSize = 16,
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

    void OnGUI()
    {
        if (!overlayOpen) return;

        InitStylesIfNeeded();

        float width = Mathf.Clamp(Screen.width * overlayWidthPercent, 240f, Screen.width - 16f);
        float height = Mathf.Clamp(Screen.height * overlayHeightPercent, 160f, Screen.height - 16f);
        float left = Mathf.Round((Screen.width - width) * 0.5f);
        float top = Mathf.Round(Screen.height * overlayTopMarginPercent);
        Rect window = new Rect(left, top, width, height);

        int colCount = Mathf.Max(1, columns);
        int count = weapons != null ? weapons.Count : 0;
        float cs = Mathf.Max(40f, cellSize);
        float gap = Mathf.Max(0f, cellSpacing);

        bool areaStarted = false;
        try
        {
            GUILayout.BeginArea(window, GUI.skin.window);
            areaStarted = true;

            var hStyle = headerStyle ?? GUI.skin.box;
            var rStyle = rowStyle ?? GUI.skin.label;
            var cStyle = cellTextStyle ?? GUI.skin.label;

            GUILayout.BeginHorizontal();
            GUILayout.Label("Dev Weapon Switcher (셀 터치=장착)", hStyle);
            if (GUILayout.Button("닫기", GUILayout.Width(90f), GUILayout.Height(40f)))
                SetOverlayOpen(false);
            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            if (count == 0)
            {
                GUILayout.Label("weapons 리스트에 WeaponDataSO를 등록하세요.", rStyle);
            }
            else
            {
                GUILayout.Label($"슬롯 수: {count} (인스펙터 순서 · 열 {colCount}개)", rStyle);
                GUILayout.Space(6);

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

                        var so = weapons[i];

                        if (GUI.Button(cellRect, GUIContent.none, GUIStyle.none))
                        {
                            if (TryEquipAtIndex(i))
                            {
                                SetOverlayOpen(false);
                                if (debugTouch) Debug.Log($"[DevWeaponSwitcher] cell click equip idx={i}");
                            }
                            else if (debugTouch)
                                Debug.Log($"[DevWeaponSwitcher] cell click failed idx={i}");
                        }

                        if (Event.current.type == EventType.Repaint)
                        {
                            GUI.Box(cellRect, GUIContent.none);

                            if (so != null)
                            {
                                if (so.icon != null)
                                {
                                    float pad = 6f;
                                    Rect iconInner = new Rect(cellRect.x + pad, cellRect.y + pad,
                                        cellRect.width - pad * 2f, cellRect.height - pad * 2f);
                                    DrawSpriteInRect(iconInner, so.icon);
                                }
                                else
                                {
                                    string label = string.IsNullOrEmpty(so.weaponName) ? "(no name)" : so.weaponName;
                                    GUI.Label(cellRect, label, cStyle);
                                }

                                if (IsWeaponInInactiveSlot(so))
                                    GUI.Box(cellRect, "다른 슬롯", cStyle);
                            }
                            else
                                GUI.Label(cellRect, "—", cStyle);
                        }

                        GUILayout.Space(gap * 0.5f);
                    }

                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.Space(gap);
                }

                GUILayout.EndScrollView();
            }
        }
        finally
        {
            if (areaStarted) GUILayout.EndArea();
        }
    }
}
