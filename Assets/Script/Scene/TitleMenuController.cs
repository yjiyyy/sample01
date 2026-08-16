using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 타이틀 화면 메뉴. Inspector에서 각 버튼의 목적지 씬을 지정할 수 있습니다.
/// </summary>
public class TitleMenuController : MonoBehaviour
{
    [Header("씬 전환 (Inspector에서 지정)")]
    [SceneName]
    [SerializeField] private string newGameScene = "Loading_00";
    [SceneName]
    [SerializeField] private string loadGameScene = "";

    public void OnNewGame()
    {
        if (!string.IsNullOrEmpty(newGameScene))
            SceneManager.LoadScene(newGameScene);
        else
            Debug.LogWarning("[TitleMenuController] newGameScene이 지정되지 않았습니다.");
    }

    public void OnLoadGame()
    {
        if (!string.IsNullOrEmpty(loadGameScene))
            SceneManager.LoadScene(loadGameScene);
        else
            Debug.Log("[TitleMenuController] loadGameScene이 지정되지 않았습니다. 세이브 시스템 연동 후 설정하세요.");
    }

    public void OnOption()
    {
        if (OptionsUI.Instance != null)
        {
            OptionsUI.Instance.Show();
            return;
        }

        Debug.LogWarning("[TitleMenuController] OptionsUI가 없습니다. 타이틀 씬에 OptionsUI를 배치하세요.");
    }

    public void OnExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
