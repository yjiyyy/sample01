using UnityEngine;
using UnityEngine.UI;

public class HPUIController : MonoBehaviour
{
    [Header("대상 설정")]
    public Transform target;
    public MonoBehaviour health; // 🔧 Health → MonoBehaviour로 변경

    [Header("UI 요소")]
    public Slider hpSlider;
    public Vector3 offset = new Vector3(0, 2f, 0);

    // 런타임에서 실제 타입 확인
    private bool isPlayerHealth = false;
    private bool isEnemyHealth = false;
    private PlayerHealth playerHP;
    private EnemyHealth enemyHP;

    void Start()
    {
        // 실제 타입 확인 및 캐싱
        if (health is PlayerHealth ph)
        {
            playerHP = ph;
            isPlayerHealth = true;
            Debug.Log("[HPUIController] PlayerHealth 감지됨");
        }
        else if (health is EnemyHealth eh)
        {
            enemyHP = eh;
            isEnemyHealth = true;
            Debug.Log("[HPUIController] EnemyHealth 감지됨");
        }
        else
        {
            Debug.LogError($"❌ {health?.name}은 지원하지 않는 Health 타입입니다!");
        }
    }

    void LateUpdate()
    {
        if (target == null || health == null || hpSlider == null)
        {
            Destroy(gameObject);
            return;
        }

        // 타입별로 메서드 호출
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

        float ratio = currentHP / maxHP;
        hpSlider.value = ratio;

        transform.position = target.position + offset;
        transform.forward = Camera.main.transform.forward;

        if (currentHP <= 0)
        {
            Destroy(gameObject);
        }
    }
}