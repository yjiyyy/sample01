using UnityEngine;

/// <summary>
/// Runtime player stats container.
/// Use this to store values that change at runtime (level up, equipment, temporary buffs).
/// PlayerFacade will initialize this from PlayerConfig on Awake.
/// Persist PlayerStats (save/load) instead of modifying the SO.
/// </summary>
public class PlayerStats : MonoBehaviour
{
    [Header("Runtime Stats (initialized from PlayerConfig)")]
    public float maxHealth;
    public float currentHealth;
    public float massMultiplier = 1f;
    public float baseMoveSpeed = 10f;
    public float rotationSpeedDegPerSec = 720f;

    [Header("Stamina (Evade Gauge)")]
    public float maxStamina = 100f;
    public float currentStamina = 100f;
    public float staminaRechargeRate = 20f;
    [Tooltip("스태미나 소비 후 자연 회복이 다시 시작되기까지 대기 시간(초)")]
    public float staminaRechargeDelay = 3f;
    private float externalStaminaRechargeMultiplier = 1f;
    private float externalStaminaRechargeDelayReduction = 0f;
    private float staminaRechargeCooldown = 0f;

    // Leveling & experience (example)
    public int level = 1;
    public float experience = 0f;
    public float expToNextLevel = 100f;

    void Awake()
    {
        currentHealth = Mathf.Min(currentHealth > 0f ? currentHealth : maxHealth, maxHealth);
        if (maxStamina <= 0f) maxStamina = 100f;
        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
        if (staminaRechargeRate < 0f) staminaRechargeRate = 0f;
        if (staminaRechargeDelay < 0f) staminaRechargeDelay = 0f;
    }

    public void InitializeFromConfig(PlayerConfig cfg)
    {
        if (cfg == null) return;
        maxHealth = cfg.maxHealth;
        currentHealth = maxHealth;
        massMultiplier = Mathf.Max(0.0001f, cfg.mass);
        baseMoveSpeed = cfg.baseMoveSpeed;
        rotationSpeedDegPerSec = cfg.rotationSpeedDegPerSec;
        maxStamina = Mathf.Max(1f, cfg.maxStamina);
        staminaRechargeRate = Mathf.Max(0f, cfg.staminaRechargeRate);
        staminaRechargeDelay = Mathf.Max(0f, staminaRechargeDelay);
        currentStamina = maxStamina;
        staminaRechargeCooldown = 0f;
    }

    public void TickStaminaRecharge(float dt)
    {
        if (dt <= 0f) return;
        if (currentStamina >= maxStamina) return;

        if (staminaRechargeCooldown > 0f)
        {
            staminaRechargeCooldown = Mathf.Max(0f, staminaRechargeCooldown - dt);
            if (staminaRechargeCooldown > 0f)
                return;
        }

        float regenRate = staminaRechargeRate * Mathf.Max(0f, externalStaminaRechargeMultiplier);
        currentStamina = Mathf.Min(maxStamina, currentStamina + regenRate * dt);
    }

    /// <summary>
    /// 업그레이드 등 외부 시스템에서 스태미나 회복속도 배율을 적용할 때 사용합니다. (1 = 기본)
    /// </summary>
    public void SetExternalStaminaRechargeMultiplier(float multiplier)
    {
        externalStaminaRechargeMultiplier = Mathf.Max(0f, multiplier);
    }

    /// <summary>
    /// 업그레이드 등 외부 시스템에서 스태미나 회복 지연 감소값(초)을 적용할 때 사용합니다.
    /// </summary>
    public void SetExternalStaminaRechargeDelayReduction(float reductionSeconds)
    {
        externalStaminaRechargeDelayReduction = Mathf.Max(0f, reductionSeconds);
    }

    public bool CanUseStamina(float amount)
    {
        if (amount <= 0f) return true;
        return currentStamina >= amount;
    }

    public bool UseStamina(float amount)
    {
        if (amount <= 0f) return true;
        if (currentStamina < amount) return false;
        currentStamina -= amount;
        if (currentStamina < 0f) currentStamina = 0f;
        StartStaminaRechargeCooldown();
        return true;
    }

    public void ConsumeStamina(float amount)
    {
        if (amount <= 0f) return;
        currentStamina = Mathf.Max(0f, currentStamina - amount);
        StartStaminaRechargeCooldown();
    }

    public void AddStamina(float amount)
    {
        if (amount <= 0f) return;
        currentStamina = Mathf.Min(maxStamina, currentStamina + amount);
    }

    private void StartStaminaRechargeCooldown()
    {
        float effectiveDelay = Mathf.Max(0f, staminaRechargeDelay - externalStaminaRechargeDelayReduction);
        staminaRechargeCooldown = effectiveDelay;
    }

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
        expToNextLevel *= 1.2f;
        maxHealth += 10f; // example rule
        currentHealth = maxHealth;
    }
}