using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StageData))]
public sealed class StageDataEditor : Editor
{
    private SerializedProperty stageName;
    private SerializedProperty clearType;
    private SerializedProperty stageDuration;
    private SerializedProperty targetKillCount;
    private SerializedProperty targetEnemyConfig;
    private SerializedProperty timeLimit;
    private SerializedProperty bossEnabled;
    private SerializedProperty bossStartType;
    private SerializedProperty bossStartTime;
    private SerializedProperty bossStartKillCount;
    private SerializedProperty monsterLevelUpInterval;
    private SerializedProperty maxStageLevel;
    private SerializedProperty levelIcons;
    private SerializedProperty shopOpenByTime;
    private SerializedProperty shopFirstOpenTime;
    private SerializedProperty shopRepeatInterval;
    private SerializedProperty shopOpenByResource;
    private SerializedProperty shopResourceCurrency;
    private SerializedProperty shopResourceAmount;
    private SerializedProperty shopOpenByTicket;

    private void OnEnable()
    {
        stageName = serializedObject.FindProperty("stageName");
        clearType = serializedObject.FindProperty("clearType");
        stageDuration = serializedObject.FindProperty("stageDuration");
        targetKillCount = serializedObject.FindProperty("targetKillCount");
        targetEnemyConfig = serializedObject.FindProperty("targetEnemyConfig");
        timeLimit = serializedObject.FindProperty("timeLimit");
        bossEnabled = serializedObject.FindProperty("bossEnabled");
        bossStartType = serializedObject.FindProperty("bossStartType");
        bossStartTime = serializedObject.FindProperty("bossStartTime");
        bossStartKillCount = serializedObject.FindProperty("bossStartKillCount");
        monsterLevelUpInterval = serializedObject.FindProperty("monsterLevelUpInterval");
        maxStageLevel = serializedObject.FindProperty("maxStageLevel");
        levelIcons = serializedObject.FindProperty("levelIcons");
        shopOpenByTime = serializedObject.FindProperty("shopOpenByTime");
        shopFirstOpenTime = serializedObject.FindProperty("shopFirstOpenTime");
        shopRepeatInterval = serializedObject.FindProperty("shopRepeatInterval");
        shopOpenByResource = serializedObject.FindProperty("shopOpenByResource");
        shopResourceCurrency = serializedObject.FindProperty("shopResourceCurrency");
        shopResourceAmount = serializedObject.FindProperty("shopResourceAmount");
        shopOpenByTicket = serializedObject.FindProperty("shopOpenByTicket");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("스테이지 기본 정보", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stageName, new GUIContent("스테이지 이름"));

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("승리 조건", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(clearType, new GUIContent("조건 종류"));

        StageClearType type = (StageClearType)clearType.enumValueIndex;
        switch (type)
        {
            case StageClearType.SurviveTime:
                EditorGUILayout.PropertyField(stageDuration, new GUIContent("생존 시간 (초)"));
                if (stageDuration.floatValue <= 0f)
                    EditorGUILayout.HelpBox("생존 시간은 0보다 커야 합니다.", MessageType.Error);
                break;

            case StageClearType.KillCount:
                EditorGUILayout.PropertyField(targetKillCount, new GUIContent("목표 처치 수"));
                EditorGUILayout.PropertyField(timeLimit, new GUIContent("제한 시간 (0=무제한)"));
                if (targetKillCount.intValue <= 0)
                    EditorGUILayout.HelpBox("목표 처치 수는 1 이상이어야 합니다.", MessageType.Error);
                break;

            case StageClearType.KillSpecific:
                EditorGUILayout.PropertyField(targetEnemyConfig, new GUIContent("처치할 보스/적"));
                EditorGUILayout.PropertyField(timeLimit, new GUIContent("제한 시간 (0=무제한)"));
                if (targetEnemyConfig.objectReferenceValue == null)
                    EditorGUILayout.HelpBox("처치할 보스/적이 비어 있어 승리할 수 없습니다.", MessageType.Error);
                break;
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("보스전", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(bossEnabled, new GUIContent("보스전 사용"));
        if (bossEnabled.boolValue)
        {
            EditorGUILayout.PropertyField(bossStartType, new GUIContent("보스 시작 조건"));
            StageBossStartType startType = (StageBossStartType)bossStartType.enumValueIndex;
            switch (startType)
            {
                case StageBossStartType.StageStart:
                    EditorGUILayout.HelpBox("스테이지 시작과 동시에 보스를 소환합니다.", MessageType.Info);
                    break;
                case StageBossStartType.ElapsedTime:
                    EditorGUILayout.PropertyField(bossStartTime, new GUIContent("보스 등장 시간 (초)"));
                    if (type == StageClearType.SurviveTime && bossStartTime.floatValue >= stageDuration.floatValue)
                        EditorGUILayout.HelpBox("보스 등장 시간이 생존 클리어 시간보다 늦거나 같습니다.", MessageType.Warning);
                    break;
                case StageBossStartType.KillCount:
                    EditorGUILayout.PropertyField(bossStartKillCount, new GUIContent("보스 등장 처치 수"));
                    if (bossStartKillCount.intValue <= 0)
                        EditorGUILayout.HelpBox("보스 등장 처치 수는 1 이상이어야 합니다.", MessageType.Error);
                    if (type == StageClearType.KillCount && bossStartKillCount.intValue >= targetKillCount.intValue)
                        EditorGUILayout.HelpBox("스테이지가 먼저 클리어되어 보스가 등장하지 않을 수 있습니다.", MessageType.Warning);
                    break;
                case StageBossStartType.MaxStageLevel:
                    EditorGUILayout.HelpBox(
                        "보스 등장 시간 = 레벨 상승 간격 × (최대 스테이지 레벨 - 1)",
                        MessageType.Info);
                    if (maxStageLevel.intValue > 1 && monsterLevelUpInterval.floatValue <= 0f)
                        EditorGUILayout.HelpBox("레벨 상승 간격이 0이라 최대 레벨에 도달할 수 없습니다.", MessageType.Error);
                    break;
                case StageBossStartType.PlayerTrigger:
                    EditorGUILayout.HelpBox(
                        "스테이지 씬의 BossEncounter에 입장 트리거가 연결되어 있어야 합니다.",
                        MessageType.Info);
                    break;
            }

            if (type == StageClearType.KillSpecific)
                EditorGUILayout.HelpBox("목표 적에는 해당 스테이지의 보스 EnemyConfig를 지정하세요.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("보스가 없는 스테이지입니다. 씬에 BossEncounter가 있어도 실행하지 않습니다.", MessageType.Info);
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("진행 난이도", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(monsterLevelUpInterval, new GUIContent("레벨 상승 간격 (초)"));
        EditorGUILayout.PropertyField(maxStageLevel, new GUIContent("최대 스테이지 레벨"));
        EditorGUILayout.PropertyField(levelIcons, new GUIContent("레벨 아이콘"), true);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("인게임 상점", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("테스트할 조건만 켜세요. 모두 꺼져 있으면 치트 메뉴의 상점 열기만 동작합니다.", MessageType.Info);

        EditorGUILayout.PropertyField(shopOpenByTime, new GUIContent("시간으로 열기"));
        if (shopOpenByTime.boolValue)
        {
            EditorGUILayout.PropertyField(shopFirstOpenTime, new GUIContent("첫 상점까지 (초)"));
            EditorGUILayout.PropertyField(shopRepeatInterval, new GUIContent("이후 간격 (초)"));
            if (shopFirstOpenTime.floatValue <= 0f)
                EditorGUILayout.HelpBox("0이면 스테이지 시작과 함께 상점이 열립니다.", MessageType.Info);
            if (shopRepeatInterval.floatValue <= 0f)
                EditorGUILayout.HelpBox("이후 간격 0이면 시간으로는 한 번만 엽니다.", MessageType.Info);
        }

        EditorGUILayout.PropertyField(shopOpenByResource, new GUIContent("재화 획득으로 열기"));
        if (shopOpenByResource.boolValue)
        {
            EditorGUILayout.PropertyField(shopResourceCurrency, new GUIContent("재화 종류"));
            EditorGUILayout.PropertyField(shopResourceAmount, new GUIContent("열리는 단위"));
            if (shopResourceAmount.intValue < 1)
                EditorGUILayout.HelpBox("열리는 단위는 1 이상이어야 합니다.", MessageType.Error);
        }

        EditorGUILayout.PropertyField(shopOpenByTicket, new GUIContent("티켓 획득으로 열기"));
        if (shopOpenByTicket.boolValue)
            EditorGUILayout.HelpBox("티켓은 자석에 끌리지 않습니다. 치트 메뉴에서 떨어뜨려 테스트할 수 있습니다.", MessageType.Info);
        else
            EditorGUILayout.HelpBox("꺼져 있으면 티켓을 먹어도 상점이 열리지 않고, 아이템도 그대로 남습니다.", MessageType.Info);

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(12f);
        using (new EditorGUI.DisabledScope(!Application.isPlaying || StageManager.Active == null))
        {
            EditorGUILayout.LabelField("플레이 모드 테스트", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("강제 승리")) StageManager.Active.DebugWinStage();
            if (GUILayout.Button("강제 패배")) StageManager.Active.DebugLoseStage();
            EditorGUILayout.EndHorizontal();
        }
    }
}
