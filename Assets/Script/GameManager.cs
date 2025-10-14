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

            // 이름 기반 우선 매핑: "hp", "evade", "shield"
            Slider[] sliders = hpui.GetComponentsInChildren<Slider>(true);
            Slider hpFound = null, evadeFound = null, shieldFound = null;

            foreach (Slider s in sliders)
            {
                string n = s.name.ToLower();
                if (n.Contains("shield")) shieldFound = s;
                else if (n.Contains("evade")) evadeFound = s;
                else if (n.Contains("hp")) hpFound = s;
            }

            // 폴백: 이름이 애매할 경우 첫 번째를 HP, 두 번째를 Evade로 할당
            if (hpFound == null && sliders.Length >= 1) hpFound = sliders[0];
            if (evadeFound == null && sliders.Length >= 2)
            {
                // HP로 잡힌 것과 다른 슬라이더를 Evade로
                foreach (var s in sliders)
                {
                    if (s != hpFound) { evadeFound = s; break; }
                }
            }

            controller.hpSlider = hpFound;
            controller.evadeSlider = evadeFound;
            controller.shieldSlider = shieldFound; // 플레이어에선 숨겨짐(Controller에서 처리)

            if (controller.hpSlider == null)
            {
                Debug.LogError("❌ 플레이어 HP UI에서 HP 슬라이더를 찾지 못했습니다. 프리팹에 'HP' 이름을 포함한 Slider를 추가해주세요.");
            }
        }
        else
        {
            Debug.LogError("❌ playerHPUIPrefab이 GameManager에 연결되지 않았습니다!");
        }
    }
}