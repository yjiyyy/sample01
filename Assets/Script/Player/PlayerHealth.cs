using UnityEngine;

/// <summary>
/// 플레이어 전용 체력 관리 시스템
/// 향후 레벨업, 스탯 성장, 장비 효과 등 확장 가능
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("기본 체력")]
    public float maxHP = 100f;
    private float currentHP;

    [Header("피격 반응 (무게)")]
    [Tooltip("값이 클수록 넉백에 덜 밀림")]
    public float weight = 1f;

    [Header("🆕 레벨업 관련 (향후 확장)")]
    [SerializeField] private int level = 1;
    [SerializeField] private float experience = 0f;
    [SerializeField] private float expToNextLevel = 100f;

    [Header("🆕 스탯 보너스 (장비/버프)")]
    [SerializeField] private float maxHPBonus = 0f;    // 장비로 체력 증가
    [SerializeField] private float weightBonus = 0f;   // 장비로 넉백 저항 증가

    // 📊 프로퍼티로 최종 수치 계산
    public float FinalMaxHP => maxHP + maxHPBonus;
    public float FinalWeight => weight + weightBonus;

    void Awake()
    {
        currentHP = FinalMaxHP;
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

        // 🆕 향후 확장: 방어력, 피해 감소 등 적용 가능
        // amount = ApplyDefenseModifier(amount);

        if (currentHP <= 0f)
        {
            Die(hitDir, weapon, impactScale);
        }
    }

    /* ───────── 넉백 처리 ───────── */
    public void ApplyKnockback(Vector3 direction, float power, float duration, float stunDuration = 0f)
    {
        // PlayerMovement나 PlayerWeaponController를 통해 넉백 처리
        if (TryGetComponent(out PlayerMovement playerMovement))
        {
            // PlayerMovement에 넉백 적용 로직이 있다면 호출
            Debug.Log($"플레이어 넉백 적용 - 방향: {direction}, 파워: {power}, 지속시간: {duration}s");
        }
        
        if (TryGetComponent(out PlayerWeaponController weaponController))
        {
            // 넉백 중에는 행동 제한
            weaponController.ForceApplyKnockback(direction, power, duration, stunDuration);
        }
    }

    public void ApplyKnockback(Vector3 direction, WeaponDataSO weapon)
    {
        if (weapon != null)
        {
            ApplyKnockback(direction, weapon.knockbackPower, weapon.knockbackDuration, weapon.stunDuration);
        }
    }

    /* ───────── 회복 처리 ───────── */
    public void Heal(float amount)
    {
        if (amount <= 0f) return;

        currentHP += amount;
        currentHP = Mathf.Clamp(currentHP, 0f, FinalMaxHP);

        Debug.Log($"플레이어가 {amount:F1} 회복됨 → 현재 HP: {currentHP:F1}");
    }

    /* ───────── 사망 처리 ───────── */
    private void Die(Vector3 hitDir, WeaponDataSO weapon, float impactScale = 1f)
    {
        Debug.Log("플레이어 사망");

        // 플레이어 사망 로직 (GameOver, 리스폰 등)
        if (TryGetComponent(out PlayerWeaponController weaponController))
        {
            // 사망 상태로 변경하여 모든 입력 차단
            // weaponController.SetState(PlayerState.Dead);
        }

        // 향후: 사망 패널티, 경험치 손실 등 추가 가능
    }

    /* ───────── 🆕 레벨업 시스템 (향후 확장) ───────── */
    public void AddExperience(float exp)
    {
        experience += exp;

        while (experience >= expToNextLevel)
        {
            LevelUp();
        }
    }

    private void LevelUp()
    {
        level++;
        experience -= expToNextLevel;
        expToNextLevel *= 1.2f; // 필요 경험치 증가

        // 레벨업 보너스
        maxHP += 10f;  // 레벨당 체력 +10
        currentHP = FinalMaxHP; // 체력 전회복

        Debug.Log($"🎉 레벨업! Lv.{level} | 최대 체력: {FinalMaxHP}");
    }

    /* ───────── 🆕 장비/버프 시스템 (향후 확장) ───────── */
    public void ApplyEquipmentBonus(float hpBonus, float weightBonus)
    {
        maxHPBonus += hpBonus;
        this.weightBonus += weightBonus;

        Debug.Log($"장비 효과 적용 | 체력 보너스: +{hpBonus}, 무게 보너스: +{weightBonus}");
    }

    public void RemoveEquipmentBonus(float hpBonus, float weightBonus)
    {
        maxHPBonus -= hpBonus;
        this.weightBonus -= weightBonus;

        // 체력이 최대치를 초과하면 조정
        currentHP = Mathf.Min(currentHP, FinalMaxHP);
    }

    /* ───────── 유틸 ───────── */
    public void SetHealth(float value) => currentHP = Mathf.Clamp(value, 0f, FinalMaxHP);
    public float GetCurrentHP() => currentHP;
    public float GetMaxHP() => FinalMaxHP;
    public float GetWeight() => FinalWeight;

    // 🆕 레벨업 정보 접근용
    public int GetLevel() => level;
    public float GetExperience() => experience;
    public float GetExpToNextLevel() => expToNextLevel;
}