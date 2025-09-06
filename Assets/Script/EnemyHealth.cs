using UnityEngine;

/// <summary>
/// 적 전용 체력 관리 시스템 (경량화)
/// 단순한 체력/넉백/사망 처리만 담당
/// </summary>
public class EnemyHealth : MonoBehaviour
{
    [Header("체력")]
    public float maxHP = 100f;
    private float currentHP;

    // weight 제거 - Enemy.cs에서 자체 관리

    void Awake()
    {
        currentHP = maxHP;
    }

    public void ApplyDamage(float amount)
    {
        ApplyDamage(amount, Vector3.zero, null, 1f);
    }

    public void ApplyDamage(float amount, WeaponDataSO weapon)
    {
        ApplyDamage(amount, Vector3.zero, weapon, 1f);
    }

    public void ApplyDamage(float amount, Vector3 hitDir, WeaponDataSO weapon)
    {
        ApplyDamage(amount, hitDir, weapon, 1f);
    }

    public void ApplyDamage(float amount, Vector3 hitDir, WeaponDataSO weapon, float impactScale)
    {
        currentHP -= amount;
        Debug.Log($"{gameObject.name}이(가) {amount:F1} 피해! scale:{impactScale:F2} | HP: {currentHP:F1}");

        if (currentHP <= 0f)
        {
            Die(hitDir, weapon, impactScale);
        }
    }

    public void Heal(float amount)
    {
        if (amount <= 0f) return;

        currentHP += amount;
        currentHP = Mathf.Clamp(currentHP, 0f, maxHP);

        Debug.Log($"{gameObject.name}이(가) {amount:F1} 회복됨 → 현재 HP: {currentHP:F1}");
    }

    private void Die(Vector3 hitDir, WeaponDataSO weapon, float impactScale = 1f)
    {
        Debug.Log($"{gameObject.name} 사망");

        if (TryGetComponent(out Enemy enemy))
        {
            enemy.Die(hitDir, weapon, impactScale);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 유틸 메서드들 (GetWeight 제거됨)
    public void SetHealth(float value) => currentHP = Mathf.Clamp(value, 0f, maxHP);
    public float GetCurrentHP() => currentHP;
    public float GetMaxHP() => maxHP;

    // GetWeight() 메서드 제거됨!
}