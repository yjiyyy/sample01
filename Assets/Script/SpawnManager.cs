using UnityEngine;
using Unity.Cinemachine; // 꼭 이 네임스페이스 사용!

public class SpawnManager : MonoBehaviour
{
    public GameObject playerPrefab;
    public CinemachineCamera followCamera;

    void Start()
    {
        GameObject player = Instantiate(playerPrefab, transform.position, transform.rotation);

        // Follow 설정 (필수!)
        followCamera.Follow = player.transform;
        followCamera.LookAt = player.transform;

        // ✅ GameManager에 UI 생성 요청
        GameManager.Instance.SpawnPlayerHPUI(player.transform);
    }
}
