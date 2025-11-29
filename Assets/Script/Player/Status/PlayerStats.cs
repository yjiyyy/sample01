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

    // Leveling & experience (example)
    public int level = 1;
    public float experience = 0f;
    public float expToNextLevel = 100f;

    void Awake()
    {
        currentHealth = Mathf.Min(currentHealth > 0f ? currentHealth : maxHealth, maxHealth);
    }

    public void InitializeFromConfig(PlayerConfig cfg)
    {
        if (cfg == null) return;
        maxHealth = cfg.maxHealth;
        currentHealth = maxHealth;
        massMultiplier = Mathf.Max(0.0001f, cfg.mass);
        baseMoveSpeed = cfg.baseMoveSpeed;
        rotationSpeedDegPerSec = cfg.rotationSpeedDegPerSec;
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