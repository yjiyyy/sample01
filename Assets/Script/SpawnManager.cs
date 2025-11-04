using System.Collections;
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

        // 안전하게 GameManager에 등록 — Instance가 없으면 대기 후 등록
        if (GameManager.Instance != null)
        {
            RegisterPlayerToGameManager(player);
        }
        else
        {
            StartCoroutine(RegisterWhenGameManagerReady(player));
        }
    }

    private void RegisterPlayerToGameManager(GameObject player)
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("❌ GameManager.Instance가 없습니다. 등록 불가");
            return;
        }

        // playerTransform 등록
        GameManager.Instance.playerTransform = player.transform;

        // PlayerHealth가 붙어있으면 Register 호출 (플레이어 헬스는 반드시 붙어있다고 하셨으므로 정상 동작)
        var ph = player.GetComponent<PlayerHealth>();
        if (ph != null)
        {
            GameManager.Instance.RegisterPlayerHealth(ph);
        }
        else
        {
            Debug.LogWarning("❌ 생성된 플레이어 오브젝트에 PlayerHealth가 없습니다. RegisterPlayerHealth를 호출하지 않습니다.");
        }
    }

    private IEnumerator RegisterWhenGameManagerReady(GameObject player)
    {
        // GameManager가 준비될 때까지 대기 (안정성 확보)
        yield return new WaitUntil(() => GameManager.Instance != null);
        RegisterPlayerToGameManager(player);
    }
}