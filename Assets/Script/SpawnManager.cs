using UnityEngine;
using Unity.Cinemachine; // 꼭 이 네임스페이스 사용!

// 실행 순서를 앞당겨 최초 프레임부터 playerTransform 준비 보장
[DefaultExecutionOrder(-100)]
public class SpawnManager : MonoBehaviour
{
    public GameObject playerPrefab;
    public CinemachineCamera followCamera;

    void Start()
    {
        GameObject player = Instantiate(playerPrefab, transform.position, transform.rotation);

        // Follow 설정 (필수!)
        if (followCamera != null)
        {
            followCamera.Follow = player.transform;
            followCamera.LookAt = player.transform;
        }

        // ✅ 플레이어 Transform을 즉시 등록하여 다른 시스템(Spawner 등)이 첫 프레임부터 접근 가능
        if (GameManager.Instance != null)
        {
            GameManager.Instance.playerTransform = player.transform;
            GameManager.Instance.SpawnPlayerHPUI(player.transform); // 플레이어 HPUI 생성
        }
        else
        {
            Debug.LogError("❌ GameManager.Instance가 없습니다. GameManager를 씬에 배치했는지 확인하세요.");
        }
    }
}