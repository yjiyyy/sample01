using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("�÷��̾� ����")]
    public Transform playerTransform;

    [Header("HP UI ������")]
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
            // ü�¹� ����
            playerHpUIInstance = Instantiate(playerHpUIPrefab);

            // ����
            HPUIController controller = playerHpUIInstance.GetComponent<HPUIController>();
            controller.target = playerTransform;
            controller.health = playerTransform.GetComponent<Health>();
            controller.hpSlider = playerHpUIInstance.GetComponentInChildren<Slider>();
        }
        else
        {
            Debug.LogWarning("[GameManager] playerTransform �Ǵ� playerHpUIPrefab�� �������� �ʾҽ��ϴ�.");
        }
    }
    public void SpawnPlayerHPUI(Transform player)
    {
        if (playerHpUIPrefab == null)
        {
            Debug.LogWarning("[GameManager] playerHpUIPrefab�� �������� �ʾҽ��ϴ�.");
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
