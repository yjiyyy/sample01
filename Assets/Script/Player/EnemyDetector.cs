using UnityEngine;
using System.Collections.Generic;

public class EnemyDetector : MonoBehaviour
{
    [Header("시야 설정")]
    [Tooltip("반각(deg). 실제 시야각은 이 값의 두 배입니다.")]
    public float viewAngle = 45f; // 반각
    public float viewDistance = 10f;
    public int segmentCount = 30;

    [Header("시각화 y 오프셋")]
    public float height = 0.8f;

    [Header("무기 상태(시각화 연동)")]
    public WeaponBehavior weaponBehavior; // 자동 주입 권장

    [Header("감지(물리 스캔)")]
    [Tooltip("물리 스캔으로 적 감지 수행")]
    public bool usePhysicsScan = true;

    public List<Transform> GetEnemiesInRange(float range)
    {
        // 단순 예제: find any transforms tagged "Enemy" within range
        List<Transform> result = new List<Transform>();
        var cols = Physics.OverlapSphere(transform.position, range);
        foreach (var c in cols)
        {
            if (c == null) continue;
            if (c.transform == transform) continue;
            if (c.CompareTag("Enemy"))
                result.Add(c.transform);
        }
        return result;
    }
}