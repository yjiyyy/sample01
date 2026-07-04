using UnityEngine;

public enum StageClearType
{
    [Tooltip("지정 시간(초) 동안 버티면 클리어")]
    SurviveTime = 0,
    [Tooltip("몬스터 N마리 처치 시 클리어")]
    KillCount = 1,
    [Tooltip("등록한 특정 몬스터 프리팹을 처치하면 클리어")]
    KillSpecific = 2,
}

[CreateAssetMenu(fileName = "StageData", menuName = "Game/StageData")]
public class StageData : ScriptableObject
{
    [Header("기본")]
    public string stageName = "Stage 1";

    [Header("클리어 조건")]
    public StageClearType clearType = StageClearType.SurviveTime;

    [Tooltip("SurviveTime: 이 시간(초)까지 버티면 클리어")]
    public float stageDuration = 300f;

    [Tooltip("KillCount: 처치해야 하는 몬스터 수")]
    public int targetKillCount = 30;

    [Tooltip("KillSpecific: 처치해야 하는 몬스터 프리팹")]
    public GameObject targetEnemyPrefab;

    [Tooltip("KillCount/KillSpecific용 제한 시간(초). 0이면 시간 제한 없음")]
    public float timeLimit = 0f;

    [Header("스테이지 레벨 (스폰 난이도)")]
    [Tooltip("경과 시간이 이 간격(초)마다 스테이지 레벨이 1 오릅니다. 0이면 레벨업 없음")]
    public float monsterLevelUpInterval = 60f;

    [Header("레벨 UI 아이콘")]
    [Tooltip("레벨 1, 2, 3… 순서대로 표시할 아이콘. 비어 있으면 StageLevelIconBar 기본 아이콘 사용")]
    public Sprite[] levelIcons;
}
