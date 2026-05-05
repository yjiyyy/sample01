using UnityEngine;

[CreateAssetMenu(
    fileName = "01_06_IronBody",
    menuName = "Game/Upgrade/Effect/01_06_IronBody",
    order = 6)]
public class Upgrade_01_06_IronBody : UpgradeEffectSO
{
    [Header("피격 시 밀림(Push) 적용")]
    [Tooltip("체크 해제 시 IronBody 발동 중에는 밀림 이동도 무시합니다.")]
    public bool applyPushDisplacement = true;

    [Header("피격 시 경직(타겟 홀드) 적용")]
    [Tooltip("체크 해제 시 IronBody 발동 중에는 피격 경직/히트스톱을 무시합니다.")]
    public bool applyTargetHitstop = true;

    [Header("추가 피해 적용 확률")]
    [Tooltip("0~1 범위. 예: 0.25 = 피격 시 25% 확률로 추가 피해 적용")]
    [Range(0f, 1f)]
    public float extraDamageProcChance = 0f;

    [Header("추가 피해 비율")]
    [Tooltip("발동 시 최종 피해 배율 = 1 + 이 값. 예: 0.3 = 30% 추가 피해")]
    [Min(0f)]
    public float extraDamageTakenPercent = 0f;
}
