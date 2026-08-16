using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 화면 위 무기 아이콘 클릭 시 활성 슬롯을 바꿉니다.
/// 무기 치트 오버레이는 키보드 ` 키로만 엽니다.
/// </summary>
[RequireComponent(typeof(Button))]
public class UIButton_DevWeaponToggle : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("비워두면 씬에서 자동 탐색")]
    public PlayerWeaponController targetPlayer;

    Button uiButton;

    void Start()
    {
        uiButton = GetComponent<Button>();
        EnsurePlayer();
    }

    void Update()
    {
        if (uiButton == null)
            return;

        bool overlayOpen = InputManager.Instance != null && InputManager.Instance.OverlayInputBlocked;
        uiButton.interactable = !overlayOpen;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (InputManager.Instance != null && InputManager.Instance.OverlayInputBlocked)
            return;

        EnsurePlayer();
        if (targetPlayer != null)
            targetPlayer.TrySwitchWeaponSlot();
        else
            Debug.LogWarning("[UIButton_DevWeaponToggle] PlayerWeaponController를 찾을 수 없습니다.");
    }

    public void ToggleFromInspector()
    {
        EnsurePlayer();
        targetPlayer?.TrySwitchWeaponSlot();
    }

    private void EnsurePlayer()
    {
        if (targetPlayer != null)
            return;

        if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
        {
            targetPlayer = GameManager.Instance.playerTransform.GetComponent<PlayerWeaponController>();
            if (targetPlayer != null)
                return;
        }

        targetPlayer = FindFirstObjectByType<PlayerWeaponController>();
    }
}
