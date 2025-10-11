using UnityEngine;

[CreateAssetMenu(menuName = "Weapon/Gun")]
public class WeaponDataSO_Gun : WeaponDataSO
{
    [Header("Gun 프로젝타일")]
    public float projectileLifetime = 5f;
    public float projectileSpeed = 10f;

    [Tooltip("관통 가능한 적 수(서로 다른 적 기준)")]
    public int pierceCount = 0;

    [Header("조준 스캔(플레이어 정면 부채꼴)")]
    [Tooltip("플레이어 정면 기준 부채꼴 각도(도)")]
    public float aimScanAngle = 25f;

    [Tooltip("스캔 최대 거리(미터)")]
    public float aimScanDistance = 12f;

    private void OnValidate()
    {
        projectileLifetime = Mathf.Max(0f, projectileLifetime);
        projectileSpeed = Mathf.Max(0f, projectileSpeed);
        pierceCount = Mathf.Max(0, pierceCount);

        aimScanAngle = Mathf.Clamp(aimScanAngle, 0f, 180f);
        aimScanDistance = Mathf.Max(0f, aimScanDistance);
    }
}