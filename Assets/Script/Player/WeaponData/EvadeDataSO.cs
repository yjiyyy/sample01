using UnityEngine;

[CreateAssetMenu(menuName = "Player/EvadeDataSO")]
public class EvadeDataSO : ScriptableObject
{
    [Header("회피 기본 설정")]
    public float maxGauge = 100f;
    public float evadeCost = 30f;
    public float rechargeRate = 20f;

    [Header("회피 성능")]
    public float evadeSpeed = 8f;
    public float evadeDuration = 0.5f;

    [Header("무적 시간")]
    public float invincibilityDuration = 0.3f;

    [Header("속도 감쇠 커브")]
    [Tooltip("시간에 따른 속도 변화 (0=시작, 1=끝)")]
    public AnimationCurve speedCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("🎮 회피 방식 선택")]
    [Tooltip("체크 시: 회피 중에도 입력에 따라 방향 실시간 변경\n해제 시: 회피 시작 시 방향 고정")]
    public bool allowDirectionChangeWhileEvading = false;

    [Header("실시간 방향 변경 설정 (위 옵션이 true일 때만 적용)")]
    [Tooltip("방향 변경 감도 (높을수록 빠르게 방향 전환)")]
    [Range(0.1f, 5f)]
    public float directionChangeSensitivity = 2f;

    [Tooltip("최소 입력 크기 (이 값 이하면 방향 변경 무시)")]
    [Range(0.1f, 0.9f)]
    public float minInputMagnitude = 0.3f;
}