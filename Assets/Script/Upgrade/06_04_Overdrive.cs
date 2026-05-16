using UnityEngine;

[CreateAssetMenu(
    fileName = "06_04_Overdrive",
    menuName = "Game/Upgrade/Effect/06_04_Overdrive",
    order = 604)]
public class Upgrade_06_04_Overdrive : UpgradeEffectSO
{
    [Header("오버드라이브")]
    [Tooltip("이 슬롯이 장착된 동안 풀에 합산되는 시간(초). 여러 슬롯이 있으면 합쳐서 한 타이머만 감소합니다.")]
    [Min(0f)]
    public float durationSeconds = 6f;

    [Tooltip("오버드라이브가 켜져 있을 때 플레이어에 붙일 월드 FX(선택). 타이머 종료 시 제거됩니다.")]
    public GameObject activeFxPrefab;

    [Header("전신 잔상 (선택)")]
    [Tooltip("비우면 잔상 없음. 오버드라이브 동안만 런타임에 FullBodySilhouetteGhost가 붙습니다.")]
    public SilhouetteGhostProfile silhouetteGhostProfile;

    [Header("FX (슬롯 소모 — HUD)")]
    [Tooltip("시간 소진으로 슬롯이 비워질 때 재생할 UI FX. FX_Slot 자식에 생성됩니다.")]
    public GameObject slotConsumeFxPrefab;

    [Tooltip("슬롯 소모 FX 자동 제거 시간(초). 0 이하면 자동 제거 안 함.")]
    [Min(0f)]
    public float slotFxAutoDestroySeconds = 5f;
}
