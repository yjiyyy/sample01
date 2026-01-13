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

    // UI/외부 연동용 이벤트: (magazine, reserve, isReloading)
    public event Action<int, int, bool> OnAmmoChanged;

    public void Initialize(WeaponDataSO_AR arData, bool force = false)
    {
        if (!force && IsInitialized && data == arData) return;

        data = arData;
        if (data == null)
        {
            Debug.LogWarning("[AR Ammo] Initialize called with null data.");
            return;
        }

        // 일관된 초기화: 기본적으로 SO에 정의된 값으로 세팅
        IsReloading = false;
        CurrentMagazine = Mathf.Clamp(data.magazineSize, 0, int.MaxValue);
        CurrentReserve = data.infiniteReserve ? 0 : Mathf.Max(0, data.initialReserve);

        IsInitialized = true;
        Debug.Log($"[AR Ammo] Init → mag:{CurrentMagazine}/{data.magazineSize} reserve:{(data.infiniteReserve ? "∞" : CurrentReserve.ToString())}");

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
        Debug.Log($"[AR Ammo] 소비: mag now {CurrentMagazine}/{data.magazineSize} reserve:{(data.infiniteReserve ? "∞" : CurrentReserve.ToString())}");

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
        if (CurrentMagazine >= data.magazineSize) return false;

        float rt = Mathf.Max(0f, data.reloadTime);
        if (rt <= 0f)
        {
            // 즉시 보충
            PerformRefill();
            Debug.Log($"[AR Ammo] Reload instant complete → mag:{CurrentMagazine}/{data.magazineSize} reserve:{(data.infiniteReserve ? "∞" : CurrentReserve.ToString())}");
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

        Debug.Log($"[AR Ammo] Reload finished | loaded:{loaded} | mag:{CurrentMagazine}/{data.magazineSize} reserve:{(data.infiniteReserve ? "∞" : CurrentReserve.ToString())}");
        OnAmmoChanged?.Invoke(CurrentMagazine, CurrentReserve, IsReloading);
    }

    private int PerformRefill()
    {
        if (data == null) return 0;
        int cap = Mathf.Max(0, data.magazineSize);
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
        CurrentMagazine = Mathf.Clamp(magazine, 0, data.magazineSize);
        CurrentReserve = data.infiniteReserve ? 0 : Mathf.Max(0, reserve);

        IsReloading = false;
        if (triggerAutoReload && IsMagazineEmpty() && HasAnyReserveOrInfinite() && data.autoReloadOnEmpty)
        {
            TryStartReload();
        }

        Debug.Log($"[AR Ammo] Snapshot applied → mag:{CurrentMagazine}/{data.magazineSize} reserve:{(data.infiniteReserve ? "∞" : CurrentReserve.ToString())}");
        OnAmmoChanged?.Invoke(CurrentMagazine, CurrentReserve, IsReloading);
    }
}