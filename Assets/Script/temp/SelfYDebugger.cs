using UnityEngine;

public class SelfYDebugger : MonoBehaviour
{
    [Header("로그 출력 주기 (초)")]
    public float interval = 1.0f;

    private float timer;

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= interval)
        {
            timer = 0f;
            Debug.Log($"📏 {gameObject.name} Y = {transform.position.y:F3}");
        }
    }
}
