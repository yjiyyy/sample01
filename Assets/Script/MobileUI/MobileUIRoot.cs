using UnityEngine;

[DisallowMultipleComponent]
public class MobileUIRoot : MonoBehaviour
{
    [Header("표시 조건")]
    [Tooltip("에디터에서 강제 표시(기기 없이 테스트)")]
    public bool forceInEditor = true;

    private void Awake()
    {
        bool active =
#if UNITY_EDITOR
            forceInEditor;
#else
            Application.isMobilePlatform;
#endif
        gameObject.SetActive(active);

        // 에디터 강제 시 InputManager에도 동일 플래그 전달
        if (active && InputManager.Instance != null)
            InputManager.Instance.forceMobileInEditor = true;
    }
}