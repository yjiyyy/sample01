using UnityEngine;

[CreateAssetMenu(menuName = "Weapon/Gun")]
public class WeaponDataSO_Gun : WeaponDataSO
{
    [Header("Gun 전용 - 투사체")]
    public float projectileLifetime = 5f;
    public float projectileSpeed = 10f;
    [Tooltip("관통 가능한 횟수")]
    public int pierceCount = 0;

    private void OnValidate()
    {
        projectileLifetime = Mathf.Max(0f, projectileLifetime);
        projectileSpeed = Mathf.Max(0f, projectileSpeed);
        pierceCount = Mathf.Max(0, pierceCount);
    }
}