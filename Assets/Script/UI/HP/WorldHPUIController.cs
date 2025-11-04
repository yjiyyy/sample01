using UnityEngine;

// 월드(캐릭터)를 따라다니는 HP UI
// 사용법: target을 캐릭터 Transform으로 설정, health에는 해당 캐릭터의 EnemyHealth/PlayerHealth 할당
public class WorldHPUIController : HPUIControllerBase
{
    [Header("월드 위치")]
    public Transform target;
    public Vector3 offset = new Vector3(0f, 2f, 0f);

    protected override void Start()
    {
        base.Start();
        // target이 없으면 경고 (하지만 Destroy 판정은 RefreshValues에서 처리)
        if (target == null)
        {
            Debug.LogWarning($"{name}: target이 설정되지 않았습니다. 월드 위치 동기화가 작동하지 않습니다.");
        }
    }

    void LateUpdate()
    {
        // 먼저 값 갱신. 유효하지 않으면 더 이상 동작하지 않음.
        if (!RefreshValues())
            return;

        // 월드 위치 추적: target 기준 (target이 없으면 health의 transform을 시도)
        Transform t = target;
        if (t == null && health != null)
        {
            // fallback: health가 붙은 오브젝트의 transform을 사용
            t = health.transform;
        }

        if (t != null)
        {
            transform.position = t.position + offset;
            if (Camera.main != null)
                transform.forward = Camera.main.transform.forward;
        }
    }
}