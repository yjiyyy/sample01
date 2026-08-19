using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 로비 오른쪽 텍스트 메뉴와 상단 바(코인/젬 아이콘·옵션).
/// 마지막으로 고른 항목의 글자가 커집니다. 모바일은 탭한 항목, PC는 올려놓은 항목도 커집니다.
/// </summary>
[ExecuteAlways]
public class LobbyMenuUI : MonoBehaviour
{
    public enum MenuAction
    {
        CharacterChange = 0,
        Upgrade = 1,
        Shop = 2,
        Inventory = 3,
        StartBattle = 4
    }

    [Serializable]
    public class MenuEntry
    {
        public Button button;
        public TextMeshProUGUI label;
        public LayoutElement layoutElement;
        public MenuAction action;
    }

    [Header("메뉴")]
    [SerializeField] private MenuEntry[] entries = Array.Empty<MenuEntry>();
    [SerializeField] private int defaultSelectedIndex = 4;
    [SerializeField] private float normalFontSize = 42f;
    [SerializeField] private float selectedFontSize = 72f;
    [SerializeField] private float normalItemHeight = 64f;
    [SerializeField] private float selectedItemHeight = 110f;
    [SerializeField] private float animSpeed = 14f;

    [Header("상단 바")]
    [SerializeField] private TextMeshProUGUI moneyText;
    [FormerlySerializedAs("jamText")]
    [SerializeField] private TextMeshProUGUI gemText;
    [SerializeField] private Button optionsButton;
    [Tooltip("비어 있으면 MoneyText 아래에 Icon_Coin을 만듭니다. 위치·크기는 이 오브젝트를 골라 조정하세요.")]
    [SerializeField] private Image moneyIcon;
    [Tooltip("비어 있으면 GemText 아래에 Icon_Gem을 만듭니다. 위치·크기는 이 오브젝트를 골라 조정하세요.")]
    [SerializeField] private Image gemIcon;

    private const float ResourceIconSize = 40f;

    [Header("동작 연결")]
    [SceneName]
    [SerializeField] private string characterSelectScene = SceneNames.CharacterSelection;
    [SerializeField] private ShopPanel shopPanel;
    [SerializeField] private StageSelectPanel stageSelectPanel;
    [SerializeField] private PlayerResources resources;

    private int _selectedIndex;
    private int _hoverIndex = -1;
    private float[] _currentFontSizes;
    private float[] _currentHeights;

    private static readonly Color NormalColor = new Color(1f, 1f, 1f, 0.62f);
    private static readonly Color SelectedColor = Color.white;

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        _selectedIndex = Mathf.Clamp(defaultSelectedIndex, 0, Mathf.Max(0, entries.Length - 1));
        _currentFontSizes = new float[entries.Length];
        _currentHeights = new float[entries.Length];

        for (int i = 0; i < entries.Length; i++)
        {
            int index = i;
            var entry = entries[i];
            if (entry == null || entry.button == null)
                continue;

            entry.button.onClick.RemoveAllListeners();
            entry.button.onClick.AddListener(() => OnItemClicked(index));

            var hover = entry.button.GetComponent<LobbyMenuHoverRelay>();
            if (hover == null)
                hover = entry.button.gameObject.AddComponent<LobbyMenuHoverRelay>();
            hover.Setup(this, index);

            bool selected = i == _selectedIndex;
            _currentFontSizes[i] = selected ? selectedFontSize : normalFontSize;
            _currentHeights[i] = selected ? selectedItemHeight : normalItemHeight;
            ApplyVisualImmediate(i);
        }

        if (optionsButton != null)
        {
            optionsButton.onClick.RemoveAllListeners();
            optionsButton.onClick.AddListener(OnOptions);
        }

        if (shopPanel == null)
            shopPanel = FindFirstObjectByType<ShopPanel>(FindObjectsInactive.Include);
        if (stageSelectPanel == null)
            stageSelectPanel = FindFirstObjectByType<StageSelectPanel>(FindObjectsInactive.Include);
    }

    private void OnEnable()
    {
        EnsureResourceIcons();
        if (Application.isPlaying)
            BindResources();
        else
            RefreshResourceLabels(0, 0);
    }

    private void OnDisable()
    {
        if (resources != null)
            resources.OnResourcesChanged -= OnResourcesChanged;
    }

    private void Start()
    {
        if (!Application.isPlaying)
            return;

        BindResources();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        if (entries == null || entries.Length == 0)
            return;

        int visualIndex = _hoverIndex >= 0 ? _hoverIndex : _selectedIndex;
        float dt = Time.unscaledDeltaTime * animSpeed;

        for (int i = 0; i < entries.Length; i++)
        {
            bool selected = i == visualIndex;
            float targetSize = selected ? selectedFontSize : normalFontSize;
            float targetHeight = selected ? selectedItemHeight : normalItemHeight;
            _currentFontSizes[i] = Mathf.Lerp(_currentFontSizes[i], targetSize, dt);
            _currentHeights[i] = Mathf.Lerp(_currentHeights[i], targetHeight, dt);
            ApplyVisualImmediate(i);
        }
    }

    /// <summary>
    /// 스폰된 캐릭터의 Money/Gem을 상단 바에 연결합니다.
    /// </summary>
    public void BindResources()
    {
        if (resources != null)
            resources.OnResourcesChanged -= OnResourcesChanged;

        resources = PlayerResources.Instance != null
            ? PlayerResources.Instance
            : FindFirstObjectByType<PlayerResources>();

        if (resources != null)
        {
            resources.OnResourcesChanged -= OnResourcesChanged;
            resources.OnResourcesChanged += OnResourcesChanged;
            RefreshResourceLabels(resources.Money, resources.Gem);
        }
        else
        {
            RefreshResourceLabels(0, 0);
        }
    }

    public void OnItemHover(int index)
    {
        _hoverIndex = index;
    }

    public void OnItemUnhover(int index)
    {
        if (_hoverIndex == index)
            _hoverIndex = -1;
    }

    private void OnItemClicked(int index)
    {
        if (index < 0 || index >= entries.Length)
            return;

        _selectedIndex = index;
        InvokeAction(entries[index].action);
    }

    private void InvokeAction(MenuAction action)
    {
        switch (action)
        {
            case MenuAction.CharacterChange:
                if (!string.IsNullOrEmpty(characterSelectScene))
                    SceneManager.LoadScene(characterSelectScene);
                else
                    Debug.LogWarning("[LobbyMenuUI] 캐릭터 선택 씬 이름이 비어 있습니다.");
                break;
            case MenuAction.Upgrade:
                Debug.Log("[LobbyMenuUI] 업그레이드는 아직 준비 중입니다.");
                break;
            case MenuAction.Shop:
                if (shopPanel != null)
                    shopPanel.Show();
                else
                    Debug.LogWarning("[LobbyMenuUI] ShopPanel이 없습니다.");
                break;
            case MenuAction.Inventory:
                Debug.Log("[LobbyMenuUI] 인벤토리는 아직 준비 중입니다.");
                break;
            case MenuAction.StartBattle:
                if (stageSelectPanel != null)
                    stageSelectPanel.Show();
                else
                    Debug.LogWarning("[LobbyMenuUI] StageSelectPanel이 없습니다.");
                break;
        }
    }

    private void OnOptions()
    {
        var options = OptionsUI.EnsureExists();
        if (options != null)
            options.Show();
    }

    private void OnResourcesChanged(int money, int gem)
    {
        RefreshResourceLabels(money, gem);
    }

    public void EnsureResourceIcons()
    {
        if (moneyIcon == null && moneyText != null)
            moneyIcon = HudResourceIcons.GetOrCreateIcon(
                moneyText, HudResourceIcons.Coin, HudResourceIcons.CoinChildName, ResourceIconSize);

        if (gemIcon == null && gemText != null)
            gemIcon = HudResourceIcons.GetOrCreateIcon(
                gemText, HudResourceIcons.Gem, HudResourceIcons.GemChildName, ResourceIconSize);
    }

    private void RefreshResourceLabels(int money, int gem)
    {
        if (moneyText != null)
            moneyText.text = money.ToString();
        if (gemText != null)
            gemText.text = gem.ToString();
    }

    private void ApplyVisualImmediate(int index)
    {
        if (index < 0 || index >= entries.Length)
            return;

        var entry = entries[index];
        int visualIndex = _hoverIndex >= 0 ? _hoverIndex : _selectedIndex;
        bool selected = index == visualIndex;

        if (entry.label != null)
        {
            entry.label.fontSize = _currentFontSizes[index];
            entry.label.color = selected ? SelectedColor : NormalColor;
        }

        if (entry.layoutElement != null)
            entry.layoutElement.preferredHeight = _currentHeights[index];
    }
}

/// <summary>
/// 메뉴 글자 위에 마우스를 올렸을 때 커지게 하는 작은 연결 부품입니다.
/// </summary>
public class LobbyMenuHoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private LobbyMenuUI _owner;
    private int _index;

    public void Setup(LobbyMenuUI owner, int index)
    {
        _owner = owner;
        _index = index;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _owner?.OnItemHover(_index);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _owner?.OnItemUnhover(_index);
    }
}
