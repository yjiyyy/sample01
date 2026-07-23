using UnityEngine;

public enum StageClearType
{
    [Tooltip("���� �ð�(��) ���� ��Ƽ�� Ŭ����")]
    SurviveTime = 0,
    [Tooltip("���� N���� óġ �� Ŭ����")]
    KillCount = 1,
    [Tooltip("����� Ư�� ���� �������� óġ�ϸ� Ŭ����")]
    KillSpecific = 2,
}

[CreateAssetMenu(fileName = "StageData", menuName = "Game/StageData")]
public class StageData : ScriptableObject
{
    [Header("�⺻")]
    public string stageName = "Stage 1";

    [Header("Ŭ���� ����")]
    public StageClearType clearType = StageClearType.SurviveTime;

    [Tooltip("SurviveTime: �� �ð�(��)���� ��Ƽ�� Ŭ����")]
    public float stageDuration = 300f;

    [Tooltip("KillCount: óġ�ؾ� �ϴ� ���� ��")]
    public int targetKillCount = 30;

    [Tooltip("KillSpecific: 처치해야 하는 적 종류(EnemyConfig)")]
    public EnemyConfig targetEnemyConfig;

    [Tooltip("KillCount/KillSpecific�� ���� �ð�(��). 0�̸� �ð� ���� ����")]
    public float timeLimit = 0f;

    [Header("�������� ���� (���� ���̵�)")]
    [Tooltip("��� �ð��� �� ����(��)���� �������� ������ 1 �����ϴ�. 0�̸� ������ ����")]
    public float monsterLevelUpInterval = 60f;

    [Tooltip("ǥ�� ���� �ִ밪. �� �̻����δ� �ö��� �ʽ��ϴ�. (��: 5�� Lv.1~5)")]
    [Min(1)]
    public int maxStageLevel = 5;

    [Header("���� UI ������")]
    [Tooltip("���� 1, 2, 3�� ������� ǥ���� ������. ��� ������ StageLevelIconBar �⺻ ������ ���")]
    public Sprite[] levelIcons;
}
