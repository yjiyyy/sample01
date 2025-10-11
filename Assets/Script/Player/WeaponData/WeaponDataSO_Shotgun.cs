using UnityEngine;

[CreateAssetMenu(menuName = "Weapon/Shotgun")]
public class WeaponDataSO_Shotgun : WeaponDataSO
{
    private void OnValidate()
    {
        weaponCategory = WeaponCategory.Shotgun;
        isMelee = false;
        isExplosiveProjectile = false;

        // ¼¦°Ç Àü¿ë
        shotgunAngle = Mathf.Clamp(shotgunAngle, 1f, 360f);
        shotgunRadius = Mathf.Max(0f, shotgunRadius);
        shotgunFalloffMin = Mathf.Clamp01(shotgunFalloffMin);
    }
}