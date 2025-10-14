using System.Collections;
using UnityEngine;

/// <summary>
/// Gun 전용 런타임 탄약/리로드 관리
/// </summary>
[DisallowMultipleComponent]
public class WeaponAmmoRuntime : MonoBehaviour
{
    private WeaponDataSO_Gun gunData;

    public int CurrentMagazine { get; private set; }
    public int CurrentReserve { get; private set; }
    public bool IsReloading { get; private set; }

    private Coroutine reloadRoutine;
    private float reloadEndTime;

    private bool initialized;
    public bool IsInitialized => initialized;

    public void Initialize(WeaponDataSO_Gun data, bool force = false)
    {
        if (!force && initialized && gunData == data)
            return;

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
        Debug.Log($"[Ammo] (Re)Init ▶ mag:{CurrentMagazine}/{gunData?.magazineSize} reserve:{(gunData == null ? "N/A" : (gunData.infiniteReserve ? "∞" : CurrentReserve.ToString()))}");
    }

    public bool UsesAmmo => gunData != null && gunData.usesAmmo;
    public bool IsMagazineEmpty() => CurrentMagazine <= 0;
    public bool HasAnyReserveOrInfinite() => gunData != null && (gunData.infiniteReserve || CurrentReserve > 0);

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
            Debug.LogWarning("[Ammo] 초기화되지 않은 상태에서 TryConsumeForShot 호출됨.");
            return false;
        }

        int need = Mathf.Max(1, amount);
        if (CurrentMagazine < need)
            return false;

        CurrentMagazine -= need;

        Debug.Log($"[Ammo] 발사! 남은 탄창: {CurrentMagazine}/{gunData.magazineSize} (예비: {(gunData.infiniteReserve ? "∞" : CurrentReserve.ToString())})");

        if (gunData.autoReloadOnEmpty && CurrentMagazine <= 0)
            TryStartReload();

        return true;
    }

    public bool TryStartReload()
    {
        if (!UsesAmmo) return false;
        if (IsReloading) return false;
        if (!initialized) return false;
        if (gunData == null) return false;
        if (CurrentMagazine >= gunData.magazineSize) return false;
        if (!gunData.infiniteReserve && CurrentReserve <= 0) return false;

        float rt = gunData.reloadTime;
        Debug.Log($"[Ammo] 탄창 소진 → 리로드 시작 (예비: {(gunData.infiniteReserve ? "∞" : CurrentReserve.ToString())}, 리로드시간:{rt:F2})");

        if (gunData.reloadTime <= 0f)
        {
            int loadedInstant = PerformRefill();
            string reserveStr = gunData.infiniteReserve ? "∞" : CurrentReserve.ToString();
            Debug.Log($"[Ammo] 리로드 완료 | 채운 탄:{loadedInstant} | 탄창:{CurrentMagazine}/{gunData.magazineSize} | 예비:{reserveStr}");
            return true;
        }

        IsReloading = true;
        reloadEndTime = Time.time + gunData.reloadTime;
        reloadRoutine = StartCoroutine(ReloadRoutine());
        return true;
    }

    private IEnumerator ReloadRoutine()
    {
        while (Time.time < reloadEndTime)
            yield return null;

        int loaded = PerformRefill();
        IsReloading = false;
        reloadRoutine = null;

        string reserveStr = gunData.infiniteReserve ? "∞" : CurrentReserve.ToString();
        Debug.Log($"[Ammo] 리로드 완료 | 채운 탄:{loaded} | 탄창:{CurrentMagazine}/{gunData.magazineSize} | 예비:{reserveStr}");
    }

    private int PerformRefill()
    {
        if (gunData == null) return 0;

        int cap = Mathf.Max(0, gunData.magazineSize);
        int need = Mathf.Max(0, cap - CurrentMagazine);
        if (need <= 0) return 0;

        int load = need;
        if (!gunData.infiniteReserve)
        {
            load = Mathf.Min(need, Mathf.Max(0, CurrentReserve));
            CurrentReserve = Mathf.Max(0, CurrentReserve - load);
        }
        CurrentMagazine += load;
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
        // 로그 생략 (필요 시 추가)
    }

    /// <summary>
    /// 무기 교체 후 탄약 상태 복원.
    /// 리로드 진행 중이던 상태는 ‘캔슬’로 보고 IsReloading=false로 고정.
    /// infiniteReserve면 전달된 reserve는 무시.
    /// triggerAutoReload=true이면 탄창 0 & 예비(또는 무한) 존재 & autoReloadOnEmpty=true → 자동 리로드.
    /// </summary>
    public void LoadSnapshot(int magazine, int reserve, bool triggerAutoReload = true)
    {
        if (gunData == null || !gunData.usesAmmo)
            return;

        int cap = Mathf.Max(0, gunData.magazineSize);
        CurrentMagazine = Mathf.Clamp(magazine, 0, cap);

        if (!gunData.infiniteReserve)
            CurrentReserve = Mathf.Max(0, reserve);

        IsReloading = false;
        if (triggerAutoReload &&
            CurrentMagazine <= 0 &&
            HasAnyReserveOrInfinite() &&
            gunData.autoReloadOnEmpty)
        {
            TryStartReload(); // 조건부 자동 리로드
        }

        Debug.Log($"[Ammo] 스냅샷 복원 ▶ mag:{CurrentMagazine}/{gunData.magazineSize} reserve:{(gunData.infiniteReserve ? "∞" : CurrentReserve.ToString())}");
    }
}