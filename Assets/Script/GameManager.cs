using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("플레이어 설정")]
    public Transform playerTransform;

    [Header("HP UI 프리팹")]
    public GameObject playerHpUIPrefab;

    private GameObject playerHpUIInstance;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (playerTransform != null && playerHpUIPrefab != null)
        {
            // 체력바 생성
            playerHpUIInstance = Instantiate(playerHpUIPrefab);

            // 연결
            HPUIController controller = playerHpUIInstance.GetComponent<HPUIController>();
            controller.target = playerTransform;
            controller.health = playerTransform.GetComponent<Health>();
            controller.hpSlider = playerHpUIInstance.GetComponentInChildren<Slider>();
        }
        else
        {
            Debug.LogWarning("[GameManager] playerTransform 또는 playerHpUIPrefab이 설정되지 않았습니다.");
        }
    }
    public void SpawnPlayerHPUI(Transform player)
    {
        if (playerHpUIPrefab == null)
        {
            Debug.LogWarning("[GameManager] playerHpUIPrefab이 설정되지 않았습니다.");
            return;
        }

        playerTransform = player;

        GameObject hpui = Instantiate(playerHpUIPrefab);
        var controller = hpui.GetComponent<HPUIController>();
        controller.target = player;
        controller.health = player.GetComponent<Health>();
        controller.hpSlider = hpui.GetComponentInChildren<Slider>();
    }

}
