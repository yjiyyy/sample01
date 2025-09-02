using UnityEngine;

public class PlayerContactDebugger : MonoBehaviour
{
    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            Debug.LogError("❗ Rigidbody가 없습니다. 이 스크립트는 플레이어의 Rigidbody에 붙여야 합니다.");
    }

    public class PlayerCollisionDebugger : MonoBehaviour
    {
        void OnCollisionStay(Collision collision)
        {
            Debug.Log($"📌 [Player] 충돌 중: {collision.gameObject.name}, Layer: {LayerMask.LayerToName(collision.gameObject.layer)}");
        }
    }
}
