using UnityEngine;

[CreateAssetMenu(menuName = "Weapon/Launcher")]
public class WeaponDataSO_Launcher : WeaponDataSO
{
    [Header("Launcher 전용 - 투사체/폭발")]
    public float projectileLifetime = 5f;
    public float projectileSpeed = 10f;

    [Tooltip("폭발 반경")]
    public float explosiveRadius = 3f;
    [Range(0f, 1f)] public float explosiveEdgeMul = 0.2f;

    [Header("데미지 판정 대상")]
    public DamageTargetType damageTargetType = DamageTargetType.EnemyOnly;

    private void OnValidate()
    {
        projectileLifetime = Mathf.Max(0f, projectileLifetime);
        projectileSpeed = Mathf.Max(0f, projectileSpeed);
        explosiveRadius = Mathf.Max(0f, explosiveRadius);
        explosiveEdgeMul = Mathf.Clamp01(explosiveEdgeMul);
    }
}