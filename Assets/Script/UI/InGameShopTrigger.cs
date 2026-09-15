using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// StageData에 설정한 시간·재화·티켓 조건으로 인게임 상점을 엽니다.
/// 실제 팝업 열기는 <see cref="InGameShopOpener"/>가 담당합니다.
/// </summary>
[DisallowMultipleComponent]
public class InGameShopTrigger : MonoBehaviour
{
    public const string TicketPrefabEditorPath = "Assets/Arts/DropItem/DropItem_ShopTicket.prefab";

    private InGameShopOpener opener;
    private PlayerResources boundResources;
    private bool started;
    private float elapsed;
    private float nextTimeOpenAt;
    private bool timeFinished;
    private int resourceAccumulated;
    private bool pendingResourceOpen;

    public static void EnsureOn(StageManager stage)
    {
        if (stage == null)
            return;
        if (stage.GetComponent<InGameShopTrigger>() == null)
            stage.gameObject.AddComponent<InGameShopTrigger>();
    }

    public void NotifyStageBegan()
    {
        UnbindResources();
        opener = GetComponent<InGameShopOpener>();
        elapsed = 0f;
        resourceAccumulated = 0;
        pendingResourceOpen = false;
        started = true;

        StageData data = ResolveStageData();
        nextTimeOpenAt = data != null ? Mathf.Max(0f, data.shopFirstOpenTime) : 0f;
        timeFinished = false;

        BindResources();
    }

    /// <summary>
    /// 상점 티켓 획득. Stage SO에서 티켓 조건이 켜져 있고 상점을 열면 true.
    /// 꺼져 있거나 열 수 없으면 false(아이템은 그대로 둡니다).
    /// </summary>
    public static bool TryCollectTicket()
    {
        InGameShopTrigger trigger = FindTrigger();
        if (trigger == null || !trigger.started)
            return false;

        StageData data = trigger.ResolveStageData();
        if (data == null || !data.shopOpenByTicket)
            return false;

        return trigger.TryOpenShop();
    }

    public void SpawnTicketNearPlayer()
    {
        GameObject prefab = LoadTicketPrefab();
        if (prefab == null)
        {
            Debug.LogWarning("[InGameShopTrigger] 상점 티켓 프리팹을 찾지 못했습니다.");
            return;
        }

        Transform player = ResolvePlayer();
        Vector3 origin = player != null ? player.position : transform.position;
        ItemDropSpawner.SpawnOne(prefab, origin, 0);
    }

    private void OnDisable()
    {
        UnbindResources();
        started = false;
    }

    private void OnDestroy()
    {
        UnbindResources();
    }

    private void Update()
    {
        if (!started)
            return;
        if (boundResources == null)
            BindResources();
        if (StageManager.Active == null || !StageManager.Active.IsStageActive)
            return;

        StageData data = ResolveStageData();
        if (data == null)
            return;

        if (data.shopOpenByTime && !timeFinished)
        {
            elapsed += GameplayTime.DeltaTime;
            if (elapsed >= nextTimeOpenAt && TryOpenShop())
                CompleteTimeOpen(data);
        }

        if (data.shopOpenByResource && pendingResourceOpen && TryOpenShop())
            CompleteResourceOpen(data);
    }

    private void CompleteTimeOpen(StageData data)
    {
        if (data.shopRepeatInterval <= 0f)
        {
            timeFinished = true;
            return;
        }

        nextTimeOpenAt += data.shopRepeatInterval;
        if (nextTimeOpenAt <= elapsed)
            nextTimeOpenAt = elapsed + data.shopRepeatInterval;
    }

    private void CompleteResourceOpen(StageData data)
    {
        int unit = Mathf.Max(1, data.shopResourceAmount);
        resourceAccumulated -= unit;
        if (resourceAccumulated < unit)
            pendingResourceOpen = false;
    }

    private void OnResourceGained(ShopCurrency currency, int amount)
    {
        if (!started || amount <= 0)
            return;

        StageData data = ResolveStageData();
        if (data == null || !data.shopOpenByResource)
            return;
        if (currency != data.shopResourceCurrency)
            return;

        resourceAccumulated += amount;
        if (resourceAccumulated >= Mathf.Max(1, data.shopResourceAmount))
            pendingResourceOpen = true;
    }

    private bool TryOpenShop()
    {
        if (opener == null)
            opener = GetComponent<InGameShopOpener>();
        if (opener == null)
            return false;
        return opener.TryOpenFromTrigger();
    }

    private void BindResources()
    {
        UnbindResources();
        boundResources = PlayerResources.Instance;
        if (boundResources == null)
            boundResources = Object.FindFirstObjectByType<PlayerResources>();
        if (boundResources != null)
            boundResources.OnResourceGained += OnResourceGained;
    }

    private void UnbindResources()
    {
        if (boundResources != null)
            boundResources.OnResourceGained -= OnResourceGained;
        boundResources = null;
    }

    private StageData ResolveStageData()
    {
        if (StageManager.Active != null)
            return StageManager.Active.stageData;
        return null;
    }

    private static InGameShopTrigger FindTrigger()
    {
        if (StageManager.Active != null)
        {
            InGameShopTrigger onStage = StageManager.Active.GetComponent<InGameShopTrigger>();
            if (onStage != null)
                return onStage;
        }

        return Object.FindFirstObjectByType<InGameShopTrigger>();
    }

    private static Transform ResolvePlayer()
    {
        if (PlayerResources.Instance != null)
            return PlayerResources.Instance.transform;
        if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
            return GameManager.Instance.playerTransform;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.transform : null;
    }

    private static GameObject LoadTicketPrefab()
    {
        var refs = Resources.Load<InGameShopRefs>(InGameShopOpener.ResourcesAssetPath);
        if (refs != null && refs.ticketPrefab != null)
            return refs.ticketPrefab;

#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<GameObject>(TicketPrefabEditorPath);
#else
        return null;
#endif
    }
}
