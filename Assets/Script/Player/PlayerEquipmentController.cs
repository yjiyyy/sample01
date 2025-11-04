using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 무기 장착/스냅샷/AOC 적용 전담
/// </summary>
[DisallowMultipleComponent]
public class PlayerEquipmentController : MonoBehaviour
{
    private Transform weaponSocket;
    private PlayerAnimationController animCtrl;

    public GameObject CurrentWeapon { get; private set; }
    public WeaponBehavior WeaponBehavior { get; private set; }
    public WeaponDataSO CurrentWeaponData { get; private set; }

    // 🆕 베이스 런타임 컨트롤러 캐시
    private RuntimeAnimatorController baseController;

    private struct AmmoSnapshot { public int magazine; public int reserve; }
    private readonly Dictionary<WeaponDataSO_Gun, AmmoSnapshot> gunAmmoSnapshots = new();
    // 🆕 AssaultRifle 전용 스냅샷
    private readonly Dictionary<WeaponDataSO_AR, AmmoSnapshot> arAmmoSnapshots = new();
    // 🆕 Shotgun 전용 스냅샷
    private readonly Dictionary<WeaponDataSO_Shotgun, AmmoSnapshot> shotgunAmmoSnapshots = new();

    // Placeholder for defaultWeaponPrefab used earlier in repo (serialize so it's assignable)
    [SerializeField] private GameObject defaultWeaponPrefab;

    public void Setup(Transform socket, PlayerAnimationController animationController)
    {
        weaponSocket = socket;
        animCtrl = animationController;

        // 초기 베이스 컨트롤러 저장(한 번만)
        if (animCtrl != null && animCtrl.GetAnimator() != null)
        {
            baseController = animCtrl.GetAnimator().runtimeAnimatorController;
#if UNITY_EDITOR
            if (baseController == null)
                Debug.LogWarning("[Equip] Animator의 baseController가 비어 있습니다. None 복귀 시 애니메이터가 비어 보일 수 있습니다.");
#endif
        }
        else
        {
#if UNITY_EDITOR
            Debug.LogWarning("[Equip] Setup 시 Animator를 찾지 못했습니다. 베이스 컨트롤러 캐시 실패.");
#endif
        }
    }

    // Backwards-compatible wrapper: 기존 코드가 Equip(...)을 호출할 수 있으므로 3-인자 시그니처 제공
    public void Equip(GameObject weaponPrefab, GameObject defaultWeaponPrefab, bool debugLogs = false)
    {
        // 이 메서드는 기존 코드(서로 다른 호출부)와 호환되도록
        // 내부에서 EquipWeapon을 호출하지만 defaultWeaponPrefab과 debugLogs를 사용해 동작을 동일하게 유지합니다.

        // 임시로 필드 defaultWeaponPrefab을 인자로 덮어쓰지 않고, EquipWeapon 내부에서 사용 가능한 형태로 호출.
        // (EquipWeapon은 단일 인자이므로, 여기서는 인자로 받은 defaultWeaponPrefab을 사용해 직접 장착 처리)
        SaveCurrentGunSnapshot();
        SaveCurrentARSnapshot();
        SaveCurrentShotgunSnapshot();

        if (CurrentWeapon != null)
            Destroy(CurrentWeapon);

        GameObject prefabToSpawn = weaponPrefab != null ? weaponPrefab : defaultWeaponPrefab;
        if (prefabToSpawn == null)
        {
            Debug.LogError("❌ 기본 무기 프리팹이 전달되지 않았습니다.");
            return;
        }

        CurrentWeapon = Instantiate(prefabToSpawn, weaponSocket);
        CurrentWeapon.transform.localPosition = Vector3.zero;
        CurrentWeapon.transform.localRotation = Quaternion.identity;

        WeaponBehavior = CurrentWeapon.GetComponent<WeaponBehavior>();

        // CurrentWeaponData 등 초기화
        CurrentWeaponData = WeaponBehavior != null ? WeaponBehavior.data : null;

        // Gun이면 탄약 초기화/스냅샷 복원
        if (CurrentWeaponData is WeaponDataSO_Gun g && g.usesAmmo)
        {
            WeaponBehavior?.EnsureAmmoInitialized();
            var ammo = WeaponBehavior.GetComponent<WeaponAmmoRuntime>();
            if (gunAmmoSnapshots.TryGetValue(g, out var snap) && ammo != null)
                ammo.LoadSnapshot(snap.magazine, snap.reserve, triggerAutoReload: true);
            else if (debugLogs)
                Debug.Log($"[Ammo] 스냅샷 없음 → 기본 초기화 gun={g.weaponName}");
        }
        // Assault Rifle 전용 런타임 복원
        else if (CurrentWeaponData is WeaponDataSO_AR ar && ar.usesAmmo)
        {
            var arAmmo = WeaponBehavior.GetComponent<WeaponAmmoRuntime_AR>();
            if (arAmmo == null) arAmmo = WeaponBehavior.gameObject.AddComponent<WeaponAmmoRuntime_AR>();
            arAmmo.Initialize(ar, force: false);

            if (arAmmoSnapshots.TryGetValue(ar, out var snap))
                arAmmo.LoadSnapshot(snap.magazine, snap.reserve, triggerAutoReload: true);
            else if (debugLogs)
                Debug.Log($"[Ammo] 스냅샷 없음 → 기본 초기화 ar={ar.weaponName}");
        }
        // Shotgun: WeaponAmmoRuntime 공유
        else if (CurrentWeaponData is WeaponDataSO_Shotgun sg && sg.usesAmmo)
        {
            WeaponBehavior?.EnsureAmmoInitialized();
            var ammo = WeaponBehavior.GetComponent<WeaponAmmoRuntime>();
            if (shotgunAmmoSnapshots.TryGetValue(sg, out var snap) && ammo != null)
                ammo.LoadSnapshot(snap.magazine, snap.reserve, triggerAutoReload: true);
            else if (debugLogs)
                Debug.Log($"[Ammo] 스냅샷 없음 → 기본 초기화 shotgun={sg.weaponName}");
        }

        // 애니메이터 컨트롤러 적용 정책
        var animator = animCtrl != null ? animCtrl.GetAnimator() : null;
        if (animator != null)
        {
            if (CurrentWeaponData != null && CurrentWeaponData.overrideController != null)
            {
                animator.runtimeAnimatorController = CurrentWeaponData.overrideController;
                if (debugLogs) Debug.Log($"[Equip] Animator ← Override({CurrentWeaponData.overrideController.name})");
            }
            else
            {
                if (baseController != null)
                {
                    animator.runtimeAnimatorController = baseController;
                    if (debugLogs) Debug.Log("[Equip] Animator ← BaseController (None/기본 무기)");
                }
                else if (debugLogs)
                {
                    Debug.LogWarning("[Equip] baseController가 비어 있어 복구할 컨트롤러가 없습니다.");
                }
            }
        }

        if (debugLogs)
            Debug.Log($"[Equip] 무기 장착됨 → {CurrentWeaponData?.weaponName ?? "null"}");
    }

    // 간편한 단일-인자 API(내부/신규 코드용)
    public void EquipWeapon(GameObject weaponPrefab)
    {
        // 기존 호출부와 동일 동작(기본 프리팹은 필드 defaultWeaponPrefab 사용)
        Equip(weaponPrefab, defaultWeaponPrefab, debugLogs: false);
    }

    // Save snapshot helpers
    private void SaveCurrentGunSnapshot()
    {
        if (WeaponBehavior == null || CurrentWeaponData == null) return;
        if (CurrentWeaponData is not WeaponDataSO_Gun gun || !gun.usesAmmo) return;

        var ammo = WeaponBehavior.GetComponent<WeaponAmmoRuntime>();
        if (ammo == null || !ammo.IsInitialized) return;

        if (ammo.IsReloading)
            ammo.InterruptReload();

        int magazine = ammo.CurrentMagazine;
        int reserve = gun.infiniteReserve ? 0 : ammo.CurrentReserve;

        gunAmmoSnapshots[gun] = new AmmoSnapshot { magazine = magazine, reserve = reserve };
#if UNITY_EDITOR
        Debug.Log($"[Ammo] 스냅샷 저장 gun={gun.weaponName} mag:{magazine}/{gun.magazineSize} reserve:{(gun.infiniteReserve ? "∞" : reserve.ToString())}");
#endif
    }

    private void SaveCurrentARSnapshot()
    {
        if (WeaponBehavior == null || CurrentWeaponData == null) return;
        if (CurrentWeaponData is not WeaponDataSO_AR ar || !ar.usesAmmo) return;

        var ammo = WeaponBehavior.GetComponent<WeaponAmmoRuntime_AR>();
        if (ammo == null || !ammo.IsInitialized) return;

        if (ammo.IsReloading)
            ammo.InterruptReload();

        int magazine = ammo.CurrentMagazine;
        int reserve = ar.infiniteReserve ? 0 : ammo.CurrentReserve;

        arAmmoSnapshots[ar] = new AmmoSnapshot { magazine = magazine, reserve = reserve };
#if UNITY_EDITOR
        Debug.Log($"[Ammo] 스냅샷 저장 ar={ar.weaponName} mag:{magazine}/{ar.magazineSize} reserve:{(ar.infiniteReserve ? "∞" : reserve.ToString())}");
#endif
    }

    private void SaveCurrentShotgunSnapshot()
    {
        if (WeaponBehavior == null || CurrentWeaponData == null) return;
        if (CurrentWeaponData is not WeaponDataSO_Shotgun sg || !sg.usesAmmo) return;

        var ammo = WeaponBehavior.GetComponent<WeaponAmmoRuntime>();
        if (ammo == null || !ammo.IsInitialized) return;

        if (ammo.IsReloading)
            ammo.InterruptReload();

        int magazine = ammo.CurrentMagazine;
        int reserve = sg.infiniteReserve ? 0 : ammo.CurrentReserve;

        shotgunAmmoSnapshots[sg] = new AmmoSnapshot { magazine = magazine, reserve = reserve };
#if UNITY_EDITOR
        Debug.Log($"[Ammo] 스냅샷 저장 shotgun={sg.weaponName} mag:{magazine}/{sg.magazineSize} reserve:{(sg.infiniteReserve ? "∞" : reserve.ToString())}");
#endif
    }
}