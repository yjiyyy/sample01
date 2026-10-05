using System;
using UnityEngine;

/// <summary>화면 표시와 전투에서 같은 계산을 사용하며 원본 설정을 변경하지 않습니다.</summary>
public static class LobbyStatUpgradeApplier
{
    public static void ApplyToPlayer(GameObject root, PlayerConfig config, CharacterDataSO character = null)
    {
        if (root == null || config == null) return;
        var catalog = LobbyStatUpgradeCatalogSO.Load();
        var profile = catalog != null
            ? (character != null ? catalog.GetCharacter(character) : catalog.GetCharacter(config)) : null;
        if (profile == null) return;
        var stats = root.GetComponentInChildren<PlayerStats>(true);
        var health = root.GetComponentInChildren<PlayerHealth>(true);
        var movement = root.GetComponentInChildren<PlayerMovement>(true);
        float hp = Value(profile, LobbyStatId.HP, config.maxHealth);
        float sta = Value(profile, LobbyStatId.STA, config.maxStamina);
        float spd = Value(profile, LobbyStatId.SPD, config.baseMoveSpeed);
        float str = Value(profile, LobbyStatId.STR, config.strength);
        // 기본값에서 계산하므로 재적용해도 중복 강화되지 않습니다.
        if (stats != null)
        {
            stats.maxHealth = stats.currentHealth = hp;
            stats.maxStamina = stats.currentStamina = sta;
            stats.baseMoveSpeed = spd;
            stats.strength = str;
        }
        if (health != null) { health.maxHP = hp; health.SetHealth(hp); }
        if (movement != null) movement.SetBaseMoveSpeed(spd);
    }

    private static float Value(LobbyCharacterUpgradeSO profile, LobbyStatId id, float fallback)
    {
        var entry = profile.GetEntry(id);
        return entry == null ? fallback : GetCurrentValue(entry, profile.Config, LobbyStatUpgradeState.GetLevel(profile, id));
    }

    public static float GetConfigBase(LobbyStatUpgradeEntry entry, PlayerConfig config)
    {
        if (entry == null) return 0f;
        if (config != null)
            switch (entry.id)
            {
                case LobbyStatId.HP: return config.maxHealth;
                case LobbyStatId.STA: return config.maxStamina;
                case LobbyStatId.SPD: return config.baseMoveSpeed;
                case LobbyStatId.STR: return config.strength;
            }
        return Mathf.Max(0f, entry.fallbackBaseValue);
    }

    public static int GetEffectiveMaxLevel(LobbyStatUpgradeEntry entry, PlayerConfig config)
    {
        if (entry == null || entry.perLevel <= 0f || entry.maxLevel <= 0) return 0;
        double remaining = (double)entry.maxValue - GetConfigBase(entry, config);
        if (remaining <= 0.00001) return 0;
        double levels = Math.Ceiling(remaining / entry.perLevel - 0.00001);
        return (int)Math.Min(entry.maxLevel, Math.Max(0, levels));
    }

    public static float GetCurrentValue(LobbyStatUpgradeEntry entry, PlayerConfig config, int level)
    {
        float basis = GetConfigBase(entry, config);
        if (entry == null) return basis;
        int safeLevel = Mathf.Clamp(level, 0, GetEffectiveMaxLevel(entry, config));
        double result = basis + (double)Mathf.Max(0f, entry.perLevel) * safeLevel;
        return (float)Math.Min(Math.Max(basis, entry.maxValue), result);
    }

    public static bool CanUpgrade(LobbyStatUpgradeEntry entry, PlayerConfig config, int level) =>
        entry != null && level < GetEffectiveMaxLevel(entry, config) &&
        GetCurrentValue(entry, config, level + 1) > GetCurrentValue(entry, config, level) + 0.00001f;

    public static int GetNextCost(LobbyStatUpgradeEntry entry, int level)
    {
        if (entry == null) return 0;
        long price = (long)Mathf.Max(0, entry.baseCost) + (long)Mathf.Max(0, entry.costPerLevel) * Mathf.Max(0, level);
        return (int)Math.Min(int.MaxValue, price);
    }
}
