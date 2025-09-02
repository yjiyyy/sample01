using UnityEngine;

/// <summary>
/// 특정 순간(예: 넉백)에만 일시적으로 물리 기반 흔들림(Rigidbody)을 활성화하는 컨트롤러
/// </summary>
public class JiggleBoneActivator : MonoBehaviour
{
    [Header("지글본 대상")]
    public Rigidbody targetRigidbody;

    [Tooltip("활성화 유지 시간 (초)")]
    public float duration = 0.5f;

    private float timer = 0f;
    private bool isActive = false;

    void Update()
    {
        if (!isActive) return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            targetRigidbody.isKinematic = true;
            isActive = false;
        }
    }

    /// <summary>
    /// 외부에서 호출 시 지글본 활성화 시작
    /// </summary>
    public void ActivateJiggle()
    {
        if (targetRigidbody == null)
        {
            Debug.LogWarning($"⚠ {name}: targetRigidbody가 연결되지 않았습니다.");
            return;
        }

        targetRigidbody.isKinematic = false;
        timer = duration;
        isActive = true;

        // ✅ 메시지 출력
        Debug.Log($"✅ 지글본 활성화됨: {targetRigidbody.name} (지속 시간: {duration:F2}s)", this);
    }
}
