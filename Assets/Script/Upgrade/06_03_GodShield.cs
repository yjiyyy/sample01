using UnityEngine;

[CreateAssetMenu(
    fileName = "06_03_GodShield",
    menuName = "Game/Upgrade/Effect/06_03_GodShield",
    order = 603)]
public class Upgrade_06_03_GodShield : UpgradeEffectSO
{
    [Header("신의 방패")]
    [Tooltip("이 슬롯이 장착된 동안 풀에 합산되는 시간(초). 여러 슬롯이 있으면 합쳐서 한 타이머만 감소합니다.")]
    [Min(0f)]
    public float durationSeconds = 5f;

    [Tooltip("무적이 켜져 있을 때 플레이어에 붙일 월드 FX(선택). 타이머 종료 시 제거됩니다.")]
    public GameObject activeFxPrefab;

    [Header("FX (슬롯 소모 — HUD)")]
    [Tooltip("시간이 다 되어 슬롯 아이콘이 사라질 때 재생할 UI FX 프리팹. 슬롯 하위 이름 \"FX_Slot\" Transform 자식으로 생성됩니다.")]
    public GameObject slotConsumeFxPrefab;

    [Tooltip("UI 슬롯 소모 FX 자동 제거 시간(초). 0 이하면 자동 제거하지 않음.")]
    [Min(0f)]
    public float slotFxAutoDestroySeconds = 5f;
}
