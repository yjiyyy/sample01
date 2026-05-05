using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Gun/Shotgun ?? ???(?????/??????) ????.
/// Initialize(WeaponDataSO / WeaponDataSO_Gun) API?? ????.
/// </summary>
[DisallowMultipleComponent]
public class WeaponAmmoRuntime : MonoBehaviour
{
    // underlying gun SO (if available)
    private WeaponDataSO_Gun gunData;

    // generic spec when SO type is not Gun
    private struct AmmoSpec
    {
        public bool usesAmmo;
        public int magazineSize;
        public int initialReserve;
        public bool infiniteReserve;
        public float reloadTime;
        public bool autoReloadOnEmpty;
        public int consumePerShot;
    }
    private AmmoSpec spec;
    private bool usingSpec = false; // true -> using spec instead of gunData

    public int CurrentMagazine { get; private set; }
    public int CurrentReserve { get; private set; }
    public bool IsReloading { get; private set; }

    private Coroutine reloadRoutine;
    private float reloadEndTime;

    /// <summary>?? ?????? ?? ???????? ?????. ???? ???? SO ????.</summary>
    private WeaponDataSO reloadModWeaponRef;

    private int lastSeenEffectiveMagazineCapacityForExtendedApply = -1;

    private bool initialized;
    public bool IsInitialized => initialized;

    // ????: (magazine, reserve, isReloading)
    public event Action<int, int, bool> OnAmmoChanged;

    #region Initialize overloads

    public void Initialize(WeaponDataSO_Gun data, bool force = false)
    {
        if (!force && initialized && gunData == data)
            return;

        usingSpec = false;
        gunData = data;
        reloadModWeaponRef = data;

        if (reloadRoutine != null)
        {
            StopCoroutine(reloadRoutine);
            reloadRoutine = null;
        }

        if (gunData != null && gunData.usesAmmo)
        {
            int cap = GetEffectiveMagazineCapacity();
            CurrentMagazine = Mathf.Min(Mathf.Max(0, gunData.magazineSize), cap);
            CurrentReserve = Mathf.Max(0, gunData.initialReserve);
            IsReloading = false;
        }
        else
        {
            CurrentMagazine = 0;
            CurrentReserve = 0;
            IsReloading = false;
        }

        initialized = true;
        lastSeenEffectiveMagazineCapacityForExtendedApply = -1;
        Debug.Log($"[Ammo] (Re)Init ?? mag:{CurrentMagazine}/{GetEffectiveMagazineCapacity()} reserve:{(GetInfiniteReserve() ? "??" : CurrentReserve.ToString())}");

        OnAmmoChanged?.Invoke(CurrentMagazine, CurrentReserve, IsReloading);
    }

    public void Initialize(WeaponDataSO data, bool force = false)
    {
        if (!force && initialized)
        {
            // if already initialized and same underlying config, attempt to skip.
            // For simplicity we don't compare by reference here.
            // But if previously initialized by Gun SO, reinit when different type desired -> allow reinit by force.
        }

        // If data is WeaponDataSO_Gun, reuse existing strongly-typed initializer for full compatibility.
        if (data is WeaponDataSO_Gun g)
        {
            Initialize(g, force);
            return;
        }

        // Extract ammo-related fields from arbitrary WeaponDataSO via reflection.
        AmmoSpec newSpec = ExtractSpecFromSO(data);

        // If not using ammo, just initialize accordingly.
        usingSpec = true;
        gunData = null;
        spec = newSpec;
        reloadModWeaponRef = data;

        if (reloadRoutine != null)
        {
            StopCoroutine(reloadRoutine);
            reloadRoutine = null;
        }

        if (spec.usesAmmo)
        {
            int cap = GetEffectiveMagazineCapacity();
            CurrentMagazine = Mathf.Min(Mathf.Max(0, spec.magazineSize), cap);
            CurrentReserve = Mathf.Max(0, spec.initialReserve);
            IsReloading = false;
        }
        else
        {
            CurrentMagazine = 0;
            CurrentReserve = 0;
            IsReloading = false;
        }

        initialized = true;
        lastSeenEffectiveMagazineCapacityForExtendedApply = -1;
        Debug.Log($"[Ammo] (Re)Init (generic) ?? mag:{CurrentMagazine}/{GetEffectiveMagazineCapacity()} reserve:{(GetInfiniteReserve() ? "??" : CurrentReserve.ToString())}");

        OnAmmoChanged?.Invoke(CurrentMagazine, CurrentReserve, IsReloading);
    }

    #endregion

    #region Helpers: read config (unify gunData/spec access)

    private int GetBaseMagazineSize()
    {
        if (!usingSpec && gunData != null) return gunData.magazineSize;
        return Mathf.Max(0, spec.magazineSize);
    }

    private int GetExtendedMagazineBonusFromUpgrades()
    {
        if (reloadModWeaponRef == null)
            return 0;

        GameObject ownerRoot = transform.root != null ? transform.root.gameObject : gameObject;
        return PlayerWeaponDamageModifiers.GetExtendedMagazineBonusCount(ownerRoot, reloadModWeaponRef);
    }

    /// <summary>SO base magazine plus extended-magazine upgrade bonus.</summary>
    public int GetEffectiveMagazineCapacity()
    {
        return Mathf.Max(0, GetBaseMagazineSize() + GetExtendedMagazineBonusFromUpgrades());
    }

    /// <summary>Runtime max magazine for UI (SlotView reflection). Can exceed SO magazineSize.</summary>
    public int EffectiveMagazineCapacity => GetEffectiveMagazineCapacity();

    /// <summary>
    /// After upgrade slots change: interrupt reload if needed, fill magazine to new capacity (reserve unchanged).
    /// </summary>
    public void ApplyExtendedMagazineAfterUpgrades()
    {
        if (!GetUsesAmmo() || !initialized || reloadModWeaponRef == null)
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
            CurrentMagazine = newCap;
        }
        else
        {
            CurrentMagazine = Mathf.Min(CurrentMagazine, newCap);
        }

        lastSeenEffectiveMagazineCapacityForExtendedApply = newCap;
        OnAmmoChanged?.Invoke(CurrentMagazine, CurrentReserve, IsReloading);
    }
    private int GetInitialReserve()
    {
        if (!usingSpec && gunData != null) return gunData.initialReserve;
        return Mathf.Max(0, spec.initialReserve);
    }
    private bool GetInfiniteReserve()
    {
        if (!usingSpec && gunData != null) return gunData.infiniteReserve;
        return spec.infiniteReserve;
    }
    private float GetReloadTime()
    {
        if (!usingSpec && gunData != null) return gunData.reloadTime;
        return spec.reloadTime;
    }

    private float GetReloadDurationAfterQuickReload()
    {
        float baseRt = Mathf.Max(0f, GetReloadTime());
        if (baseRt <= 0f || reloadModWeaponRef == null)
            return baseRt;

        GameObject ownerRoot = transform.root != null ? transform.root.gameObject : gameObject;
        return PlayerWeaponDamageModifiers.GetReloadTimeWithQuickReload(ownerRoot, reloadModWeaponRef, baseRt);
    }
    private bool GetAutoReloadOnEmpty()
    {
        if (!usingSpec && gunData != null) return gunData.autoReloadOnEmpty;
        return spec.autoReloadOnEmpty;
    }
    private bool GetUsesAmmo()
    {
        if (!usingSpec && gunData != null) return gunData.usesAmmo;
        return spec.usesAmmo;
    }

    /// <summary>
    /// Reflection helper: try to read common ammo fields from arbitrary WeaponDataSO.
    /// If a field is missing, a sensible default is used.
    /// Expected field names (matching Gun SO):
    /// - usesAmmo (bool)
    /// - magazineSize (int)
    /// - initialReserve (int)
    /// - infiniteReserve (bool)
    /// - reloadTime (float)
    /// - autoReloadOnEmpty (bool)
    /// - consumePerShot (int)  // optional
    /// </summary>
    private AmmoSpec ExtractSpecFromSO(WeaponDataSO data)
    {
        AmmoSpec s = new AmmoSpec
        {
            usesAmmo = false,
            magazineSize = 0,
            initialReserve = 0,
            infiniteReserve = false,
            reloadTime = 0f,
            autoReloadOnEmpty = false,
            consumePerShot = 1
        };

        if (data == null) return s;

        var t = data.GetType();
        BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

        FieldInfo f;

        f = t.GetField("usesAmmo", flags);
        if (f != null && f.FieldType == typeof(bool)) s.usesAmmo = (bool)f.GetValue(data);

        f = t.GetField("magazineSize", flags);
        if (f != null && f.FieldType == typeof(int)) s.magazineSize = (int)f.GetValue(data);

        f = t.GetField("initialReserve", flags);
        if (f != null && f.FieldType == typeof(int)) s.initialReserve = (int)f.GetValue(data);

        f = t.GetField("infiniteReserve", flags);
        if (f != null && f.FieldType == typeof(bool)) s.infiniteReserve = (bool)f.GetValue(data);

        f = t.GetField("reloadTime", flags);
        if (f != null && (f.FieldType == typeof(float) || f.FieldType == typeof(double)))
            s.reloadTime = (float)f.GetValue(data);

        f = t.GetField("autoReloadOnEmpty", flags);
        if (f != null && f.FieldType == typeof(bool)) s.autoReloadOnEmpty = (bool)f.GetValue(data);

        f = t.GetField("consumePerShot", flags);
        if (f != null && f.FieldType == typeof(int)) s.consumePerShot = (int)f.GetValue(data);

        // Also try properties if fields not present (defensive)
        if (!s.usesAmmo)
        {
            var p = t.GetProperty("usesAmmo", flags);
            if (p != null && p.PropertyType == typeof(bool)) s.usesAmmo = (bool)p.GetValue(data);
        }

        return s;
    }

    #endregion

    public bool UsesAmmo => GetUsesAmmo();
    public bool IsMagazineEmpty() => CurrentMagazine <= 0;
    public bool HasAnyReserveOrInfinite() => GetInfiniteReserve() || CurrentReserve > 0;

    public float GetReloadRemaining()
    {
        if (!IsReloading) return 0f;
        return Mathf.Max(0f, reloadEndTime - Time.time);
    }

    public bool CanFire(int consume = 1)
    {
        if (!UsesAmmo) return true;
        if (IsReloading) return false;
        return CurrentMagazine >= Mathf.Max(1, consume);
    }

    public bool TryConsumeForShot(int amount)
    {
        if (!UsesAmmo) return true;
        if (IsReloading) return false;
        if (!initialized)
        {
            Debug.LogWarning("[Ammo] ???????? ????? TryConsumeForShot ????.");
            return false;
        }

        int need = Mathf.Max(1, amount);
        if (CurrentMagazine < need)
            return false;

        CurrentMagazine -= need;

        Debug.Log($"[Ammo] ???! ????: {CurrentMagazine}/{GetEffectiveMagazineCapacity()} (????: {(GetInfiniteReserve() ? "??" : CurrentReserve.ToString())})");

        OnAmmoChanged?.Invoke(CurrentMagazine, CurrentReserve, IsReloading);

        if (GetAutoReloadOnEmpty() && CurrentMagazine <= 0)
            TryStartReload();

        return true;
    }

    public bool TryStartReload()
    {
        if (!GetUsesAmmo()) return false;
        if (IsReloading) return false;
        if (!initialized) return false;
        if (GetEffectiveMagazineCapacity() <= 0) return false; // cap zero guard
        if (GetInfiniteReserve() == false && CurrentReserve <= 0) return false;
        if (CurrentMagazine >= GetEffectiveMagazineCapacity()) return false;

        float rt = GetReloadDurationAfterQuickReload();
        Debug.Log($"[Ammo] ?????? ???? (????:{(GetInfiniteReserve() ? "??" : CurrentReserve.ToString())}, ????:{rt:F2})");

        if (rt <= 0f)
        {
            int loadedInstant = PerformRefill();
            string reserveStr = GetInfiniteReserve() ? "??" : CurrentReserve.ToString();
            Debug.Log($"[Ammo] ?????? ??? ??? | ???:{loadedInstant} | mag:{CurrentMagazine}/{GetEffectiveMagazineCapacity()} | reserve:{reserveStr}");

            OnAmmoChanged?.Invoke(CurrentMagazine, CurrentReserve, IsReloading);
            return true;
        }

        IsReloading = true;
        reloadEndTime = Time.time + rt;
        reloadRoutine = StartCoroutine(ReloadRoutine());

        OnAmmoChanged?.Invoke(CurrentMagazine, CurrentReserve, IsReloading);
        return true;
    }

    private IEnumerator ReloadRoutine()
    {
        while (Time.time < reloadEndTime)
            yield return null;

        int loaded = PerformRefill();
        IsReloading = false;
        reloadRoutine = null;

        string reserveStr = GetInfiniteReserve() ? "??" : CurrentReserve.ToString();
        Debug.Log($"[Ammo] ?????? ??? | ???:{loaded} | mag:{CurrentMagazine}/{GetEffectiveMagazineCapacity()} | reserve:{reserveStr}");

        OnAmmoChanged?.Invoke(CurrentMagazine, CurrentReserve, IsReloading);
    }

    private int PerformRefill()
    {
        int cap = Mathf.Max(0, GetEffectiveMagazineCapacity());
        int need = Mathf.Max(0, cap - CurrentMagazine);
        if (need <= 0) return 0;

        int load = need;
        if (!GetInfiniteReserve())
        {
            load = Mathf.Min(need, Mathf.Max(0, CurrentReserve));
            CurrentReserve = Mathf.Max(0, CurrentReserve - load);
        }
        CurrentMagazine += load;

        OnAmmoChanged?.Invoke(CurrentMagazine, CurrentReserve, IsReloading);

        return load;
    }

    public void InterruptReload()
    {
        if (!IsReloading) return;
        if (reloadRoutine != null)
        {
            StopCoroutine(reloadRoutine);
            reloadRoutine = null;
        }
        IsReloading = false;
        OnAmmoChanged?.Invoke(CurrentMagazine, CurrentReserve, IsReloading);
    }

    /// <summary>
    /// ??????? snapshot(?????/??????) ????.
    /// - triggerAutoReload=true?? mag==0?? ?? ???? ?????? auto reload ???
    /// </summary>
    public void LoadSnapshot(int magazine, int reserve, bool triggerAutoReload = true)
    {
        if (!GetUsesAmmo())
            return;

        int cap = Mathf.Max(0, GetEffectiveMagazineCapacity());
        CurrentMagazine = Mathf.Clamp(magazine, 0, cap);

        if (!GetInfiniteReserve())
            CurrentReserve = Mathf.Max(0, reserve);

        IsReloading = false;
        lastSeenEffectiveMagazineCapacityForExtendedApply = GetEffectiveMagazineCapacity();
        if (triggerAutoReload &&
            CurrentMagazine <= 0 &&
            HasAnyReserveOrInfinite() &&
            GetAutoReloadOnEmpty())
        {
            TryStartReload();
        }

        Debug.Log($"[Ammo] Snapshot ???? ?? mag:{CurrentMagazine}/{GetEffectiveMagazineCapacity()} reserve:{(GetInfiniteReserve() ? "??" : CurrentReserve.ToString())}");

        OnAmmoChanged?.Invoke(CurrentMagazine, CurrentReserve, IsReloading);
    }
}