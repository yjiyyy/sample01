using UnityEngine;

public class MovementTracer : MonoBehaviour
{
    private Vector3 lastPos;

    void Start()
    {
        lastPos = transform.position;
    }

    void LateUpdate()
    {
        Vector3 delta = transform.position - lastPos;
        if (delta.magnitude > 0.005f)
        {
            Debug.LogWarning($"🚨 이동 감지! Δpos={delta} | pos={transform.position} | parent={transform.parent?.name}");
        }
        lastPos = transform.position;
    }
}
