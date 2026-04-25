using UnityEngine;

/// <summary>
/// 업그레이드 실제 효과를 담는 베이스 SO입니다.
/// </summary>
public abstract class UpgradeEffectSO : ScriptableObject
{
    [Header("고유 식별자 (중복 금지)")]
    public string id;

    [Header("업그레이드 이름")]
    public string upgradeName;

    [Header("아이콘")]
    public Sprite icon;
}
