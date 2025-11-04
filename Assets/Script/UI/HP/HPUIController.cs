using UnityEngine;
using UnityEngine.UI;

// 공통 로직: HP/Shield/Evade 값 갱신, health 타입 판별, 공통 유틸
public class HPUIControllerBase : MonoBehaviour
{
    [Header("대상 설정")]
    [Tooltip("PlayerHealth 또는 EnemyHealth를 할당하세요.")]
    public MonoBehaviour health; // PlayerHealth 또는 EnemyHealth

    [Header("UI 요소")]
    public Slider hpSlider;
    [Tooltip("실드 (선택) - Enemy 전용")] public Slider shieldSlider;

    // 회피 (플레이어 전용)
    [Tooltip("플레이어 회피 게이지 (항상 표시)")]
    public Slider evadeSlider;

    // 내부 상태
    protected bool isPlayerHealth = false;
    protected bool isEnemyHealth = false;
    protected PlayerHealth playerHP;
    protected EnemyHealth enemyHP;

    // 플레이어 무기/회피 컨트롤러 (health가 붙은 오브젝트에서 가져옵니다)
    protected PlayerWeaponController playerWeapon;

    // 초기화 여부 (health가 할당되어 내부 셋업이 끝났는지)
    protected bool initialized = false;

    // 회피 색상(파랑)
    protected static readonly Color EvadeColor = new Color32(0x2E, 0xA7, 0xFF, 0xFF);

    // 외부에서 나중에 health를 할당할 때 사용합니다.
    // (GameManager나 다른 코드가 씬에 미리 위치한 UI를 찾아서 할당할 경우 호출)
    public void Initialize(MonoBehaviour healthComponent)
    {
        if (healthComponent == null) return;
        health = healthComponent;
        SetupForHealth();
    }

    // Start 시 health가 이미 할당되어 있으면 셋업, 아니면 대기
    protected virtual void Start()
    {
        if (hpSlider == null)
        {
            Debug.LogError($"❌ {name}: hpSlider가 할당되지 않았습니다. UI 오브젝트를 확인하세요.");
            Destroy(gameObject);
            return;
        }

        if (health != null)
        {
            SetupForHealth();
        }
        else
        {
            // health가 나중에 할당될 수 있으므로 대기 상태로 남김 (지연 초기화 지원)
            initialized = false;
        }
    }

    // 값 갱신을 수행하는 공용 메서드.
    // 반환값: true이면 계속 유지(위치 갱신 등 수행 가능), false면 Destroy되었거나 더 이상 유효하지 않음.
    protected bool RefreshValues()
    {
        // hpSlider가 없으면 더 이상 의미가 없으므로 파괴
        if (hpSlider == null)
        {
            Destroy(gameObject);
            return false;
        }

        // 아직 초기화되지 않았지만 health가 나중에 할당되었다면 Setup 시도
        if (!initialized && health != null)
        {
            SetupForHealth();
        }

        // 초기화가 안 된 상태라면 값 갱신을 건너뛰지만 객체는 유지
        if (!initialized)
        {
            return true;
        }

        // 이제 안전하게 체력/실드/회피 값을 갱신
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
            // health가 잘못되었으면 파괴
            Destroy(gameObject);
            return false;
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
                evadeSlider.gameObject.SetActive(false);
            }
        }

        // 체력이 0이면 제거
        if (currentHP <= 0f)
        {
            Destroy(gameObject);
            return false;
        }

        return true;
    }

    // health 타입에 따라 내부 상태를 설정하는 공용 유틸
    protected void SetupForHealth()
    {
        if (health == null)
        {
            initialized = false;
            return;
        }

        // 기존에는 target에서 playerWeapon을 찾았으나, 이제는 health에 붙어있는 컴포넌트로 일관되게 처리합니다.
        if (health is PlayerHealth ph)
        {
            playerHP = ph;
            isPlayerHealth = true;
            isEnemyHealth = false;
            playerWeapon = health.GetComponent<PlayerWeaponController>();
        }
        else if (health is EnemyHealth eh)
        {
            enemyHP = eh;
            isEnemyHealth = true;
            isPlayerHealth = false;
            playerWeapon = null;
        }
        else
        {
            Debug.LogError($"❌ {name}: 지원하지 않는 Health 타입: {health.GetType().Name}");
            initialized = false;
            return;
        }

        // 실드바 표시는 Enemy + 실드 사용 시에만
        if (shieldSlider != null)
        {
            shieldSlider.gameObject.SetActive(isEnemyHealth && enemyHP != null && enemyHP.UseShield());
        }

        // Evade 슬라이더은 플레이어에서만 사용(없으면 자동 숨김)
        if (evadeSlider != null)
        {
            bool enableEvade = isPlayerHealth && playerWeapon != null;
            evadeSlider.gameObject.SetActive(enableEvade);

            // 색상 자동 적용(가능한 경우)
            TryStyleEvadeSlider(evadeSlider, EvadeColor);
        }

        initialized = true;
    }

    // Evade 색상 스타일 적용 유틸
    protected void TryStyleEvadeSlider(Slider slider, Color color)
    {
        if (slider == null) return;

        if (slider.fillRect != null)
        {
            var img = slider.fillRect.GetComponent<Image>();
            if (img != null)
                img.color = color;
        }
        else
        {
            var img = slider.GetComponentInChildren<Image>();
            if (img != null)
                img.color = color;
        }
    }
}