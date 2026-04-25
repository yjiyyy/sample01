using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어의 <see cref="Upgrade"/> 슬롯 데이터를 읽어 HUD 아이콘을 갱신합니다.
/// </summary>
[DisallowMultipleComponent]
public class UpgradeHUD : MonoBehaviour
{
    [Header("UI (아이콘 표시용 Image 5개)")]
    [SerializeField] private Image[] slotImages = new Image[Upgrade.SlotCount];
    [SerializeField] private Sprite[] defaultSlotSprites = new Sprite[Upgrade.SlotCount];

    [Header("데이터 소스")]
    [Tooltip("비워두면 씬에서 Upgrade 컴포넌트를 자동 검색합니다.")]
    [SerializeField] private Upgrade upgrade;

    private void OnEnable()
    {
        EnsureSlotImages();
        CaptureDefaultSlotSpritesIfNeeded();
        BindUpgrade();
        Refresh();
    }

    private void Start()
    {
        EnsureSlotImages();
        CaptureDefaultSlotSpritesIfNeeded();
        BindUpgrade();
        Refresh();
    }

    private void OnDisable()
    {
        UnbindUpgrade();
    }

    private void OnValidate()
    {
        if (slotImages == null || slotImages.Length != Upgrade.SlotCount)
        {
            System.Array.Resize(ref slotImages, Upgrade.SlotCount);
        }
        if (defaultSlotSprites == null || defaultSlotSprites.Length != Upgrade.SlotCount)
        {
            System.Array.Resize(ref defaultSlotSprites, Upgrade.SlotCount);
        }

        EnsureSlotImages();
        CaptureDefaultSlotSpritesIfNeeded();
        Refresh();
    }

    public void Refresh()
    {
        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            var img = slotImages != null && i < slotImages.Length ? slotImages[i] : null;
            if (img == null)
                continue;

            UpgradeEffectSO slotData = upgrade != null ? upgrade.GetSlot(i) : null;
            if (slotData != null && slotData.icon != null)
            {
                img.sprite = slotData.icon;
                img.enabled = true;
                img.gameObject.SetActive(true);
            }
            else
            {
                // 빈 슬롯이어도 슬롯 오브젝트(박스)는 항상 보이도록 유지합니다.
                // 기본 슬롯 스프라이트(에디터에서 넣은 박스 이미지)는 지우지 않습니다.
                if (defaultSlotSprites != null && i < defaultSlotSprites.Length)
                    img.sprite = defaultSlotSprites[i];
                img.gameObject.SetActive(true);
                img.enabled = true;
            }
        }
    }

    private void BindUpgrade()
    {
        if (upgrade == null)
            upgrade = Object.FindFirstObjectByType<Upgrade>();

        if (upgrade == null)
            return;

        upgrade.OnSlotsChanged -= Refresh;
        upgrade.OnSlotsChanged += Refresh;
    }

    private void UnbindUpgrade()
    {
        if (upgrade == null)
            return;

        upgrade.OnSlotsChanged -= Refresh;
    }

    private void EnsureSlotImages()
    {
        if (slotImages == null || slotImages.Length != Upgrade.SlotCount)
            System.Array.Resize(ref slotImages, Upgrade.SlotCount);

        bool hasEmpty = false;
        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            if (slotImages[i] == null)
            {
                hasEmpty = true;
                break;
            }
        }

        if (!hasEmpty)
            return;

        int childCount = Mathf.Min(transform.childCount, Upgrade.SlotCount);
        for (int i = 0; i < childCount; i++)
        {
            if (slotImages[i] != null)
                continue;

            Transform child = transform.GetChild(i);
            if (child == null)
                continue;

            Image img = child.GetComponent<Image>();
            if (img == null)
                img = child.GetComponentInChildren<Image>(true);

            slotImages[i] = img;
        }
    }

    private void CaptureDefaultSlotSpritesIfNeeded()
    {
        if (slotImages == null)
            return;

        if (defaultSlotSprites == null || defaultSlotSprites.Length != Upgrade.SlotCount)
            System.Array.Resize(ref defaultSlotSprites, Upgrade.SlotCount);

        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            if (slotImages[i] == null)
                continue;

            if (defaultSlotSprites[i] == null)
                defaultSlotSprites[i] = slotImages[i].sprite;
        }
    }
}
