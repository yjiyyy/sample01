using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// PlayerEquipmentController
/// - 장착 관련 API 호환성(기존 Equip(...) 호출을 유지)
/// - 무기 소켓 바인딩은 WeaponDataSO.socketNames(List<string>) 를 사용하여 플레이어 계층에서 이름으로 검색합니다.
/// - Instantiate 시 부모를 지정하여 WeaponBehavior.Awake()가 부모 계층에서 실행되도록 구성.
///
/// [DualWield 확장]
/// - CurrentWeaponData.dualWield == true 이고 socketNames에 2개 이상 있으면:
///   socketNames[0] = 메인(오른손), socketNames[1] = 서브(왼손)으로 같은 프리팹을 2개 장착합니다.
/// - 탄창 공유를 위해 서브(왼손) 인스턴스에서는 WeaponBehavior/AmmoRuntime을 제거하고 모델만 남깁니다.
/// </summary>
[DisallowMultipleComponent]
public class PlayerEquipmentController : MonoBehaviour
{
    private PlayerAnimationController animCtrl;

    public GameObject CurrentWeapon { get; private set; }   // 메인(오른손)
    public GameObject SecondaryWeapon { get; private set; } // 서브(왼손, 모델만)

    public WeaponBehavior WeaponBehavior { get; private set; } // 메인만 유지
    public WeaponDataSO CurrentWeaponData { get; private set; }

    // Default weapon prefab assigned by PlayerFacade (PlayerConfig.defaultWeaponPrefab)
    private GameObject defaultWeaponPrefab;
    public GameObject DefaultWeaponPrefab
    {
        get => defaultWeaponPrefab;
        set => defaultWeaponPrefab = value;
    }

    // runtime ammo snapshots (kept for backward compat; details omitted)
    private struct AmmoSnapshot { public int magazine; public int reserve; }
    private readonly Dictionary<WeaponDataSO_Gun, AmmoSnapshot> gunAmmoSnapshots = new();
    private readonly Dictionary<WeaponDataSO_AR, AmmoSnapshot> arAmmoSnapshots = new();
    private readonly Dictionary<WeaponDataSO_Shotgun, AmmoSnapshot> shotgunAmmoSnapshots = new();

    // cache base animator controller for fallback
    private RuntimeAnimatorController baseController;

    // ----------------- Setup overloads for backward compatibility -----------------
    public void Setup(Transform socket, PlayerAnimationController animationController)
    {
        Setup(animationController);
    }

    public void Setup(PlayerAnimationController animationController)
    {
        animCtrl = animationController;

        if (animCtrl != null && animCtrl.GetAnimator() != null)
        {
            baseController = animCtrl.GetAnimator().runtimeAnimatorController;
#if UNITY_EDITOR
            if (baseController == null)
                Debug.LogWarning("[Equip] Animator baseController is null; fallback may not apply correctly.");
#endif
        }
        else
        {
#if UNITY_EDITOR
            Debug.LogWarning("[Equip] Setup could not find Animator via PlayerAnimationController.");
#endif
        }
    }

    // ----------------- Public Equip APIs (compatibility) -----------------
    public void Equip(GameObject weaponPrefab, GameObject defaultWeaponPrefab, bool debugLogs = false)
    {
        SaveCurrentSnapshots();
        Unequip();

        GameObject prefabToSpawn = weaponPrefab != null ? weaponPrefab : defaultWeaponPrefab;
        if (prefabToSpawn == null)
        {
            Debug.LogError("Equip called without weaponPrefab and without defaultWeaponPrefab.");
            return;
        }

        // debugLogs를 EquipPrefab로 전달 (스코프 에러 방지)
        EquipPrefab(prefabToSpawn, transform.root, debugLogs);
    }

    public void EquipWeapon(GameObject weaponPrefab)
    {
        Equip(weaponPrefab, defaultWeaponPrefab, debugLogs: false);
    }

    // New API: equip by prefab, searching player root for named sockets
    // debugLogs 기본값 false로 둬서 기존 호출과 호환
    public void EquipPrefab(GameObject weaponPrefab, Transform playerRoot, bool debugLogs = false)
    {
        if (weaponPrefab == null)
        {
            Unequip();
            return;
        }

        Unequip();

        // ----------------- 1) 메인 무기(오른손) -----------------
        GameObject instMain = playerRoot != null
            ? Instantiate(weaponPrefab, playerRoot, false)
            : Instantiate(weaponPrefab);

        instMain.name = weaponPrefab.name;
        CurrentWeapon = instMain;

        WeaponBehavior wb = instMain.GetComponent<WeaponBehavior>();
        WeaponBehavior = wb;
        CurrentWeaponData = wb != null ? wb.data : null;

        // 메인 소켓: socketNames[0] 우선, 없으면 기존 호환(순회)
        Transform mainMount = null;
        if (playerRoot != null && CurrentWeaponData != null && CurrentWeaponData.socketNames != null)
        {
            if (CurrentWeaponData.socketNames.Count > 0 && !string.IsNullOrEmpty(CurrentWeaponData.socketNames[0]))
                mainMount = FindDeepChild(playerRoot, CurrentWeaponData.socketNames[0]);

            if (mainMount == null)
            {
                foreach (var n in CurrentWeaponData.socketNames)
                {
                    if (string.IsNullOrEmpty(n)) continue;
                    mainMount = FindDeepChild(playerRoot, n);
                    if (mainMount != null) break;
                }
            }
        }

        AttachToMount(instMain.transform, mainMount);

        if (wb != null)
            wb.EnsureAmmoInitialized();

        ApplyAnimatorOverride(debugLogs);

        // ----------------- 2) 듀얼이면 서브 무기(왼손, 모델만) -----------------
        if (playerRoot != null &&
            CurrentWeaponData != null &&
            CurrentWeaponData.dualWield &&
            CurrentWeaponData.socketNames != null &&
            CurrentWeaponData.socketNames.Count >= 2 &&
            !string.IsNullOrEmpty(CurrentWeaponData.socketNames[1]))
        {
            Transform subMount = FindDeepChild(playerRoot, CurrentWeaponData.socketNames[1]);
            if (subMount == null)
            {
                Debug.LogWarning($"[Equip] dualWield=true 이지만 왼손 소켓을 못 찾음: '{CurrentWeaponData.socketNames[1]}'");
                return;
            }

            GameObject instSub = Instantiate(weaponPrefab, playerRoot, false);
            instSub.name = weaponPrefab.name + "_Sub";
            SecondaryWeapon = instSub;

            // 탄창 공유(중복 공격 방지)를 위해 서브는 모델 전용으로 만든다
            var subWB = instSub.GetComponent<WeaponBehavior>();
            if (subWB != null) Destroy(subWB);

            var subAmmo = instSub.GetComponent<WeaponAmmoRuntime>();
            if (subAmmo != null) Destroy(subAmmo);

            var subAmmoAR = instSub.GetComponent<WeaponAmmoRuntime_AR>();
            if (subAmmoAR != null) Destroy(subAmmoAR);

            AttachToMount(instSub.transform, subMount);

#if UNITY_EDITOR
            if (debugLogs) Debug.Log("[Equip] DualWield: spawned secondary weapon model.");
#endif
        }
    }

    // Convenience: equip default prefab set on this controller
    public void EquipDefault(Transform playerRoot)
    {
        if (DefaultWeaponPrefab == null) return;
        EquipPrefab(DefaultWeaponPrefab, playerRoot, debugLogs: false);
    }

    public void Unequip()
    {
        if (CurrentWeapon != null)
        {
            Destroy(CurrentWeapon);
            CurrentWeapon = null;
        }

        if (SecondaryWeapon != null)
        {
            Destroy(SecondaryWeapon);
            SecondaryWeapon = null;
        }

        WeaponBehavior = null;
        CurrentWeaponData = null;
    }

    // ----------------- Snapshot helpers (kept lightweight) -----------------
    private void SaveCurrentSnapshots()
    {
        SaveCurrentGunSnapshot();
        SaveCurrentARSnapshot();
        SaveCurrentShotgunSnapshot();
    }

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
    }

    // ----------------- Utilities -----------------
    private Transform FindDeepChild(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; ++i)
        {
            var t = FindDeepChild(parent.GetChild(i), name);
            if (t != null) return t;
        }
        return null;
    }

    private void AttachToMount(Transform inst, Transform mount)
    {
        if (inst == null) return;

        if (mount != null && inst.parent != mount)
            inst.SetParent(mount, false);

        inst.localPosition = Vector3.zero;
        inst.localRotation = Quaternion.identity;
        inst.localScale = Vector3.one;
    }

    private void ApplyAnimatorOverride(bool debugLogs)
    {
        var animator = animCtrl != null ? animCtrl.GetAnimator() : null;
        if (animator == null) return;

        if (CurrentWeaponData != null && CurrentWeaponData.overrideController != null)
        {
            animator.runtimeAnimatorController = CurrentWeaponData.overrideController;
#if UNITY_EDITOR
            if (debugLogs) Debug.Log($"[Equip] Animator <- Override({CurrentWeaponData.overrideController.name})");
#endif
        }
        else
        {
            if (baseController != null)
            {
                animator.runtimeAnimatorController = baseController;
#if UNITY_EDITOR
                if (debugLogs) Debug.Log("[Equip] Animator <- BaseController (default)");
#endif
            }
        }
    }
}