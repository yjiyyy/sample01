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

    // 🆕 베이스 런타임 컨트롤러 캐시
    private RuntimeAnimatorController baseController;

    private struct AmmoSnapshot { public int magazine; public int reserve; }
    private readonly Dictionary<WeaponDataSO_Gun, AmmoSnapshot> gunAmmoSnapshots = new();

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

        // 🆕 애니메이터 컨트롤러 적용 정책
        // - overrideController가 있으면 그걸 사용
        // - 없으면 초기 baseController로 복귀(None 무기 케이스)
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
        else if (debugLogs)
        {
            Debug.LogWarning("[Equip] Animator를 찾지 못했습니다. 컨트롤러 적용 불가.");
        }

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