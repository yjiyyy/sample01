using UnityEngine;

public class SpringBone : MonoBehaviour
{
    [Header("Tip 자식 본")]
    public Transform child;

    [Header("본 방향 (local axis 기준)")]
    public Vector3 boneAxis = new Vector3(-1, 0, 0);

    [Header("힘 설정")]
    public float stiffness = 0.01f;
    public float drag = 0.4f;
    public Vector3 externalForce = new Vector3(0, -0.0001f, 0);

    [Header("회전 혼합 비율")]
    [Range(0f, 1f)] public float blend = 1f;

    private float boneLength;
    private Quaternion initialLocalRotation;
    private Transform trs;
    private Vector3 prevTipPos, currTipPos;

    private void Awake()
    {
        trs = transform;
        initialLocalRotation = trs.localRotation;

        if (child == null)
        {
            Debug.LogWarning($"⚠ {name}에 child가 연결되지 않았습니다.");
            enabled = false;
            return;
        }

        boneLength = Vector3.Distance(trs.position, child.position);
        prevTipPos = currTipPos = child.position;
    }

    private void LateUpdate()
    {
        float sqrDt = Time.deltaTime * Time.deltaTime;
        if (sqrDt <= 0f) return;

        trs.localRotation = initialLocalRotation;

        // 힘 계산
        Vector3 force = trs.rotation * (boneAxis * stiffness) / sqrDt;
        force += (prevTipPos - currTipPos) * drag / sqrDt;
        force += externalForce / sqrDt;

        // Verlet 방식
        Vector3 temp = currTipPos;
        currTipPos = (currTipPos - prevTipPos) + currTipPos + (force * sqrDt);
        currTipPos = ((currTipPos - trs.position).normalized * boneLength) + trs.position;
        prevTipPos = temp;

        // 회전 적용
        Vector3 aimVector = trs.TransformDirection(boneAxis);
        Quaternion aimRot = Quaternion.FromToRotation(aimVector, currTipPos - trs.position);
        trs.rotation = Quaternion.Lerp(trs.rotation, aimRot * trs.rotation, blend);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (child == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, child.position);
        Gizmos.DrawWireSphere(child.position, 0.02f);
    }
#endif
}
