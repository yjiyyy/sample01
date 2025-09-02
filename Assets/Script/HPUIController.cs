using UnityEngine;
using UnityEngine.UI;

public class HPUIController : MonoBehaviour
{
    [Header("대상 설정")]
    public Transform target;
    public Health health;

    [Header("UI 요소")]
    public Slider hpSlider;
    public Vector3 offset = new Vector3(0, 2f, 0);

    void LateUpdate()
    {
        if (target == null || health == null || hpSlider == null)
        {
            Destroy(gameObject);  // 대상이 사라지면 UI도 제거
            return;
        }

        float ratio = health.GetCurrentHP() / health.GetMaxHP();
        hpSlider.value = ratio;

        transform.position = target.position + offset;

        // ✅ 기존 방식: 카메라 방향 따라가기
        transform.forward = Camera.main.transform.forward;

        if (health.GetCurrentHP() <= 0)
        {
            Destroy(gameObject);  // 체력 0이면 UI 제거
        }
    }
}
