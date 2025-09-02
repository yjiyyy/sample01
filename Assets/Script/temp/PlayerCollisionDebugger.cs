using UnityEngine;

public class PlayerCollisionDebugger : MonoBehaviour
{
    void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"👤 플레이어가 충돌: {collision.gameObject.name}, 레이어: {LayerMask.LayerToName(collision.gameObject.layer)}");
    }

    void OnCollisionStay(Collision collision)
    {
        Debug.Log($"🔁 플레이어와 지속 충돌 중: {collision.gameObject.name}, 레이어: {LayerMask.LayerToName(collision.gameObject.layer)}");
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"🚪 플레이어가 트리거 진입: {other.gameObject.name}, 레이어: {LayerMask.LayerToName(other.gameObject.layer)}");
    }

    void OnTriggerStay(Collider other)
    {
        Debug.Log($"🔁 플레이어가 트리거 안에 있음: {other.gameObject.name}, 레이어: {LayerMask.LayerToName(other.gameObject.layer)}");
    }
}


