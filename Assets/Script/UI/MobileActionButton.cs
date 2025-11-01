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

    [Tooltip("눌린 동안 시각 피드백(선택)")]
    public Image holdHighlight;

    private bool pressed;

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
        if (holdHighlight != null) holdHighlight.enabled = true;

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
        if (!pressed) return;
        pressed = false;
        if (holdHighlight != null) holdHighlight.enabled = false;

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
}