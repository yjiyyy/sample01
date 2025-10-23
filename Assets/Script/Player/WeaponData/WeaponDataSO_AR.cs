using UnityEngine;

[CreateAssetMenu(menuName = "Player/AssaultRifle")]
public class WeaponDataSO_AR : WeaponDataSO
{
    [Header("연사 설정")]
    [Tooltip("연사(홀드) 중에 플레이어 회전을 잠글지 여부를 결정합니다.")]
    public bool lockRotationDuringFiring = true;

    [Tooltip("연사(홀드) 중에도 플레이어 이동을 허용할지 여부를 결정합니다.")]
    public bool allowMoveWhileFiring = true;

    [Header("발사체 설정")]
    [Tooltip("발사체의 초기 속도 (m/s)")]
    public float projectileSpeed = 20f;
    [Tooltip("발사체 수명 (초)")]
    public float projectileLifetime = 5f;
    [Tooltip("관통 수 (0이면 관통 없음)")]
    public int pierceCount = 0;

    [Header("탄약")]
    [Tooltip("탄약 시스템을 사용하는지 여부")]
    public bool usesAmmo = true;
    [Tooltip("탄창 크기")]
    public int magazineSize = 30;
    [Tooltip("초기 예비 탄약")]
    public int initialReserve = 90;
    [Tooltip("무한 예비 탄약 여부 (true면 예비가 무한)")]
    public bool infiniteReserve = false;
    [Tooltip("재장전 소요 시간 (초)")]
    public float reloadTime = 2.0f;
    [Tooltip("1회 발사에 소비되는 탄 수")]
    public int consumePerShot = 1;

    [Tooltip("탄창이 비었을 때 자동으로 재장전할지 여부")]
    public bool autoReloadOnEmpty = true;

    [Tooltip("리로드 중에도 공격 버튼을 계속 누르고 있으면, 리로드 완료 후 자동으로 연사를 재개할지 여부")]
    public bool autoReloadResumeWhileHeld = false;

    /* ───────── 새 필드: 스프레드 설정 (full-angle: 0..180) ───────── */
    [Header("발사 스프레드")]
    [Tooltip("총알이 퍼지는 전체 각도(Full-angle). 0이면 일직선, 180이면 반구 범위에서 랜덤 발사.")]
    [Range(0f, 180f)]
    public float spreadAngle = 0f;

    [Tooltip("true면 3D(위/아래 포함) 콘 분산, false면 수평(Yaw)만 분산")]
    public bool spread3D = true;
    /* ─────────────────────────────────────────────────────────────── */

    /* ───────── 새 필드: AR 연사 중 이동 속도 배율(0..1) ───────── */
    [Header("연사 중 이동 속도")]
    [Tooltip("AR 연사 중 플레이어 이동 속도 배율 (0: 정지, 1: 기본 이동속도와 동일)")]
    [Range(0f, 1f)]
    public float moveSpeedWhileFiring = 1f;
    /* ─────────────────────────────────────────────────────────────── */

#if UNITY_EDITOR
    private void OnValidate()
    {
        // cooldown은 0.01 이상으로 보정
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

        // moveSpeedWhileFiring 범위 보장
        moveSpeedWhileFiring = Mathf.Clamp01(moveSpeedWhileFiring);
    }
#endif
}