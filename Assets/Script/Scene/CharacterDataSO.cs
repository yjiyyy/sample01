using UnityEngine;

/// <summary>
/// 캐릭터 선택 화면용 데이터. Inspector에서 드래그 앤 드롭으로 지정합니다.
/// </summary>
[CreateAssetMenu(menuName = "Character/CharacterDataSO", fileName = "CharacterData_")]
public class CharacterDataSO : ScriptableObject
{
    [Header("캐릭터 표시")]
    [Tooltip("하단 선택 슬롯에 표시할 초상화.")]
    public Sprite portrait;

    [Tooltip("선택 시 왼쪽에 크게 표시할 2D 일러스트.")]
    public Sprite illustration;

    [Tooltip("캐릭터 이름 (UI 표시용).")]
    public string displayName = "";

    [Header("스탯 표시 (1~5칸)")]
    [Range(1, 5)] public int hpTiers = 3;
    [Range(1, 5)] public int stTiers = 3;
    [Range(1, 5)] public int spdTiers = 3;
    [Range(1, 5)] public int strTiers = 3;
    [Range(1, 5)] public int meleeAtkTiers = 3;
    [Range(1, 5)] public int rangedAtkTiers = 3;

    [Header("초기 스탯 연동")]
    [Tooltip("이 캐릭터가 새 게임을 시작할 때 기준으로 사용할 PlayerConfig.")]
    public PlayerConfig playerConfig;

    [Tooltip("켜면 편집 모드에서 티어와 계산 설정을 바꿀 때 연결된 PlayerConfig에 즉시 반영합니다.")]
    public bool autoSyncPlayerConfig;

    [SerializeField, HideInInspector] private float hpTier3Value = 100f;
    [SerializeField, HideInInspector] private float hpPerTier = 10f;
    [SerializeField, HideInInspector] private float staminaTier3Value = 10f;
    [SerializeField, HideInInspector] private float staminaPerTier = 1f;
    [SerializeField, HideInInspector] private float speedTier3Value = 5f;
    [SerializeField, HideInInspector] private float speedPerTier = 0.5f;
    [SerializeField, HideInInspector] private float strengthTier3Value = 10f;
    [SerializeField, HideInInspector] private float strengthPerTier = 1f;
    [SerializeField, HideInInspector] private float meleeAttackTier3Value = 1f;
    [SerializeField, HideInInspector] private float meleeAttackPerTier = 0.1f;
    [SerializeField, HideInInspector] private float rangedAttackTier3Value = 1f;
    [SerializeField, HideInInspector] private float rangedAttackPerTier = 0.1f;

    [Header("기타")]
    [TextArea(2, 5)]
    public string description = "";

    [Tooltip("잠금 캐릭터면 선택 불가.")]
    public bool isLocked;

    [Header("캐릭터 선택·로비 전시")]
    [Tooltip("선택/로비 화면에 보여줄 3D 프리뷰 프리팹 (예: PC_Pre_Cool).")]
    public GameObject previewPrefab;

    [Header("게임플레이 (스테이지)")]
    [Tooltip("스테이지·전투에 스폰할 3D 모델 프리팹.")]
    public GameObject modelPrefab;

    /// <summary>선택/로비 전시용. previewPrefab 우선, 없으면 modelPrefab.</summary>
    public GameObject GetPreviewPrefab() => previewPrefab != null ? previewPrefab : modelPrefab;

    /// <summary>스테이지용. modelPrefab 우선, 없으면 previewPrefab.</summary>
    public GameObject GetGameplayPrefab() => modelPrefab != null ? modelPrefab : previewPrefab;

    public float GetInitialMaxHealth() => CalculateTierValue(hpTiers, hpTier3Value, hpPerTier, 0f);
    public float GetInitialMaxStamina() => CalculateTierValue(stTiers, staminaTier3Value, staminaPerTier, 1f);
    public float GetInitialMoveSpeed() => CalculateTierValue(spdTiers, speedTier3Value, speedPerTier, 0f);
    public float GetInitialStrength() => CalculateTierValue(strTiers, strengthTier3Value, strengthPerTier, 0f);
    public float GetInitialMeleeAttack() => CalculateTierValue(meleeAtkTiers, meleeAttackTier3Value, meleeAttackPerTier, 0f);
    public float GetInitialRangedAttack() => CalculateTierValue(rangedAtkTiers, rangedAttackTier3Value, rangedAttackPerTier, 0f);

    /// <summary>티어로 계산한 초기 수치를 지정한 Config에 적용합니다.</summary>
    public void ApplyInitialStatsTo(PlayerConfig targetConfig)
    {
        if (targetConfig == null)
            return;

        targetConfig.maxHealth = GetInitialMaxHealth();
        targetConfig.maxStamina = GetInitialMaxStamina();
        targetConfig.baseMoveSpeed = GetInitialMoveSpeed();
        targetConfig.strength = GetInitialStrength();
        targetConfig.meleeAttack = GetInitialMeleeAttack();
        targetConfig.rangedAttack = GetInitialRangedAttack();
    }

    private static float CalculateTierValue(int tier, float tier3Value, float perTier, float minimum)
    {
        int safeTier = Mathf.Clamp(tier, 1, 5);
        return Mathf.Max(minimum, tier3Value + (safeTier - 3) * perTier);
    }
}
