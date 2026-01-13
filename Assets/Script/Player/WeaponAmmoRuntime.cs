using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Gun/Shotgun 등 탄약(매거진/리저브) 관리.
/// Initialize(WeaponDataSO / WeaponDataSO_Gun) API로 초기화.
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

    private bool initialized;
    public bool IsInitialized => initialized;

    // 이벤트: (magazine, reserve, isReloading)
    public event Action<int, int, bool> OnAmmoChanged;

    #region Initialize overloads

    public void Initialize(WeaponDataSO_Gun data, bool force = false)
    {
        if (!force && initialized && gunData == data)
            return;

        usingSpec = false;
        gunData = data;

        if (reloadRoutine != null)
        {
            StopCoroutine(reloadRoutine);
            reloadRoutine = null;
        }

        if (gunData != null && gunData.usesAmmo)
        {
            CurrentMagazine = Mathf.Clamp(gunData.magazineSize, 0, int.MaxValue);
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
        Debug.Log($"[Ammo] (Re)Init → mag:{CurrentMagazine}/{GetMagazineSize()} reserve:{(GetInfiniteReserve() ? "∞" : CurrentReserve.ToString())}");

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

        if (reloadRoutine != null)
        {
            StopCoroutine(reloadRoutine);
            reloadRoutine = null;
        }

        if (spec.usesAmmo)
        {
            CurrentMagazine = Mathf.Clamp(spec.magazineSize, 0, int.MaxValue);
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
        Debug.Log($"[Ammo] (Re)Init (generic) → mag:{CurrentMagazine}/{GetMagazineSize()} reserve:{(GetInfiniteReserve() ? "∞" : CurrentReserve.ToString())}");

        OnAmmoChanged?.Invoke(CurrentMagazine, CurrentReserve, IsReloading);
    }

    #endregion

    #region Helpers: read config (unify gunData/spec access)

    private int GetMagazineSize()
    {
        if (!usingSpec && gunData != null) return gunData.magazineSize;
        return Mathf.Max(0, spec.magazineSize);
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
            Debug.LogWarning("[Ammo] 초기화되지 않았는데 TryConsumeForShot 호출됨.");
            return false;
        }

        int need = Mathf.Max(1, amount);
        if (CurrentMagazine < need)
            return false;

        CurrentMagazine -= need;

        Debug.Log($"[Ammo] 소비! 남은: {CurrentMagazine}/{GetMagazineSize()} (예비: {(GetInfiniteReserve() ? "∞" : CurrentReserve.ToString())})");

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
        if (GetMagazineSize() <= 0) return false; // cap zero guard
        if (GetInfiniteReserve() == false && CurrentReserve <= 0) return false;
        if (CurrentMagazine >= GetMagazineSize()) return false;

        float rt = GetReloadTime();
        Debug.Log($"[Ammo] 재장전 시작 (예비:{(GetInfiniteReserve() ? "∞" : CurrentReserve.ToString())}, 시간:{rt:F2})");

        if (rt <= 0f)
        {
            int loadedInstant = PerformRefill();
            string reserveStr = GetInfiniteReserve() ? "∞" : CurrentReserve.ToString();
            Debug.Log($"[Ammo] 재장전 즉시 완료 | 채움:{loadedInstant} | mag:{CurrentMagazine}/{GetMagazineSize()} | reserve:{reserveStr}");

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

        string reserveStr = GetInfiniteReserve() ? "∞" : CurrentReserve.ToString();
        Debug.Log($"[Ammo] 재장전 완료 | 채움:{loaded} | mag:{CurrentMagazine}/{GetMagazineSize()} | reserve:{reserveStr}");

        OnAmmoChanged?.Invoke(CurrentMagazine, CurrentReserve, IsReloading);
    }

    private int PerformRefill()
    {
        int cap = Mathf.Max(0, GetMagazineSize());
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
    /// 외부에서 snapshot(매거진/리저브) 적용.
    /// - triggerAutoReload=true면 mag==0일 때 예비가 있으면 auto reload 시도
    /// </summary>
    public void LoadSnapshot(int magazine, int reserve, bool triggerAutoReload = true)
    {
        if (!GetUsesAmmo())
            return;

        int cap = Mathf.Max(0, GetMagazineSize());
        CurrentMagazine = Mathf.Clamp(magazine, 0, cap);

        if (!GetInfiniteReserve())
            CurrentReserve = Mathf.Max(0, reserve);

        IsReloading = false;
        if (triggerAutoReload &&
            CurrentMagazine <= 0 &&
            HasAnyReserveOrInfinite() &&
            GetAutoReloadOnEmpty())
        {
            TryStartReload();
        }

        Debug.Log($"[Ammo] Snapshot 적용 → mag:{CurrentMagazine}/{GetMagazineSize()} reserve:{(GetInfiniteReserve() ? "∞" : CurrentReserve.ToString())}");

        OnAmmoChanged?.Invoke(CurrentMagazine, CurrentReserve, IsReloading);
    }
}