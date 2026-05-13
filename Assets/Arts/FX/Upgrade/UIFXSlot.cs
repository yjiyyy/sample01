using UnityEngine;

public class UIFXSlot : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject fxObject;

    [Header("Play Settings")]
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool restartWhenPlay = true;

    private ParticleSystem[] particles;

    private void Awake()
    {
        CacheParticles();
    }

    private void OnEnable()
    {
        if (playOnEnable)
        {
            PlayFX();
        }
    }

    public void PlayFX()
    {
        if (fxObject == null)
        {
            Debug.LogWarning($"[UIFXSlot] FX Object is missing on {gameObject.name}");
            return;
        }

        fxObject.SetActive(true);

        CacheParticles();

        foreach (ParticleSystem ps in particles)
        {
            if (ps == null)
                continue;

            if (restartWhenPlay)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            ps.Play(true);
        }
    }

    public void StopFX()
    {
        CacheParticles();

        foreach (ParticleSystem ps in particles)
        {
            if (ps == null)
                continue;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void CacheParticles()
    {
        if (fxObject == null)
        {
            particles = null;
            return;
        }

        particles = fxObject.GetComponentsInChildren<ParticleSystem>(true);
    }
}