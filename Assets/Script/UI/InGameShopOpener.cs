using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 인게임 상점 열기와 구매·슬롯 교체·재화 차감을 처리합니다.
/// 상점은 플레이어 초상화의 개발자 메뉴에서 엽니다.
/// </summary>
[DisallowMultipleComponent]
public class InGameShopOpener : MonoBehaviour
{
    public const string PrefabEditorPath = "Assets/Arts/UI/Popup/Popup_InGameShop.prefab";
    public const string ResourcesAssetPath = "UI/InGameShopRefs";

    private InGameShopPopup popup;
    private Canvas shopCanvas;
    private bool pausedByShop;
    private UpgradeHUD upgradeHud;
    private InGameShopOffer pendingReplaceOffer;

    public static void EnsureOn(StageManager stage)
    {
        if (stage == null)
            return;
        if (stage.GetComponent<InGameShopOpener>() == null)
            stage.gameObject.AddComponent<InGameShopOpener>();
    }

    private void OnDisable()
    {
        CloseShop();
    }

    private void OnDestroy()
    {
        UnbindPopup();
        EndSlotReplaceMode();
    }

    public void OpenShop()
    {
        if (popup != null && popup.IsShown)
            return;
        if (GameplayTime.IsGameplayPaused)
            return;
        if (InputManager.Instance != null && InputManager.Instance.IsGameplayInputBlocked)
            return;

        if (!EnsurePopup())
            return;

        EndSlotReplaceMode();
        GameplayTime.Pause();
        pausedByShop = true;
        popup.ShowForGameplay();
    }

    public void CloseShop()
    {
        EndSlotReplaceMode();
        if (popup != null)
            popup.HideForGameplay();
        ResumeIfPausedByShop();
    }

    private void OnPopupClosed()
    {
        EndSlotReplaceMode();
        ResumeIfPausedByShop();
    }

    private void OnPurchaseClicked(int cardIndex)
    {
        if (popup == null)
            return;

        InGameShopOffer offer = popup.GetSelectedOffer();
        if (offer == null || offer.upgrade == null)
            return;

        Upgrade upgrade = FindPlayerUpgrade();
        if (upgrade == null)
        {
            Debug.LogWarning("[InGameShopOpener] 플레이어 Upgrade를 찾지 못했습니다.");
            return;
        }

        PlayerResources resources = PlayerResources.Instance;
        if (resources != null && !resources.CanAfford(offer.currency, offer.price))
        {
            Debug.Log("[InGameShopOpener] 재화가 부족합니다.");
            return;
        }

        // 같은 칸에 더 쌓을 수 있으면 바로 구매
        if (upgrade.CanStackInPlace(offer.upgrade))
        {
            if (!TrySpend(resources, offer))
                return;
            if (!upgrade.TryInstallFromShop(offer.upgrade))
                return;
            CompletePurchaseSuccess(offer);
            return;
        }

        // 빈 칸이 있으면 1번부터 장착
        if (upgrade.HasEmptySlot())
        {
            if (!TrySpend(resources, offer))
                return;
            if (!upgrade.TryInstallFromShop(offer.upgrade))
                return;
            CompletePurchaseSuccess(offer);
            return;
        }

        // 슬롯 가득 → 버릴 칸 선택
        BeginSlotReplaceMode(offer);
    }

    private void BeginSlotReplaceMode(InGameShopOffer offer)
    {
        pendingReplaceOffer = offer;
        popup.SetWaitingForSlotReplace(true);

        upgradeHud = UpgradeHUD.ResolveAndBindHud(FindPlayerUpgrade(), false);
        if (upgradeHud == null)
        {
            Debug.LogWarning("[InGameShopOpener] UpgradeHUD가 없어 슬롯을 고를 수 없습니다.");
            pendingReplaceOffer = null;
            popup.SetWaitingForSlotReplace(false);
            return;
        }

        upgradeHud.BeginSlotPickMode(OnUpgradeSlotPickedForReplace);
        Debug.Log("[InGameShopOpener] 슬롯이 가득 찼습니다. 버릴 업그레이드 슬롯을 고르세요.");
    }

    private void OnUpgradeSlotPickedForReplace(int slotIndex)
    {
        if (pendingReplaceOffer == null || pendingReplaceOffer.upgrade == null)
        {
            EndSlotReplaceMode();
            return;
        }

        Upgrade upgrade = FindPlayerUpgrade();
        if (upgrade == null)
        {
            EndSlotReplaceMode();
            return;
        }

        PlayerResources resources = PlayerResources.Instance;
        if (!TrySpend(resources, pendingReplaceOffer))
        {
            EndSlotReplaceMode();
            return;
        }

        InGameShopOffer offer = pendingReplaceOffer;
        if (!upgrade.TryReplaceSlotFromShop(slotIndex, offer.upgrade))
        {
            // 차감 후 실패하면 환불
            Refund(resources, offer);
            EndSlotReplaceMode();
            return;
        }

        EndSlotReplaceMode();
        CompletePurchaseSuccess(offer);
    }

    private void EndSlotReplaceMode()
    {
        pendingReplaceOffer = null;
        if (popup != null)
            popup.SetWaitingForSlotReplace(false);
        if (upgradeHud != null)
            upgradeHud.EndSlotPickMode();
    }

    private void CompletePurchaseSuccess(InGameShopOffer offer)
    {
        Debug.Log($"[InGameShopOpener] 구매 완료: {offer.upgrade.GetDisplayName()} ({offer.price})");
        CloseShop();
    }

    private static bool TrySpend(PlayerResources resources, InGameShopOffer offer)
    {
        if (offer == null)
            return false;
        if (resources == null)
            return true;
        return resources.TrySpend(offer.currency, offer.price);
    }

    private static void Refund(PlayerResources resources, InGameShopOffer offer)
    {
        if (resources == null || offer == null || offer.price <= 0)
            return;
        if (offer.currency == ShopCurrency.Gem)
            resources.AddGem(offer.price);
        else
            resources.AddMoney(offer.price);
    }

    private static Upgrade FindPlayerUpgrade()
    {
        return Object.FindFirstObjectByType<Upgrade>();
    }

    private void ResumeIfPausedByShop()
    {
        if (!pausedByShop)
            return;

        pausedByShop = false;
        GameplayTime.Resume();
    }

    private bool EnsurePopup()
    {
        if (popup != null)
            return true;

        GameObject prefab = LoadPrefab();
        if (prefab == null)
        {
            Debug.LogWarning("[InGameShopOpener] 상점 팝업 프리팹을 찾지 못했습니다.");
            return false;
        }

        EnsureCanvas();
        var instance = Instantiate(prefab, shopCanvas.transform, false);
        instance.name = "Popup_InGameShop";
        instance.SetActive(false);

        popup = instance.GetComponent<InGameShopPopup>();
        if (popup == null)
        {
            Debug.LogWarning("[InGameShopOpener] 프리팹에 InGameShopPopup이 없습니다.");
            Destroy(instance);
            return false;
        }

        popup.OnCloseClicked += OnPopupClosed;
        popup.OnPurchaseClicked += OnPurchaseClicked;
        return true;
    }

    private void UnbindPopup()
    {
        if (popup != null)
        {
            popup.OnCloseClicked -= OnPopupClosed;
            popup.OnPurchaseClicked -= OnPurchaseClicked;
        }

        popup = null;
    }

    private void EnsureCanvas()
    {
        if (shopCanvas != null)
            return;

        var canvasGo = new GameObject("InGameShopCanvas", typeof(RectTransform));
        canvasGo.layer = 5;
        canvasGo.transform.SetParent(transform, false);

        shopCanvas = canvasGo.AddComponent<Canvas>();
        shopCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        shopCanvas.sortingOrder = 18;
        shopCanvas.overrideSorting = true;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
    }

    private static GameObject LoadPrefab()
    {
        var refs = Resources.Load<InGameShopRefs>(ResourcesAssetPath);
        if (refs != null && refs.popupPrefab != null)
            return refs.popupPrefab;

#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabEditorPath);
#else
        return null;
#endif
    }
}
