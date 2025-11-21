using UnityEngine;

/// <summary>
/// 씬에 배치되어 플레이어가 자신의 영역에 들어왔는지 감지하고 로그를 출력하는 디버깅용 스크립트입니다.
/// </summary>
public class PlayerDetector : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // GameManager의 isDebugMode가 false이면 로그를 출력하지 않고 즉시 종료합니다.
        if (GameManager.Instance == null || !GameManager.Instance.isDebugMode)
        {
            return;
        }

        // 플레이어 태그를 가진 객체와 충돌했는지 확인합니다.
        if (other.CompareTag("Player"))
        {
            Debug.Log($"<color=cyan>✅ [PlayerDetector] 플레이어 감지 성공!</color> 감지된 객체: {other.name}");
        }
        else
        {
            // 플레이어가 아닌 다른 객체와 충돌했을 때도 로그를 남겨서, 충돌 이벤트 자체는 발생하는지 확인합니다.
            Debug.Log($"<color=yellow>⚠️ [PlayerDetector] 다른 객체 감지:</color> {other.name} (Tag: {other.tag}, Layer: {LayerMask.LayerToName(other.gameObject.layer)})");
        }
    }

    private void Start()
    {
        // 이 오브젝트의 설정이 올바른지 시작 시점에 확인합니다.
        Collider col = GetComponent<Collider>();
        if (col == null || !col.isTrigger)
        {
            Debug.LogError($"[PlayerDetector] 오류: 이 오브젝트({name})에 'Is Trigger'가 활성화된 Collider가 없습니다!");
        }

        if (GetComponent<Rigidbody>() == null)
        {
            Debug.LogError($"[PlayerDetector] 오류: 이 오브젝트({name})에 Rigidbody 컴포넌트가 없습니다! OnTriggerEnter가 안정적으로 동작하려면 Rigidbody가 필요합니다.");
        }
        
        // GameManager의 isDebugMode가 true일 때만 시작 로그를 출력합니다.
        if (GameManager.Instance != null && GameManager.Instance.isDebugMode)
        {
            Debug.Log($"[PlayerDetector] '{name}'가 플레이어 감지를 시작합니다. Layer: {LayerMask.LayerToName(gameObject.layer)}");
        }
    }
}