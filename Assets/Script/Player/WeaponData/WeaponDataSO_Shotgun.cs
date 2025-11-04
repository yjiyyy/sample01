using UnityEngine;
using System;

/// <summary>
/// Shotgun 전용 SO.
/// - 기존 샷건 전용 필드(섹터 각도/반경 등) 유지
/// - 탄약/리로드 관련 필드를 Gun과 '동일한 이름/스펙'으로 추가하여
///   WeaponAmmoRuntime을 그대로 재사용할 수 있게 함.
/// </summary>
[CreateAssetMenu(menuName = "Player/Shotgun")]
public class WeaponDataSO_Shotgun : WeaponDataSO
{
    [Header("샷건(섹터) 파라미터")]
    public float shotgunRadius = 5.0f;
    [Range(1f, 360f)] public float shotgunAngle = 30f;
    public bool shotgunUseDistanceFalloff = true;
    [Range(0f, 1f)] public float shotgunFalloffMin = 0.2f;

    [Header("디버그 시각화")]
    public bool shotgunDebugVisualize = true;
    public Color shotgunDebugColor = new Color(1f, 0.6f, 0f, 0.25f);
    public Color shotgunDebugActualColor = new Color(0f, 1f, 0f, 0.25f);

    /* ───────── Ammo fields (Gun과 동일 명명/스펙) ───────── */
    [Header("탄약/리로드 (Gun과 동일 스펙)")]
    [Tooltip("체크하면 이 무기는 탄약/리로드 시스템을 사용합니다.")]
    public bool usesAmmo = false;

    [Tooltip("한 탄창의 크기")]
    public int magazineSize = 8;

    [Tooltip("초기 예비 탄약(Initialize 시 할당). infiniteReserve가 true면 무시")]
    public int initialReserve = 24;

    [Tooltip("예비 무한 여부")]
    public bool infiniteReserve = false;

    [Tooltip("리로드 시간(초). 0이면 즉시 로드")]
    public float reloadTime = 1.2f;

    [Tooltip("자동 리로드: 탄창이 0일 때 자동으로 리로드를 시도")]
    public bool autoReloadOnEmpty = true;

    [Tooltip("발사 시 소모 탄약(개)")]
    public int consumePerShot = 1;

    private void OnValidate()
    {
        shotgunRadius = Mathf.Max(0f, shotgunRadius);
        shotgunAngle = Mathf.Clamp(shotgunAngle, 1f, 360f);
        shotgunFalloffMin = Mathf.Clamp01(shotgunFalloffMin);

        // Ammo fields validation
        magazineSize = Mathf.Clamp(magazineSize, 0, int.MaxValue);
        initialReserve = Mathf.Max(0, initialReserve);
        reloadTime = Mathf.Max(0f, reloadTime);
        consumePerShot = Mathf.Max(1, consumePerShot);
    }
}