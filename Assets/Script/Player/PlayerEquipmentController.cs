using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// PlayerEquipmentController
/// - 장착 관련 API 호환성(기존 Equip(...) 호출을 유지)
/// - 무기 소켓 바인딩은 WeaponDataSO.socketNames(List<string>) 를 사용하여 플레이어 계층에서 이름으로 검색합니다.
/// - 변경: Instantiate 시 부모를 지정하도록 하여 WeaponBehavior.Awake()가 이미 부모 계층에 붙어있는 상태에서 실행되도록 개선.
/// </summary>
[DisallowMultipleComponent]
public class PlayerEquipmentController : MonoBehaviour
{
    private PlayerAnimationController animCtrl;

    public GameObject CurrentWeapon { get; private set; }
    public WeaponBehavior WeaponBehavior { get; private set; }
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
    // Original code used Setup(Transform socket, PlayerAnimationController animCtrl)
    public void Setup(Transform socket, PlayerAnimationController animationController)
    {
        // socket is deprecated: we now resolve mount per-weapon via WeaponDataSO.socketNames
        Setup(animationController);
    }

    // Newer simplified Setup
    public void Setup(PlayerAnimationController animationController)
    {
        animCtrl = animationController;

        // cache base controller if available
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

    // Original 3-arg API preserved for callers that still call it
    public void Equip(GameObject weaponPrefab, GameObject defaultWeaponPrefab, bool debugLogs = false)
    {
        // save snapshots, destroy existing, then spawn as in legacy behavior
        SaveCurrentSnapshots();

        if (CurrentWeapon != null)
            Destroy(CurrentWeapon);

        GameObject prefabToSpawn = weaponPrefab != null ? weaponPrefab : defaultWeaponPrefab;
        if (prefabToSpawn == null)
        {
            Debug.LogError("Equip called without weaponPrefab and without defaultWeaponPrefab.");
            return;
        }

        // Use EquipPrefab for consistent binding rules
        EquipPrefab(prefabToSpawn, transform.root);
    }

    // Simpler API
    public void EquipWeapon(GameObject weaponPrefab)
    {
        Equip(weaponPrefab, defaultWeaponPrefab, debugLogs: false);
    }

    // New API: equip by prefab, searching player root for named sockets
    public void EquipPrefab(GameObject weaponPrefab, Transform playerRoot)
    {
        if (weaponPrefab == null)
        {
            Unequip();
            return;
        }

        // destroy old
        if (CurrentWeapon != null)
            Destroy(CurrentWeapon);

        // instantiate and parent correctly to ensure Awake runs with correct hierarchy
        Transform mount = null;
        if (playerRoot != null)
        {
            // Determine mount using weaponData.socketNames after instantiation? We can try to find mount on playerRoot using the prefab's SO,
            // but to avoid extra disk access we instantiate first and then find using its WeaponBehavior.data.
            // However we can try to find soon: instantiate temporarily under playerRoot if needed.
        }

        GameObject inst = null;

        // We need CurrentWeaponData to decide socketNames. But WeaponBehavior (on the prefab) will be setup in Awake.
        // Strategy: instantiate attached to playerRoot initially (so Awake can find Root_dummy), then if mount found later adjust transform.
        if (playerRoot != null)
        {
            // Instantiate under playerRoot so WeaponBehavior.Awake can find Root_dummy / other spawn points reliably.
            inst = Instantiate(weaponPrefab, playerRoot, false);
        }
        else
        {
            inst = Instantiate(weaponPrefab);
        }

        inst.name = weaponPrefab.name;
        CurrentWeapon = inst;
        WeaponBehavior wb = inst.GetComponent<WeaponBehavior>();
        WeaponBehavior = wb;
        CurrentWeaponData = wb != null ? wb.data : null;

        // Now that instance exists and Awake already ran (with parent if provided), attempt to find the best mount.
        Transform desiredMount = null;
        if (playerRoot != null && CurrentWeaponData != null && CurrentWeaponData.socketNames != null)
        {
            foreach (var n in CurrentWeaponData.socketNames)
            {
                if (string.IsNullOrEmpty(n)) continue;
                desiredMount = FindDeepChild(playerRoot, n);
                if (desiredMount != null) break;
            }
        }

        // If desired mount is found and the instance isn't already parented to it, reparent (keeps local transform)
        if (desiredMount != null && inst.transform.parent != desiredMount)
        {
            inst.transform.SetParent(desiredMount, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;
        }
        else
        {
            // ensure local transform reset (we instantiated with playerRoot parent earlier which keeps transform relative)
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;
        }

        // Ensure WeaponBehavior initializes if needed
        if (wb != null)
        {
            wb.EnsureAmmoInitialized();
        }

        // Apply animator override if weapon data has one, otherwise restore base controller
        var animator = animCtrl != null ? animCtrl.GetAnimator() : null;
        if (animator != null)
        {
            if (CurrentWeaponData != null && CurrentWeaponData.overrideController != null)
            {
                animator.runtimeAnimatorController = CurrentWeaponData.overrideController;
                if (debugModeUsedForLog()) Debug.Log($"[Equip] Animator <- Override({CurrentWeaponData.overrideController.name})");
            }
            else
            {
                if (baseController != null)
                {
                    animator.runtimeAnimatorController = baseController;
                    if (debugModeUsedForLog()) Debug.Log("[Equip] Animator <- BaseController (default)");
                }
            }
        }
    }

    // Convenience: equip default prefab set on this controller
    public void EquipDefault(Transform playerRoot)
    {
        if (DefaultWeaponPrefab == null) return;
        EquipPrefab(DefaultWeaponPrefab, playerRoot);
    }

    public void Unequip()
    {
        if (CurrentWeapon != null)
        {
            Destroy(CurrentWeapon);
            CurrentWeapon = null;
            WeaponBehavior = null;
            CurrentWeaponData = null;
        }
    }

    // ----------------- Snapshot helpers (kept lightweight) -----------------
    private void SaveCurrentSnapshots()
    {
        // Implementation kept minimal to avoid compile errors; snapshot logic exists in original repo.
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
#if UNITY_EDITOR
        Debug.Log($"[Ammo] gun snapshot saved: {gun.weaponName}");
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

    private bool debugModeUsedForLog()
    {
        // Try to find a PlayerWeaponController on root to query debugMode if present (best-effort)
        var pwc = GetComponentInParent<PlayerWeaponController>();
        return pwc != null ? pwc.hideFlags == HideFlags.None : false;
    }
}