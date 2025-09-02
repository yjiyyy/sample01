using UnityEngine;

public class HeadJerkToBoneController : MonoBehaviour
{
    [Header("회전 대상 본")]
    public Transform headBone;

    [Header("튕기는 회전")]
    public Vector3 jerkEulerAngles = new Vector3(20, 0, 0);

    [Header("튕기기 지속 시간")]
    public float jerkDuration = 0.2f;

    private Quaternion originalRotation;
    private Quaternion targetRotation;
    private float jerkTimer = 0f;
    private bool jerkActive = false;

    void Start()
    {
        if (headBone == null)
        {
            Debug.LogWarning("⚠ HeadJerkToBoneController: headBone이 지정되지 않았습니다.");
            enabled = false;
            return;
        }

        originalRotation = headBone.localRotation;
    }

    void LateUpdate()
    {
        if (!jerkActive) return;

        jerkTimer += Time.deltaTime;

        // 시간 비율 계산 (0~1)
        float t = jerkTimer / jerkDuration;

        if (t >= 1f)
        {
            // 종료
            headBone.localRotation = originalRotation;
            jerkActive = false;
            return;
        }

        // 회전 보간
        Quaternion blended = Quaternion.Slerp(targetRotation, originalRotation, t);
        headBone.localRotation = blended;
    }

    public void TriggerJerk()
    {
        if (headBone == null) return;

        originalRotation = headBone.localRotation;
        targetRotation = originalRotation * Quaternion.Euler(jerkEulerAngles);

        jerkTimer = 0f;
        jerkActive = true;

        Debug.Log("💥 머리 튕김 효과 TriggerJerk 실행");
    }
}
