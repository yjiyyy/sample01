using UnityEngine;

public class StageManager : MonoBehaviour
{
    public static StageManager Active { get; private set; }

    [Header("스테이지 설정")]
    public StageData stageData;
    public StageUI ui;
    public EnemySpawner spawner;
    public ItemBoxSpawner itemBoxSpawner;

    [Header("낙하 처리")]
    [Tooltip("플레이어 y가 이 값 이하로 내려가면 낙사 처리")]
    public float killY = 0f;

    private float elapsedTime;
    private int currentLevel;
    private int killCount;
    private bool stageActive;
    private bool stageEnded;
    private bool hasBegun;
    private bool maxLevelBossNotified;

    private void OnEnable()
    {
        Active = this;
        GameplayPauseOptionsBinder.BindSettingButton();
    }

    private void Start()
    {
        GameplayPauseOptionsBinder.BindSettingButton();
    }

    private void OnDisable()
    {
        if (Active == this)
            Active = null;
    }

    /// <summary>StageSceneLoader가 스테이지 씬 로드 후 호출합니다.</summary>
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
        maxLevelBossNotified = false;

        ResolveSpawner();
        if (spawner != null)
            spawner.SetSpawnLevel(currentLevel, isStageBegin: true);

        itemBoxSpawner = FindEnvironmentItemBoxSpawner();

        if (itemBoxSpawner != null)
            itemBoxSpawner.BeginSpawning();
        else
            Debug.LogWarning("[StageManager] ItemBoxSpawner가 아트 씬에 없습니다. 스테이지 씬에 ItemBoxSpawner를 배치하세요.");

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

        // maxStageLevel이 1이면 시작 시점부터 이미 max → 보스전이 있으면 즉시 시작
        TryNotifyBossEncounterIfAtMaxLevel();
        GameplayPauseOptionsBinder.BindSettingButton();
    }

    /// <summary>잡몹·아이템 박스 신규 스폰만 중지합니다. 이미 나온 오브젝트는 유지합니다.</summary>
    public void StopWaveSpawning()
    {
        ResolveSpawner();
        spawner?.StopSpawning();

        if (itemBoxSpawner == null)
            itemBoxSpawner = FindEnvironmentItemBoxSpawner();

        itemBoxSpawner?.StopSpawning();
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

    public bool IsStageActive => stageActive;

    /// <returns>스테이지가 활성일 때만 true. 배치 스포너가 등록 타이밍을 재시도할 때 사용.</returns>
    public bool RegisterEnemyKillTracking(EnemyHealth health, EnemyConfig sourceConfig)
    {
        if (!stageActive || health == null)
            return false;

        health.OnDeath += () => HandleEnemyKilled(sourceConfig);
        return true;
    }

    private void HandleEnemyKilled(EnemyConfig sourceConfig)
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
                if (IsTargetEnemy(sourceConfig))
                    EndStage(success: true);
                break;
        }
    }

    private void UpdateStageLevel()
    {
        if (stageData.monsterLevelUpInterval <= 0f)
            return;

        int maxIndex = Mathf.Max(0, stageData.maxStageLevel - 1);
        if (currentLevel >= maxIndex)
            return;

        int newLevel = Mathf.FloorToInt(elapsedTime / stageData.monsterLevelUpInterval);
        newLevel = Mathf.Min(newLevel, maxIndex);
        if (newLevel == currentLevel)
            return;

        currentLevel = newLevel;

        // 보스전이 있는 스테이지: max 도달 시 BossEncounter가 UI·스폰 중지를 담당
        if (currentLevel >= maxIndex && TryNotifyBossEncounter())
            return;

        ResolveSpawner();
        spawner?.SetSpawnLevel(currentLevel);
        ui?.UpdateLevel(GetDisplayLevel());
    }

    private void TryNotifyBossEncounterIfAtMaxLevel()
    {
        if (stageData == null)
            return;

        int maxIndex = Mathf.Max(0, stageData.maxStageLevel - 1);
        if (currentLevel < maxIndex)
            return;

        TryNotifyBossEncounter();
    }

    /// <returns>씬에 BossEncounter가 있어 보스 페이즈를 시작했으면 true.</returns>
    private bool TryNotifyBossEncounter()
    {
        if (maxLevelBossNotified)
            return true;

        BossEncounter encounter = FindFirstObjectByType<BossEncounter>();
        if (encounter == null)
            return false;

        maxLevelBossNotified = true;
        encounter.HandleMaxStageLevelReached(this);
        return true;
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

    private bool IsTargetEnemy(EnemyConfig sourceConfig)
    {
        if (stageData.targetEnemyConfig == null || sourceConfig == null)
            return false;

        return sourceConfig == stageData.targetEnemyConfig;
    }

    private int GetDisplayLevel()
    {
        return currentLevel + 1;
    }

    private void ResolveSpawner()
    {
        if (spawner != null)
            return;

        spawner = FindFirstObjectByType<EnemySpawner>();
        if (spawner == null)
            Debug.LogWarning("[StageManager] EnemySpawner를 찾을 수 없습니다. StageManager.spawner를 연결하세요.");
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

        if (itemBoxSpawner == null)
            itemBoxSpawner = FindEnvironmentItemBoxSpawner();

        itemBoxSpawner?.StopAndClear();
        itemBoxSpawner = null;

        GameObject[] allEnemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (var enemy in allEnemies)
            Destroy(enemy);
    }

    public void HandlePlayerFall(GameObject player)
    {
        // TODO: 플레이어 사망/낙사 처리 연결
    }

    private static ItemBoxSpawner FindEnvironmentItemBoxSpawner()
    {
        ItemBoxSpawner[] spawners = FindObjectsByType<ItemBoxSpawner>(FindObjectsSortMode.None);
        ItemBoxSpawner found = null;

        for (int i = 0; i < spawners.Length; i++)
        {
            ItemBoxSpawner spawner = spawners[i];
            if (spawner == null)
                continue;

            string sceneName = spawner.gameObject.scene.name;
            if (sceneName == StageSceneNames.Backup)
                continue;

            if (!StageSceneNames.IsStageEnvironmentScene(sceneName))
                continue;

            if (found != null)
            {
                Debug.LogWarning(
                    $"[StageManager] 아트 씬에 ItemBoxSpawner가 여러 개 있습니다. '{found.gameObject.scene.name}'의 '{found.name}'을 사용합니다.",
                    found);
                continue;
            }

            found = spawner;
        }

        return found;
    }
}
