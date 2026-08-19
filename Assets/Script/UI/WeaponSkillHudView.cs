using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI_Weapon_Skill: 지금 든 무기의 차지 공격 이름을 보여 줍니다.
/// 차지가 없으면 이 패널을 숨깁니다.
/// </summary>
[DisallowMultipleComponent]
public class WeaponSkillHudView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text skillNameText;

    [Header("Optional")]
    [SerializeField] private PlayerWeaponController playerController;

    private PlayerEquipmentController equipComp;
    private CanvasGroup canvasGroup;
    private bool subscribed;

    private void Awake()
    {
        AutoBindIfNeeded();
        EnsureCanvasGroup();
    }

    private void OnEnable()
    {
        LanguageManager.LanguageChanged += Refresh;
        EnsurePlayer();
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        LanguageManager.LanguageChanged -= Refresh;
        Unsubscribe();
    }

    private void Start()
    {
        EnsurePlayer();
        Subscribe();
        Refresh();
    }

    private void Update()
    {
        if (equipComp != null)
            return;

        EnsurePlayer();
        if (equipComp == null)
            return;

        Subscribe();
        Refresh();
    }

    private void OnWeaponChanged(WeaponDataSO _)
    {
        Refresh();
    }

    private void Refresh()
    {
        AutoBindIfNeeded();
        EnsurePlayer();

        var slot = PlayerChargeController.GetChargeSlotForCurrentWeapon(
            equipComp != null ? equipComp.CurrentWeaponData : null);

        bool show = slot != null;
        SetVisible(show);

        if (!show || skillNameText == null)
            return;

        GameLanguage language = LanguageManager.Instance != null
            ? LanguageManager.Instance.CurrentLanguage
            : GameLanguage.Korean;

        skillNameText.text = slot.displayName.Get(language);
    }

    private void SetVisible(bool visible)
    {
        EnsureCanvasGroup();
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private void EnsureCanvasGroup()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void Subscribe()
    {
        if (subscribed || equipComp == null)
            return;

        equipComp.OnWeaponChanged -= OnWeaponChanged;
        equipComp.OnWeaponChanged += OnWeaponChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (equipComp != null)
            equipComp.OnWeaponChanged -= OnWeaponChanged;
        subscribed = false;
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
        if (skillNameText != null)
            return;

        var named = FindChildByName(transform, "SkillName");
        if (named != null)
            skillNameText = named.GetComponent<TMP_Text>();

        if (skillNameText == null)
        {
            var fallback = transform.Find("Text (TMP)");
            if (fallback != null)
                skillNameText = fallback.GetComponent<TMP_Text>();
        }
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
}
