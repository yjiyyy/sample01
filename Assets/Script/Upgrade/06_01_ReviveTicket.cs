using UnityEngine;

[CreateAssetMenu(
    fileName = "06_01_ReviveTicket",
    menuName = "Game/Upgrade/Effect/06_01_ReviveTicket",
    order = 601)]
public class Upgrade_06_01_ReviveTicket : UpgradeEffectSO
{
    [Header("부활 설정")]
    [Tooltip("사망 후 리스폰까지 대기 시간(초).")]
    [Min(0f)] public float respawnDelaySeconds = 5f;

    [Tooltip("부활 시 최대 체력 대비 회복 비율 (0~1). 예: 0.5 = 최대 체력의 50%")]
    [Range(0.01f, 1f)] public float respawnHealthRatio = 1f;

    [Tooltip("리스폰 시 죽은 위치에서 위로 올릴 보정값.")]
    [Min(0f)] public float respawnYOffset = 0.5f;

    [Tooltip("부활 직후 무적 시간(초). 0이면 무적 없음.")]
    [Min(0f)] public float invincibleSecondsAfterRespawn = 1f;

    [Header("FX (선택)")]
    [Tooltip("업그레이드 슬롯에서 티켓 소모 시 재생할 UI FX 프리팹. 각 슬롯 하위 이름 \"FX_Slot\" Transform 자식으로 생성됩니다.")]
    public GameObject slotConsumeFxPrefab;

    [Tooltip("플레이어 사망 시 죽은 위치에 재생할 캐스팅 FX 프리팹.")]
    public GameObject reviveCastFxPrefab;

    [Tooltip("플레이어 리스폰 시 죽은 위치에 재생할 FX 프리팹.")]
    public GameObject respawnFxPrefab;

    [Tooltip("UI 슬롯 FX 자동 제거 시간(초). 0 이하면 자동 제거하지 않음.")]
    [Min(0f)] public float slotFxAutoDestroySeconds = 5f;

    [Tooltip("월드 FX 자동 제거 시간(초). 0 이하면 자동 제거하지 않음.")]
    [Min(0f)] public float worldFxAutoDestroySeconds = 5f;
}
