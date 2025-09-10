using UnityEngine;

public class CollisionDebugger : MonoBehaviour
{
    void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"🔴 충돌 시작: {collision.gameObject.name} (Tag: {collision.gameObject.tag})");
        LogCollisionDetails(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        if (Time.frameCount % 30 == 0) // 30프레임마다만 로그
        {
            Debug.Log($"🟡 충돌 지속: {collision.gameObject.name}");
        }
    }

    void OnCollisionExit(Collision collision)
    {
        Debug.Log($"🟢 충돌 종료: {collision.gameObject.name}");
    }

    private void LogCollisionDetails(Collision collision)
    {
        var otherRb = collision.rigidbody;
        var otherCol = collision.collider;

        Debug.Log($"   - Rigidbody: {(otherRb != null ? $"mass={otherRb.mass}, isKinematic={otherRb.isKinematic}" : "없음")}");
        Debug.Log($"   - Collider: isTrigger={otherCol.isTrigger}, bounds={otherCol.bounds.size}");
        Debug.Log($"   - Layer: {LayerMask.LayerToName(collision.gameObject.layer)}");
    }

    void Start()
    {
        // 현재 GameObject 설정 출력
        var rb = GetComponent<Rigidbody>();
        var col = GetComponent<Collider>();

        Debug.Log($"🔍 {name} 설정:");
        Debug.Log($"   - Rigidbody: mass={rb?.mass}, isKinematic={rb?.isKinematic}, useGravity={rb?.useGravity}");
        Debug.Log($"   - Collider: isTrigger={col?.isTrigger}, type={col?.GetType().Name}");
        Debug.Log($"   - Layer: {LayerMask.LayerToName(gameObject.layer)}");
        Debug.Log($"   - Tag: {gameObject.tag}");
    }
}