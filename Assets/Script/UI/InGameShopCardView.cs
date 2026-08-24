using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인게임 상점 카드 한 장. 아이콘·이름·설명·가격·프레임을 보여 줍니다.
/// 선택 시 같은 프레임을 더 밝게 하고, 카드만 조금 커집니다.
/// </summary>
[DisallowMultipleComponent]
public class InGameShopCardView : MonoBehaviour
{
    [Header("표시")]
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private Image frame;
    [SerializeField] private Image categoryIcon;
    [SerializeField] private TextMeshProUGUI itemName;
    [SerializeField] private TextMeshProUGUI description;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private Image priceIcon;
    [SerializeField] private TextMeshProUGUI duplicationText;

    [Header("선택 표시")]
    [SerializeField] private RectTransform scaleRoot;
    [SerializeField] private float selectedScale = 1.06f;
    [SerializeField] private Color frameNormalColor = new Color(0.62f, 0.62f, 0.62f, 1f);
    [SerializeField] private Color frameSelectedColor = Color.white;

    [Header("가격 아이콘 (비우면 HUD 아이콘 사용)")]
    [SerializeField] private Sprite moneySprite;
    [SerializeField] private Sprite gemSprite;

    public Button Button { get; private set; }

    private static Sprite _raycastSprite;
    private Vector3 _normalScale = Vector3.one;
    private bool _selected;

    private void Awake()
    {
        Cache();
        EnsureFrame();
        EnsureCategory();
    }

    private void Cache()
    {
        if (Button == null)
            Button = GetComponent<Button>();
        if (scaleRoot == null)
            scaleRoot = transform as RectTransform;
        _normalScale = Vector3.one;
    }

    public void SetSelected(bool selected)
    {
        Cache();
        EnsureFrame();
        _selected = selected;
        if (scaleRoot != null)
            scaleRoot.localScale = selected ? _normalScale * selectedScale : _normalScale;
        ApplyFrameColor();
    }

    public void Apply(
        string nameText,
        string descriptionText,
        Sprite iconSprite,
        int price,
        bool useGem,
        Sprite frameSprite = null)
    {
        Cache();
        EnsureFrame();
        EnsureCategory();
        EnsureDuplication();

        if (background != null)
        {
            background.sprite = RaycastSprite();
            background.raycastTarget = true;
        }

        if (itemName != null)
            itemName.text = nameText ?? string.Empty;
        if (description != null)
            description.text = descriptionText ?? string.Empty;
        if (icon != null)
        {
            icon.sprite = iconSprite;
            icon.color = Color.white;
            icon.enabled = iconSprite != null;
        }

        if (priceText != null)
            priceText.text = price.ToString();

        Sprite currency = useGem
            ? (gemSprite != null ? gemSprite : HudResourceIcons.Gem)
            : (moneySprite != null ? moneySprite : HudResourceIcons.Coin);
        if (priceIcon != null)
        {
            priceIcon.sprite = currency;
            priceIcon.enabled = currency != null;
        }

        if (frame != null)
        {
            frame.sprite = frameSprite;
            frame.raycastTarget = true;
            frame.enabled = frameSprite != null;
            frame.gameObject.SetActive(frameSprite != null);
            ApplyFrameColor();
        }

        Sprite categorySprite = ShopCardArt.GetCategoryIcon(frameSprite);
        if (categoryIcon != null)
        {
            categoryIcon.sprite = categorySprite;
            categoryIcon.preserveAspect = true;
            categoryIcon.raycastTarget = false;
            categoryIcon.enabled = categorySprite != null;
            categoryIcon.gameObject.SetActive(categorySprite != null);
        }

        SetDuplication(0);
    }

    /// <summary>같은 칸 스택 표시. displayPlus 0이면 숨김, 1이면 "+1".</summary>
    public void SetDuplication(int displayPlus)
    {
        EnsureDuplication();
        if (duplicationText == null)
            return;

        if (displayPlus > 0)
        {
            duplicationText.gameObject.SetActive(true);
            duplicationText.enabled = true;
            duplicationText.text = $"+{displayPlus}";
            duplicationText.alpha = 1f;
        }
        else
        {
            duplicationText.text = string.Empty;
            duplicationText.enabled = false;
            // 카드의 Duplication은 보통 텍스트 전용이므로 숨깁니다.
            duplicationText.gameObject.SetActive(false);
        }
    }

    private void EnsureDuplication()
    {
        if (duplicationText != null)
            return;

        Transform found = transform.Find("Duplication");
        if (found == null && Application.isPlaying)
            found = CloneDuplicationTemplateFromAnotherCard();
        if (found == null)
            return;

        duplicationText = found.GetComponent<TextMeshProUGUI>();
        if (duplicationText == null)
            duplicationText = found.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    /// <summary>
    /// 런타임 상점 프리팹에는 Card001에만 Duplication 양식이 있으므로,
    /// 다른 카드에는 그 오브젝트를 복제하여 같은 위치·폰트·크기를 사용합니다.
    /// </summary>
    private Transform CloneDuplicationTemplateFromAnotherCard()
    {
        InGameShopCardView[] allCards = UnityEngine.Object.FindObjectsByType<InGameShopCardView>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < allCards.Length; i++)
        {
            InGameShopCardView sourceCard = allCards[i];
            if (sourceCard == null || sourceCard == this)
                continue;

            Transform template = sourceCard.transform.Find("Duplication");
            if (template == null)
                continue;

            GameObject clone = UnityEngine.Object.Instantiate(template.gameObject, transform, false);
            clone.name = "Duplication";
            return clone.transform;
        }

        return null;
    }

    private void ApplyFrameColor()
    {
        if (frame == null || !frame.enabled)
            return;
        frame.color = _selected ? frameSelectedColor : frameNormalColor;
    }

    private void EnsureFrame()
    {
        if (frame != null)
            return;

        Transform found = transform.Find("Frame");
        if (found == null)
            found = transform.Find("Selected");
        if (found != null)
        {
            found.name = "Frame";
            frame = found.GetComponent<Image>();
            if (frame != null)
            {
                frame.raycastTarget = true;
                frame.color = frameNormalColor;
            }

            found.gameObject.SetActive(true);
            return;
        }

        var go = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        go.transform.SetAsFirstSibling();

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        frame = go.GetComponent<Image>();
        frame.raycastTarget = true;
        frame.preserveAspect = false;
        frame.color = frameNormalColor;
    }

    private void EnsureCategory()
    {
        if (categoryIcon != null)
        {
            categoryIcon.raycastTarget = false;
            return;
        }

        Transform found = transform.Find("Category");
        if (found == null)
        {
            var go = new GameObject("Category", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(50f, 50f);
            rect.anchoredPosition = new Vector2(50f, -15f);

            int frameIndex = 0;
            Transform frameTf = transform.Find("Frame");
            if (frameTf != null)
                frameIndex = frameTf.GetSiblingIndex();
            go.transform.SetSiblingIndex(frameIndex + 1);

            found = go.transform;
        }

        categoryIcon = found.GetComponent<Image>();
        if (categoryIcon != null)
        {
            categoryIcon.raycastTarget = false;
            categoryIcon.preserveAspect = true;
            categoryIcon.color = Color.white;
        }
    }

    private static Sprite RaycastSprite()
    {
        if (_raycastSprite != null)
            return _raycastSprite;

        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.hideFlags = HideFlags.HideAndDontSave;
        _raycastSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        _raycastSprite.hideFlags = HideFlags.HideAndDontSave;
        return _raycastSprite;
    }
}
