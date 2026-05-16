using UnityEngine;

/// <summary>
/// <see cref="ISilhouetteGhostSpawnSource"/> 구현: 오버드라이브 중에만 잔상.
/// <see cref="FullBodySilhouetteGhost"/>의 기본 동작과 같지만, 다른 오브젝트에 붙여 연결할 때 사용합니다.
/// </summary>
public class OverdriveSilhouetteSpawnSource : MonoBehaviour, ISilhouetteGhostSpawnSource
{
    [SerializeField] private PlayerOverdriveUpgradeRuntime overdriveRuntime;

    public bool ShouldSpawnSilhouettes =>
        overdriveRuntime != null && overdriveRuntime.IsOverdriveActive;

    private void Awake()
    {
        if (overdriveRuntime == null)
        {
            overdriveRuntime = GetComponent<PlayerOverdriveUpgradeRuntime>() ??
                               GetComponentInChildren<PlayerOverdriveUpgradeRuntime>(true) ??
                               GetComponentInParent<PlayerOverdriveUpgradeRuntime>();
            if (overdriveRuntime == null && transform.root != null)
                overdriveRuntime = transform.root.GetComponentInChildren<PlayerOverdriveUpgradeRuntime>(true);
        }
    }
}
