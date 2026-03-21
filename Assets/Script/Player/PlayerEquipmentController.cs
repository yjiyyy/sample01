using UnityEngine;
using System;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class PlayerEquipmentController : MonoBehaviour
{
    private PlayerAnimationController animCtrl;

    public GameObject CurrentWeapon { get; private set; }
    public GameObject SecondaryWeapon { get; private set; }

    public WeaponBehavior WeaponBehavior { get; private set; }
    public WeaponDataSO CurrentWeaponData { get; private set; }

    private WeaponDataSO defaultWeaponData;
    public WeaponDataSO DefaultWeaponData
    {
        get => defaultWeaponData;
        set => defaultWeaponData = value;
    }

    // UI 구독용 이벤트
    public event Action<WeaponDataSO> OnWeaponChanged;
    public event Action<int, int, bool> OnAmmoChanged; // magazine, reserve, isReloading

    private struct AmmoSnapshot { public int magazine; public int reserve; }
    private readonly Dictionary<WeaponDataSO_Gun, AmmoSnapshot> gunAmmoSnapshots = new();
    private readonly Dictionary<WeaponDataSO_AR, AmmoSnapshot> arAmmoSnapshots = new();
    private readonly Dictionary<WeaponDataSO_Shotgun, AmmoSnapshot> shotgunAmmoSnapshots = new();

    private RuntimeAnimatorController baseController;

    // 현재 구독중인 ammo 런타임들
    private WeaponAmmoRuntime currentAmmoRuntime;
    private WeaponAmmoRuntime_AR currentAmmoRuntimeAR;

    public void Setup(Transform socket, PlayerAnimationController animationController) => Setup(animationController);

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
#if UNITY_EDITOR
        else
        {
            Debug.LogWarning("[Equip] Setup could not find Animator via PlayerAnimationController.");
        }
#endif
    }

    public void Equip(GameObject weaponPrefab, bool debugLogs = false)
    {
        SaveCurrentSnapshots();

        GameObject prefabToSpawn = weaponPrefab != null
            ? weaponPrefab
            : (DefaultWeaponData != null ? DefaultWeaponData.weaponPrefab : null);

        if (prefabToSpawn == null)
        {
            Debug.LogError("[Equip] Equip called without weaponPrefab and without DefaultWeaponData/weaponPrefab.");
            return;
        }

        EquipPrefabInternal(prefabToSpawn, transform.root, dataToApply: DefaultWeaponData, debugLogs: debugLogs);
        OnWeaponChanged?.Invoke(CurrentWeaponData);
    }

    public void EquipWeapon(GameObject weaponPrefab) => Equip(weaponPrefab, debugLogs: false);

    public void EquipByData(WeaponDataSO so, Transform playerRoot = null, bool debugLogs = false)
    {
        if (so == null)
        {
            Debug.LogWarning("[Equip] EquipByData called with null WeaponDataSO.");
            return;
        }

        if (so.weaponPrefab == null)
        {
            Debug.LogWarning($"[Equip] EquipByData: '{so.name}' SO에 weaponPrefab이 비어 있습니다.");
            return;
        }

        if (playerRoot == null) playerRoot = transform.root;

        SaveCurrentSnapshots();

        DefaultWeaponData = so;

        EquipPrefabInternal(so.weaponPrefab, playerRoot, dataToApply: so, debugLogs: debugLogs);
        OnWeaponChanged?.Invoke(CurrentWeaponData);
    }

    public void EquipPrefab(GameObject weaponPrefab, Transform playerRoot, bool debugLogs = false)
    {
        SaveCurrentSnapshots();
        EquipPrefabInternal(weaponPrefab, playerRoot, dataToApply: DefaultWeaponData, debugLogs: debugLogs);
        OnWeaponChanged?.Invoke(CurrentWeaponData);
    }

    private void EquipPrefabInternal(GameObject weaponPrefab, Transform playerRoot, WeaponDataSO dataToApply, bool debugLogs)
    {
        if (weaponPrefab == null)
        {
            Unequip();
            return;
        }

        if (playerRoot == null) playerRoot = transform.root;

        Unequip();

        GameObject instMain = playerRoot != null
            ? Instantiate(weaponPrefab, playerRoot, false)
            : Instantiate(weaponPrefab);

        instMain.name = weaponPrefab.name;
        CurrentWeapon = instMain;

        WeaponBehavior wb = instMain.GetComponent<WeaponBehavior>();
        WeaponBehavior = wb;

        if (wb != null && dataToApply != null)
        {
            wb.ApplyData(dataToApply, forceReinit: true);
            CurrentWeaponData = dataToApply;
        }
        else
        {
            CurrentWeaponData = wb != null ? wb.data : null;
        }

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

        ApplyAnimatorOverride(debugLogs);

        // 복원
        RestoreCurrentSnapshots();

        // ammo 구독(AR/Gun/Shotgun 모두)
        SubscribeToAmmoFromWeaponBehavior();

        // 듀얼 서브 모델
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

            var subWB = instSub.GetComponent<WeaponBehavior>();
            if (subWB != null) Destroy(subWB);

            var subAmmo = instSub.GetComponent<WeaponAmmoRuntime>();
            if (subAmmo != null) Destroy(subAmmo);

            var subAmmoAR = instSub.GetComponent<WeaponAmmoRuntime_AR>();
            if (subAmmoAR != null) Destroy(subAmmoAR);

            AttachToMount(instSub.transform, subMount);

            if (CurrentWeaponData.UseWeaponCollider)
            {
                foreach (var hb in instSub.GetComponentsInChildren<HitBox_PC>(true))
                {
                    var col = hb.GetComponent<Collider>();
                    if (col != null) col.enabled = false;
                }
            }

#if UNITY_EDITOR
            if (debugLogs) Debug.Log("[Equip] DualWield: spawned secondary weapon model.");
#endif
        }
    }

    public void EquipDefault(Transform playerRoot)
    {
        if (DefaultWeaponData == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning("[Equip] DefaultWeaponData is null.");
#endif
            return;
        }

        if (DefaultWeaponData.weaponPrefab == null)
        {
            Debug.LogWarning($"[Equip] DefaultWeaponData '{DefaultWeaponData.name}' has no weaponPrefab.");
            return;
        }

        EquipByData(DefaultWeaponData, playerRoot, debugLogs: false);
    }

    public void Unequip()
    {
        UnsubscribeCurrentAmmo();

        if (CurrentWeapon != null) { Destroy(CurrentWeapon); CurrentWeapon = null; }
        if (SecondaryWeapon != null) { Destroy(SecondaryWeapon); SecondaryWeapon = null; }
        WeaponBehavior = null;
        CurrentWeaponData = null;
    }

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

        if (ammo.IsReloading) ammo.InterruptReload();

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

        if (ammo.IsReloading) ammo.InterruptReload();

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

        if (ammo.IsReloading) ammo.InterruptReload();

        int magazine = ammo.CurrentMagazine;
        int reserve = sg.infiniteReserve ? 0 : ammo.CurrentReserve;
        shotgunAmmoSnapshots[sg] = new AmmoSnapshot { magazine = magazine, reserve = reserve };
    }

    private void RestoreCurrentSnapshots()
    {
        if (WeaponBehavior == null || CurrentWeaponData == null) return;

        if (CurrentWeaponData is WeaponDataSO_Gun gun && gun.usesAmmo)
        {
            if (gunAmmoSnapshots.TryGetValue(gun, out AmmoSnapshot snap))
            {
                var ammo = WeaponBehavior.GetComponent<WeaponAmmoRuntime>();
                if (ammo != null && ammo.IsInitialized)
                {
                    ammo.LoadSnapshot(snap.magazine, snap.reserve, triggerAutoReload: true);
#if UNITY_EDITOR
                    Debug.Log($"[Equip] Gun snapshot restored: {snap.magazine}/{snap.reserve} for {gun.name}");
#endif
                }
            }
        }

        if (CurrentWeaponData is WeaponDataSO_AR ar && ar.usesAmmo)
        {
            if (arAmmoSnapshots.TryGetValue(ar, out AmmoSnapshot snap))
            {
                var ammo = WeaponBehavior.GetComponent<WeaponAmmoRuntime_AR>();
                if (ammo != null && ammo.IsInitialized)
                {
                    ammo.LoadSnapshot(snap.magazine, snap.reserve, triggerAutoReload: true);
#if UNITY_EDITOR
                    Debug.Log($"[Equip] AR snapshot restored: {snap.magazine}/{snap.reserve} for {ar.name}");
#endif
                }
            }
        }

        if (CurrentWeaponData is WeaponDataSO_Shotgun sg && sg.usesAmmo)
        {
            if (shotgunAmmoSnapshots.TryGetValue(sg, out AmmoSnapshot snap))
            {
                var ammo = WeaponBehavior.GetComponent<WeaponAmmoRuntime>();
                if (ammo != null && ammo.IsInitialized)
                {
                    ammo.LoadSnapshot(snap.magazine, snap.reserve, triggerAutoReload: true);
#if UNITY_EDITOR
                    Debug.Log($"[Equip] Shotgun snapshot restored: {snap.magazine}/{snap.reserve} for {sg.name}");
#endif
                }
            }
        }
    }

    // Ammo subscription helpers
    private void SubscribeToAmmoFromWeaponBehavior()
    {
        UnsubscribeCurrentAmmo();

        if (WeaponBehavior == null) return;

        var gunAmmo = WeaponBehavior.GetComponent<WeaponAmmoRuntime>();
        if (gunAmmo != null)
        {
            currentAmmoRuntime = gunAmmo;
            currentAmmoRuntime.OnAmmoChanged += InternalAmmoChanged;
            InternalAmmoChanged(currentAmmoRuntime.CurrentMagazine, currentAmmoRuntime.CurrentReserve, currentAmmoRuntime.IsReloading);
        }

        var arAmmo = WeaponBehavior.GetComponent<WeaponAmmoRuntime_AR>();
        if (arAmmo != null)
        {
            currentAmmoRuntimeAR = arAmmo;
            currentAmmoRuntimeAR.OnAmmoChanged += InternalAmmoChanged;
            InternalAmmoChanged(currentAmmoRuntimeAR.CurrentMagazine, currentAmmoRuntimeAR.CurrentReserve, currentAmmoRuntimeAR.IsReloading);
        }
    }

    private void UnsubscribeCurrentAmmo()
    {
        if (currentAmmoRuntime != null)
        {
            currentAmmoRuntime.OnAmmoChanged -= InternalAmmoChanged;
            currentAmmoRuntime = null;
        }
        if (currentAmmoRuntimeAR != null)
        {
            currentAmmoRuntimeAR.OnAmmoChanged -= InternalAmmoChanged;
            currentAmmoRuntimeAR = null;
        }
    }

    private void InternalAmmoChanged(int magazine, int reserve, bool isReloading)
    {
        OnAmmoChanged?.Invoke(magazine, reserve, isReloading);
    }

    // Utilities
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
        if (mount != null && inst.parent != mount) inst.SetParent(mount, false);
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
        else if (baseController != null)
        {
            animator.runtimeAnimatorController = baseController;
#if UNITY_EDITOR
            if (debugLogs) Debug.Log("[Equip] Animator <- BaseController (default)");
#endif
        }
    }
}