using UnityEngine;

[CreateAssetMenu(menuName = "Weapon/Melee (None Charged)")]
public class WeaponDataSO_NoneCharged : WeaponDataSO_Melee
{
    [Header("Charged Attack")]
    [Tooltip("이 시간 이상 누르고 떼야 발동(미만은 실패)")]
    public float minHoldTime = 0.5f;

    [Tooltip("Animator에 재생할 상태 이름(클립이 지정되면 클립 이름이 우선)")]
    public string chargedStateName = "Attack_Charged01";

    [Tooltip("선택: 지정하면 이 클립 이름으로 재생합니다.")]
    public AnimationClip chargedClip;

    [Header("무적 판정(발동 시)")]
    [Tooltip("차지 공격이 발동하는 순간부터 유지되는 무적 시간(초)")]
    public float invincibilityDuration = 0.2f;

    [Header("중복 타격 옵션")]
    [Tooltip("같은 적에게 일정 간격으로만 반복 타격 허용")]
    public bool allowDuplicateHit = false;

    [Tooltip("같은 적에게 다시 타격 허용까지의 간격(초)")]
    public float duplicateHitInterval = 0.1f;

    private void OnValidate()
    {
        minHoldTime = Mathf.Max(0f, minHoldTime);
        invincibilityDuration = Mathf.Max(0f, invincibilityDuration);
        duplicateHitInterval = Mathf.Max(0f, duplicateHitInterval);

        range = Mathf.Max(0f, range);
        hitBoxLifetime = Mathf.Max(0f, hitBoxLifetime);
        knockbackDuration = Mathf.Max(0f, knockbackDuration);
        stunDuration = Mathf.Max(0f, stunDuration);
    }
}