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

    public void Initialize(WeaponDataSO_AR arData, bool force = false)
    {
        if (!force && IsInitialized && data == arData) return;

        data = arData;
        if (data == null)
        {
            Debug.LogWarning("[AR Ammo] 데이터가 비어 있습니다.");
            return;
        }

        IsReloading = false;
        if (data.usesAmmo)
        {
            // 초기화 규칙: 기존 값 보존, 완전 신규면 initialReserve에서 채움
            if (CurrentMagazine == 0 && CurrentReserve == 0)
            {
                CurrentMagazine = Mathf.Min(data.magazineSize, data.initialReserve);
                CurrentReserve = Mathf.Max(0, data.initialReserve - CurrentMagazine);
            }
            else
            {
                CurrentMagazine = Mathf.Clamp(CurrentMagazine, 0, data.magazineSize);
                if (!data.infiniteReserve)
                    CurrentReserve = Mathf.Max(0, CurrentReserve);
                else
                    CurrentReserve = 0;
            }
        }
        else
        {
            CurrentMagazine = data.magazineSize;
            CurrentReserve = 0;
        }

        IsInitialized = true;
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
        return true;
    }

    public bool TryStartReload()
    {
        if (data == null) return false;
        if (!data.usesAmmo) return false;
        if (IsReloading) return false;
        if (!data.infiniteReserve && CurrentReserve <= 0) return false;
        if (CurrentMagazine >= data.magazineSize) return false;

        if (reloadRoutine != null) StopCoroutine(reloadRoutine);
        reloadRoutine = StartCoroutine(ReloadRoutine());
        return true;
    }

    public void InterruptReload()
    {
        if (reloadRoutine != null)
        {
            StopCoroutine(reloadRoutine);
            reloadRoutine = null;
        }
        IsReloading = false;
    }

    private IEnumerator ReloadRoutine()
    {
        IsReloading = true;
        reloadEndTime = Time.time + Mathf.Max(0f, data.reloadTime);

        while (Time.time < reloadEndTime)
        {
            yield return null;
        }

        if (data.infiniteReserve)
        {
            CurrentMagazine = data.magazineSize;
        }
        else
        {
            int need = data.magazineSize - CurrentMagazine;
            int used = Mathf.Min(need, CurrentReserve);
            CurrentMagazine += used;
            CurrentReserve -= used;
        }

        IsReloading = false;
        reloadRoutine = null;
    }

    public float GetReloadRemaining()
    {
        if (!IsReloading) return 0f;
        return Mathf.Max(0f, reloadEndTime - Time.time);
    }

    public bool IsMagazineEmpty() => CurrentMagazine <= 0;
    public bool HasAnyReserveOrInfinite() => data != null && (data.infiniteReserve || CurrentReserve > 0);

    public void LoadSnapshot(int magazine, int reserve, bool triggerAutoReload)
    {
        if (data == null) return;
        CurrentMagazine = Mathf.Clamp(magazine, 0, data.magazineSize);
        CurrentReserve = data.infiniteReserve ? 0 : Mathf.Max(0, reserve);

        if (triggerAutoReload && IsMagazineEmpty() && HasAnyReserveOrInfinite())
            TryStartReload();
    }
}