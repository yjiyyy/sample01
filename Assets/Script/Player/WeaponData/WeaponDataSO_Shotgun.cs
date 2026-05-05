using UnityEngine;
using System;

/// <summary>
/// Shotgun 전용 SO.
/// - 섹터 판정 대신 프로젝타일(펠릿) 다발 발사
/// - 탄약/리로드 필드는 Gun과 같은 이름/스펙으로 유지하여
///   WeaponAmmoRuntime을 그대로 재사용 가능
/// </summary>
[CreateAssetMenu(menuName = "Player/Shotgun")]
public class WeaponDataSO_Shotgun : WeaponDataSO
{
    [Header("샷건(프로젝타일) 파라미터")]
    [Tooltip("한 번 발사할 때 생성되는 펠릿 수")]
    public int pelletCount = 10;
    [Tooltip("부채꼴 전체 각도(도). pelletCount가 1이면 중앙 1발만 발사")]
    [Range(0f, 360f)] public float spreadAngle = 60f;
    [Tooltip("펠릿 1개 기준 기본 데미지")]
    public float damagePerPellet = 10f;
    [Tooltip("펠릿 속도")]
    public float projectileSpeed = 16f;
    [Tooltip("스폰 위치 기준 누적 이동거리. 도달 시 즉시 파괴")]
    public float maxTravelDistance = 30f;
    [Tooltip("거리 감쇠 시작 거리")]
    public float falloffStartDistance = 5f;
    [Tooltip("최대 감쇠 시 최소 데미지 배율")]
    [Range(0f, 1f)] public float minDamageMultiplier = 0.2f;
    [Tooltip("펠릿 1개당 관통 횟수")]
    public int pierceCount = 0;

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
        pelletCount = Mathf.Max(1, pelletCount);
        spreadAngle = Mathf.Clamp(spreadAngle, 0f, 360f);
        damagePerPellet = Mathf.Max(0f, damagePerPellet);
        projectileSpeed = Mathf.Max(0f, projectileSpeed);
        maxTravelDistance = Mathf.Max(0.01f, maxTravelDistance);
        falloffStartDistance = Mathf.Max(0f, falloffStartDistance);
        minDamageMultiplier = Mathf.Clamp01(minDamageMultiplier);
        pierceCount = Mathf.Max(0, pierceCount);

        // Ammo fields validation
        magazineSize = Mathf.Clamp(magazineSize, 0, int.MaxValue);
        initialReserve = Mathf.Max(0, initialReserve);
        reloadTime = Mathf.Max(0f, reloadTime);
        consumePerShot = Mathf.Max(1, consumePerShot);
    }
}
