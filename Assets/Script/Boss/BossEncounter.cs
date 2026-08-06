using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스테이지별 보스전 흐름의 공통 기반.
/// 씬에 이 컴포넌트(또는 자식 클래스)가 있을 때만 보스전이 시작됩니다.
/// </summary>
public abstract class BossEncounter : MonoBehaviour
{
    [Serializable]
    public class BossSpawnSite
    {
        [Tooltip("보스가 서 있는 위치")]
        public Transform spawnPoint;
    }

    [Header("UI")]
    [Tooltip("LevelIconBar에 표시할 문구")]
    [SerializeField] private string bossTimeLabel = "Boss time";

    private bool bossPhaseStarted;
    private GameObject spawnedBoss;
    private BossSpawnSite activeSpawnSite;

    protected GameObject SpawnedBoss => spawnedBoss;
    protected BossSpawnSite ActiveSpawnSite => activeSpawnSite;

    /// <summary>StageManager가 스테이지 레벨 max 도달 시 1회 호출합니다.</summary>
    public void HandleMaxStageLevelReached(StageManager stageManager)
    {
        if (bossPhaseStarted)
            return;

        bossPhaseStarted = true;

        if (stageManager == null)
        {
            Debug.LogError($"[{GetType().Name}] StageManager가 null입니다.", this);
            return;
        }

        stageManager.StopWaveSpawning();
        stageManager.ui?.ShowBossTime(bossTimeLabel);

        OnBossPhaseStarted(stageManager);
    }

    /// <summary>보스 페이즈 시작(스폰 중지·Boss time 표시 이후). 스테이지별 소환·연출은 여기서 확장합니다.</summary>
    protected virtual void OnBossPhaseStarted(StageManager stageManager)
    {
    }

    public bool IsBossPhaseStarted => bossPhaseStarted;

    /// <summary>등록된 스폰 지점 중 플레이어와 가장 먼 곳에 보스를 소환합니다.</summary>
    protected bool TrySpawnBossAtFarthestSite(
        StageManager stageManager,
        EnemyConfig bossConfig,
        GameObject hpUiPrefab,
        BossSpawnSite[] spawnSites,
        BossDirectionArrowUI directionArrow)
    {
        if (bossConfig == null)
        {
            Debug.LogError($"[{GetType().Name}] bossConfig가 비어 있습니다.", this);
            return false;
        }

        if (!TryPickFarthestSpawnSite(spawnSites, out BossSpawnSite site, out Vector3 spawnPosition))
        {
            Debug.LogError($"[{GetType().Name}] 유효한 BossSpawnPoint가 없습니다.", this);
            return false;
        }

        if (!bossConfig.TryPickBodyPrefab(out GameObject bodyPrefab))
        {
            Debug.LogError(
                $"[{GetType().Name}] '{bossConfig.name}' Appearance Pool에 Body Prefabs가 없습니다.",
                this);
            return false;
        }

        Quaternion spawnRotation = GetSpawnFacingRotation(spawnPosition);
        GameObject enemy = EnemyConfigSpawner.Spawn(bossConfig, bodyPrefab, spawnPosition, spawnRotation);
        if (enemy == null)
            return false;

        spawnedBoss = enemy;
        activeSpawnSite = site;

        ApplyBossPreEncounterWait(enemy);
        RegisterBossKillTracking(stageManager, enemy, bossConfig);
        CreateBossHpUi(enemy, hpUiPrefab);

        if (directionArrow != null)
            directionArrow.SetTarget(enemy.transform);

        return true;
    }

    /// <summary>3단계에서 컷씬 시작 시 호출 예정.</summary>
    protected static void SetCombatZoneCollidersEnabled(Collider[] colliders, bool enabled)
    {
        if (colliders == null)
            return;

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = enabled;
        }
    }

    private static bool TryPickFarthestSpawnSite(
        BossSpawnSite[] spawnSites,
        out BossSpawnSite bestSite,
        out Vector3 spawnPosition)
    {
        bestSite = null;
        spawnPosition = default;

        if (spawnSites == null || spawnSites.Length == 0)
            return false;

        Transform player = GameObject.FindWithTag("Player")?.transform;
        Vector3 origin = player != null ? player.position : Vector3.zero;

        float bestSqr = float.NegativeInfinity;
        for (int i = 0; i < spawnSites.Length; i++)
        {
            BossSpawnSite site = spawnSites[i];
            if (site?.spawnPoint == null)
                continue;

            Vector3 delta = site.spawnPoint.position - origin;
            delta.y = 0f;
            float sqr = delta.sqrMagnitude;
            if (sqr <= bestSqr)
                continue;

            bestSqr = sqr;
            bestSite = site;
            spawnPosition = site.spawnPoint.position;
            spawnPosition.y = site.spawnPoint.position.y;
        }

        return bestSite != null;
    }

    private static Quaternion GetSpawnFacingRotation(Vector3 spawnPos)
    {
        Transform player = GameObject.FindWithTag("Player")?.transform;
        if (player == null)
            return Quaternion.identity;

        Vector3 lookDir = player.position - spawnPos;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude < 0.0001f)
            return Quaternion.identity;

        return Quaternion.LookRotation(lookDir.normalized, Vector3.up);
    }

    private static void ApplyBossPreEncounterWait(GameObject enemy)
    {
        if (enemy == null)
            return;

        EnemyAI ai = enemy.GetComponent<EnemyAI>();
        ai?.SetStandStillPeace(true);
    }

    private static void RegisterBossKillTracking(StageManager stageManager, GameObject enemy, EnemyConfig bossConfig)
    {
        if (enemy == null || bossConfig == null)
            return;

        EnemyHealth health = enemy.GetComponent<EnemyHealth>();
        if (health == null)
            return;

        if (stageManager != null && stageManager.RegisterEnemyKillTracking(health, bossConfig))
            return;

        Debug.LogWarning(
            "[BossEncounter] StageManager에 보스 처치 추적을 등록하지 못했습니다. 스테이지가 비활성 상태일 수 있습니다.",
            enemy);
    }

    private static void CreateBossHpUi(GameObject enemy, GameObject hpUiPrefab)
    {
        if (hpUiPrefab == null || enemy == null)
            return;

        GameObject hpui = Instantiate(hpUiPrefab);
        HPUIControllerBase baseController = hpui.GetComponent<HPUIControllerBase>();
        if (baseController == null)
            return;

        baseController.health = enemy.GetComponent<EnemyHealth>();

        WorldHPUIController worldController = hpui.GetComponent<WorldHPUIController>();
        if (worldController != null)
            worldController.target = enemy.transform;

        Slider[] sliders = hpui.GetComponentsInChildren<Slider>(true);
        for (int i = 0; i < sliders.Length; i++)
        {
            string sliderName = sliders[i].name.ToLowerInvariant();
            if (sliderName.Contains("shield"))
                baseController.shieldSlider = sliders[i];
            else if (sliderName.Contains("hp"))
                baseController.hpSlider = sliders[i];
            else if (sliderName.Contains("evade"))
                baseController.evadeSlider = sliders[i];
        }

        baseController.Initialize(baseController.health);
    }
}
