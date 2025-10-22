using UnityEngine;

[CreateAssetMenu(menuName = "Player/AssaultRifle")]
public class WeaponDataSO_AR : WeaponDataSO
{
    [Header("연사 동작 옵션")]
    [Tooltip("연사 시작 시 캐릭터 각도를 스냅샷하고, 연사 종료까지 고정")]
    public bool lockRotationDuringFiring = true;

    [Tooltip("공격(연사) 중 이동 허용")]
    public bool allowMoveWhileFiring = true;

    [Header("프로젝타일")]
    public float projectileSpeed = 20f;
    public float projectileLifetime = 5f;
    public int pierceCount = 0;

    [Header("탄약")]
    public bool usesAmmo = true;
    public int magazineSize = 30;
    public int initialReserve = 90;
    public bool infiniteReserve = false;
    public float reloadTime = 2.0f;
    public int consumePerShot = 1;

    [Tooltip("탄창이 비면 자동으로 리로드 시작")]
    public bool autoReloadOnEmpty = true;

    [Tooltip("홀드 유지 중 리로드 완료되면 자동으로 연사 재개")]
    public bool autoReloadResumeWhileHeld = false;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // cooldown은 연사 간격으로 사용되므로 0 방지
        cooldown = Mathf.Max(0.01f, cooldown);

        projectileSpeed = Mathf.Max(0f, projectileSpeed);
        projectileLifetime = Mathf.Max(0.01f, projectileLifetime);
        pierceCount = Mathf.Max(0, pierceCount);

        magazineSize = Mathf.Max(0, magazineSize);
        initialReserve = Mathf.Max(0, initialReserve);
        reloadTime = Mathf.Max(0f, reloadTime);
        consumePerShot = Mathf.Max(1, consumePerShot);
    }
#endif
}