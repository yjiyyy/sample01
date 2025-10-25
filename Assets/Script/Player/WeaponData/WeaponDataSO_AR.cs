using UnityEngine;

[CreateAssetMenu(menuName = "Player/AssaultRifle")]
public class WeaponDataSO_AR : WeaponDataSO
{
    [Header("기본 옵션")]
    [Tooltip("발사(연사) 중 회전 고정 여부")]
    public bool lockRotationDuringFiring = true;

    [Tooltip("발사(연사) 중 이동 허용 여부")]
    public bool allowMoveWhileFiring = true;

    [Header("탄체")]
    [Tooltip("발사체의 초기 속도 (m/s)")]
    public float projectileSpeed = 20f;
    [Tooltip("발사체 수명 (초)")]
    public float projectileLifetime = 5f;
    [Tooltip("관통 횟수 (0이면 관통 없음)")]
    public int pierceCount = 0;

    [Header("탄약")]
    [Tooltip("무기가 탄약을 사용하는가")]
    public bool usesAmmo = true;
    [Tooltip("탄창 용량")]
    public int magazineSize = 30;
    [Tooltip("초기 예비 탄약")]
    public int initialReserve = 90;
    [Tooltip("무한 예비 탄약 (true이면 무한)")]
    public bool infiniteReserve = false;
    [Tooltip("재장전 시간 (초)")]
    public float reloadTime = 2.0f;
    [Tooltip("한 발당 소비 수량")]
    public int consumePerShot = 1;

    [Tooltip("탄창 비었을 때 자동 재장전")]
    public bool autoReloadOnEmpty = true;

    [Tooltip("리로드 중 홀드 시 자동 재개 (회전잠금 등 유지)")]
    public bool autoReloadResumeWhileHeld = false;

    /* spread 설정 */
    [Header("발사 스프레드")]
    [Tooltip("발사 콘의 전체 각도 (0..180). 0이면 직선 발사")]
    [Range(0f, 180f)]
    public float spreadAngle = 0f;

    [Tooltip("true면 3D(위/아래 포함) 스프레드, false면 Yaw(수평)만")]
    public bool spread3D = true;

    /* AR 전용 이동 속도 (A) */
    [Header("AR 연사 중 이동/애니 속도 설정")]
    [Tooltip("AR 발사 중 플레이어 이동 속도 곱셈 계수 A (0: 정지, 1: 기본 이동속도 유지)")]
    [Range(0f, 1f)]
    public float moveSpeedWhileFiring = 1f;

    [Tooltip("AR 발사 중 하체 애니메이션 재생속도 계수 B (1 = 기본 속도). 하체 재생속도에만 적용됩니다.")]
    [Range(0f, 2f)]
    public float animPlaybackSpeedWhileFiring = 1f;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // cooldown 최소값
        cooldown = Mathf.Max(0.01f, cooldown);

        projectileSpeed = Mathf.Max(0f, projectileSpeed);
        projectileLifetime = Mathf.Max(0.01f, projectileLifetime);
        pierceCount = Mathf.Max(0, pierceCount);

        magazineSize = Mathf.Max(0, magazineSize);
        initialReserve = Mathf.Max(0, initialReserve);
        reloadTime = Mathf.Max(0f, reloadTime);
        consumePerShot = Mathf.Max(1, consumePerShot);

        // spreadAngle 범위 보장
        spreadAngle = Mathf.Clamp(spreadAngle, 0f, 180f);

        // moveSpeedWhileFiring: 기존 정책(0..1 슬라이더 유지)
        moveSpeedWhileFiring = Mathf.Clamp01(moveSpeedWhileFiring);

        // animPlaybackSpeedWhileFiring: 0 이상으로 보장 (슬라이더 상 0..2)
        animPlaybackSpeedWhileFiring = Mathf.Max(0f, animPlaybackSpeedWhileFiring);
    }
#endif
}