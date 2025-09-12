using UnityEngine;

public class SimpleLogTester : MonoBehaviour
{
    [Header("로그 출력 주기 (초)")]
    public float interval = 0.5f;

    private float timer;

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= interval)
        {
            timer = 0f;
            Debug.Log($"[SimpleLogTester] 오브젝트:{gameObject.name} | 위치:{transform.position} | 상태:{enabled}");
        }
    }
}