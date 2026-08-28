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

    [Header("스탯 표시 (0~5칸, 선택 화면 전용 / PlayerConfig와 연동 없음)")]
    [Range(0, 5)] public int hpTiers = 3;
    [Range(0, 5)] public int stTiers = 3;
    [Range(0, 5)] public int spdTiers = 3;
    [Range(0, 5)] public int strTiers = 3;
    [Range(0, 5)] public int meleeAtkTiers = 3;
    [Range(0, 5)] public int rangedAtkTiers = 3;

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
}
