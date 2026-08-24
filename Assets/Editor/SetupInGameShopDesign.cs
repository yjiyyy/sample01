using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Popup_Shop을 복사해 카드 3장 상점 프리팹과 디자인용 빈 씬을 만듭니다.
/// 메뉴: Tools → UI → Create InGame Shop Design Prefab
/// </summary>
public static class SetupInGameShopDesign
{
    private const string MenuPath = "Tools/UI/Create InGame Shop Design Prefab";
    private const string AutoRunFlagPath = "Assets/Editor/SetupInGameShopDesign.run";
    private const string SourcePrefabPath = "Assets/Arts/UI/Popup/Popup_Shop.prefab";
    private const string DestPrefabPath = "Assets/Arts/UI/Popup/Popup_InGameShop.prefab";
    private const string DesignScenePath = "Assets/Scenes/UI/InGameShop_Design.unity";
    private const string FontPath = "Assets/Arts/Fonts/BlackHanSans-Regular SDF.asset";

    [InitializeOnLoadMethod]
    private static void AutoRunIfFlagExists()
    {
        EditorApplication.delayCall += TryAutoRun;
    }

    private static void TryAutoRun()
    {
        if (!File.Exists(AutoRunFlagPath))
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryAutoRun;
            return;
        }

        try
        {
            File.Delete(AutoRunFlagPath);
        }
        catch
        {
            /* ignore */
        }

        CreatePrefabAndDesignScene();
    }

    [MenuItem(MenuPath, false, 20)]
    public static void CreatePrefabAndDesignScene()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
        if (source == null)
        {
            Debug.LogError("[SetupInGameShopDesign] Popup_Shop 프리팹을 찾지 못했습니다: " + SourcePrefabPath);
            return;
        }

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        var previewScene = EditorSceneManager.NewPreviewScene();
        GameObject instance = null;
        string prefabPath;

        try
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(source, previewScene);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = "Popup_InGameShop";

            BuildShopLayout(instance, font);
            PrefabUtility.SaveAsPrefabAsset(instance, DestPrefabPath);
            prefabPath = DestPrefabPath;
        }
        finally
        {
            if (instance != null)
                Object.DestroyImmediate(instance);
            EditorSceneManager.ClosePreviewScene(previewScene);
        }

        CreateDesignScene(prefabPath);
        AssetDatabase.Refresh();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DestPrefabPath);
        if (prefab != null)
        {
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }

        Debug.Log("[SetupInGameShopDesign] 프리팹: " + DestPrefabPath + " / 디자인 씬: " + DesignScenePath);
    }

    private static void BuildShopLayout(GameObject root, TMP_FontAsset font)
    {
        SetActiveByName(root.transform, "Glow", false);
        SetActiveByName(root.transform, "Icon", false);

        var titleText = FindTmp(root.transform, "Text_Title");
        if (titleText != null)
            titleText.text = "Upgrade Shop";

        RenameAndSetButtonText(root.transform, "Button_Claim", "Button_Purchase", "Purchase");
        RenameAndSetButtonText(root.transform, "Button_2xClaim", "Button_Reroll", "리롤");

        var cardsRoot = root.transform.Find("Reward_ItemChest_Group");
        if (cardsRoot == null)
        {
            var go = CreateUiObject("CardsRoot", root.transform);
            cardsRoot = go.transform;
        }
        else
        {
            cardsRoot.gameObject.name = "CardsRoot";
            for (int i = cardsRoot.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(cardsRoot.GetChild(i).gameObject);
        }

        var cardsRect = cardsRoot.GetComponent<RectTransform>();
        cardsRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardsRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardsRect.pivot = new Vector2(0.5f, 0.5f);
        cardsRect.sizeDelta = new Vector2(1400f, 640f);
        cardsRect.anchoredPosition = new Vector2(0f, 40f);

        var cardBg = LoadSprite("991ab21a90695442e809e840acc518ed");
        var previews = new[]
        {
            ("속도 증가", "이동 속도가 조금 빨라집니다.", 30, false),
            ("회복", "체력을 서서히 회복합니다.", 2, true),
            ("관통 탄", "탄환이 적을 통과합니다.", 50, false)
        };

        var cardViews = new InGameShopCardView[InGameShopPopup.CardCount];
        float[] xs = { -460f, 0f, 460f };
        for (int i = 0; i < InGameShopPopup.CardCount; i++)
        {
            cardViews[i] = CreateCard(
                cardsRoot,
                $"Card00{i + 1}",
                xs[i],
                font,
                cardBg,
                previews[i].Item1,
                previews[i].Item2,
                previews[i].Item3,
                previews[i].Item4);
        }

        var popup = root.GetComponent<InGameShopPopup>();
        if (popup == null)
            popup = root.AddComponent<InGameShopPopup>();

        var so = new SerializedObject(popup);
        var cardsProp = so.FindProperty("cards");
        cardsProp.arraySize = InGameShopPopup.CardCount;
        for (int i = 0; i < InGameShopPopup.CardCount; i++)
            cardsProp.GetArrayElementAtIndex(i).objectReferenceValue = cardViews[i];

        so.FindProperty("purchaseButton").objectReferenceValue =
            FindButton(root.transform, "Button_Purchase") ?? FindButton(root.transform, "Button_Claim");
        so.FindProperty("rerollButton").objectReferenceValue =
            FindButton(root.transform, "Button_Reroll") ?? FindButton(root.transform, "Button_2xClaim");
        so.FindProperty("closeButton").objectReferenceValue = FindButton(root.transform, "Button_Close");

        var catalog = AssetDatabase.LoadAssetAtPath<InGameShopCatalogSO>(InGameShopPopup.DefaultCatalogPath);
        var catalogProp = so.FindProperty("catalog");
        if (catalogProp != null)
            catalogProp.objectReferenceValue = catalog;
        var hideProp = so.FindProperty("hideOnClose");
        if (hideProp != null)
            hideProp.boolValue = false;

        var previewProp = so.FindProperty("previewCards");
        previewProp.arraySize = InGameShopPopup.CardCount;
        for (int i = 0; i < InGameShopPopup.CardCount; i++)
        {
            var e = previewProp.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("itemName").stringValue = previews[i].Item1;
            e.FindPropertyRelative("description").stringValue = previews[i].Item2;
            e.FindPropertyRelative("price").intValue = previews[i].Item3;
            e.FindPropertyRelative("useGem").boolValue = previews[i].Item4;
        }

        so.FindProperty("selectedIndex").intValue = 0;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (cardViews[0] != null)
            cardViews[0].SetSelected(true);
    }

    private static InGameShopCardView CreateCard(
        Transform parent,
        string name,
        float x,
        TMP_FontAsset font,
        Sprite bgSprite,
        string itemName,
        string description,
        int price,
        bool useGem)
    {
        var card = CreateUiObject(name, parent);
        var rect = card.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(400f, 600f);
        rect.anchoredPosition = new Vector2(x, 0f);

        var bg = card.AddComponent<Image>();
        bg.sprite = null;
        bg.type = Image.Type.Simple;
        bg.color = new Color(0.12f, 0.16f, 0.28f, 0.95f);
        bg.raycastTarget = true;

        var button = card.AddComponent<Button>();
        button.targetGraphic = bg;
        button.transition = Selectable.Transition.ColorTint;

        var frameGo = CreateUiObject("Frame", card.transform);
        Stretch(frameGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
        var frameImg = frameGo.AddComponent<Image>();
        frameImg.raycastTarget = false;
        frameImg.color = new Color(0.62f, 0.62f, 0.62f, 1f);
        frameGo.transform.SetAsFirstSibling();

        var categoryGo = CreateUiObject("Category", card.transform);
        var categoryRect = categoryGo.GetComponent<RectTransform>();
        categoryRect.anchorMin = new Vector2(0f, 1f);
        categoryRect.anchorMax = new Vector2(0f, 1f);
        categoryRect.pivot = new Vector2(0.5f, 1f);
        categoryRect.sizeDelta = new Vector2(50f, 50f);
        categoryRect.anchoredPosition = new Vector2(50f, -15f);
        var categoryImg = categoryGo.AddComponent<Image>();
        categoryImg.preserveAspect = true;
        categoryImg.raycastTarget = false;
        categoryImg.color = Color.white;
        categoryGo.transform.SetSiblingIndex(1);

        var iconGo = CreateUiObject("Icon", card.transform);
        var iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.sizeDelta = new Vector2(220f, 220f);
        iconRect.anchoredPosition = new Vector2(0f, -36f);
        var icon = iconGo.AddComponent<Image>();
        icon.color = Color.white;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        var nameTmp = CreateTmp(
            "Name", card.transform, font, itemName, 36f,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -276f), new Vector2(340f, 50f),
            TextAlignmentOptions.Center, false);

        var descTmp = CreateTmp(
            "Description", card.transform, font, description, 24f,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -40f), new Vector2(340f, 160f),
            TextAlignmentOptions.Top, true);

        var priceRow = CreateUiObject("PriceRow", card.transform);
        var priceRect = priceRow.GetComponent<RectTransform>();
        priceRect.anchorMin = new Vector2(0.5f, 0f);
        priceRect.anchorMax = new Vector2(0.5f, 0f);
        priceRect.pivot = new Vector2(0.5f, 0f);
        priceRect.sizeDelta = new Vector2(300f, 56f);
        priceRect.anchoredPosition = new Vector2(0f, 28f);

        var priceIconGo = CreateUiObject("PriceIcon", priceRow.transform);
        var priceIconRect = priceIconGo.GetComponent<RectTransform>();
        priceIconRect.anchorMin = new Vector2(0f, 0.5f);
        priceIconRect.anchorMax = new Vector2(0f, 0.5f);
        priceIconRect.pivot = new Vector2(0f, 0.5f);
        priceIconRect.sizeDelta = new Vector2(48f, 48f);
        priceIconRect.anchoredPosition = new Vector2(40f, 0f);
        var priceIcon = priceIconGo.AddComponent<Image>();
        priceIcon.sprite = useGem ? HudResourceIcons.Gem : HudResourceIcons.Coin;
        priceIcon.preserveAspect = true;
        priceIcon.raycastTarget = false;

        var priceTmp = CreateTmp(
            "PriceText", priceRow.transform, font, price.ToString(), 32f,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(100f, 0f), new Vector2(180f, 56f),
            TextAlignmentOptions.MidlineLeft, false);

        var view = card.AddComponent<InGameShopCardView>();
        var viewSo = new SerializedObject(view);
        viewSo.FindProperty("background").objectReferenceValue = bg;
        viewSo.FindProperty("icon").objectReferenceValue = icon;
        viewSo.FindProperty("itemName").objectReferenceValue = nameTmp;
        viewSo.FindProperty("description").objectReferenceValue = descTmp;
        viewSo.FindProperty("priceText").objectReferenceValue = priceTmp;
        viewSo.FindProperty("priceIcon").objectReferenceValue = priceIcon;
        viewSo.FindProperty("frame").objectReferenceValue = frameImg;
        viewSo.FindProperty("categoryIcon").objectReferenceValue = categoryImg;
        viewSo.FindProperty("scaleRoot").objectReferenceValue = rect;
        viewSo.FindProperty("moneySprite").objectReferenceValue = HudResourceIcons.Coin;
        viewSo.FindProperty("gemSprite").objectReferenceValue = HudResourceIcons.Gem;
        viewSo.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    private static void CreateDesignScene(string prefabPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            return;

        EnsureFolder("Assets/Scenes", "UI");

        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
        EditorSceneManager.SetActiveScene(scene);

        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        esGo.AddComponent<InputSystemUIInputModule>();
#else
        esGo.AddComponent<StandaloneInputModule>();
#endif

        var shop = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        shop.transform.SetParent(canvasGo.transform, false);
        var shopRect = shop.GetComponent<RectTransform>();
        if (shopRect != null)
        {
            shopRect.anchorMin = Vector2.zero;
            shopRect.anchorMax = Vector2.one;
            shopRect.offsetMin = Vector2.zero;
            shopRect.offsetMax = Vector2.zero;
        }

        EditorSceneManager.SaveScene(scene, DesignScenePath);
        EditorSceneManager.CloseScene(scene, true);
        if (previous.IsValid())
            EditorSceneManager.SetActiveScene(previous);
    }

    private static TextMeshProUGUI CreateTmp(
        string name,
        Transform parent,
        TMP_FontAsset font,
        string text,
        float fontSize,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPos,
        Vector2 size,
        TextAlignmentOptions align,
        bool wrap)
    {
        var go = CreateUiObject(name, parent);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = align;
        tmp.enableWordWrapping = wrap;
        tmp.overflowMode = wrap ? TextOverflowModes.Truncate : TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = -offsetMax;
    }

    private static void SetActiveByName(Transform root, string name, bool active)
    {
        var t = root.Find(name);
        if (t != null)
            t.gameObject.SetActive(active);
    }

    private static void RenameAndSetButtonText(Transform root, string currentName, string newName, string label)
    {
        var t = root.Find(currentName);
        if (t == null)
            t = root.Find(newName);
        if (t == null)
            return;

        t.gameObject.name = newName;
        var tmp = t.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
            tmp.text = label;
    }

    private static Button FindButton(Transform root, string name)
    {
        var t = root.Find(name);
        return t != null ? t.GetComponent<Button>() : null;
    }

    private static TextMeshProUGUI FindTmp(Transform root, string name)
    {
        var t = FindDeep(root, name);
        return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name)
            return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var found = FindDeep(parent.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }

    private static Sprite LoadSprite(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path))
            return null;
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
