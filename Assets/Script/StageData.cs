using UnityEngine;

[CreateAssetMenu(fileName = "StageData", menuName = "Game/StageData")]
public class StageData : ScriptableObject
{
    public float stageDuration = 300f; // ���� �ð�
    public float monsterLevelUpInterval = 60f;
    public string stageName = "Stage 1";
}
