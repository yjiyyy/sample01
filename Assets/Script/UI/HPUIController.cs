using UnityEngine;
using UnityEngine.UI;

public class HPUIController : MonoBehaviour
{
    [Header("대상 설정")]
    public Transform target;
    public MonoBehaviour health; // PlayerHealth 또는 EnemyHealth

    [Header("UI 요소")]
    public Slider hpSlider;
    [Tooltip("실드 (선택) - Enemy 전용")] public Slider shieldSlider;

    // 회피 (플레이어 전용)
    [Tooltip("플레이어 회피 게이지 (항상 표시)")]
    public Slider evadeSlider;

    [Header("위치/모드")]
    public Vector3 offset = new Vector3(0, 2f, 0);

    [Tooltip("스크린 캔버스에 고정해서 사용할지 여부 (플레이어 전용)")]
    public bool useScreenSpaceUI = false;

    [Tooltip("스크린 모드에서의 좌/상 패딩(px)")]
    public float leftPadding = 10f;
    public float topPadding = 10f;

    private bool isPlayerHealth = false;
    private bool isEnemyHealth = false;
    private PlayerHealth playerHP;
    private EnemyHealth enemyHP;

    // 플레이어 무기/회피 컨트롤러
    private PlayerWeaponController playerWeapon;

    // 회피 색상(파랑)
    private static readonly Color EvadeColor = new Color32(0x2E, 0xA7, 0xFF, 0xFF);

    void Start()
    {
        if (health is PlayerHealth ph)
        {
            playerHP = ph;
            isPlayerHealth = true;
            if (target != null)
                playerWeapon = target.GetComponent<PlayerWeaponController>();
        }
        else if (health is EnemyHealth eh)
        {
            enemyHP = eh;
            isEnemyHealth = true;
        }
        else
        {
            Debug.LogError($"❌ {health?.name}은 지원하지 않는 Health 타입입니다!");
        }

        // 실드바 표시는 Enemy + 실드 사용 시에만
        if (shieldSlider != null)
        {
            shieldSlider.gameObject.SetActive(isEnemyHealth && enemyHP != null && enemyHP.UseShield());
        }

        // Evade 슬라이더는 플레이어에서만 사용, 항상 표기(없으면 자동 숨김)
        if (evadeSlider != null)
        {
            bool enableEvade = isPlayerHealth && playerWeapon != null;
            evadeSlider.gameObject.SetActive(enableEvade);

            // 색상 자동 적용(가능한 경우)
            TryStyleEvadeSlider(evadeSlider, EvadeColor);
        }

        // 스크린 모드일 경우 RectTransform 기본 위치 세팅(게임매니저에서 추가로 덮어쓸 수 있음)
        if (useScreenSpaceUI)
        {
            RectTransform rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(leftPadding, -topPadding);
            }
        }
    }

    void LateUpdate()
    {
        if (target == null || health == null || hpSlider == null)
        {
            Destroy(gameObject);
            return;
        }

        float currentHP = 0f;
        float maxHP = 1f;

        if (isPlayerHealth && playerHP != null)
        {
            currentHP = playerHP.GetCurrentHP();
            maxHP = playerHP.GetMaxHP();
        }
        else if (isEnemyHealth && enemyHP != null)
        {
            currentHP = enemyHP.GetCurrentHP();
            maxHP = enemyHP.GetMaxHP();
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        hpSlider.value = maxHP > 0f ? currentHP / maxHP : 0f;

        if (shieldSlider != null && isEnemyHealth && enemyHP != null && enemyHP.UseShield())
        {
            shieldSlider.gameObject.SetActive(true);
            float shMax = enemyHP.GetMaxShield();
            float shCur = enemyHP.GetCurrentShield();
            shieldSlider.value = shMax > 0f ? shCur / shMax : 0f;
        }
        else if (shieldSlider != null)
        {
            shieldSlider.gameObject.SetActive(false);
        }

        // 회피 게이지 갱신 (플레이어 전용, 항상 표기)
        if (evadeSlider != null)
        {
            if (isPlayerHealth && playerWeapon != null)
            {
                float eCur = playerWeapon.GetEvadeGauge();
                float eMax = playerWeapon.GetMaxEvadeGauge();
                evadeSlider.gameObject.SetActive(true);
                evadeSlider.value = eMax > 0f ? eCur / eMax : 0f;
            }
            else
            {
                // 플레이어가 아니면 숨김
                evadeSlider.gameObject.SetActive(false);
            }
        }

        // 화면 고정 모드이면 위치 갱신(월드 위치 갱신은 건너뜀)
        if (!useScreenSpaceUI)
        {
            transform.position = target.position + offset;
            if (Camera.main != null)
                transform.forward = Camera.main.transform.forward;
        }
        else
        {
            // 스크린 모드일 경우 추가로 RectTransform이 없으면 무시. (위치는 GameManager에서 세팅)
            // 만약 게임 도중 padding 변경을 원하면 아래 주석을 해제하고 leftPadding/topPadding을 업데이트하세요.
            /*
            RectTransform rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(leftPadding, -topPadding);
            }
            */
        }

        if (currentHP <= 0)
        {
            Destroy(gameObject);
        }
    }

    private void TryStyleEvadeSlider(Slider slider, Color color)
    {
        if (slider == null) return;

        // Slider의 Fill 영역 색상 지정
        if (slider.fillRect != null)
        {
            var img = slider.fillRect.GetComponent<Image>();
            if (img != null)
                img.color = color;
        }
        else
        {
            // 폴백: 자식 중 첫 Image를 찾아 변경(HP와 충돌 방지 위해 fillRect 우선)
            var img = slider.GetComponentInChildren<Image>();
            if (img != null)
                img.color = color;
        }
    }
}