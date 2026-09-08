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
    /// ApplyExtendedMagazineAfterUpgrades에서 직전 용량을 기억해 델타만 반영.
    /// (UpgradeEffectRuntime OnEnable 타이밍과 어긋나면 탄창이 두 번 채워질 수 있음).
    /// </summary>
    private int lastSeenEffectiveMagazineCapacityForExtendedApply = -1;

    // UI/HUD 갱신 이벤트: (magazine, reserve, isReloading)
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
            // 확장 탄창 슬롯 반영 시 탄창만 가득(예비탄 불변). 델타만큼만 더하면 체감상 ‘숫자만큼만’ 채워지는 느낌이 됨.
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

        // 초기 탄약: SO 값 + 확장 탄창 보너스를 반영(이후 Subscribe 타이밍이 달라도 Apply에서 맞춤)
        IsReloading = false;
        int cap = GetEffectiveMagazineCapacity();
        CurrentMagazine = Mathf.Min(Mathf.Max(0, data.magazineSize), cap);
        CurrentReserve = data.infiniteReserve ? 0 : Mathf.Max(0, data.initialReserve);

        IsInitialized = true;
        lastSeenEffectiveMagazineCapacityForExtendedApply = -1;
        Debug.Log($"[AR Ammo] Init 완료 mag:{CurrentMagazine}/{GetEffectiveMagazineCapacity()} reserve:{(data.infiniteReserve ? "무한" : CurrentReserve.ToString())}");

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
        Debug.Log($"[AR Ammo] 발사: mag now {CurrentMagazine}/{GetEffectiveMagazineCapacity()} reserve:{(data.infiniteReserve ? "무한" : CurrentReserve.ToString())}");

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
            // 즉시 리로드
            PerformRefill();
            Debug.Log($"[AR Ammo] Reload instant complete 완료 mag:{CurrentMagazine}/{GetEffectiveMagazineCapacity()} reserve:{(data.infiniteReserve ? "무한" : CurrentReserve.ToString())}");
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

        Debug.Log($"[AR Ammo] Reload finished | loaded:{loaded} | mag:{CurrentMagazine}/{GetEffectiveMagazineCapacity()} reserve:{(data.infiniteReserve ? "무한" : CurrentReserve.ToString())}");
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

        Debug.Log($"[AR Ammo] Snapshot applied 완료 mag:{CurrentMagazine}/{GetEffectiveMagazineCapacity()} reserve:{(data.infiniteReserve ? "무한" : CurrentReserve.ToString())}");
        OnAmmoChanged?.Invoke(CurrentMagazine, CurrentReserve, IsReloading);
    }
}