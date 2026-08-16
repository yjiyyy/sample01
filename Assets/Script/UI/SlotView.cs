using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
#if TMP_PRESENT
using TMPro;
#endif

/// <summary>
/// SlotView (???? ??? ???, TMP & Legacy Text ??? ????)
/// - ???? ????:
///   Slot (RectTransform) [SlotView ???????]
///     Content (RectTransform)        <- contentRoot (optional)
///       Icon (GameObject, Image)     <- iconImage (UI Image)
///       CountText (GameObject, Text or TMP_Text) <- countTextLegacy / countTextTMP
///
/// - ????:
///   * ??????? SetData(WeaponDataSO data, int count)?? ?????? ??? ???? ?????? ?????? ????????.
///   * ??? ??????? ?????? Update???? player ??? ?????? ??????? UI?? ????????.
///   * ???????? UI Image?? ?????? ???, SpriteRenderer?? ???????? ??????.
/// </summary>
public class SlotView : MonoBehaviour
{
    [Header("UI References (Single-root simplified)")]
    [Tooltip("Icon must be a UI Image (not a SpriteRenderer).")]
    public Image iconImage;

    [Tooltip("Inactive weapon icon shown behind the active icon.")]
    public Image inactiveIconImage;

    [Tooltip("Legacy UI Text (optional). If both TMP and Legacy are present, TMP is used.")]
    public Text countTextLegacy;

    [Tooltip("TextMeshProUGUI or TMP_Text (optional). If present, it is preferred.")]
#if UNITY_2020_1_OR_NEWER
    public TMPro.TMP_Text countTextTMP;
#else
    public TMPro.TMP_Text countTextTMP; // assume TMPro exists in project; if not, leave null
#endif

    [Tooltip("Optional root content (if empty, children will be auto-scanned)")]
    public RectTransform contentRoot;

    [Header("Optional")]
    [Tooltip("Optional player controller reference (auto-find if empty)")]
    public PlayerWeaponController playerController;

    // internal cached values for detecting changes
    private string lastWeaponId = null;
    private Sprite lastIcon = null;
    private Sprite lastInactiveIcon = null;
    private int lastMagazine = -1;
    private int lastMagazineSize = -1;
    private int lastReserve = -1;
    private bool lastShowCounts = false;

    // runtime caches
    private PlayerEquipmentController equipComp;
    private WeaponBehavior weaponBehavior;
    private WeaponDataSO currentData;

    // when SetData is called by SlotContainer, externalDataMode = true and polling update won't overwrite it
    private bool externalDataMode = false;

    void Reset()
    {
        // helpful default: if this is created freshly, try to auto-bind children
        AutoBindChildren();
    }

    void Start()
    {
        // Auto-bind if user forgot to connect references in Inspector
        AutoBindChildren();
        EnsureInactiveIcon();

        if (playerController == null)
        {
            // Use explicit UnityEngine.Object to avoid ambiguity
            playerController = UnityEngine.Object.FindFirstObjectByType<PlayerWeaponController>();
        }

        if (playerController != null)
            equipComp = playerController.GetComponent<PlayerEquipmentController>();
    }

    void Update()
    {
        if (externalDataMode) return; // external data mode: do not poll

        // ensure player/equip references
        if (playerController == null)
        {
            playerController = UnityEngine.Object.FindFirstObjectByType<PlayerWeaponController>();
            if (playerController == null) return;
        }
        if (equipComp == null)
        {
            equipComp = playerController.GetComponent<PlayerEquipmentController>() ?? UnityEngine.Object.FindFirstObjectByType<PlayerEquipmentController>();
        }

        // Get runtime data
        WeaponDataSO data = equipComp != null ? equipComp.CurrentWeaponData : null;
        WeaponDataSO inactive = equipComp != null ? equipComp.InactiveWeaponData : null;
        WeaponBehavior wb = equipComp != null ? equipComp.WeaponBehavior : null;

        // Update UI from runtime values
        UpdateUIFromRuntime(data, wb);
        UpdateInactiveIcon(inactive);
    }

    // ---------- Public API for SlotContainer / external callers ----------

    // Called by inventory UI (SlotContainer) to explicitly set the slot content
    public void SetData(WeaponDataSO data, int count)
    {
        externalDataMode = true;
        currentData = data;

        // set icon
        if (iconImage != null)
        {
            iconImage.sprite = data != null ? data.icon : null;
            iconImage.enabled = data != null && data.icon != null;
        }
        if (inactiveIconImage != null)
            inactiveIconImage.enabled = false;

        bool noBullet = data != null && !string.IsNullOrEmpty(data.id) && data.id.IndexOf("NoBullet", StringComparison.OrdinalIgnoreCase) >= 0;

        bool showCounts = data != null && !noBullet;

        // update count text (explicit inventory count)
        if (showCounts)
        {
            SetCountTextActive(true);
            SetCountTextValue(count.ToString()); // <-- FIX: convert int to string
        }
        else
        {
            SetCountTextActive(false);
            SetCountTextValue(String.Empty);
        }

        // cache
        lastWeaponId = data != null ? data.id ?? "" : "";
        lastIcon = data != null ? data.icon : null;
        lastMagazine = -1; lastMagazineSize = -1; lastReserve = -1; lastShowCounts = showCounts;
    }

    // Called to clear this slot
    public void Clear()
    {
        externalDataMode = false;
        currentData = null;

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }
        if (inactiveIconImage != null)
            inactiveIconImage.enabled = false;
        SetCountTextActive(false);
        SetCountTextValue(String.Empty);

        lastWeaponId = null;
        lastIcon = null;
        lastInactiveIcon = null;
        lastMagazine = -1; lastMagazineSize = -1; lastReserve = -1; lastShowCounts = false;
    }

    // Force next Update to repaint UI
    public void ForceRefresh()
    {
        lastWeaponId = null;
        lastIcon = null;
        lastInactiveIcon = null;
        lastMagazine = -1; lastMagazineSize = -1; lastReserve = -1; lastShowCounts = false;
        externalDataMode = false;
    }

    // ---------- Internal helpers ----------

    private void EnsureInactiveIcon()
    {
        if (inactiveIconImage != null)
            return;
        if (iconImage == null)
            return;

        Transform parent = iconImage.transform.parent;
        var go = new GameObject("InactiveIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        go.transform.SetAsFirstSibling();

        var rt = go.GetComponent<RectTransform>();
        var srcRt = iconImage.rectTransform;
        rt.anchorMin = srcRt.anchorMin;
        rt.anchorMax = srcRt.anchorMax;
        rt.pivot = srcRt.pivot;
        rt.sizeDelta = srcRt.sizeDelta * 0.78f;
        rt.anchoredPosition = srcRt.anchoredPosition + new Vector2(16f, -16f);

        inactiveIconImage = go.GetComponent<Image>();
        inactiveIconImage.preserveAspect = iconImage.preserveAspect;
        inactiveIconImage.raycastTarget = false;
        inactiveIconImage.color = new Color(0.72f, 0.72f, 0.72f, 0.95f);
        inactiveIconImage.enabled = false;

        iconImage.transform.SetSiblingIndex(Mathf.Max(1, go.transform.GetSiblingIndex() + 1));
        if (countTextTMP != null)
            countTextTMP.transform.SetAsLastSibling();
        else if (countTextLegacy != null)
            countTextLegacy.transform.SetAsLastSibling();
    }

    private void UpdateInactiveIcon(WeaponDataSO inactive)
    {
        EnsureInactiveIcon();
        if (inactiveIconImage == null)
            return;

        Sprite icon = inactive != null ? inactive.icon : null;
        if (lastInactiveIcon == icon && inactiveIconImage.sprite == icon)
            return;

        lastInactiveIcon = icon;
        inactiveIconImage.sprite = icon;
        inactiveIconImage.enabled = icon != null;
    }

    private void UpdateUIFromRuntime(WeaponDataSO data, WeaponBehavior wb)
    {
        // ID / icon
        string id = data != null ? data.id ?? "" : string.Empty;
        Sprite icon = data != null ? data.icon : null;

        bool noBullet = !string.IsNullOrEmpty(id) && id.IndexOf("NoBullet", StringComparison.OrdinalIgnoreCase) >= 0;
        bool showCounts = data != null && !noBullet;

        // try to get magazine/reserve info from runtime ammo object (via WeaponBehavior or its components)
        int magSize = TryGetIntFromObject(data, new[] { "magazineSize", "magazine", "magazine_capacity", "magSize" }, defaultValue: -1);

        int curMag = -1, reserve = -1;
        object ammoObj = null;

        if (wb != null)
        {
            var wbType = wb.GetType();
            var ammoProp = wbType.GetProperty("Ammo", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (ammoProp != null)
            {
                try { ammoObj = ammoProp.GetValue(wb); } catch { ammoObj = null; }
            }

            if (ammoObj == null)
            {
                Component c = wb.GetComponent("WeaponAmmoRuntime") as Component ?? wb.GetComponent("WeaponAmmoRuntime_AR") as Component;
                if (c != null) ammoObj = c;
            }
        }

        if (ammoObj != null)
        {
            curMag = TryGetIntFromObject(ammoObj, new[] { "CurrentMagazine", "currentMagazine", "currentMagazineCount", "current_magazine" }, defaultValue: -1);
            reserve = TryGetIntFromObject(ammoObj, new[] { "CurrentReserve", "currentReserve", "CurrentAmmoReserve", "current_reserve", "CurrentAmmo" }, defaultValue: -1);
            // SO magazineSize?? ?????? ??????. ??? ?? ???? ????? ????? ???? ??X?? ???????.
            int capFromAmmo = TryGetIntFromObject(
                ammoObj,
                new[] { "EffectiveMagazineCapacity", "MagazineCapacity", "magazineSize", "magazine_capacity" },
                defaultValue: -1);
            if (capFromAmmo > 0)
                magSize = capFromAmmo;
            else if (magSize <= 0)
                magSize = TryGetIntFromObject(ammoObj, new[] { "MagazineCapacity", "magazineSize", "magazine_capacity" }, defaultValue: magSize);
        }

        if (showCounts && (curMag < 0 || magSize <= 0))
        {
            // no meaningful runtime ammo -> treat as no-bullet
            showCounts = false;
        }

        // update icon if changed
        bool iconChanged = lastIcon != icon;
        bool idChanged = lastWeaponId != id;
        bool countsChanged = lastMagazine != curMag || lastMagazineSize != magSize || lastReserve != reserve || lastShowCounts != showCounts;

        if (iconChanged || idChanged)
        {
            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }
        }

        if (showCounts)
        {
            SetCountTextActive(true);
            if (countsChanged)
            {
                string reserveStr = reserve >= 0 ? (reserve == int.MaxValue ? "??" : reserve.ToString()) : "?";
                bool infiniteReserve = TryGetBoolFromObject(data, new[] { "infiniteReserve" }, false);
                if (infiniteReserve) reserveStr = "??";

                string text = $"{(curMag >= 0 ? curMag.ToString() : "?")}/{(magSize > 0 ? magSize.ToString() : "?")} ({reserveStr})";
                SetCountTextValue(text);
            }
        }
        else
        {
            SetCountTextActive(false);
            SetCountTextValue(String.Empty);
        }

        // cache
        lastWeaponId = id;
        lastIcon = icon;
        lastMagazine = curMag;
        lastMagazineSize = magSize;
        lastReserve = reserve;
        lastShowCounts = showCounts;
    }

    // Try to auto-bind children: Image + Text/TMP
    private void AutoBindChildren()
    {
        // If explicitly assigned, do nothing
        if (iconImage != null && (countTextLegacy != null || countTextTMP != null))
            return;

        // search under contentRoot if provided, otherwise search under this.transform
        Transform root = (contentRoot != null) ? contentRoot.transform : transform;

        // find first Image component (UI) in children
        if (iconImage == null)
        {
            var imgs = root.GetComponentsInChildren<Image>(true);
            foreach (var img in imgs)
            {
                if (img == null || img == inactiveIconImage || img.name == "InactiveIcon")
                    continue;
                iconImage = img;
                break;
            }
        }

        // Prefer TMP if present
        if (countTextTMP == null)
        {
            var tmp = root.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (tmp != null) countTextTMP = tmp;
        }
        if (countTextLegacy == null)
        {
            var txt = root.GetComponentInChildren<Text>(true);
            if (txt != null) countTextLegacy = txt;
        }
    }

    // Helper to set count text active/inactive (supports both TMP and Legacy)
    private void SetCountTextActive(bool active)
    {
        if (countTextTMP != null && countTextTMP.gameObject.activeSelf != active)
            countTextTMP.gameObject.SetActive(active);
        if (countTextLegacy != null && countTextLegacy.gameObject.activeSelf != active)
            countTextLegacy.gameObject.SetActive(active);
    }

    // Helper to set count text value
    private void SetCountTextValue(string s)
    {
        if (countTextTMP != null)
        {
            try { countTextTMP.text = s; return; } catch { /* fallthrough */ }
        }
        if (countTextLegacy != null)
        {
            try { countTextLegacy.text = s; return; } catch { /* fallthrough */ }
        }
    }

    // Reflection helpers (robust)
    private static int TryGetIntFromObject(object obj, string[] names, int defaultValue)
    {
        if (obj == null) return defaultValue;
        var t = obj.GetType();
        foreach (var name in names)
        {
            var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
            if (p != null)
            {
                try
                {
                    var val = p.GetValue(obj);
                    if (val is int i) return i;
                    if (val is long l) return (int)l;
                    if (val is float f) return (int)f;
                    if (val != null && Int32.TryParse(val.ToString(), out int parsed)) return parsed;
                }
                catch { }
            }
            var fi = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
            if (fi != null)
            {
                try
                {
                    var val = fi.GetValue(obj);
                    if (val is int i2) return i2;
                    if (val is long l2) return (int)l2;
                    if (val is float f2) return (int)f2;
                    if (val != null && Int32.TryParse(val.ToString(), out int parsed)) return parsed;
                }
                catch { }
            }
        }
        return defaultValue;
    }

    private static bool TryGetBoolFromObject(object obj, string[] names, bool defaultValue)
    {
        if (obj == null) return defaultValue;
        var t = obj.GetType();
        foreach (var name in names)
        {
            var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
            if (p != null)
            {
                try
                {
                    var val = p.GetValue(obj);
                    if (val is bool b) return b;
                    if (val != null && Boolean.TryParse(val.ToString(), out bool parsed)) return parsed;
                }
                catch { }
            }
            var fi = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
            if (fi != null)
            {
                try
                {
                    var val = fi.GetValue(obj);
                    if (val is bool b2) return b2;
                    if (val != null && Boolean.TryParse(val.ToString(), out bool parsed2)) return parsed2;
                }
                catch { }
            }
        }
        return defaultValue;
    }
}