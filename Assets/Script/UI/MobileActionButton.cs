using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum MobileActionType
{
    Attack,
    Evade
}

[DisallowMultipleComponent]
public class MobileActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public MobileActionType actionType = MobileActionType.Attack;

    [Tooltip("???? ???? ?????(????)")]
    public Image holdHighlight;

    [Tooltip("?????? ?? ?? Push ???????. ??? ?????? ??��??? Push ????? ??????.")]
    public GameObject pushObject;

    private bool pressed;
    private Image pushFillImage;
    private PlayerChargeController charge;

    void Awake()
    {
        if (pushObject == null)
        {
            var push = FindChildByName(transform, "Push");
            if (push != null)
                pushObject = push.gameObject;
        }

        if (pushObject != null)
        {
            pushFillImage = pushObject.GetComponent<Image>();
            pushObject.SetActive(false);
            if (pushFillImage != null)
                pushFillImage.fillAmount = 0f;
        }

        DisableNestedRaycasts();
        DisableNestedButtons();
    }

    void OnDisable()
    {
        if (pressed)
            Release();
        if (actionType == MobileActionType.Attack)
            HideAttackPush();
    }

    void LateUpdate()
    {
        if (actionType == MobileActionType.Attack)
            UpdateAttackPushFill();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
        SetPressedVisual(true);

        switch (actionType)
        {
            case MobileActionType.Attack:
                InputManager.Instance?.MobileAttackDown();
                break;
            case MobileActionType.Evade:
                InputManager.Instance?.MobileEvadeDown();
                break;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Release();
    }

    private void Release()
    {
        if (!pressed)
            return;
        pressed = false;
        SetPressedVisual(false);

        switch (actionType)
        {
            case MobileActionType.Attack:
                InputManager.Instance?.MobileAttackUp();
                break;
            case MobileActionType.Evade:
                InputManager.Instance?.MobileEvadeUp();
                break;
        }
    }

    private void SetPressedVisual(bool on)
    {
        if (holdHighlight != null)
            holdHighlight.enabled = on;
        // ???? ??? Push?? LateUpdate???? ???? ?????? ???? ???????.
        if (actionType == MobileActionType.Attack)
            return;
        if (pushObject != null)
            pushObject.SetActive(on);
    }

    private void UpdateAttackPushFill()
    {
        if (pushObject == null)
            return;

        EnsureCharge();

        bool attackHeld = pressed;
        if (!attackHeld && InputManager.Instance != null)
            attackHeld = InputManager.Instance.GetAttack();

        bool charging = charge != null && charge.IsChargeHoldActive;
        if (!attackHeld && !charging)
        {
            HideAttackPush();
            return;
        }

        float fill = 1f;
        if (charging)
            fill = charge.ChargeHoldProgress;
        else if (charge != null && charge.CurrentWeaponHasChargeSlot)
            fill = 0f;

        if (pushFillImage != null)
            pushFillImage.fillAmount = fill;
        if (!pushObject.activeSelf)
            pushObject.SetActive(true);
    }

    private void HideAttackPush()
    {
        if (pushFillImage != null)
            pushFillImage.fillAmount = 0f;
        if (pushObject != null && pushObject.activeSelf)
            pushObject.SetActive(false);
    }

    private void EnsureCharge()
    {
        if (charge != null)
            return;
        charge = FindFirstObjectByType<PlayerChargeController>();
    }

    private void DisableNestedRaycasts()
    {
        var graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null && graphics[i].gameObject != gameObject)
                graphics[i].raycastTarget = false;
        }
    }

    private void DisableNestedButtons()
    {
        var buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].gameObject != gameObject)
                buttons[i].enabled = false;
        }
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        if (root == null)
            return null;
        if (root.name == name)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChildByName(root.GetChild(i), name);
            if (found != null)
                return found;
        }
        return null;
    }
}
