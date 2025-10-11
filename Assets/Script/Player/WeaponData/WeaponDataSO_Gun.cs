using UnityEngine;

[CreateAssetMenu(menuName = "Weapon/Gun")]
public class WeaponDataSO_Gun : WeaponDataSO
{
    private void OnValidate()
    {
        weaponCategory = WeaponCategory.Gun;
        isMelee = false;
        isExplosiveProjectile = false;

        projectileCount = Mathf.Max(1, projectileCount);
        projectileSpeed = Mathf.Max(0f, projectileSpeed);
        projectileLifetime = Mathf.Max(0f, projectileLifetime);
    }
}