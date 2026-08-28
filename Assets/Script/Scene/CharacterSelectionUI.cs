using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 선택 화면 UI. 슬롯·일러스트·스탯·하단 버튼 표시.
/// </summary>
public class CharacterSelectionUI : MonoBehaviour
{
    public const int StatSegmentCount = 5;

    [Serializable]
    public class CarouselSlot
    {
        public Button button;
        public Image portrait;
        public Image selectFrame;
        public GameObject lockOverlay;
    }

    [Serializable]
    public class StatRow
    {
        public Image[] segments = new Image[StatSegmentCount];
    }

    [Header("표시")]
    [SerializeField] private Image illustrationImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("스탯 행 (표시 전용)")]
    [SerializeField] private StatRow hpRow;
    [SerializeField] private StatRow stRow;
    [SerializeField] private StatRow spdRow;
    [SerializeField] private StatRow strRow;
    [SerializeField] private StatRow meleeAtkRow;
    [SerializeField] private StatRow rangedAtkRow;

    [Header("캐러셀")]
    [SerializeField] private CarouselSlot[] slots = new CarouselSlot[5];

    [Header("슬롯 선택 표시")]
    [SerializeField] private float selectedSlotScale = 1.18f;
    [SerializeField] private float normalSlotScale = 1f;

    [Header("하단")]
    [SerializeField] private Button returnButton;
    [SerializeField] private Button confirmButton;

    [Header("스탯 색")]
    [SerializeField] private Color statFilledColor = new Color(1f, 0.28f, 0.55f, 1f);
    [SerializeField] private Color statEmptyColor = new Color(0.28f, 0.28f, 0.32f, 0.55f);

    private CharacterDataSO[] _characters = Array.Empty<CharacterDataSO>();
    private int _selectedIndex = -1;

    public event Action<int> SelectionChanged;
    public event Action ReturnClicked;
    public event Action ConfirmClicked;

    public int SelectedIndex => _selectedIndex;

    public CharacterDataSO SelectedCharacter =>
        _characters != null && _selectedIndex >= 0 && _selectedIndex < _characters.Length
            ? _characters[_selectedIndex]
            : null;

    private void Awake()
    {
        if (returnButton != null)
            returnButton.onClick.AddListener(() => ReturnClicked?.Invoke());
        if (confirmButton != null)
            confirmButton.onClick.AddListener(() => ConfirmClicked?.Invoke());

        for (int i = 0; i < slots.Length; i++)
        {
            int idx = i;
            if (slots[i]?.button != null)
                slots[i].button.onClick.AddListener(() => TrySelectIndex(idx));

            if (slots[i]?.selectFrame != null)
                slots[i].selectFrame.gameObject.SetActive(false);
        }

        RefreshSelectionFrames();
    }

    public void BindCharacters(CharacterDataSO[] characters)
    {
        _characters = characters ?? Array.Empty<CharacterDataSO>();
        RefreshSlotPortraits();

        if (_characters.Length == 0)
        {
            ClearDetailPanel();
            return;
        }

        int start = FindFirstSelectableIndex(0, 1);
        if (start >= 0)
            SelectIndex(start, false);
        else
            ClearDetailPanel();
    }

    public void SelectIndex(int index, bool notify = true)
    {
        if (_characters == null || index < 0 || index >= _characters.Length)
            return;

        var data = _characters[index];
        if (data == null || data.isLocked)
            return;

        _selectedIndex = index;
        RefreshDetailPanel(data);
        RefreshSelectionFrames();

        if (notify)
            SelectionChanged?.Invoke(_selectedIndex);
    }

    private void TrySelectIndex(int index)
    {
        if (_characters == null || index < 0 || index >= _characters.Length)
            return;

        var data = _characters[index];
        if (data == null || data.isLocked)
            return;

        SelectIndex(index);
    }

    private int FindFirstSelectableIndex(int start, int direction)
    {
        if (_characters == null || _characters.Length == 0)
            return -1;

        int count = _characters.Length;
        start = Mathf.Clamp(start, 0, count - 1);

        for (int step = 0; step < count; step++)
        {
            int i = start;
            var data = _characters[i];
            if (data != null && !data.isLocked)
                return i;

            start += direction;
            if (start < 0)
                start = count - 1;
            else if (start >= count)
                start = 0;
        }

        return -1;
    }

    private void RefreshSlotPortraits()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot == null)
                continue;

            CharacterDataSO data = _characters != null && i < _characters.Length ? _characters[i] : null;
            bool hasData = data != null;
            bool locked = hasData && data.isLocked;

            if (slot.portrait != null)
            {
                slot.portrait.sprite = hasData ? data.portrait : null;
                slot.portrait.color = hasData
                    ? (locked ? new Color(0.35f, 0.35f, 0.35f, 0.85f) : Color.white)
                    : new Color(0.22f, 0.22f, 0.26f, 0.7f);
                slot.portrait.enabled = true;
            }

            if (slot.lockOverlay != null)
                slot.lockOverlay.SetActive(locked);

            if (slot.button != null)
                slot.button.interactable = hasData && !locked;
        }

        RefreshSelectionFrames();
    }

    private void RefreshSelectionFrames()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot?.button == null)
                continue;

            if (slot.selectFrame != null)
                slot.selectFrame.gameObject.SetActive(false);

            bool selected = i == _selectedIndex;
            var slotRect = slot.button.transform as RectTransform;
            if (slotRect != null)
            {
                float scale = selected ? selectedSlotScale : normalSlotScale;
                slotRect.localScale = new Vector3(scale, scale, 1f);
            }
        }
    }

    private void RefreshDetailPanel(CharacterDataSO data)
    {
        if (data == null)
        {
            ClearDetailPanel();
            return;
        }

        if (illustrationImage != null)
        {
            illustrationImage.sprite = data.illustration != null ? data.illustration : data.portrait;
            illustrationImage.color = illustrationImage.sprite != null
                ? Color.white
                : new Color(0.25f, 0.35f, 0.55f, 0.35f);
            illustrationImage.enabled = true;
            if (!illustrationImage.gameObject.activeSelf)
                illustrationImage.gameObject.SetActive(true);
        }

        if (nameText != null)
            nameText.text = string.IsNullOrWhiteSpace(data.displayName) ? "???" : data.displayName.ToUpperInvariant();

        if (descriptionText != null)
            descriptionText.text = data.description ?? string.Empty;

        ApplyStatRow(hpRow, data.hpTiers);
        ApplyStatRow(stRow, data.stTiers);
        ApplyStatRow(spdRow, data.spdTiers);
        ApplyStatRow(strRow, data.strTiers);
        ApplyStatRow(meleeAtkRow, data.meleeAtkTiers);
        ApplyStatRow(rangedAtkRow, data.rangedAtkTiers);
    }

    private void ClearDetailPanel()
    {
        if (illustrationImage != null)
        {
            illustrationImage.sprite = null;
            illustrationImage.color = new Color(0.25f, 0.35f, 0.55f, 0.35f);
        }

        if (nameText != null)
            nameText.text = string.Empty;

        if (descriptionText != null)
            descriptionText.text = string.Empty;

        ApplyStatRow(hpRow, 0);
        ApplyStatRow(stRow, 0);
        ApplyStatRow(spdRow, 0);
        ApplyStatRow(strRow, 0);
        ApplyStatRow(meleeAtkRow, 0);
        ApplyStatRow(rangedAtkRow, 0);
        RefreshSelectionFrames();
    }

    private void ApplyStatRow(StatRow row, int tier)
    {
        if (row?.segments == null)
            return;

        tier = Mathf.Clamp(tier, 0, row.segments.Length);
        for (int i = 0; i < row.segments.Length; i++)
        {
            var seg = row.segments[i];
            if (seg == null)
                continue;

            bool filled = i < tier;
            seg.color = filled ? statFilledColor : statEmptyColor;
            seg.enabled = true;
        }
    }
}
