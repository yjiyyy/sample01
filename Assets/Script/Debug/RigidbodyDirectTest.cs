using UnityEngine;
public class RigidbodyDirectTest : MonoBehaviour
{
    void Start()
    {
        var rb = GetComponent<Rigidbody>();
        if (!rb)
        {
            Debug.LogError("[RigidbodyDirectTest] Rigidbody ����");
            enabled = false;
            return;
        }

        Debug.Log($"[RigidbodyDirectTest] pre: vel={rb.linearVelocity}, kinematic={rb.isKinematic}, gravity={rb.useGravity}");
        rb.AddForce(Vector3.down * 10f, ForceMode.Impulse);
        Debug.Log($"[RigidbodyDirectTest] postAddForce: vel={rb.linearVelocity}");
        rb.isKinematic = true;
        rb.isKinematic = false;
        Debug.Log($"[RigidbodyDirectTest] afterToggle: vel={rb.linearVelocity}, kinematic={rb.isKinematic}");
        enabled = false; // 1ȸ ���� �� �ڵ� ����
    }
}