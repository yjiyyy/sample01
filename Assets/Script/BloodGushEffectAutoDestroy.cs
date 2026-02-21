using UnityEngine;

/// <summary>
/// 이펙트 오브젝트를 지정 시간 후 자동 파괴합니다.
/// BloodGushEffect 프리팹 전용.
/// </summary>
public class BloodGushEffectAutoDestroy : MonoBehaviour
{
    public float lifetime = 3f;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }
}
