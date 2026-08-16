using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI_Weapon HUD: 앞칸(활성) 아이콘+탄창/최대, 뒤칸(비활성) 아이콘만.
/// 장전 중에는 Text_Reload 깜빡임 + 가로 반투명 막, 총알이 완전히 없으면 Text_NO AMMO.
/// 영역 전체 터치 또는 순환 버튼으로 슬롯 교체.
/// </summary>
public class WeaponHudView : MonoBehaviour, IPointerClickHandler
{
    private enum AmmoHudMode
    {
        Unset,
        Hidden,
        Ammo,
        Reloading,
        Empty
    }

    [Header("Active (front)")]
    public Image activeIcon;
    public TMP_Text ammoText;
    public TMP_Text reloadText;
    public TMP_Text emptyText;
    public Image reloadCover;

    [Header("Inactive (back)")]
    public Image inactiveIcon;

    [Header("Switch")]
    public Button switchButton;

    [Header("Optional")]
    public PlayerWeaponController playerController;

    private PlayerEquipmentController equipComp;
    private Sprite lastActiveIcon;
    private Sprite lastInactiveIcon;
    private string lastAmmoText;
    private AmmoHudMode lastMode = AmmoHudMode.Unset;
    private Color reloadBaseColor = Color.white;
    private float reloadCoverDuration = 1f;
    private float lastReloadRemaining;

    private const float StatusBlinkPeriod = 0.4f;
    private static readonly Color InactiveIconTint = new Color(0.78f, 0.78f, 0.78f, 1f);
    private static readonly Color ReloadCoverColor = new Color(0.05f, 0.05f, 0.05f, 0.55f);
    private static readonly Color EmptyCoverColor = new Color(0.05f, 0.05f, 0.05f, 0.55f);
    private static Sprite whiteSprite;

    void Start()
    {
        AutoBindIfNeeded();
        CacheReloadDefaults();
        HideStatusTexts();
        SetReloadCoverVisible(false);

        if (switchButton != null)
        {
            switchButton.onClick.RemoveListener(OnSwitchClicked);
            switchButton.onClick.AddListener(OnSwitchClicked);
        }

        EnsurePlayer();
    }

    void OnDestroy()
    {
        if (switchButton != null)
            switchButton.onClick.RemoveListener(OnSwitchClicked);
    }

    void Update()
    {
        EnsurePlayer();
        if (equipComp == null)
            return;

        ApplyIcons(equipComp.CurrentWeaponData, equipComp.InactiveWeaponData);
        ApplyAmmo(equipComp.CurrentWeaponData, equipComp.WeaponBehavior);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnSwitchClicked();
    }

    private void OnSwitchClicked()
    {
        EnsurePlayer();
        playerController?.TrySwitchWeaponSlot();
    }

    private void ApplyIcons(WeaponDataSO active, WeaponDataSO inactive)
    {
        Sprite activeSprite = active != null ? active.icon : null;
        if (activeIcon != null && lastActiveIcon != activeSprite)
        {
            lastActiveIcon = activeSprite;
            activeIcon.sprite = activeSprite;
            activeIcon.enabled = activeSprite != null;
            activeIcon.color = Color.white;
        }

        Sprite inactiveSprite = inactive != null ? inactive.icon : null;
        if (inactiveIcon != null && lastInactiveIcon != inactiveSprite)
        {
            lastInactiveIcon = inactiveSprite;
            inactiveIcon.sprite = inactiveSprite;
            inactiveIcon.enabled = inactiveSprite != null;
            inactiveIcon.color = InactiveIconTint;
        }
    }

    private void ApplyAmmo(WeaponDataSO data, WeaponBehavior wb)
    {
        AmmoHudMode mode = AmmoHudMode.Hidden;
        int mag = 0;
        int cap = 0;
        float reloadRemaining = 0f;

        if (data != null && wb != null && !IsNoBulletWeapon(data) &&
            TryReadAmmo(wb, out mag, out cap, out bool reloading, out bool empty, out reloadRemaining))
        {
            if (cap > 0)
            {
                if (reloading)
                    mode = AmmoHudMode.Reloading;
                else if (empty)
                    mode = AmmoHudMode.Empty;
                else
                    mode = AmmoHudMode.Ammo;
            }
        }

        if (mode != lastMode)
        {
            lastMode = mode;
            SetAmmoVisible(mode == AmmoHudMode.Ammo);
            SetTextVisible(reloadText, mode == AmmoHudMode.Reloading);
            SetTextVisible(emptyText, mode == AmmoHudMode.Empty);
            SetReloadCoverVisible(mode == AmmoHudMode.Reloading || mode == AmmoHudMode.Empty);

            if (mode == AmmoHudMode.Reloading)
            {
                ApplyCoverStyle(ReloadCoverColor);
                BeginReloadCover(reloadRemaining);
            }
            else if (mode == AmmoHudMode.Empty)
            {
                ApplyCoverStyle(EmptyCoverColor);
                if (reloadCover != null)
                    reloadCover.fillAmount = 1f;
            }

            if (mode != AmmoHudMode.Reloading)
                SetReloadAlpha(1f);
        }

        if (mode == AmmoHudMode.Reloading)
        {
            float t = Mathf.PingPong(Time.time * (2f / StatusBlinkPeriod), 1f);
            SetReloadAlpha(Mathf.Lerp(0.25f, 1f, t));
            UpdateReloadCover(reloadRemaining);
        }

        if (mode != AmmoHudMode.Ammo)
            return;

        string text = mag + "/" + cap;
        if (text != lastAmmoText)
        {
            lastAmmoText = text;
            if (ammoText != null)
                ammoText.text = text;
        }
    }

    private static bool TryReadAmmo(WeaponBehavior wb, out int mag, out int cap, out bool reloading, out bool empty, out float reloadRemaining)
    {
        mag = 0;
        cap = 0;
        reloading = false;
        empty = false;
        reloadRemaining = 0f;

        var gunAmmo = wb.GetComponent<WeaponAmmoRuntime>();
        if (gunAmmo != null && gunAmmo.IsInitialized)
        {
            mag = gunAmmo.CurrentMagazine;
            cap = gunAmmo.EffectiveMagazineCapacity;
            reloading = gunAmmo.IsReloading;
            empty = gunAmmo.IsMagazineEmpty() && !gunAmmo.HasAnyReserveOrInfinite();
            reloadRemaining = gunAmmo.GetReloadRemaining();
            return true;
        }

        var arAmmo = wb.GetComponent<WeaponAmmoRuntime_AR>();
        if (arAmmo != null && arAmmo.IsInitialized)
        {
            mag = arAmmo.CurrentMagazine;
            cap = arAmmo.EffectiveMagazineCapacity;
            reloading = arAmmo.IsReloading;
            empty = arAmmo.IsMagazineEmpty() && !arAmmo.HasAnyReserveOrInfinite();
            reloadRemaining = arAmmo.GetReloadRemaining();
            return true;
        }

        return false;
    }

    private void ApplyCoverStyle(Color color)
    {
        if (reloadCover != null)
            reloadCover.color = color;
    }

    private void BeginReloadCover(float remaining)
    {
        reloadCoverDuration = Mathf.Max(0.01f, remaining);
        lastReloadRemaining = remaining;
        if (reloadCover != null)
            reloadCover.fillAmount = 1f;
    }

    private void UpdateReloadCover(float remaining)
    {
        if (reloadCover == null)
            return;

        if (remaining > lastReloadRemaining + 0.05f)
            reloadCoverDuration = Mathf.Max(0.01f, remaining);

        lastReloadRemaining = remaining;
        reloadCover.fillAmount = Mathf.Clamp01(remaining / reloadCoverDuration);
    }

    private void SetReloadCoverVisible(bool visible)
    {
        if (reloadCover != null)
            reloadCover.gameObject.SetActive(visible);
    }

    private void SetAmmoVisible(bool visible)
    {
        if (ammoText != null)
            ammoText.gameObject.SetActive(visible);
    }

    private static void SetTextVisible(TMP_Text text, bool visible)
    {
        if (text != null)
            text.gameObject.SetActive(visible);
    }

    private void HideStatusTexts()
    {
        SetTextVisible(reloadText, false);
        SetTextVisible(emptyText, false);
    }

    private void SetReloadAlpha(float alpha)
    {
        if (reloadText == null)
            return;
        Color c = reloadBaseColor;
        c.a = alpha;
        reloadText.color = c;
    }

    private static bool IsNoBulletWeapon(WeaponDataSO data)
    {
        if (data == null)
            return true;
        if (PlayerConfig.IsUnarmedAsset(data))
            return true;
        return !string.IsNullOrEmpty(data.id) &&
               data.id.IndexOf("NoBullet", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void EnsurePlayer()
    {
        if (playerController == null)
        {
            if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
                playerController = GameManager.Instance.playerTransform.GetComponent<PlayerWeaponController>();
            if (playerController == null)
                playerController = FindFirstObjectByType<PlayerWeaponController>();
        }

        if (equipComp == null && playerController != null)
            equipComp = playerController.GetComponent<PlayerEquipmentController>();
    }

    private void AutoBindIfNeeded()
    {
        var gold = FindChildByName(transform, "Frame_GoldBullet");

        if (activeIcon == null && gold != null)
            activeIcon = FindChildComponent<Image>(gold, "ItemIcon");

        if (ammoText == null && gold != null)
        {
            ammoText = FindChildComponent<TMP_Text>(gold, "Text_Bullet");
            if (ammoText == null)
                ammoText = FindChildComponent<TMP_Text>(gold, "Text (TMP)");
        }

        if (reloadText == null)
            reloadText = FindNamedText(gold, "Text_Reload");
        if (emptyText == null)
        {
            emptyText = FindNamedText(gold, "Text_NO AMMO");
            if (emptyText == null)
                emptyText = FindNamedText(gold, "Text_Empty");
        }

        if (gold != null)
            EnsureReloadCover(gold);

        var bronze = FindChildByName(transform, "Frame_BronzeBullet");
        if (bronze != null)
        {
            if (inactiveIcon == null)
                inactiveIcon = FindChildComponent<Image>(bronze, "ItemIcon");

            var extraAmmoIcon = FindChildByName(bronze, "Icon");
            if (extraAmmoIcon != null)
                extraAmmoIcon.gameObject.SetActive(false);
        }

        if (switchButton == null)
        {
            var btn = FindChildByName(transform, "Btn_Switch");
            if (btn == null && transform.parent != null)
                btn = FindChildByName(transform.parent, "Btn_Switch");
            if (btn != null)
                switchButton = btn.GetComponent<Button>();
        }
    }

    private void EnsureReloadCover(Transform gold)
    {
        var mask = gold.GetComponent<Mask>();
        if (mask == null)
            mask = gold.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        if (reloadCover == null)
        {
            var existing = FindChildByName(gold, "Image_ReloadCover");
            if (existing != null)
                reloadCover = existing.GetComponent<Image>();
        }

        if (reloadCover == null)
        {
            var go = new GameObject("Image_ReloadCover", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = gold.gameObject.layer;
            go.transform.SetParent(gold, false);
            reloadCover = go.GetComponent<Image>();
        }

        var rt = reloadCover.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;

        int iconIndex = activeIcon != null ? activeIcon.transform.GetSiblingIndex() : 0;
        reloadCover.transform.SetSiblingIndex(iconIndex + 1);

        reloadCover.sprite = GetWhiteSprite();
        reloadCover.color = ReloadCoverColor;
        reloadCover.type = Image.Type.Filled;
        reloadCover.fillMethod = Image.FillMethod.Horizontal;
        reloadCover.fillOrigin = (int)Image.OriginHorizontal.Left;
        reloadCover.fillAmount = 1f;
        reloadCover.raycastTarget = false;
        reloadCover.maskable = true;
        reloadCover.preserveAspect = false;
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite == null)
        {
            var tex = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            whiteSprite.hideFlags = HideFlags.HideAndDontSave;
        }
        return whiteSprite;
    }

    private TMP_Text FindNamedText(Transform gold, string name)
    {
        TMP_Text found = null;
        if (gold != null)
            found = FindChildComponent<TMP_Text>(gold, name);
        if (found == null)
            found = FindChildComponent<TMP_Text>(transform, name);
        if (found == null && transform.parent != null)
            found = FindChildComponent<TMP_Text>(transform.parent, name);
        return found;
    }

    private void CacheReloadDefaults()
    {
        if (reloadText == null)
            return;

        reloadBaseColor = reloadText.color;
        reloadBaseColor.a = 1f;
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        if (root == null)
            return null;
        if (root.name == name)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChildByName(root.GetChild(i), name);
            if (found != null)
                return found;
        }
        return null;
    }

    private static T FindChildComponent<T>(Transform root, string name) where T : Component
    {
        var t = FindChildByName(root, name);
        return t != null ? t.GetComponent<T>() : null;
    }
}
