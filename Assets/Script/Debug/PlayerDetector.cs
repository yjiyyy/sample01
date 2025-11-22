using UnityEngine;

/// <summary>
/// 플레이어가 트리거 범위에 들어왔는지 감지. 디버그 모드일 때 감지 관련 로그 출력.
/// 설정 오류(LogError)는 디버그 여부와 관계없이 항상 출력.
/// </summary>
public class PlayerDetector : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // 디버그 모드가 아니면 감지 로그를 남기지 않고 종료
        if (GameManager.Instance == null || !GameManager.Instance.isDebugMode)
        {
            return;
        }

        if (other.CompareTag("Player"))
        {
            Debug.Log($"<color=cyan>✅ [PlayerDetector] 플레이어 감지!</color> 객체: {other.name}");
        }
        else
        {
            Debug.Log($"<color=yellow>⚠️ [PlayerDetector] 다른 객체 감지:</color> {other.name} (Tag: {other.tag}, Layer: {LayerMask.LayerToName(other.gameObject.layer)})");
        }
    }

    private void Start()
    {
        // 설정 오류는 항상 출력
        Collider col = GetComponent<Collider>();
        if (col == null || !col.isTrigger)
        {
            Debug.LogError($"[PlayerDetector] 오류: '{name}'에 IsTrigger 활성 Collider가 필요합니다.");
        }

        var rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError($"[PlayerDetector] 오류: '{name}'에 Rigidbody가 없습니다. (트리거 안정성과 예측 가능한 물리 이벤트를 위해 Kinematic 권장)");
        }

        // 시작 정보 로그는 디버그 모드일 때만
        if (GameManager.Instance != null && GameManager.Instance.isDebugMode)
        {
            Debug.Log($"[PlayerDetector] '{name}' 감지 시작. Layer: {LayerMask.LayerToName(gameObject.layer)}");
        }
    }
}