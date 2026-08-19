using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스테이지 HUD의 SettingButton을 찾아, 누르면 일시정지 + 옵션 창이 열리게 연결합니다.
/// 씬 파일을 직접 고치지 않고 실행 때 버튼을 붙입니다.
/// </summary>
public static class GameplayPauseOptionsBinder
{
    private const string ButtonObjectName = "SettingButton";

    public static void BindSettingButton()
    {
        var go = GameObject.Find(ButtonObjectName);
        if (go == null)
            return;

        var button = go.GetComponent<Button>();
        if (button == null)
            button = go.AddComponent<Button>();

        var graphic = go.GetComponent<Graphic>();
        if (graphic == null)
        {
            var image = go.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
            graphic = image;
        }
        else
        {
            graphic.raycastTarget = true;
        }

        button.targetGraphic = graphic;
        button.transition = Selectable.Transition.None;
        button.onClick.RemoveListener(OnSettingClicked);
        button.onClick.AddListener(OnSettingClicked);
    }

    private static void OnSettingClicked()
    {
        var options = OptionsUI.EnsureExists();
        if (options != null)
            options.ShowAndPauseGameplay();
    }
}
