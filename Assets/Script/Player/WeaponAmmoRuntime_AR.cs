using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class WeaponAmmoRuntime_AR : MonoBehaviour
{
    public bool IsInitialized { get; private set; }
    public bool IsReloading { get; private set; }
    public int CurrentMagazine { get; private set; }
    public int CurrentReserve { get; private set; }

    private WeaponDataSO_AR data;
    private float reloadEndTime;
    private Coroutine reloadRoutine;

    /// <summary>
    /// ApplyExtendedMagazineAfterUpgrades?? ?????????? ?? ??? ?? ??X. ??X?? ?????? ???? ??? ????? ????
    /// (UpgradeEffectRuntime OnEnable ?????? ????? ????? ?? ???? ???? ????).
    /// </summary>
    private int lastSeenEffectiveMagazineCapacityForExtendedApply = -1;

    // UI/??? ?????? ????: (magazine, reserve, isReloading)
    public event Action<int, int, bool> OnAmmoChanged;

    private int GetExtendedMagazineBonusFromUpgrades()
    {
        if (data == null)
            return 0;

        GameObject ownerRoot = transform.root != null ? transform.root.gameObject : gameObject;
        return PlayerWeaponDamageModifiers.GetExtendedMagazineBonusCount(ownerRoot, data);
    }

    public int GetEffectiveMagazineCapacity()
    {
        if (data == null)
            return 0;
        return Mathf.Max(0, data.magazineSize + GetExtendedMagazineBonusFromUpgrades());
    }

    /// <summary>Runtime max magazine for UI (SlotView reflection).</summary>
    public int EffectiveMagazineCapacity => GetEffectiveMagazineCapacity();

    /// <summary>
    /// After upgrade slots change: interrupt reload if needed, fill magazine to new capacity (reserve unchanged).
    /// </summary>
    public void ApplyExtendedMagazineAfterUpgrades()
    {
        if (data == null || !data.usesAmmo || !IsInitialized)
            return;

        if (IsReloading)
            InterruptReload();

        int newCap = GetEffectiveMagazineCapacity();
        if (newCap <= 0)
            return;

        if (lastSeenEffectiveMagazineCapacityForExtendedApply >= 0 &&
            newCap == lastSeenEffectiveMagazineCapacityForExtendedApply)
            return;

        if (lastSeenEffectiveMagazineCapacityForExtendedApply < 0)
        {
            CurrentMagazine = newCap;
        }
        else if (newCap > lastSeenEffectiveMagazineCapacityForExtendedApply)
        {
            // È®Àå ÅºÃ¢ ½½·Ô ¹Ý¿µ ½Ã ÅºÃ¢¸¸ °¡µæ(¿¹ºñÅº ºÒº¯). µ¨Å¸¸¸Å­¸¸ ´õÇÏ¸é Ã¼°¨»ó ¡®¼ýÀÚ¸¸Å­¸¸¡¯ Ã¤¿öÁö´Â ´À³¦ÀÌ µÊ.
            CurrentMagazine = newCap;
        }
        else
        {
            CurrentMagazine = Mathf.Min(CurrentMagazine, newCap);
        }

        lastSeenEffectiveMagazineCapacityForExtendedApply = newCap;
        OnAmmoChanged?.Invoke(CurrentMagazine, CurrentReserve, IsReloading);
    }

    public void Initialize(WeaponDataSO_AR arData, bool force = false)
    {
        if (!force && IsInitialized && data == arData) return;

        data = arData;
        if (data == null)
        {
            Debug.LogWarning("[AR Ammo] Initialize called with null data.");
            return;
        }

        // ????? ????: SO ?? + ??? ?? ????? ???(???? ???? Subscribe ?????? ?? ?? ?? Apply?? ????)
        IsReloading = false;
        int cap = GetEffectiveMagazineCapacity();
        CurrentMagazine = Mathf.Min(Mathf.Max(0, data.magazineSize), cap);
        CurrentReserve = data.infiniteReserve ? 0 : Mathf.Max(0, data.initialReserve);

        IsInitialized = true;
        lastSeenEffectiveMagazineCapacityForExtendedApply = -1;
        Debug.Log($"[AR Ammo] Init ?? mag:{CurrentMagazine}/{GetEffectiveMagazineCapacity()} reserve:{(data.infiniteReserve ? "??" : CurrentReserve.ToString())}");

        OnAmmoChanged?.Invoke(CurrentMagazine, CurrentReserve, IsReloading);
    }

    private void OnDisable()
    {
        InterruptReload();
    }

    public bool CanFire(int need)
    {
        if (data == null) return false;
        if (!data.usesAmmo) return !IsReloading;
        if (IsReloading) return false;
        return CurrentMagazine >= need;
    }

    public bool TryConsumeForShot(int amount)
    {
        if (data == null) return false;
        if (!data.usesAmmo) return true;

        if (IsReloading) return false;
        if (CurrentMagazine < amount) return false;

        CurrentMagazine -= amount;
        Debug.Log($"[AR Ammo] ???: mag now {CurrentMagazine}/{GetEffectiveMagazineCapacity()} reserve:{(data.infiniteReserve ? "??" : CurrentReserve.ToString())}");

        OnAmmoChanged?.Invoke(CurrentMagazine, CurrentReserve, IsReloading);

        if (data.autoReloadOnEmpty && CurrentMagazine <= 0)
            TryStartReload();

        return true;
    }

    public bool TryStartReload()
    {
        if (data == null) return false;
        if (!data.usesAmmo) return false;
        if (IsReloading) return false;
        if (!data.infiniteReserve && CurrentReserve <= 0) return false;
        if (CurrentMagazine >= GetEffectiveMagazineCapacity()) return false;

        float baseRt = Mathf.Max(0f, data.reloadTime);
        GameObject ownerRoot = transform.root != null ? transform.root.gameObject : gameObject;
        float rt = PlayerWeaponDamageModifiers.GetReloadTimeWithQuickReload(ownerRoot, data, baseRt);
        if (rt <= 0f)
        {
            // ??? ????
            PerformRefill();
            Debug.Log($"[AR Ammo] Reload instant complete ?? mag:{CurrentMagazine}/{GetEffectiveMagazineCapacity()} reserve:{(data.infiniteReserve ? "??" : CurrentReserve.ToString())}");
            OnAmmoChanged?.Invoke(CurrentMagazine, CurrentReserve, IsReloading);
            return true;
        }

        if (reloadRoutine != null) StopCoroutine(reloadRoutine);
        IsReloading = true;
        reloadEndTime = Time.time + rt;
        reloadRoutine = StartCoroutine(ReloadRoutine());

        Debug.Log($"[AR Ammo] Reload started ({rt:F2}s)");
        OnAmmoChanged?.Invoke(CurrentMagazine, CurrentReserve, IsReloading);
        return true;
    }

    private IEnumerator ReloadRoutine()
    {
        while (Time.time < reloadEndTime)
        {
            yield return null;
        }

        int loaded = PerformRefill();
        IsReloading = false;
        reloadRoutine = null;

        Debug.Log($"[AR Ammo] Reload finished | loaded:{loaded} | mag:{CurrentMagazine}/{GetEffectiveMagazineCapacity()} reserve:{(data.infiniteReserve ? "??" : CurrentReserve.ToString())}");
        OnAmmoChanged?.Invoke(CurrentMagazine, CurrentReserve, IsReloading);
    }

    private int PerformRefill()
    {
        if (data == null) return 0;
        int cap = Mathf.Max(0, GetEffectiveMagazineCapacity());
        int need = Mathf.Max(0, cap - CurrentMagazine);
        if (need <= 0) return 0;

        int load = need;
        if (!data.infiniteReserve)
        {
            load = Mathf.Min(need, Mathf.Max(0, CurrentReserve));
            CurrentReserve = Mathf.Max(0, CurrentReserve - load);
        }
        CurrentMagazine += load;
        return load;
    }

    public void InterruptReload()
    {
        if (reloadRoutine != null)
        {
            StopCoroutine(reloadRoutine);
            reloadRoutine = null;
        }
        if (IsReloading)
        {
            IsReloading = false;
            Debug.Log("[AR Ammo] Reload interrupted");
            OnAmmoChanged?.Invoke(CurrentMagazine, CurrentReserve, IsReloading);
        }
    }

    public float GetReloadRemaining()
    {
        if (!IsReloading) return 0f;
        return Mathf.Max(0f, reloadEndTime - Time.time);
    }

    public bool IsMagazineEmpty() => CurrentMagazine <= 0;
    public bool HasAnyReserveOrInfinite() => data != null && (data.infiniteReserve || CurrentReserve > 0);

    public void LoadSnapshot(int magazine, int reserve, bool triggerAutoReload = true)
    {
        if (data == null) return;
        CurrentMagazine = Mathf.Clamp(magazine, 0, GetEffectiveMagazineCapacity());
        CurrentReserve = data.infiniteReserve ? 0 : Mathf.Max(0, reserve);

        IsReloading = false;
        lastSeenEffectiveMagazineCapacityForExtendedApply = GetEffectiveMagazineCapacity();
        if (triggerAutoReload && IsMagazineEmpty() && HasAnyReserveOrInfinite() && data.autoReloadOnEmpty)
        {
            TryStartReload();
        }

        Debug.Log($"[AR Ammo] Snapshot applied ?? mag:{CurrentMagazine}/{GetEffectiveMagazineCapacity()} reserve:{(data.infiniteReserve ? "??" : CurrentReserve.ToString())}");
        OnAmmoChanged?.Invoke(CurrentMagazine, CurrentReserve, IsReloading);
    }
}