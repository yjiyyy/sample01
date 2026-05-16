using UnityEngine;

[CreateAssetMenu(
    fileName = "PoisonStatusConfig",
    menuName = "Game/Player/PoisonStatusConfig",
    order = 10)]
public class PoisonStatusConfigSO : ScriptableObject
{
    [Header("중독 수치 (갱신 시 다른 SO와 비교해 큰 값이 유지됨)")]
    [Tooltip("중독 지속 시간(초). 새로 맞을 때마다 남은 시간에 더하지 않고 이 상한 기준으로 타이머가 리셋됩니다.")]
    [Min(0.01f)]
    public float poisonDurationSeconds = 5f;

    [Tooltip("틱당 HP 피해(배리어 무시). 다른 SO와 비교해 큰 값이 유지됩니다.")]
    [Min(0f)]
    public float poisonDamagePerTick = 2f;

    [Tooltip("중독 상태가 유지되는 동안, 위 틱당 피해를 HP에 적용하는 간격(초) — 즉 중독 HP 공격 간격입니다. 이 SO가 마지막으로 중독을 갱신할 때 적용됩니다.")]
    [Min(0.05f)]
    public float poisonTickIntervalSeconds = 0.5f;

    [Header("표시")]
    [Tooltip("중독 중 플레이어 HP 슬라이더 Fill 이미지에 적용할 색입니다.")]
    public Color hpBarFillWhilePoisoned = new Color(0.35f, 0.95f, 0.35f, 1f);

    [Header("루프 FX")]
    [Tooltip("중독 중 플레이어 루트에 붙일 루프 FX. 비우면 생략.")]
    public GameObject poisonLoopFxPrefab;

    [Tooltip("루프 FX 프리팹 localScale에 곱할 배율. 1이면 프리팹에 설정한 크기 그대로 사용합니다.")]
    [Min(0.01f)]
    public float poisonFxScaleMultiplier = 1f;
}
