using UnityEngine;

[CreateAssetMenu(menuName = "Camera/CameraShakeData")]
public class CameraShakeData : ScriptableObject
{
    [Tooltip("총 지속시간(초)")]
    public float duration = 0.25f;

    [Tooltip("진폭(기본 세기)")]
    public float magnitude = 0.4f;

    [Tooltip("주파수(진동 빠르기) - 사용 시 참고용")]
    public float frequency = 25f;

    [Tooltip("진폭이 시간에 따라 어떻게 줄어드는지 정의하는 커브(0..1 입력)")]
    public AnimationCurve falloff = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Tooltip("Cinemachine Impulse 사용여부(있으면 Perlin 대신 Impulse를 발생시킬 수 있음). 기본은 false(Perlin/Direct 방식).")]
    public bool useCinemachineImpulse = false;

    [Tooltip("Cinemachine Impulse 강도(Impulse 사용 시)")]
    public float impulseAmplitude = 1f;

    private void Reset()
    {
        duration = 0.25f;
        magnitude = 0.4f;
        frequency = 25f;
        falloff = AnimationCurve.EaseInOut(0, 1, 1, 0);
        useCinemachineImpulse = false;
        impulseAmplitude = 1f;
    }
}