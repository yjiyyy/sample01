using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-500)]
public class CameraShakePlayer : MonoBehaviour
{
    public static CameraShakePlayer Instance { get; private set; }

    [Header("Camera")]
    public Camera fallbackCamera;

    private readonly List<Coroutine> running = new List<Coroutine>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        if (fallbackCamera == null) fallbackCamera = Camera.main;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void PlayShake(CameraShakeData data, float scale = 1f)
    {
        if (data == null) return;

        var cam = fallbackCamera != null ? fallbackCamera : Camera.main;
        if (cam == null) return;
        var co = StartCoroutine(TransformShakeRoutine(cam.transform, data, scale));
        running.Add(co);
    }

    private IEnumerator TransformShakeRoutine(Transform camTr, CameraShakeData data, float scale)
    {
        Vector3 originalPos = camTr.localPosition;
        float dur = Mathf.Max(0.0001f, data.duration);
        float elapsed = 0f;

        while (elapsed < dur)
        {
            float t = elapsed / dur;
            float curve = data.falloff != null ? data.falloff.Evaluate(t) : (1f - t);
            float mag = data.magnitude * curve * scale;

            Vector3 offset = new Vector3(
                (Random.value * 2f - 1f),
                (Random.value * 2f - 1f),
                (Random.value * 2f - 1f)
            ) * 0.5f * mag;

            camTr.localPosition = originalPos + offset;

            elapsed += Time.deltaTime;
            yield return null;
        }

        camTr.localPosition = originalPos;
    }
}
