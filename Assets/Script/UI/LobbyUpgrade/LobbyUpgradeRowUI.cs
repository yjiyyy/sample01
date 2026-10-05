using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>기본 능력(흰색), 강화량(청록색), 선택(금색)을 구분합니다.</summary>
public class LobbyUpgradeRowUI : MonoBehaviour
{
    [SerializeField] private LobbyStatId statId;
    [SerializeField] private Image icon, selectionBorder, baseFill, totalFill, coin;
    [SerializeField] private TextMeshProUGUI nameLabel, valueLabel, detailLabel, levelLabel, costLabel;
    [SerializeField] private Button selectButton;
    public LobbyStatId StatId => statId;
    public static readonly Color Gold = new Color(1f, 0.82f, 0.28f);
    public static readonly Color Growth = new Color(0.45f, 0.85f, 0.83f);

    public void Setup(LobbyStatId id, Image iconImage, Image border, Image basis, Image total,
        Image coinImage, TextMeshProUGUI name, TextMeshProUGUI value, TextMeshProUGUI detail,
        TextMeshProUGUI level, TextMeshProUGUI cost, Button button)
    {
        statId = id; icon = iconImage; selectionBorder = border; baseFill = basis; totalFill = total;
        coin = coinImage; nameLabel = name; valueLabel = value; detailLabel = detail;
        levelLabel = level; costLabel = cost; selectButton = button;
        Wire();
    }

    private void OnEnable() => Wire();
    private void Wire()
    {
        if (selectButton == null) return;
        selectButton.onClick.RemoveListener(Select);
        selectButton.onClick.AddListener(Select);
    }
    private void Select() => GetComponentInParent<LobbyUpgradePanel>(true)?.SelectStat(statId);

    public void Refresh(LobbyStatUpgradeEntry entry, LobbyCharacterUpgradeSO profile, bool selected)
    {
        if (entry == null) { gameObject.SetActive(false); return; }
        if (icon != null) icon.sprite = entry.icon;
        var config = profile != null ? profile.Config : null;
        int saved = LobbyStatUpgradeState.GetLevel(profile, statId);
        int max = LobbyStatUpgradeApplier.GetEffectiveMaxLevel(entry, config);
        float basis = LobbyStatUpgradeApplier.GetConfigBase(entry, config);
        float value = LobbyStatUpgradeApplier.GetCurrentValue(entry, config, saved);
        float next = LobbyStatUpgradeApplier.GetCurrentValue(entry, config, Mathf.Min(saved, max) + 1);
        bool maxed = !LobbyStatUpgradeApplier.CanUpgrade(entry, config, saved);
        bool ko = LanguageManager.Instance == null || LanguageManager.Instance.CurrentLanguage == GameLanguage.Korean;
        nameLabel.text = entry.displayName.Get(ko ? GameLanguage.Korean : GameLanguage.English);
        valueLabel.text = selected && !maxed
            ? $"{Format(value)} <color=#FFD147>→ {Format(next)}</color>" : Format(value);
        detailLabel.text = ko
            ? $"기본 {Format(basis)} +{Format(value - basis)}  /  최대 {Format(Mathf.Max(basis, entry.maxValue))}"
            : $"Base {Format(basis)} +{Format(value - basis)}  /  Max {Format(Mathf.Max(basis, entry.maxValue))}";
        levelLabel.text = (ko ? "강화 " : "Upgrades ") + $"{Mathf.Min(saved, max)} / {max}";
        costLabel.text = maxed ? "MAX" : LobbyStatUpgradeApplier.GetNextCost(entry, saved).ToString("N0");
        costLabel.color = maxed ? new Color(0.72f, 0.75f, 0.78f) : Color.white;
        if (coin != null) coin.enabled = !maxed;
        selectionBorder.color = selected ? Gold : Color.clear;
        float end = Mathf.Max(0.0001f, basis, entry.maxValue);
        SetWidth(totalFill.rectTransform, value / end);
        SetWidth(baseFill.rectTransform, basis / end);
    }

    public static string Format(float value) => value.ToString("0.##");
    private static void SetWidth(RectTransform rect, float ratio)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
