using UnityEngine;

/// <summary>
/// 업그레이드 실제 효과를 담는 베이스 SO입니다.
/// 상점 카드에 쓰는 아이콘·이름·설명도 여기에 둡니다.
/// </summary>
public abstract class UpgradeEffectSO : ScriptableObject
{
    [Header("고유 식별자 (중복 금지)")]
    public string id;

    [Header("내부 이름 (로그·디버그, 기존 값 유지)")]
    public string upgradeName;

    [Header("상점 카드 / HUD")]
    [Tooltip("카드 위 아이콘. 비어 있으면 아이콘이 안 보입니다.")]
    public Sprite icon;

    [Tooltip("상점 카드 테두리 프레임. 선택돼도 같은 그림을 쓰고, 더 밝게만 보입니다.")]
    public Sprite cardFrame;

    [Tooltip("상점에 보이는 이름. 한/영을 따로 적습니다. 비어 있으면 내부 이름을 씁니다.")]
    public LocalizedString displayName;

    [Tooltip("카드 아래 설명. 한/영을 따로 적습니다.")]
    public LocalizedString description;

    public string GetDisplayName()
    {
        return GetDisplayName(CurrentLanguage());
    }

    public string GetDisplayName(GameLanguage language)
    {
        string localized = displayName.Get(language);
        if (!string.IsNullOrEmpty(localized))
            return localized;
        return upgradeName ?? string.Empty;
    }

    public string GetDescription()
    {
        return GetDescription(CurrentLanguage());
    }

    public string GetDescription(GameLanguage language)
    {
        return description.Get(language) ?? string.Empty;
    }

    private static GameLanguage CurrentLanguage()
    {
        if (LanguageManager.Instance != null)
            return LanguageManager.Instance.CurrentLanguage;
        return GameLanguage.Korean;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(displayName.english) && !string.IsNullOrEmpty(upgradeName))
            displayName.english = upgradeName;
    }
#endif
}
