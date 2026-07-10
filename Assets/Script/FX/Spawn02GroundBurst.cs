using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 스폰 FX 프리팹 루트.
/// - GroundShard_* 자식을 지면 아래에서 올려 깨진 지면 형태를 유지합니다.
/// - Dust 파티클은 보조 연출로 사용합니다.
/// </summary>
[DisallowMultipleComponent]
public class Spawn02GroundBurst : MonoBehaviour
{
    [Header("Lifetime")]
    public float lifetime = 2f;
    [Header("Ground Shards")]
    public float emergeDuration = 0.2f;
    public float startBuriedY = -0.32f;

    private readonly List<ShardPose> shardPoses = new List<ShardPose>(16);

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        CacheShardTargetsAndReset();
        StartCoroutine(EmergeRoutine());
        Destroy(gameObject, Mathf.Max(0.5f, lifetime));
    }

    private void CacheShardTargetsAndReset()
    {
        shardPoses.Clear();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (!child.name.StartsWith("GroundShard_"))
                continue;

            Vector3 targetPos = child.localPosition;
            Quaternion targetRot = child.localRotation;
            Vector3 targetScale = child.localScale;

            Vector3 startPos = new Vector3(targetPos.x, startBuriedY, targetPos.z);
            Vector3 startScale = targetScale;

            child.localPosition = startPos;
            child.localRotation = targetRot;
            child.localScale = startScale;

            shardPoses.Add(new ShardPose
            {
                tr = child,
                startPos = startPos,
                targetPos = targetPos,
                startRot = child.localRotation,
                targetRot = targetRot,
                startScale = startScale,
                targetScale = targetScale
            });
        }
    }

    private IEnumerator EmergeRoutine()
    {
        float duration = Mathf.Max(0.05f, emergeDuration);
        float end = Time.time + duration;
        while (Time.time < end)
        {
            float t = 1f - ((end - Time.time) / duration);
            t = t * t * (3f - 2f * t); // smoothstep
            ApplyShardPose(t);
            yield return null;
        }

        ApplyShardPose(1f);
    }

    private void ApplyShardPose(float t)
    {
        for (int i = 0; i < shardPoses.Count; i++)
        {
            ShardPose s = shardPoses[i];
            if (s.tr == null)
                continue;

            s.tr.localPosition = Vector3.LerpUnclamped(s.startPos, s.targetPos, t);
            s.tr.localRotation = s.targetRot;
            s.tr.localScale = Vector3.LerpUnclamped(s.startScale, s.targetScale, t);
        }
    }

    private struct ShardPose
    {
        public Transform tr;
        public Vector3 startPos;
        public Vector3 targetPos;
        public Quaternion startRot;
        public Quaternion targetRot;
        public Vector3 startScale;
        public Vector3 targetScale;
    }
}
