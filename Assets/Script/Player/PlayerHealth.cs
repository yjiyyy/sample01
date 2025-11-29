using UnityEngine;

/// <summary>
/// 플레이어 전용 체력 관리 시스템
/// - 레벨업/경험 관련 로직은 PlayerStats로 이동하였습니다.
/// - 이 컴포넌트는 체력 관련만 담당합니다.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("기본 체력")]
    public float maxHP = 100f;
    private float currentHP;

    [Header("피격 반응 (무게)")]
    [Tooltip("값이 클수록 넉백에 덜 밀림")]
    public float weight = 1f;

    void Awake()
    {
        currentHP = maxHP;
    }

    /* ───────── 피해 처리 ───────── */
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
        Debug.Log($"플레이어가 {amount:F1} 피해! scale:{impactScale:F2} | HP: {currentHP:F1}");

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

        Debug.Log($"플레이어가 {amount:F1} 회복됨 → 현재 HP: {currentHP:F1}");
    }

    /* ───────── 사망 처리 ───────── */
    private void Die(Vector3 hitDir, WeaponDataSO weapon, float impactScale = 1f)
    {
        Debug.Log("플레이어 사망");

        // 플레이어 사망 로직 (GameOver, 리스폰 등)
        if (TryGetComponent(out PlayerWeaponController weaponController))
        {
            // weaponController.SetState(PlayerState.Dead); // 필요 시 사용
        }
    }

    /* ───────── 유틸 ───────── */
    public void SetHealth(float value) => currentHP = Mathf.Clamp(value, 0f, maxHP);
    public float GetCurrentHP() => currentHP;
    public float GetMaxHP() => maxHP;
    public float GetWeight() => weight;
}