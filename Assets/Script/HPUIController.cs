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
    public Vector3 offset = new Vector3(0, 2f, 0);

    private bool isPlayerHealth = false;
    private bool isEnemyHealth = false;
    private PlayerHealth playerHP;
    private EnemyHealth enemyHP;

    void Start()
    {
        if (health is PlayerHealth ph)
        {
            playerHP = ph;
            isPlayerHealth = true;
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

        if (shieldSlider != null)
        {
            // 플레이어는 현재 실드 미지원이므로 숨김
            shieldSlider.gameObject.SetActive(isEnemyHealth && enemyHP != null && enemyHP.UseShield());
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

        transform.position = target.position + offset;
        if (Camera.main != null)
            transform.forward = Camera.main.transform.forward;

        if (currentHP <= 0)
        {
            Destroy(gameObject);
        }
    }
}