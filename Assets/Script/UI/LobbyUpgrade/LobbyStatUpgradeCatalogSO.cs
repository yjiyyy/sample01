using System;
using UnityEngine;

/// <summary>
/// 로비 업그레이드 한 줄. 이름·대사·숫자·아이콘은 Inspector에서 한/영으로 고칩니다.
/// </summary>
[Serializable]
public class LobbyStatUpgradeEntry
{
    public LobbyStatId id;
    public LocalizedString displayName;
    public Sprite icon;
    [Tooltip("레벨 0일 때 보여주는 기본 값. 캐릭터 Config가 있으면 그쪽을 우선합니다.")]
    public float fallbackBaseValue = 100f;
    [Tooltip("레벨 1 올릴 때마다 더하는 값")]
    public float perLevel = 10f;
    [Min(0)]
    public int maxLevel = 10;
    [Min(0f), Tooltip("막대 오른쪽 끝의 최종 능력치. 기본값 + 증가량 × 최대 횟수와 맞추세요.")]
    public float maxValue = 200f;
    [Tooltip("레벨 0에서 1로 올릴 때 골드")]
    public int baseCost = 50;
    [Tooltip("레벨이 오를 때마다 늘어나는 골드")]
    public int costPerLevel = 50;
    [Tooltip("이 줄을 골랐을 때 NPC가 하는 말")]
    public LocalizedString selectLine;
}

/// <summary>
/// 로비 업그레이드 문구와 수치. 한/영은 이 에셋만 고치면 됩니다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Lobby/Stat Upgrade Catalog", fileName = "LobbyStatUpgradeCatalog")]
public class LobbyStatUpgradeCatalogSO : ScriptableObject
{
    public const string ResourcesPath = "UI/LobbyStatUpgradeCatalog";
    public const string ResourcesSpriteFolder = "UI/LobbyUpgrade";

    [Header("NPC")]
    public Sprite npcPortrait;
    [Tooltip("지금 화면에는 이름 칸이 없습니다. 나중에 이름을 다시 띄울 때 씁니다.")]
    public LocalizedString npcName = new LocalizedString
    {
        korean = "코치",
        english = "Coach"
    };
    public LocalizedString idleLine = new LocalizedString
    {
        korean = "오늘은 어디를 키울 거야?",
        english = "What are we training today?"
    };
    public LocalizedString cannotAffordLine = new LocalizedString
    {
        korean = "돈이 모자라. 조금 더 모아 와.",
        english = "Not enough gold. Come back later."
    };
    public LocalizedString maxedLine = new LocalizedString
    {
        korean = "여긴 더 안 늘어. 다른 데를 보자.",
        english = "That's as far as this one goes."
    };
    public LocalizedString upgradedLine = new LocalizedString
    {
        korean = "좋아. 몸이 반응하네.",
        english = "Good. I can feel that."
    };

    [Header("버튼")]
    public LocalizedString backLabel = new LocalizedString
    {
        korean = "돌아가기",
        english = "Back"
    };
    [Tooltip("우측 하단 실행 버튼 문구")]
    public LocalizedString upgradeLabel = new LocalizedString
    {
        korean = "업그레이드",
        english = "Upgrade"
    };

    [HideInInspector]
    public LobbyStatUpgradeEntry[] stats = CreateDefaultStats();

    [Header("캐릭터별 업그레이드 설정")]
    public LobbyCharacterUpgradeSO[] characters = new LobbyCharacterUpgradeSO[0];

    public LobbyCharacterUpgradeSO GetCharacter(CharacterDataSO character)
    {
        if (character != null && characters != null)
            foreach (var profile in characters)
                if (profile != null && profile.character == character) return profile;
        return null;
    }

    public LobbyCharacterUpgradeSO GetCharacter(PlayerConfig config)
    {
        if (config != null && characters != null)
            foreach (var profile in characters)
                if (profile != null && profile.Config == config) return profile;
        return null;
    }

    public static LobbyStatUpgradeCatalogSO Load()
    {
        var settings = Resources.Load<LobbyUpgradeSettingsSO>("UI/LobbyUpgradeSettings");
        var loaded = settings != null ? settings.catalog : Resources.Load<LobbyStatUpgradeCatalogSO>(ResourcesPath);
        if (loaded != null)
            loaded.EnsureLabels();
        return loaded;
    }

    /// <summary>
    /// 에셋을 만든 뒤에 새로 생긴 문구 칸은 비어 있습니다. 비면 기본 문구로 채웁니다.
    /// </summary>
    public void EnsureLabels()
    {
        if (string.IsNullOrEmpty(backLabel.korean))
            backLabel = new LocalizedString { korean = "돌아가기", english = "Back" };
        if (string.IsNullOrEmpty(upgradeLabel.korean))
            upgradeLabel = new LocalizedString { korean = "업그레이드", english = "Upgrade" };
    }

    public LobbyStatUpgradeEntry GetEntry(LobbyStatId id)
    {
        if (stats == null)
            return null;
        for (int i = 0; i < stats.Length; i++)
        {
            if (stats[i] != null && stats[i].id == id)
                return stats[i];
        }
        return null;
    }

    public static LobbyStatUpgradeEntry[] CreateDefaultStats()
    {
        return new[]
        {
            new LobbyStatUpgradeEntry
            {
                id = LobbyStatId.HP,
                displayName = new LocalizedString { korean = "HP", english = "HP" },
                fallbackBaseValue = 100f,
                perLevel = 10f,
                maxLevel = 10,
                baseCost = 50,
                costPerLevel = 50,
                selectLine = new LocalizedString
                {
                    korean = "한 대 더 버티게 해줄게.",
                    english = "You'll take one more hit."
                }
            },
            new LobbyStatUpgradeEntry
            {
                id = LobbyStatId.STA,
                displayName = new LocalizedString { korean = "STA", english = "STA" },
                fallbackBaseValue = 10f,
                perLevel = 1f,
                maxLevel = 10,
                maxValue = 20f,
                baseCost = 100,
                costPerLevel = 50,
                selectLine = new LocalizedString
                {
                    korean = "숨부터 만들자. 피할 힘이 생겨.",
                    english = "More breath. More evades."
                }
            },
            new LobbyStatUpgradeEntry
            {
                id = LobbyStatId.SPD,
                displayName = new LocalizedString { korean = "SPD", english = "SPD" },
                fallbackBaseValue = 10f,
                perLevel = 0.4f,
                maxLevel = 10,
                maxValue = 14f,
                baseCost = 150,
                costPerLevel = 50,
                selectLine = new LocalizedString
                {
                    korean = "발을 가볍게 해줄게.",
                    english = "Let's get those feet lighter."
                }
            },
            new LobbyStatUpgradeEntry
            {
                id = LobbyStatId.STR,
                displayName = new LocalizedString { korean = "STR", english = "STR" },
                fallbackBaseValue = 10f,
                perLevel = 1f,
                maxLevel = 10,
                maxValue = 20f,
                baseCost = 200,
                costPerLevel = 50,
                selectLine = new LocalizedString
                {
                    korean = "더 무거운 것도 들게 해줄게.",
                    english = "You'll carry heavier weapons."
                }
            }
        };
    }

    public static Sprite LoadSprite(string fileName)
    {
        var sprite = Resources.Load<Sprite>(ResourcesSpriteFolder + "/" + fileName);
        if (sprite != null)
            return sprite;

        var tex = Resources.Load<Texture2D>(ResourcesSpriteFolder + "/" + fileName);
        if (tex == null)
            return null;

        return Sprite.Create(
            tex,
            new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            100f);
    }
}
