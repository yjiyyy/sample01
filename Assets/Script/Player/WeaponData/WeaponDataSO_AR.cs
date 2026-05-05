using UnityEngine;

public enum ARFireInputMode
{
    HoldWhilePressed,
    TapTimed
}

public enum ARFacingMode
{
    LockAtStart,
    MoveDirection,
    NearestTargetAutoAim
}

[CreateAssetMenu(menuName = "Player/AssaultRifle")]
public class WeaponDataSO_AR : WeaponDataSO
{
    [Header("Fire Input")]
    [Tooltip("Fire input style. HoldWhilePressed: auto fire while held, TapTimed: auto fire for a fixed duration after tap. [Sniper] Not used.")]
    public ARFireInputMode fireInputMode = ARFireInputMode.HoldWhilePressed;

    [Tooltip("Facing mode while firing. [Sniper] Uses only LockAtStart or MoveDirection (NearestTargetAutoAim ignored).")]
    public ARFacingMode facingMode = ARFacingMode.LockAtStart;

    [Tooltip("Lock character rotation while firing. [Sniper] Applied only when FacingMode is LockAtStart.")]
    public bool lockRotationDuringFiring = true;

    [Tooltip("Allow movement while firing.")]
    public bool allowMoveWhileFiring = true;

    [Header("Projectile")]
    [Tooltip("Projectile speed (m/s).")]
    public float projectileSpeed = 20f;
    [Tooltip("Projectile lifetime (seconds).")]
    public float projectileLifetime = 5f;
    [Tooltip("Additional pierce count. 0 = no extra pierce.")]
    public int pierceCount = 0;

    [Header("Ammo")]
    [Tooltip("Use ammo and reload system.")]
    public bool usesAmmo = true;
    [Tooltip("Magazine size.")]
    public int magazineSize = 30;
    [Tooltip("Initial reserve ammo.")]
    public int initialReserve = 90;
    [Tooltip("Infinite reserve ammo.")]
    public bool infiniteReserve = false;
    [Tooltip("Reload time (seconds).")]
    public float reloadTime = 2.0f;
    [Tooltip("Ammo consumed per shot.")]
    public int consumePerShot = 1;

    [Tooltip("Auto reload when magazine is empty.")]
    public bool autoReloadOnEmpty = true;

    [Tooltip("If true, resume firing automatically while attack is still held after reload. [Sniper] Not used.")]
    public bool autoReloadResumeWhileHeld = false;

    [Header("TapTimed Mode")]
    [Tooltip("Auto fire duration after a tap (seconds). Another tap refreshes to now + duration. [Sniper] Not used.")]
    public float tapFireDuration = 1.2f;

    [Header("Spread")]
    [Tooltip("Maximum spread angle (0..180). 0 = no spread.")]
    [Range(0f, 180f)]
    public float spreadAngle = 0f;

    [Tooltip("Use true for full 3D spread. False means yaw-only spread.")]
    public bool spread3D = true;

    [Header("Move/Anim While Firing")]
    [Tooltip("Movement speed multiplier while firing. 0 = stop, 1 = normal speed.")]
    [Range(0f, 1f)]
    public float moveSpeedWhileFiring = 1f;

    [Tooltip("Lower body animation playback speed while firing. 1 = normal.")]
    [Range(0f, 2f)]
    public float animPlaybackSpeedWhileFiring = 1f;

#if UNITY_EDITOR
    private void OnValidate()
    {
        cooldown = Mathf.Max(0.01f, cooldown);

        projectileSpeed = Mathf.Max(0f, projectileSpeed);
        projectileLifetime = Mathf.Max(0.01f, projectileLifetime);
        pierceCount = Mathf.Max(0, pierceCount);

        magazineSize = Mathf.Max(0, magazineSize);
        initialReserve = Mathf.Max(0, initialReserve);
        reloadTime = Mathf.Max(0f, reloadTime);
        consumePerShot = Mathf.Max(1, consumePerShot);
        tapFireDuration = Mathf.Max(0.05f, tapFireDuration);

        spreadAngle = Mathf.Clamp(spreadAngle, 0f, 180f);
        moveSpeedWhileFiring = Mathf.Clamp01(moveSpeedWhileFiring);
        animPlaybackSpeedWhileFiring = Mathf.Max(0f, animPlaybackSpeedWhileFiring);
    }
#endif
}
