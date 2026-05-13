using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// 스테이지 씬에서 플레이어 스폰.
/// 로비에서 선택한 캐릭터가 있으면 그 캐릭터 사용, 없으면 playerPrefab (테스트용).
/// </summary>
[DefaultExecutionOrder(-100)]
public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    [Tooltip("테스트용. Stage 씬을 직접 실행할 때만 사용. 로비→스테이지 흐름에서는 선택된 캐릭터가 스폰됩니다.")]
    public GameObject playerPrefab;
    public CinemachineCamera followCamera;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Debug.LogWarning("[SpawnManager] 중복 SpawnManager를 감지해 현재 오브젝트를 제거합니다.");
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        var player = SpawnPlayerAt(transform.position, transform.rotation);
        if (player == null)
        {
            return;
        }
    }

    public GameObject ResolvePlayerPrefab()
    {
        var data = GameState.Instance?.SelectedCharacter;
        if (data != null && data.modelPrefab != null)
            return data.modelPrefab;
        return playerPrefab;
    }

    public void ScheduleRevive(Upgrade_06_01_ReviveTicket ticket, Vector3 deathPosition, UpgradeEffectSO[] preservedSlots, Transform corpseRoot)
    {
        if (ticket == null)
            return;

        StartCoroutine(CoRevive(ticket, deathPosition, preservedSlots, corpseRoot));
    }

    private IEnumerator CoRevive(Upgrade_06_01_ReviveTicket ticket, Vector3 deathPosition, UpgradeEffectSO[] preservedSlots, Transform corpseRoot)
    {
        float delay = Mathf.Max(0f, ticket.respawnDelaySeconds);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        // UI 슬롯 재연결 시 기존 PlayerHealth 참조가 남지 않도록 먼저 제거합니다.
        if (corpseRoot != null)
            Destroy(corpseRoot.gameObject);
        yield return null;

        Vector3 respawnPos = deathPosition + Vector3.up * Mathf.Max(0f, ticket.respawnYOffset);
        GameObject player = SpawnPlayerAt(respawnPos, transform.rotation);
        if (player == null)
            yield break;

        var upgrade = player.GetComponent<Upgrade>();
        if (upgrade != null && preservedSlots != null)
        {
            for (int i = 0; i < Upgrade.SlotCount && i < preservedSlots.Length; i++)
            {
                upgrade.TrySetSlot(i, preservedSlots[i]);
            }
        }

        var health = player.GetComponent<PlayerHealth>();
        if (health != null)
        {
            float ratio = Mathf.Clamp01(ticket.respawnHealthRatio);
            health.SetHealth(health.GetMaxHP() * ratio);
            health.SetTemporaryInvincible(ticket.invincibleSecondsAfterRespawn);
        }

        if (ticket.respawnFxPrefab != null)
        {
            GameObject fx = Instantiate(ticket.respawnFxPrefab, respawnPos, Quaternion.identity);
            if (ticket.worldFxAutoDestroySeconds > 0f)
                Destroy(fx, ticket.worldFxAutoDestroySeconds);
        }
    }

    private GameObject SpawnPlayerAt(Vector3 position, Quaternion rotation)
    {
        var prefab = ResolvePlayerPrefab();
        if (prefab == null)
        {
            Debug.LogError("[SpawnManager] 스폰할 플레이어 프리팹이 없습니다. playerPrefab을 지정하거나, 로비에서 캐릭터를 선택한 뒤 스테이지로 진입하세요.");
            return null;
        }

        GameObject player = Instantiate(prefab, position, rotation);

        // Follow 설정 (필수!)
        if (followCamera != null)
        {
            followCamera.Follow = player.transform;
            followCamera.LookAt = player.transform;
        }

        // 안전하게 GameManager에 등록 — Instance가 없으면 대기 후 등록
        if (GameManager.Instance != null)
        {
            RegisterPlayerToGameManager(player);
        }
        else
        {
            StartCoroutine(RegisterWhenGameManagerReady(player));
        }

        return player;
    }

    private void RegisterPlayerToGameManager(GameObject player)
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("❌ GameManager.Instance가 없습니다. 등록 불가");
            return;
        }

        // playerTransform 등록
        GameManager.Instance.playerTransform = player.transform;

        // PlayerHealth가 붙어있으면 Register 호출 (플레이어 헬스는 반드시 붙어있다고 하셨으므로 정상 동작)
        var ph = player.GetComponent<PlayerHealth>();
        if (ph != null)
        {
            bool assigned = GameManager.Instance.RegisterPlayerHealth(ph);
            if (!assigned)
                GameManager.Instance.AssignExistingPlayerUIs();
        }
        else
        {
            Debug.LogWarning("❌ 생성된 플레이어 오브젝트에 PlayerHealth가 없습니다. RegisterPlayerHealth를 호출하지 않습니다.");
        }
    }

    private IEnumerator RegisterWhenGameManagerReady(GameObject player)
    {
        // GameManager가 준비될 때까지 대기 (안정성 확보)
        yield return new WaitUntil(() => GameManager.Instance != null);
        RegisterPlayerToGameManager(player);
    }
}