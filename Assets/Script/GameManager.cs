using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("플레이어 관련")]
    public Transform playerTransform;
    public GameObject playerHPUIPrefab; // 플레이어용 HP UI 프리팹 (UI 기반)
    [Tooltip("HUD로 사용할 Canvas (예: 가상패드가 있는 Canvas)")]
    public Canvas hudCanvas;

    [Header("플레이어 HP UI 위치 (스크린)")]
    public float playerHPLeftPadding = 10f;
    public float playerHPTopPadding = 10f;

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

    // 플레이어 HP UI 생성 메서드
    public void SpawnPlayerHPUI(Transform playerTransform)
    {
        if (playerHPUIPrefab == null)
        {
            Debug.LogError("❌ playerHPUIPrefab이 설정되지 않았습니다!");
            return;
        }

        if (hudCanvas == null)
        {
            Debug.LogError("❌ HUD용 Canvas(hudCanvas)가 연결되지 않았습니다! 가상패드 Canvas를 연결하세요.");
            return;
        }

        // hudCanvas 아래에 생성(로컬 transform 유지)
        GameObject hpui = Instantiate(playerHPUIPrefab, hudCanvas.transform, false);

        HPUIController controller = hpui.GetComponent<HPUIController>();
        if (controller == null)
        {
            Debug.LogError("❌ playerHPUIPrefab에 HPUIController 컴포넌트가 없습니다.");
            return;
        }

        // target과 health는 플레이어로 연결(슬라이더 업데이트는 Controller가 담당)
        controller.target = playerTransform;
        controller.health = playerTransform.GetComponent<PlayerHealth>();

        // 화면 고정 모드로 설정
        controller.useScreenSpaceUI = true;
        controller.leftPadding = playerHPLeftPadding;
        controller.topPadding = playerHPTopPadding;

        // RectTransform을 좌상단 앵커로 세팅(스크린 고정)
        RectTransform rt = hpui.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(playerHPLeftPadding, -playerHPTopPadding);
        }

        // CanvasGroup으로 터치 이벤트 차단 방지
        CanvasGroup cg = hpui.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = hpui.AddComponent<CanvasGroup>();
        }
        cg.blocksRaycasts = false; // HUD가 입력(가상패드)을 가로막지 않도록

        // SafeArea 보정용 컴포넌트 추가(없으면 추가)
        var safe = hpui.GetComponent<SafeAreaFitter>();
        if (safe == null)
        {
            hpui.AddComponent<SafeAreaFitter>();
        }

        // 이름 기반 슬라이더 자동 매핑 (기존 로직 유지)
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
            foreach (var s in sliders)
            {
                if (s != hpFound) { evadeFound = s; break; }
            }
        }

        controller.hpSlider = hpFound;
        controller.evadeSlider = evadeFound;
        controller.shieldSlider = shieldFound; // 플레이어에선 숨김(Controller에서 처리)

        if (controller.hpSlider == null)
        {
            Debug.LogError("❌ 플레이어 HP UI에서 HP 슬라이더를 찾지 못했습니다. 프리팹에 'HP' 이름을 포함한 Slider를 추가해주세요.");
        }
    }
}