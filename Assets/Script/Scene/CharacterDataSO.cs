using UnityEngine;

/// <summary>
/// 캐릭터 선택 화면용 데이터. 초상화와 3D 모델 프리팹을 Inspector에서 드래그 앤 드롭으로 지정합니다.
/// CreateAssetMenu로 생성 후 CharacterSelectionController에 등록하세요.
/// </summary>
[CreateAssetMenu(menuName = "Character/CharacterDataSO", fileName = "CharacterData_")]
public class CharacterDataSO : ScriptableObject
{
    [Header("캐릭터 표시")]
    [Tooltip("왼쪽 그리드에 표시할 초상화. 드래그 앤 드롭으로 지정하세요.")]
    public Sprite portrait;

    [Tooltip("오른쪽에 표시할 3D 모델 프리팹. 드래그 앤 드롭으로 지정하세요.")]
    public GameObject modelPrefab;

    [Tooltip("캐릭터 이름 (UI 표시용, 선택)")]
    public string displayName = "";
}
