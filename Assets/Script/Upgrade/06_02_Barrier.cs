using UnityEngine;

[CreateAssetMenu(
    fileName = "06_02_Barrier",
    menuName = "Game/Upgrade/Effect/06_02_Barrier",
    order = 602)]
public class Upgrade_06_02_Barrier : UpgradeEffectSO
{
    [Header("베리어")]
    [Tooltip("이 슬롯에 장착 시 부여되는 베리어 최대량(힐과 무관, 피해 시 먼저 소모).")]
    [Min(0f)]
    public float barrierMaxPoints = 50f;

    [Header("FX (선택)")]
    [Tooltip("베리어가 다 소모되어 슬롯에서 제거될 때 재생할 UI FX 프리팹. 슬롯 하위 이름 \"FX_Slot\" Transform 자식으로 생성됩니다.")]
    public GameObject slotConsumeFxPrefab;

    [Tooltip("UI 슬롯 FX 자동 제거 시간(초). 0 이하면 자동 제거하지 않음.")]
    [Min(0f)]
    public float slotFxAutoDestroySeconds = 5f;

    [Header("FX (베리어 피격)")]
    [Tooltip("베리어 게이지가 피해로 줄어들 때마다 해당 슬롯에 재생할 UI FX. 슬롯 소모 FX와는 별개(소모는 게이지가 0이 된 뒤 슬롯이 비워질 때).")]
    public GameObject barrierGaugeHitSlotFxPrefab;

    [Tooltip("베리어 피격 FX 자동 제거 시간(초). 0 이하면 자동 제거하지 않음.")]
    [Min(0f)]
    public float barrierGaugeHitSlotFxAutoDestroySeconds = 3f;

    [Header("슬롯 제거")]
    [Tooltip("베리어가 다 소모된 뒤 슬롯 데이터를 비우기까지 대기 시간(초). 0이면 즉시 제거.")]
    [Min(0f)]
    public float slotClearDelaySeconds;
}
