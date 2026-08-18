using UnityEngine;

// 캐릭터를 따라다니는 월드 HP UI
public class WorldHPUIController : HPUIControllerBase
{
    [Header("월드 위치")]
    public Transform target;
    public Vector3 offset = new Vector3(0f, 2f, 0f);

    protected override void Start()
    {
        base.Start();
        if (target == null)
            Debug.LogWarning($"{name}: target이 지정되지 않았습니다. 월드 위치 추적이 동작하지 않습니다.");
    }

    void LateUpdate()
    {
        if (!RefreshValues())
            return;

        Transform t = target;
        if (t == null && health != null)
            t = health.transform;

        if (t != null)
        {
            transform.position = t.position + offset;
            if (Camera.main != null)
                transform.forward = Camera.main.transform.forward;
        }
    }
}
