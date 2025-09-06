using UnityEngine;
using UnityEngine.UI; // ✅ 이 줄을 추가!

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("플레이어 관련")]
    public Transform playerTransform;
    public GameObject playerHPUIPrefab; // ✅ 누락된 필드도 추가

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ✅ 플레이어 HP UI 생성 메서드
    public void SpawnPlayerHPUI(Transform playerTransform)
    {
        if (playerHPUIPrefab != null)
        {
            GameObject hpui = Instantiate(playerHPUIPrefab);
            HPUIController controller = hpui.GetComponent<HPUIController>();
            controller.target = playerTransform;
            controller.health = playerTransform.GetComponent<PlayerHealth>();
            controller.hpSlider = hpui.GetComponentInChildren<Slider>();
        }
        else
        {
            Debug.LogError("❌ playerHPUIPrefab이 GameManager에 연결되지 않았습니다!");
        }
    }
}