using UnityEngine;

[CreateAssetMenu(menuName = "Weapon/Melee")]
public class WeaponDataSO_Melee : WeaponDataSO
{
    private void OnValidate()
    {
        weaponCategory = WeaponCategory.Bat;
        isMelee = true;

        // 투사체/폭발 비활성 기본값
        isExplosiveProjectile = false;
        projectileCount = Mathf.Max(1, projectileCount);
        projectileSpeed = Mathf.Max(0f, projectileSpeed);
        projectileLifetime = Mathf.Max(0f, projectileLifetime);

        // 샷건 디폴트는 의미 없지만 안전한 기본값
        shotgunAngle = Mathf.Clamp(shotgunAngle, 1f, 360f);
        shotgunRadius = Mathf.Max(0f, shotgunRadius);
        shotgunFalloffMin = Mathf.Clamp01(shotgunFalloffMin);
    }
}