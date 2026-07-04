using UnityEngine;

/// <summary>
/// 플레이어 위치를 FixedUpdate마다 한 번만 읽어 두는 공유 캐시.
/// 적 AI는 Transform을 직접 읽지 않고 이 값을 참조한다.
/// </summary>
[DefaultExecutionOrder(-200)]
public class PlayerTargetCache : MonoBehaviour
{
    public static PlayerTargetCache Instance { get; private set; }

    public Vector3 Position { get; private set; }
    public Transform Transform { get; private set; }
    public bool HasValidTarget { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        var go = new GameObject(nameof(PlayerTargetCache));
        go.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(go);
        go.AddComponent<PlayerTargetCache>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void FixedUpdate()
    {
        Refresh();
    }

    public static bool TryGetPosition(out Vector3 position)
    {
        if (Instance != null && Instance.HasValidTarget)
        {
            position = Instance.Position;
            return true;
        }

        position = default;
        return false;
    }

    private void Refresh()
    {
        if (Transform == null)
        {
            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj == null)
            {
                HasValidTarget = false;
                return;
            }

            var ph = playerObj.GetComponent<PlayerHealth>() ?? playerObj.GetComponentInChildren<PlayerHealth>();
            if (ph != null && ph.GetCurrentHP() <= 0f)
            {
                Transform = null;
                HasValidTarget = false;
                return;
            }

            Transform = playerObj.transform;
        }
        else
        {
            var ph = Transform.GetComponent<PlayerHealth>() ?? Transform.GetComponentInChildren<PlayerHealth>();
            if (ph != null && ph.GetCurrentHP() <= 0f)
            {
                Transform = null;
                HasValidTarget = false;
                return;
            }

            if (Transform == null)
            {
                HasValidTarget = false;
                return;
            }
        }

        Position = Transform.position;
        HasValidTarget = true;
    }
}
