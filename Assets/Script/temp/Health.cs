using UnityEngine;

public class Health : MonoBehaviour
{
    [Header("체력")]
    public float maxHP = 100f;
    private float currentHP;

    [Header("피격 반응 (무게)")]
    [Tooltip("값이 클수록 넉백에 덜 밀림. PC/몬스터 공용 스탯")]
    public float weight = 1f;   // 1 = 기본, 2 = 절반만 밀림, 0.5 = 두 배로 밀림

    void Awake()
    {
        currentHP = maxHP;
    }

    /* ───────── 피해 처리 ───────── */
    // ✅ 단순 피해 (방향 없음)
    public void ApplyDamage(float amount)
    {
        ApplyDamage(amount, Vector3.zero, null, 1f);
    }

    // ✅ 무기 데이터만 전달 (방향 없음)
    public void ApplyDamage(float amount, WeaponDataSO weapon)
    {
        ApplyDamage(amount, Vector3.zero, weapon, 1f);
    }

    // ✅ 방향 + 무기 데이터 전달 (impactScale 기본값 1)
    public void ApplyDamage(float amount, Vector3 hitDir, WeaponDataSO weapon)
    {
        ApplyDamage(amount, hitDir, weapon, 1f);
    }

    // ✅ 최종 버전 (모든 인자 전달)
    public void ApplyDamage(float amount, Vector3 hitDir, WeaponDataSO weapon, float impactScale)
    {
        currentHP -= amount;
        Debug.Log($"{gameObject.name}이(가) {amount:F1} 피해! scale:{impactScale:F2} | HP: {currentHP:F1}");

        if (currentHP <= 0f)
        {
            Die(hitDir, weapon, impactScale);
        }
    }

    /* ───────── 회복 처리 ───────── */
    public void Heal(float amount)
    {
        if (amount <= 0f) return;

        currentHP += amount;
        currentHP = Mathf.Clamp(currentHP, 0f, maxHP);

        Debug.Log($"{gameObject.name}이(가) {amount:F1} 회복됨 → 현재 HP: {currentHP:F1}");
    }

    /* ───────── 사망 처리 ───────── */
    private void Die(Vector3 hitDir, WeaponDataSO weapon, float impactScale = 1f)
    {
        Debug.Log($"{gameObject.name} 사망");

        if (TryGetComponent(out Enemy enemy) && enemy != null)
        {
            enemy.Die(hitDir, weapon, impactScale);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /* ───────── 유틸 ───────── */
    public void SetHealth(float value) => currentHP = Mathf.Clamp(value, 0f, maxHP);
    public float GetCurrentHP() => currentHP;
    public float GetMaxHP() => maxHP;

    /// <summary>
    /// 무게 값 반환 (공용)
    /// </summary>
    public float GetWeight() => weight;
}
