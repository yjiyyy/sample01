using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if CINEMACHINE
using Cinemachine;
#endif

[DefaultExecutionOrder(-500)]
public class CameraShakePlayer : MonoBehaviour
{
    public static CameraShakePlayer Instance { get; private set; }

    // If you have a Cinemachine Virtual Camera in scene, assign it (optional).
#if CINEMACHINE
    [Header("Cinemachine (optional)")]
    public CinemachineVirtualCamera targetVirtualCamera;
    private CinemachineBasicMultiChannelPerlin cinemachinePerlin;
#endif

    [Header("Fallback camera")]
    public Camera fallbackCamera; // if null, Camera.main will be used

    private readonly List<Coroutine> running = new List<Coroutine>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        if (fallbackCamera == null) fallbackCamera = Camera.main;
#if CINEMACHINE
        if (targetVirtualCamera != null)
        {
            cinemachinePerlin = targetVirtualCamera.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
        }
#endif
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Play a camera shake described by CameraShakeData.
    /// scale: overall multiplier (useful for boss vs small enemy).
    /// </summary>
    public void PlayShake(CameraShakeData data, float scale = 1f)
    {
        if (data == null) return;
        // If cinematic impulse usage requested, you can extend this to generate impulse.
        // For now we support Perlin-based adjustment (Cinemachine) and fallback manual transform shake.
#if CINEMACHINE
        if (data.useCinemachineImpulse && cinemachinePerlin != null)
        {
            // simple additive amplitude bump over duration using coroutine
            var c = StartCoroutine(PerlinShakeRoutine(cinemachinePerlin, data, scale));
            running.Add(c);
            return;
        }
#endif
        // If there's an available Cinemachine perlin, use it (even if not using impulse)
#if CINEMACHINE
        if (cinemachinePerlin != null)
        {
            var c = StartCoroutine(PerlinShakeRoutine(cinemachinePerlin, data, scale));
            running.Add(c);
            return;
        }
#endif
        // Fallback: transform-based shake on main camera
        var cam = fallbackCamera != null ? fallbackCamera : Camera.main;
        if (cam == null) return;
        var co = StartCoroutine(TransformShakeRoutine(cam.transform, data, scale));
        running.Add(co);
    }

#if CINEMACHINE
    private IEnumerator PerlinShakeRoutine(CinemachineBasicMultiChannelPerlin perlin, CameraShakeData data, float scale)
    {
        if (perlin == null) yield break;

        float dur = Mathf.Max(0.0001f, data.duration);
        float elapsed = 0f;
        float baseGain = perlin.m_AmplitudeGain;

        while (elapsed < dur)
        {
            float t = elapsed / dur;
            float curve = data.falloff != null ? data.falloff.Evaluate(t) : (1f - t);
            perlin.m_AmplitudeGain = baseGain + data.magnitude * curve * scale;
            elapsed += Time.deltaTime;
            yield return null;
        }

        perlin.m_AmplitudeGain = baseGain;
    }
#endif

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

            // Per-frame random offset (could be replaced with sin/fbm for smoother)
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