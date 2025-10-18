using System.Collections.Generic;
using UnityEngine;

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

    private struct AmmoSnapshot { public int magazine; public int reserve; }
    private readonly Dictionary<WeaponDataSO_Gun, AmmoSnapshot> gunAmmoSnapshots = new();

    public void Setup(Transform socket, PlayerAnimationController animationController)
    {
        weaponSocket = socket;
        animCtrl = animationController;
    }

    public void Equip(GameObject weaponPrefab, GameObject defaultWeaponPrefab, bool debugLogs = false)
    {
        SaveCurrentGunSnapshot();

        if (CurrentWeapon != null)
            Destroy(CurrentWeapon);

        GameObject prefabToSpawn = weaponPrefab != null ? weaponPrefab : defaultWeaponPrefab;
        if (prefabToSpawn == null)
        {
            Debug.LogError("❌ 기본 무기 프리팹이 연결되지 않았습니다.");
            return;
        }

        CurrentWeapon = Instantiate(prefabToSpawn, weaponSocket);
        CurrentWeapon.transform.localPosition = Vector3.zero;
        CurrentWeapon.transform.localRotation = Quaternion.identity;

        WeaponBehavior = CurrentWeapon.GetComponent<WeaponBehavior>();
        CurrentWeaponData = WeaponBehavior != null ? WeaponBehavior.data : null;

        if (CurrentWeaponData is WeaponDataSO_Gun g && g.usesAmmo)
        {
            WeaponBehavior?.EnsureAmmoInitialized();
            var ammo = WeaponBehavior.GetComponent<WeaponAmmoRuntime>();
            if (gunAmmoSnapshots.TryGetValue(g, out var snap) && ammo != null)
                ammo.LoadSnapshot(snap.magazine, snap.reserve, triggerAutoReload: true);
            else if (debugLogs)
                Debug.Log($"[Ammo] 스냅샷 없음 → 기본 초기화 gun={g.weaponName}");
        }

        if (animCtrl != null && CurrentWeaponData != null && CurrentWeaponData.overrideController != null)
            animCtrl.GetAnimator().runtimeAnimatorController = CurrentWeaponData.overrideController;

        if (debugLogs)
            Debug.Log($"[Equip] 무기 장착됨 → {CurrentWeaponData?.weaponName ?? "null"}");
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
        Debug.Log($"[Ammo] 스냅샷 저장 gun={gun.weaponName} mag:{magazine}/{gun.magazineSize} reserve:{(gun.infiniteReserve ? "∞" : reserve.ToString())}");
#endif
    }
}