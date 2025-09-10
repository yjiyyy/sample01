using UnityEngine;

/// <summary>
/// 플레이어 전용 체력 관리 시스템
/// 향후 레벨업, 스탯 성장, 장비 효과 등 확장 가능
/// Health 클래스를 상속하여 기본 기능 유지 + 플레이어 전용 기능 추가
/// </summary>
public class PlayerHealth : Health
{
    [Header("🆕 레벨업 관련 (향후 확장)")]
    [SerializeField] private int level = 1;
    [SerializeField] private float experience = 0f;
    [SerializeField] private float expToNextLevel = 100f;

    [Header("🆕 스탯 보너스 (장비/버프)")]
    [SerializeField] private float maxHPBonus = 0f;    // 장비로 체력 증가
    [SerializeField] private float weightBonus = 0f;   // 장비로 넉백 저항 증가

    // 📊 프로퍼티로 최종 수치 계산 (base 클래스 값에 보너스 추가)
    public float FinalMaxHP => maxHP + maxHPBonus;
    public float FinalWeight => weight + weightBonus;

    protected override void Awake()
    {
        base.Awake(); // 부모 클래스 초기화
        // maxHP 값이 변경되었다면 currentHP도 업데이트
        if (maxHPBonus != 0)
        {
            SetHealth(FinalMaxHP);
        }
    }

    // Override된 메서드들 - 플레이어 전용 로직 추가
    public override void ApplyDamage(float amount, Vector3 hitDir, WeaponDataSO weapon, float impactScale)
    {
        float currentHP = GetCurrentHP();
        currentHP -= amount;
        Debug.Log($"플레이어가 {amount:F1} 피해! scale:{impactScale:F2} | HP: {currentHP:F1}");

        // 🆕 향후 확장: 방어력, 피해 감소 등 적용 가능
        // amount = ApplyDefenseModifier(amount);

        SetHealth(currentHP);

        if (currentHP <= 0f)
        {
            Die(hitDir, weapon, impactScale);
        }
    }

    public override void Heal(float amount)
    {
        if (amount <= 0f) return;

        float currentHP = GetCurrentHP();
        currentHP += amount;
        currentHP = Mathf.Clamp(currentHP, 0f, FinalMaxHP);
        SetHealth(currentHP);

        Debug.Log($"플레이어가 {amount:F1} 회복됨 → 현재 HP: {currentHP:F1}");
    }

    // Override die method with player-specific logic
    protected override void Die(Vector3 hitDir, WeaponDataSO weapon, float impactScale = 1f)
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

    /* ───────── 넉백 처리 ───────── */
    public void ApplyKnockback(Vector3 direction, float force, float duration, Transform attacker = null)
    {
        // PlayerMovement 컴포넌트에 위임
        if (TryGetComponent(out PlayerMovement movement))
        {
            movement.ApplyKnockback(direction, force, duration, attacker);
        }
        else
        {
            Debug.LogWarning("[PlayerHealth] PlayerMovement 컴포넌트를 찾을 수 없어 넉백을 적용할 수 없습니다.");
        }
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
        SetHealth(FinalMaxHP); // 체력 전회복

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
        float currentHP = GetCurrentHP();
        SetHealth(Mathf.Min(currentHP, FinalMaxHP));
    }

    /* ───────── 추가 유틸 메서드 오버라이드 ───────── */
    public new float GetMaxHP() => FinalMaxHP;  // 보너스 포함된 최대 체력
    public new float GetWeight() => FinalWeight; // 보너스 포함된 무게

    // 🆕 레벨업 정보 접근용
    public int GetLevel() => level;
    public float GetExperience() => experience;
    public float GetExpToNextLevel() => expToNextLevel;
}