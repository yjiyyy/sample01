using UnityEngine;

public enum StageClearType
{
    [Tooltip("지정 시간(초) 동안 버티면 클리어")]
    SurviveTime = 0,
    [Tooltip("적 N마리를 처치하면 클리어")]
    KillCount = 1,
    [Tooltip("지정한 특정 종류 몬스터를 처치하면 클리어")]
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

    [Tooltip("KillCount: 처치해야 하는 적 수")]
    public int targetKillCount = 30;

    [Tooltip("KillSpecific: 처치해야 하는 적 종류(EnemyConfig)")]
    public EnemyConfig targetEnemyConfig;

    [Tooltip("KillCount/KillSpecific용 제한 시간(초). 0이면 시간 제한 없음")]
    public float timeLimit = 0f;

    [Header("몬스터 레벨 (시간 스케일)")]
    [Tooltip("매 이 시간(초)마다 몬스터 레벨이 1 올라갑니다. 0이면 레벨업 없음")]
    public float monsterLevelUpInterval = 60f;

    [Tooltip("최대 레벨 상한. 이 이상으로는 올라가지 않습니다. (예: 5면 Lv.1~5)")]
    [Min(1)]
    public int maxStageLevel = 5;

    [Header("레벨 UI 아이콘")]
    [Tooltip("레벨 1, 2, 3에 순서대로 표시할 아이콘. 비어 있으면 StageLevelIconBar 기본 아이콘 사용")]
    public Sprite[] levelIcons;
}
