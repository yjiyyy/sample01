using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 화면 HP HUD. 씬에 놓인 캐릭터의 PlayerConfig에서 이름·초상화를 가져옵니다.
/// HP 바 길이는 최대 체력 100을 현재 프리팹 길이로 보고 비례해서 늘어납니다.
/// </summary>
public class PlayerHpHud : HPUIControllerBase
{
    const float ReferenceMaxHpForWidth = 100f;

    [Header("Player_HP")]
    [Tooltip("초상화 이미지 (Character_Sample)")]
    [SerializeField] Image portraitImage;
    [Tooltip("캐릭터 이름")]
    [SerializeField] TMP_Text nameText;
    [Tooltip("HP 바 위에 겹치는 아머(베리어) 채움")]
    [SerializeField] Image barrierFill;

    RectTransform hpBarRect;
    float hpBarWidthFor100;
    float hpBarLeft;
    float lastAppliedMaxHp = -1f;
    bool hudBindingsResolved;

    void Awake()
    {
        applyEvadeSliderColor = false;
        ResolveHudBindings();
        CaptureHpBarBaseWidth();
        HideValueLabels();
        NormalizeSlider(hpSlider);
        NormalizeSlider(evadeSlider);
    }

    protected override void Start()
    {
        ResolveHudBindings();
        base.Start();
    }

    void LateUpdate()
    {
        if (!RefreshValues())
            return;

        ApplyHpBarWidth();
        ApplyBarrierOverlay();
    }

    protected override void OnHealthSetupComplete()
    {
        ApplyPlayerConfigVisuals();
        lastAppliedMaxHp = -1f;
        ApplyHpBarWidth();
        ApplyBarrierOverlay();
    }

    void ResolveHudBindings()
    {
        if (hudBindingsResolved)
            return;

        if (hpSlider == null)
            hpSlider = FindSliderByName("HP_Slider");
        if (evadeSlider == null)
            evadeSlider = FindSliderByName("ST_Slider") ?? FindSliderByName("ST_Slider ");

        if (portraitImage == null)
        {
            var portraitTr = FindDeep(transform, "Character_Sample");
            if (portraitTr != null)
                portraitImage = portraitTr.GetComponent<Image>();
        }

        if (nameText == null)
        {
            var nameTr = FindDeep(transform, "Text_Player Name");
            if (nameTr != null)
                nameText = nameTr.GetComponent<TMP_Text>();
        }

        if (barrierFill == null)
        {
            var barrierTr = FindDeep(transform, "BarrierFill");
            if (barrierTr != null)
                barrierFill = barrierTr.GetComponent<Image>();
        }

        if (barrierFill == null)
            barrierFill = CreateBarrierOverlay();

        hudBindingsResolved = hpSlider != null;
    }

    void CaptureHpBarBaseWidth()
    {
        if (hpSlider == null)
            return;

        hpBarRect = hpSlider.transform as RectTransform;
        if (hpBarRect == null)
            return;

        hpBarWidthFor100 = Mathf.Max(1f, hpBarRect.sizeDelta.x);
        hpBarLeft = hpBarRect.anchoredPosition.x - hpBarRect.sizeDelta.x * hpBarRect.pivot.x;
    }

    void HideValueLabels()
    {
        HideChildValueText(hpSlider);
        HideChildValueText(evadeSlider);
    }

    static void HideChildValueText(Slider slider)
    {
        if (slider == null)
            return;

        var labels = slider.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] != null)
                labels[i].gameObject.SetActive(false);
        }
    }

    static void NormalizeSlider(Slider slider)
    {
        if (slider == null)
            return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.interactable = false;
    }

    void ApplyPlayerConfigVisuals()
    {
        if (!isPlayerHealth || playerHP == null)
            return;

        PlayerFacade facade = playerHP.GetComponent<PlayerFacade>() ??
                              playerHP.GetComponentInParent<PlayerFacade>() ??
                              playerHP.transform.root.GetComponentInChildren<PlayerFacade>(true);
        if (facade == null || facade.config == null)
            return;

        PlayerConfig cfg = facade.config;
        if (nameText != null && !string.IsNullOrEmpty(cfg.displayName))
            nameText.text = cfg.displayName;

        if (portraitImage != null && cfg.portrait != null)
            portraitImage.sprite = cfg.portrait;
    }

    void ApplyHpBarWidth()
    {
        if (!initialized || !isPlayerHealth || playerHP == null || hpBarRect == null)
            return;

        float maxHp = Mathf.Max(1f, playerHP.GetMaxHP());
        if (Mathf.Abs(maxHp - lastAppliedMaxHp) < 0.01f)
            return;

        lastAppliedMaxHp = maxHp;
        float width = hpBarWidthFor100 * (maxHp / ReferenceMaxHpForWidth);
        width = Mathf.Max(1f, width);

        Vector2 size = hpBarRect.sizeDelta;
        size.x = width;
        hpBarRect.sizeDelta = size;

        Vector2 pos = hpBarRect.anchoredPosition;
        pos.x = hpBarLeft + width * hpBarRect.pivot.x;
        hpBarRect.anchoredPosition = pos;
    }

    void ApplyBarrierOverlay()
    {
        if (barrierFill == null)
            return;

        if (!initialized || !isPlayerHealth || playerHP == null)
        {
            barrierFill.gameObject.SetActive(false);
            return;
        }

        if (playerBarrier == null)
        {
            playerBarrier = playerHP.GetComponent<PlayerBarrierUpgradeRuntime>() ??
                            playerHP.GetComponentInChildren<PlayerBarrierUpgradeRuntime>(true) ??
                            playerHP.transform.root.GetComponentInChildren<PlayerBarrierUpgradeRuntime>(true);
        }

        if (playerBarrier == null)
        {
            barrierFill.gameObject.SetActive(false);
            return;
        }

        float maxB = playerBarrier.GetBarrierTotalMax();
        float curB = playerBarrier.GetBarrierTotalCurrent();
        if (maxB <= 0f)
        {
            barrierFill.gameObject.SetActive(false);
            return;
        }

        barrierFill.gameObject.SetActive(true);
        float ratio = Mathf.Clamp01(curB / maxB);
        RectTransform rt = barrierFill.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = new Vector2(ratio, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }

    Image CreateBarrierOverlay()
    {
        if (hpSlider == null || hpSlider.fillRect == null)
            return null;

        Transform fillArea = hpSlider.fillRect.parent;
        if (fillArea == null)
            return null;

        var go = new GameObject("BarrierFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(fillArea, false);
        go.transform.SetAsLastSibling();

        var img = go.GetComponent<Image>();
        Image hpFill = hpSlider.fillRect.GetComponent<Image>();
        if (hpFill != null)
        {
            img.sprite = hpFill.sprite;
            img.type = hpFill.type;
            img.preserveAspect = hpFill.preserveAspect;
        }

        img.color = new Color(0.55f, 0.78f, 0.95f, 0.85f);
        img.raycastTarget = false;

        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        return img;
    }

    Slider FindSliderByName(string objectName)
    {
        Transform found = FindDeep(transform, objectName);
        return found != null ? found.GetComponent<Slider>() : null;
    }

    static Transform FindDeep(Transform root, string objectName)
    {
        if (root == null)
            return null;
        if (root.name == objectName || root.name.Trim() == objectName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = FindDeep(root.GetChild(i), objectName);
            if (child != null)
                return child;
        }

        return null;
    }
}
