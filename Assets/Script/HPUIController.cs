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

    // 🆕 회피 (플레이어 전용)
    [Tooltip("플레이어 회피 게이지 (항상 표시)")]
    public Slider evadeSlider;

    public Vector3 offset = new Vector3(0, 2f, 0);

    private bool isPlayerHealth = false;
    private bool isEnemyHealth = false;
    private PlayerHealth playerHP;
    private EnemyHealth enemyHP;

    // 🆕 플레이어 무기/회피 컨트롤러
    private PlayerWeaponController playerWeapon;

    // 🆕 회피 색상(파랑)
    private static readonly Color EvadeColor = new Color32(0x2E, 0xA7, 0xFF, 0xFF);

    void Start()
    {
        if (health is PlayerHealth ph)
        {
            playerHP = ph;
            isPlayerHealth = true;
            // 플레이어 컨트롤러 캐싱
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

        // 🆕 Evade 슬라이더는 플레이어에서만 사용, 항상 표기(없으면 자동 숨김)
        if (evadeSlider != null)
        {
            bool enableEvade = isPlayerHealth && playerWeapon != null;
            evadeSlider.gameObject.SetActive(enableEvade);

            // 색상 자동 적용(가능한 경우)
            TryStyleEvadeSlider(evadeSlider, EvadeColor);
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

        // 🆕 회피 게이지 갱신 (플레이어 전용, 항상 표기)
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

        transform.position = target.position + offset;
        if (Camera.main != null)
            transform.forward = Camera.main.transform.forward;

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