using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 스토리 설명 화면. 풀스크린 이미지를 순차적으로 표시하고,
/// 키/터치로 넘기면 마지막 이미지 이후 로비로 이동합니다.
/// Inspector에서 Sprite 배열에 등록한 개수만큼 표시됩니다.
/// </summary>
[RequireComponent(typeof(Image))]
public class StorySequenceController : MonoBehaviour
{
    [Header("스토리 이미지")]
    [Tooltip("표시할 이미지들. Inspector에서 등록한 순서대로 표시됩니다.")]
    public Sprite[] storyImages;

    [Header("씬 전환")]
    [Tooltip("마지막 이미지 이후 이동할 씬. Inspector에서 지정하세요.")]
    [SceneName]
    [SerializeField] private string nextScene = "02_CharacterSelectionLevel";

    [Header("입력")]
    [Tooltip("이미지를 넘길 때 받을 키 (Space, Return, 클릭 등 모두 가능)")]
    public KeyCode advanceKey = KeyCode.Space;

    private Image _image;
    private int _currentIndex;

    private void Awake()
    {
        _image = GetComponent<Image>();
        if (_image != null)
            _image.preserveAspect = true; // 레터박스: 화면 비율이 달라도 이미지 비율 유지
    }

    private void Start()
    {
        if (storyImages == null || storyImages.Length == 0)
        {
            Debug.LogWarning("[StorySequenceController] storyImages가 비어 있습니다. nextScene으로 이동합니다.");
            LoadNextScene();
            return;
        }

        ShowImage(0);
    }

    private void Update()
    {
        if (storyImages == null || storyImages.Length == 0) return;

        // 키 입력만 Update에서 처리. 클릭/터치는 버튼(OnAdvance)에서 처리
        bool advance = GetKeyDown(advanceKey) ||
                       GetKeyDown(KeyCode.Return) ||
                       GetKeyDown(KeyCode.KeypadEnter);

        if (advance)
        {
            if (_currentIndex + 1 < storyImages.Length)
            {
                ShowImage(_currentIndex + 1);
            }
            else
            {
                LoadNextScene();
            }
        }
    }

    /// <summary>
    /// 터치/클릭으로 넘기기 (버튼 등에서 호출 가능)
    /// </summary>
    public void OnAdvance()
    {
        if (storyImages == null || storyImages.Length == 0) return;

        if (_currentIndex + 1 < storyImages.Length)
        {
            ShowImage(_currentIndex + 1);
        }
        else
        {
            LoadNextScene();
        }
    }

    private void ShowImage(int index)
    {
        _currentIndex = index;
        if (_image != null && index >= 0 && index < storyImages.Length && storyImages[index] != null)
        {
            _image.sprite = storyImages[index];
            _image.enabled = true;
        }
    }

    private void LoadNextScene()
    {
        if (!string.IsNullOrEmpty(nextScene))
            SceneManager.LoadScene(nextScene);
        else
            Debug.LogWarning("[StorySequenceController] nextScene이 지정되지 않았습니다.");
    }

    /// <summary>
    /// Input System 패키지 사용 시 Keyboard.current로 키 입력 처리.
    /// 프로젝트가 Input System 전용(activeInputHandler=1)이면 UnityEngine.Input 사용 불가.
    /// </summary>
    private static bool GetKeyDown(KeyCode kc)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;

        switch (kc)
        {
            case KeyCode.Space: return kb.spaceKey != null && kb.spaceKey.wasPressedThisFrame;
            case KeyCode.Return:
            case KeyCode.KeypadEnter: return kb.enterKey != null && kb.enterKey.wasPressedThisFrame;
            case KeyCode.E: return kb.eKey != null && kb.eKey.wasPressedThisFrame;
            case KeyCode.F: return kb.fKey != null && kb.fKey.wasPressedThisFrame;
            case KeyCode.Z: return kb.zKey != null && kb.zKey.wasPressedThisFrame;
            case KeyCode.X: return kb.xKey != null && kb.xKey.wasPressedThisFrame;
            case KeyCode.Escape: return kb.escapeKey != null && kb.escapeKey.wasPressedThisFrame;
            default: return false;
        }
#else
        return UnityEngine.Input.GetKeyDown(kc);
#endif
    }
}
