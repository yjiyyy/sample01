using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class JerkBone
{
    public Transform bone;
    public Vector3 jerkEulerAngles = new Vector3(20, 0, 0);  // 최대 회전 각도
}

public class MultiBoneJerkController : MonoBehaviour
{
    [Header("랜덤 튕김 대상 본 리스트")]
    public List<JerkBone> jerkBones = new();

    private class JerkRuntime
    {
        public Transform bone;
        public Vector3 jerkEulerAngles;
        public float duration;
        public float timer;
    }

    private JerkRuntime currentBone = null;
    private bool isJerkActive = false;

    /// <summary>
    /// 무기 데이터에서 지정한 세기와 지속 시간으로 튕김
    /// </summary>
    public void TriggerJerk(float intensity, float duration)
    {
        if (jerkBones.Count == 0 || intensity <= 0f) return;

        int idx = Random.Range(0, jerkBones.Count);
        var bone = jerkBones[idx];

        currentBone = new JerkRuntime
        {
            bone = bone.bone,
            jerkEulerAngles = bone.jerkEulerAngles * intensity,
            duration = duration,
            timer = 0f
        };

        isJerkActive = true;
    }

    private void Update()
    {
        if (!isJerkActive || currentBone == null || currentBone.bone == null)
            return;

        currentBone.timer += Time.deltaTime;
        float t = currentBone.timer / currentBone.duration;

        if (t >= 1f)
        {
            isJerkActive = false;
            currentBone = null;
            return;
        }

        // Additive 보간 방식: 현재 회전에 매 프레임 덧씌우기
        Quaternion jerkRot = Quaternion.Euler(currentBone.jerkEulerAngles * (1f - t) * Time.deltaTime * 60f);
        currentBone.bone.localRotation *= jerkRot;
    }

    public void StopJerk()
    {
        isJerkActive = false;
        currentBone = null;
    }
}
