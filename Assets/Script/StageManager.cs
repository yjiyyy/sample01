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
    [Tooltip("플레이어 y가 이 값 이하로 내려가면 낙사 처리. 맵의 가장 낮은 바닥보다 충분히 아래로 설정하세요.")]
    public float killY = -10f;

    [Tooltip("스폰 직후 물리 안정화 동안 낙하 판정을 무시하는 시간(초)")]
    [Min(0f)] public float fallCheckGraceSeconds = 1f;

    private float elapsedTime;
    private int currentLevel;
    private int killCount;
    private bool stageActive;
    private bool stageEnded;
    private bool hasBegun;
    private bool bossEncounterStarted;
    private bool bossConfigurationErrorLogged;
    private float stageStartedAtUnscaledTime;

    private void OnEnable()
    {
        Active = this;
        GameplayPauseOptionsBinder.BindSettingButton();
        InGameShopOpener.EnsureOn(this);
        DevCheatConsole.EnsureOn(this);
    }

    private void Start()
    {
        GameplayPauseOptionsBinder.BindSettingButton();
        InGameShopOpener.EnsureOn(this);
        DevCheatConsole.EnsureOn(this);
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

        if (!stageData.TryValidate(out string validationError))
        {
            Debug.LogError($"[StageManager] '{stageData.name}' 설정 오류: {validationError}", stageData);
            return;
        }

        elapsedTime = 0f;
        currentLevel = 0;
        killCount = 0;
        stageActive = true;
        stageEnded = false;
        bossEncounterStarted = false;
        bossConfigurationErrorLogged = false;
        stageStartedAtUnscaledTime = Time.unscaledTime;

        GameObject spawnedPlayer = GameObject.FindWithTag("Player");
        if (spawnedPlayer != null && spawnedPlayer.transform.position.y <= killY)
        {
            Debug.LogError(
                $"[StageManager] 플레이어 스폰 높이({spawnedPlayer.transform.position.y:F2})가 Kill Y({killY:F2}) 이하입니다. " +
                "PlayerSpawnPoint를 올리거나 Kill Y를 더 낮게 설정하세요.",
                this);
        }

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

        ValidateBossSceneConfiguration();
        TryStartBossAtStageBegin();
        GameplayPauseOptionsBinder.BindSettingButton();
        InGameShopOpener.EnsureOn(this);
        DevCheatConsole.EnsureOn(this);
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
        CheckTimedBossStart();
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

        if (stageData.bossEnabled &&
            stageData.bossStartType == StageBossStartType.KillCount &&
            killCount >= stageData.bossStartKillCount)
        {
            TryStartBossEncounter();
        }

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

        // 최대 레벨 도달이 보스 시작 조건인 경우에만 보스 페이즈로 전환합니다.
        if (currentLevel >= maxIndex &&
            stageData.bossEnabled &&
            stageData.bossStartType == StageBossStartType.MaxStageLevel &&
            TryStartBossEncounter())
            return;

        ResolveSpawner();
        spawner?.SetSpawnLevel(currentLevel);
        ui?.UpdateLevel(GetDisplayLevel());
    }

    private void TryStartBossAtStageBegin()
    {
        if (stageData == null || !stageData.bossEnabled)
            return;

        if (stageData.bossStartType == StageBossStartType.StageStart)
        {
            TryStartBossEncounter();
            return;
        }

        if (stageData.bossStartType == StageBossStartType.MaxStageLevel && stageData.maxStageLevel <= 1)
            TryStartBossEncounter();
    }

    private void CheckTimedBossStart()
    {
        if (bossEncounterStarted || stageData == null || !stageData.bossEnabled)
            return;

        if (stageData.bossStartType == StageBossStartType.ElapsedTime &&
            elapsedTime >= stageData.bossStartTime)
        {
            TryStartBossEncounter();
        }
    }

    /// <summary>PlayerTrigger 방식에서 보스 입장 구역이 호출합니다.</summary>
    public bool TryStartBossFromPlayerTrigger(BossEncounter requestingEncounter)
    {
        if (!stageActive || stageEnded || stageData == null || !stageData.bossEnabled)
            return false;

        if (stageData.bossStartType != StageBossStartType.PlayerTrigger)
            return false;

        return TryStartBossEncounter(requestingEncounter);
    }

    /// <returns>씬의 BossEncounter가 보스 페이즈를 시작했으면 true.</returns>
    private bool TryStartBossEncounter(BossEncounter preferredEncounter = null)
    {
        if (bossEncounterStarted)
            return true;

        if (stageData == null || !stageData.bossEnabled)
            return false;

        BossEncounter encounter = preferredEncounter != null
            ? preferredEncounter
            : FindFirstObjectByType<BossEncounter>();
        if (encounter == null)
        {
            LogBossConfigurationErrorOnce("보스전 사용이 켜져 있지만 씬에 BossEncounter가 없습니다.");
            return false;
        }

        bossEncounterStarted = encounter.TryStartEncounter(this);
        return bossEncounterStarted;
    }

    private void ValidateBossSceneConfiguration()
    {
        if (stageData == null || !stageData.bossEnabled)
            return;

        BossEncounter encounter = FindFirstObjectByType<BossEncounter>();
        if (encounter == null)
        {
            LogBossConfigurationErrorOnce("보스전 사용이 켜져 있지만 씬에 BossEncounter가 없습니다.");
            return;
        }

        if (stageData.bossStartType == StageBossStartType.PlayerTrigger && !encounter.HasPlayerStartTrigger)
        {
            LogBossConfigurationErrorOnce("보스 시작 조건이 PlayerTrigger이지만 BossEncounter에 입장 트리거가 연결되지 않았습니다.");
        }

        if (stageData.clearType == StageClearType.KillSpecific &&
            encounter.ConfiguredBoss != null &&
            stageData.targetEnemyConfig != encounter.ConfiguredBoss)
        {
            Debug.LogWarning(
                "[StageManager] 목표 적 처치가 클리어 조건이지만 StageData의 목표 적과 BossEncounter의 보스가 다릅니다.",
                this);
        }
    }

    private void LogBossConfigurationErrorOnce(string message)
    {
        if (bossConfigurationErrorLogged)
            return;

        bossConfigurationErrorLogged = true;
        Debug.LogError($"[StageManager] {message}", this);
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

        InputManager.SetPlayerDeathBlock(true);
        InputManager.Instance?.ClearPlayerInput();

        if (spawner != null)
            spawner.enabled = false;

        if (itemBoxSpawner == null)
            itemBoxSpawner = FindEnvironmentItemBoxSpawner();

        itemBoxSpawner?.StopAndClear();
        itemBoxSpawner = null;

        GameObject[] allEnemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (var enemy in allEnemies)
            Destroy(enemy);

        StageResultUI.ShowResult(success);
        Time.timeScale = 0f;
    }

    public bool HandlePlayerFall(GameObject player)
    {
        if (!stageActive || stageEnded)
            return false;

        if (Time.unscaledTime - stageStartedAtUnscaledTime < fallCheckGraceSeconds)
            return false;

        EndStage(success: false);
        return true;
    }

    /// <summary>부활 수단이 없는 플레이어의 최종 사망 때 호출합니다.</summary>
    public void NotifyPlayerFinalDeath(PlayerHealth playerHealth)
    {
        if (!stageActive || stageEnded)
            return;

        EndStage(success: false);
    }

    [ContextMenu("Debug/강제 승리")]
    public void DebugWinStage() => EndStage(success: true);

    [ContextMenu("Debug/강제 패배")]
    public void DebugLoseStage() => EndStage(success: false);

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
