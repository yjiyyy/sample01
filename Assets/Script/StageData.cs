using UnityEngine;

public enum StageClearType
{
    [InspectorName("시간 생존")]
    [Tooltip("지정 시간(초) 동안 버티면 클리어")]
    SurviveTime = 0,
    [InspectorName("목표 처치 수")]
    [Tooltip("적 N마리를 처치하면 클리어")]
    KillCount = 1,
    [InspectorName("목표 적 처치")]
    [Tooltip("지정한 특정 종류 몬스터를 처치하면 클리어")]
    KillSpecific = 2,
}

public enum StageBossStartType
{
    [InspectorName("시작 즉시")]
    [Tooltip("스테이지가 시작되자마자 보스전을 시작")]
    StageStart = 0,
    [InspectorName("지정 시간 후")]
    [Tooltip("지정한 시간이 지나면 보스전을 시작")]
    ElapsedTime = 1,
    [InspectorName("일반 적 처치 수")]
    [Tooltip("일반 적을 지정한 수만큼 처치하면 보스전을 시작")]
    KillCount = 2,
    [InspectorName("최대 스테이지 레벨")]
    [Tooltip("몬스터가 최대 스테이지 레벨에 도달하면 보스전을 시작")]
    MaxStageLevel = 3,
    [InspectorName("플레이어 입장 구역")]
    [Tooltip("플레이어가 보스 입장 구역에 들어가면 보스전을 시작")]
    PlayerTrigger = 4,
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

    [Header("보스전")]
    [Tooltip("이 스테이지에서 보스전을 사용할지 선택")]
    public bool bossEnabled;

    [Tooltip("보스전이 시작되는 조건")]
    public StageBossStartType bossStartType = StageBossStartType.MaxStageLevel;

    [Tooltip("ElapsedTime: 보스전이 시작될 시간(초)")]
    public float bossStartTime = 60f;

    [Tooltip("KillCount: 보스전 시작 전에 처치할 일반 적 수")]
    public int bossStartKillCount = 30;

    [Header("몬스터 레벨 (시간 스케일)")]
    [Tooltip("매 이 시간(초)마다 몬스터 레벨이 1 올라갑니다. 0이면 레벨업 없음")]
    public float monsterLevelUpInterval = 60f;

    [Tooltip("최대 레벨 상한. 이 이상으로는 올라가지 않습니다. (예: 5면 Lv.1~5)")]
    [Min(1)]
    public int maxStageLevel = 5;

    [Header("레벨 UI 아이콘")]
    [Tooltip("레벨 1, 2, 3에 순서대로 표시할 아이콘. 비어 있으면 StageLevelIconBar 기본 아이콘 사용")]
    public Sprite[] levelIcons;

    [Header("인게임 상점")]
    [Tooltip("켜면 시간이 조건을 충족할 때 상점을 엽니다.")]
    public bool shopOpenByTime;

    [Tooltip("스테이지 시작 후 첫 상점이 열릴 시간(초). 0이면 시작하자마자.")]
    public float shopFirstOpenTime;

    [Tooltip("첫 상점 이후 다시 여는 간격(초). 0이면 시간으로는 한 번만 엽니다.")]
    public float shopRepeatInterval = 30f;

    [Tooltip("켜면 지정한 재화를 일정량 모을 때마다 상점을 엽니다.")]
    public bool shopOpenByResource;

    [Tooltip("상점 조건에 쓸 재화.")]
    public ShopCurrency shopResourceCurrency = ShopCurrency.Money;

    [Tooltip("이 양만큼 모을 때마다 상점을 엽니다.")]
    public int shopResourceAmount = 50;

    [Tooltip("켜면 상점 티켓을 먹었을 때 상점을 엽니다. 꺼져 있으면 티켓을 먹어도 아무 일도 없습니다.")]
    public bool shopOpenByTicket;

    public bool TryValidate(out string error)
    {
        switch (clearType)
        {
            case StageClearType.SurviveTime when stageDuration <= 0f:
                error = "SurviveTime의 생존 시간은 0보다 커야 합니다.";
                return false;
            case StageClearType.KillCount when targetKillCount <= 0:
                error = "KillCount의 목표 처치 수는 1 이상이어야 합니다.";
                return false;
            case StageClearType.KillSpecific when targetEnemyConfig == null:
                error = "KillSpecific의 처치 대상 EnemyConfig가 비어 있습니다.";
                return false;
        }

        if (timeLimit < 0f)
        {
            error = "제한 시간은 0 이상이어야 합니다. 0은 무제한입니다.";
            return false;
        }

        if (bossEnabled)
        {
            if (bossStartType == StageBossStartType.ElapsedTime && bossStartTime < 0f)
            {
                error = "보스 등장 시간은 0 이상이어야 합니다.";
                return false;
            }

            if (bossStartType == StageBossStartType.KillCount && bossStartKillCount <= 0)
            {
                error = "보스 등장에 필요한 처치 수는 1 이상이어야 합니다.";
                return false;
            }

            if (bossStartType == StageBossStartType.MaxStageLevel &&
                maxStageLevel > 1 &&
                monsterLevelUpInterval <= 0f)
            {
                error = "최대 레벨 보스 시작을 사용하려면 레벨 상승 간격이 0보다 커야 합니다.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private void OnValidate()
    {
        stageDuration = Mathf.Max(0f, stageDuration);
        targetKillCount = Mathf.Max(0, targetKillCount);
        timeLimit = Mathf.Max(0f, timeLimit);
        bossStartTime = Mathf.Max(0f, bossStartTime);
        bossStartKillCount = Mathf.Max(1, bossStartKillCount);
        monsterLevelUpInterval = Mathf.Max(0f, monsterLevelUpInterval);
        maxStageLevel = Mathf.Max(1, maxStageLevel);
        shopFirstOpenTime = Mathf.Max(0f, shopFirstOpenTime);
        shopRepeatInterval = Mathf.Max(0f, shopRepeatInterval);
        shopResourceAmount = Mathf.Max(1, shopResourceAmount);
    }
}
