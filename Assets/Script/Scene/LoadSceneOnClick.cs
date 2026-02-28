using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 버튼에 붙이면, 클릭 시 지정한 씬으로 이동합니다.
/// Inspector에서 씬 이름을 입력하거나, 씬 파일을 드래그하면 이름이 자동으로 채워집니다.
/// </summary>
[RequireComponent(typeof(Button))]
public class LoadSceneOnClick : MonoBehaviour
{
    [Tooltip("로드할 씬. Build Settings에 등록된 씬 목록에서 선택하세요.")]
    [SceneName]
    [SerializeField] private string targetSceneName;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        if (_button != null && !string.IsNullOrEmpty(targetSceneName))
            _button.onClick.AddListener(LoadTargetScene);
    }

    /// <summary>
    /// 다른 스크립트에서 호출해도 됩니다.
    /// </summary>
    public void LoadTargetScene()
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogWarning("[LoadSceneOnClick] targetSceneName이 지정되지 않았습니다.", gameObject);
            return;
        }

        SceneManager.LoadScene(targetSceneName);
    }

}
