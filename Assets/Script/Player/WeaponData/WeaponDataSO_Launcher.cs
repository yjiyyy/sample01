using UnityEngine;

[CreateAssetMenu(menuName = "Weapon/Launcher")]
public class WeaponDataSO_Launcher : WeaponDataSO
{
    private void OnValidate()
    {
        weaponCategory = WeaponCategory.Launcher;
        isMelee = false;

        // 폭발형 기본값
        isExplosiveProjectile = true;
        explosiveRadius = Mathf.Max(0f, explosiveRadius);
        explosiveEdgeMul = Mathf.Clamp01(explosiveEdgeMul);

        projectileCount = Mathf.Max(1, projectileCount);
        projectileSpeed = Mathf.Max(0f, projectileSpeed);
        projectileLifetime = Mathf.Max(0f, projectileLifetime);
    }
}