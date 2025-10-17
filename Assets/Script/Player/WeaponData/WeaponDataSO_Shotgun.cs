using UnityEngine;

[CreateAssetMenu(menuName = "Player/Shotgun")]
public class WeaponDataSO_Shotgun : WeaponDataSO
{
    [Header("샷건(섹터) 파라미터")]
    public float shotgunRadius = 5.0f;
    [Range(1f, 360f)] public float shotgunAngle = 30f;
    public bool shotgunUseDistanceFalloff = true;
    [Range(0f, 1f)] public float shotgunFalloffMin = 0.2f;

    [Header("샷건 섹터 시각화")]
    public bool shotgunDebugVisualize = true;
    public Color shotgunDebugColor = new Color(1f, 0.6f, 0f, 0.25f);
    public Color shotgunDebugActualColor = new Color(0f, 1f, 0f, 0.25f);

    private void OnValidate()
    {
        shotgunRadius = Mathf.Max(0f, shotgunRadius);
        shotgunAngle = Mathf.Clamp(shotgunAngle, 1f, 360f);
        shotgunFalloffMin = Mathf.Clamp01(shotgunFalloffMin);
    }
}