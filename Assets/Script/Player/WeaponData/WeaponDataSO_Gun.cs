using UnityEngine;

[CreateAssetMenu(menuName = "Player/Gun")]
public class WeaponDataSO_Gun : WeaponDataSO
{
    [Header("Gun 프로젝타일")]
    public float projectileLifetime = 5f;
    public float projectileSpeed = 10f;

    [Tooltip("한 발이 관통할 수 있는 수(같은 적은 한 번)")]
    public int pierceCount = 0;

    [Header("조준 스캔(플레이어 감지기)")]
    [Tooltip("플레이어 정면 기준 스캔 각도(도)")]
    public float aimScanAngle = 25f;

    [Tooltip("스캔 최대 거리(미터)")]
    public float aimScanDistance = 12f;

    /* ───────── 탄약 / 리로드 ───────── */
    [Header("탄약 / 리로드")]
    [Tooltip("탄약 시스템 사용 여부")]
    public bool usesAmmo = true;

    [Tooltip("탄창 용량")]
    public int magazineSize = 10;

    [Tooltip("초기 소지 탄약(예비탄)")]
    public int initialReserve = 30;

    [Tooltip("예비탄 무한 (true면 reserve는 무시)")]
    public bool infiniteReserve = false;

    [Tooltip("리로드 시간(초). 0이면 즉시 리로드")]
    public float reloadTime = 1.8f;

    [Tooltip("한 번 발사 시 소모 탄 수")]
    public int consumePerShot = 1;

    [Tooltip("탄창이 0이 되는 순간 자동 리로드")]
    public bool autoReloadOnEmpty = true;

    private void OnValidate()
    {
        projectileLifetime = Mathf.Max(0f, projectileLifetime);
        projectileSpeed = Mathf.Max(0f, projectileSpeed);
        pierceCount = Mathf.Max(0, pierceCount);

        aimScanAngle = Mathf.Clamp(aimScanAngle, 0f, 180f);
        aimScanDistance = Mathf.Max(0f, aimScanDistance);

        magazineSize = Mathf.Max(0, magazineSize);
        initialReserve = Mathf.Max(0, initialReserve);
        reloadTime = Mathf.Max(0f, reloadTime);
        consumePerShot = Mathf.Max(1, consumePerShot);
    }
}