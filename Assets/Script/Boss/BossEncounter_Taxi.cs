using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Taxi 스테이지용 보스전.
/// </summary>
public class BossEncounter_Taxi : BossEncounter
{
    [Header("보스")]
    [SerializeField] private EnemyConfig bossConfig;
    [SerializeField] private GameObject hpUiPrefab;

    [Header("스폰 지점")]
    [SerializeField] private BossSpawnSite[] spawnSites;

    [Header("전투 존 (공통)")]
    [Tooltip("보스 스폰 지점과 상관없이 공통으로 켜고 끌 벽 콜라이더들입니다.")]
    [SerializeField] private Collider[] combatZoneColliders;

    [Header("입장 트리거 / 컷씬")]
    [SerializeField] private BossEncounterTriggerZone introTriggerZone;
    [SerializeField] private PlayableDirector introDirector;
    [Tooltip("컷씬 중 플레이어를 이동시킬 위치. 비워두면 현재 위치 유지.")]
    [SerializeField] private Transform playerCombatStartPoint;
    [Tooltip("컷씬 중 보스를 이동시킬 위치. 비워두면 현재 위치 유지.")]
    [SerializeField] private Transform bossCombatStartPoint;

    [Header("화살표")]
    [SerializeField] private BossDirectionArrowUI directionArrow;

    private bool introTriggered;
    private bool introCompleted;

    private void Awake()
    {
        SetCombatZoneCollidersEnabled(combatZoneColliders, false);
        if (introTriggerZone != null)
            introTriggerZone.SetOwner(this);
    }

    protected override void OnBossPhaseStarted(StageManager stageManager)
    {
        BossDirectionArrowUI arrow = directionArrow != null
            ? directionArrow
            : FindFirstObjectByType<BossDirectionArrowUI>();

        if (!TrySpawnBossAtFarthestSite(stageManager, bossConfig, hpUiPrefab, spawnSites, arrow))
            Debug.LogWarning("[BossEncounter_Taxi] 보스 소환에 실패했습니다.", this);
    }

    public void HandleIntroTriggerEntered()
    {
        if (!IsBossPhaseStarted || introTriggered)
            return;

        introTriggered = true;
        directionArrow?.ClearTarget();

        DestroyRemainingEnemiesExceptBoss();
        SetCombatZoneCollidersEnabled(combatZoneColliders, true);
        SetPlayerControlLocked(true);

        if (introDirector == null)
        {
            CompleteIntroCutscene();
            return;
        }

        introDirector.stopped -= OnIntroDirectorStopped;
        introDirector.stopped += OnIntroDirectorStopped;
        introDirector.Play();
    }

    private void OnIntroDirectorStopped(PlayableDirector director)
    {
        if (introDirector != null)
            introDirector.stopped -= OnIntroDirectorStopped;

        CompleteIntroCutscene();
    }

    private void CompleteIntroCutscene()
    {
        if (introCompleted)
            return;

        introCompleted = true;
        RepositionCombatActors();
        StartBossCombat();
        SetPlayerControlLocked(false);
    }

    private void RepositionCombatActors()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null && playerCombatStartPoint != null)
            player.transform.SetPositionAndRotation(playerCombatStartPoint.position, playerCombatStartPoint.rotation);

        if (SpawnedBoss != null && bossCombatStartPoint != null)
            SpawnedBoss.transform.SetPositionAndRotation(bossCombatStartPoint.position, bossCombatStartPoint.rotation);
    }

    private void StartBossCombat()
    {
        if (SpawnedBoss == null)
            return;

        EnemyAI ai = SpawnedBoss.GetComponent<EnemyAI>();
        if (ai == null)
            return;

        ai.SetStandStillPeace(false);
        ai.SkipFindGoToCombat();
    }

    private void DestroyRemainingEnemiesExceptBoss()
    {
        GameObject[] allEnemies = GameObject.FindGameObjectsWithTag("Enemy");
        for (int i = 0; i < allEnemies.Length; i++)
        {
            GameObject enemy = allEnemies[i];
            if (enemy == null || enemy == SpawnedBoss)
                continue;

            Destroy(enemy);
        }
    }

    private static void SetPlayerControlLocked(bool locked)
    {
        if (InputManager.Instance == null)
            return;

        InputManager.SetPlayerDeathBlock(locked);
        InputManager.Instance.ClearPlayerInput();
    }
}
