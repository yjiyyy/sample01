using UnityEngine;

public class StageManager : MonoBehaviour
{
    public static StageManager Active { get; private set; }

    [Header("스테이지 설정")]
    public StageData stageData;
    public StageUI ui;
    public EnemySpawner spawner;

    [Header("낙하 처리")]
    [Tooltip("플레이어 y가 이 값 이하로 내려가면 낙사 처리")]
    public float killY = 0f;

    private float elapsedTime;
    private int currentLevel;
    private int killCount;
    private bool stageActive;
    private bool stageEnded;
    private bool hasBegun;

    private void OnEnable()
    {
        Active = this;
    }

    private void OnDisable()
    {
        if (Active == this)
            Active = null;
    }

    /// <summary>StageSceneLoader가 스테이지 시작 시 호출합니다.</summary>
    public void BeginStage(StageData overrideData = null)
    {
        if (hasBegun)
            return;

        hasBegun = true;

        if (overrideData != null)
            stageData = overrideData;

        if (stageData == null)
        {
            Debug.LogError("[StageManager] stageData가 없습니다.");
            return;
        }

        elapsedTime = 0f;
        currentLevel = 0;
        killCount = 0;
        stageActive = true;
        stageEnded = false;

        if (spawner != null)
            spawner.SetSpawnLevel(currentLevel);

        if (ui != null)
        {
            ui.InitializeLevelIcons(stageData);
            ui.ShowStartText();
            ui.UpdateElapsedTime(0f);
            ui.UpdateLevel(GetDisplayLevel());
            bool showKillProgress = stageData.clearType == StageClearType.KillCount;
            ui.SetKillProgressVisible(showKillProgress);
            if (showKillProgress)
                ui.UpdateKillProgress(0, stageData.targetKillCount);
        }
    }

    private void Update()
    {
        if (!stageActive || stageData == null)
            return;

        elapsedTime += Time.deltaTime;
        ui?.UpdateElapsedTime(elapsedTime);
        UpdateStageLevel();
        CheckTimeFail();
        CheckSurviveClear();
    }

    public void RegisterEnemyKillTracking(EnemyHealth health, GameObject sourcePrefab)
    {
        if (!stageActive || health == null)
            return;

        health.OnDeath += () => HandleEnemyKilled(sourcePrefab);
    }

    private void HandleEnemyKilled(GameObject sourcePrefab)
    {
        if (!stageActive || stageEnded)
            return;

        killCount++;

        if (ui != null && stageData.clearType == StageClearType.KillCount)
            ui.UpdateKillProgress(killCount, stageData.targetKillCount);

        switch (stageData.clearType)
        {
            case StageClearType.KillCount:
                if (killCount >= stageData.targetKillCount)
                    EndStage(success: true);
                break;

            case StageClearType.KillSpecific:
                if (IsTargetEnemy(sourcePrefab))
                    EndStage(success: true);
                break;
        }
    }

    private void UpdateStageLevel()
    {
        if (stageData.monsterLevelUpInterval <= 0f)
            return;

        int newLevel = Mathf.FloorToInt(elapsedTime / stageData.monsterLevelUpInterval);
        if (newLevel == currentLevel)
            return;

        currentLevel = newLevel;
        spawner?.SetSpawnLevel(currentLevel);
        ui?.UpdateLevel(GetDisplayLevel());
    }

    private void CheckSurviveClear()
    {
        if (stageData.clearType != StageClearType.SurviveTime)
            return;

        if (elapsedTime >= stageData.stageDuration)
            EndStage(success: true);
    }

    private void CheckTimeFail()
    {
        if (stageData.clearType == StageClearType.SurviveTime)
            return;

        if (stageData.timeLimit <= 0f)
            return;

        if (elapsedTime >= stageData.timeLimit)
            EndStage(success: false);
    }

    private bool IsTargetEnemy(GameObject sourcePrefab)
    {
        if (stageData.targetEnemyPrefab == null || sourcePrefab == null)
            return false;

        return sourcePrefab == stageData.targetEnemyPrefab;
    }

    private int GetDisplayLevel()
    {
        return currentLevel + 1;
    }

    private void EndStage(bool success)
    {
        if (stageEnded) return;
        stageEnded = true;
        stageActive = false;

        if (ui != null)
        {
            if (success) ui.ShowSuccessText();
            else ui.ShowFailText();
        }

        if (spawner != null)
            spawner.enabled = false;

        GameObject[] allEnemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (var enemy in allEnemies)
            Destroy(enemy);
    }

    public void HandlePlayerFall(GameObject player)
    {
        // TODO: 플레이어 사망/낙사 처리 연결
    }
}
