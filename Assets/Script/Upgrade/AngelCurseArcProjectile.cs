using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class AngelCurseArcProjectile : MonoBehaviour
{
    private Rigidbody rb;
    private Upgrade_05_03_AngelCurse config;
    private bool initialized;
    private bool landed;
    private float lived;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void Initialize(Upgrade_05_03_AngelCurse sourceConfig, Vector3 spawnPos, Vector3 targetWorld)
    {
        config = sourceConfig;
        initialized = config != null && rb != null;
        landed = false;
        lived = 0f;

        if (!initialized)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = spawnPos;
        SetupBody();

        if (!TryBuildArcVelocity(spawnPos, targetWorld, out Vector3 initialVelocity))
        {
            Vector3 forward = transform.forward.sqrMagnitude > 0.0001f ? transform.forward : Vector3.forward;
            initialVelocity = forward.normalized * Mathf.Max(0.1f, config.projectileSpeed);
        }

        rb.linearVelocity = initialVelocity;
    }

    private void SetupBody()
    {
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private bool TryBuildArcVelocity(Vector3 startPos, Vector3 targetPos, out Vector3 velocity)
    {
        velocity = Vector3.zero;

        Vector3 to = targetPos - startPos;
        Vector3 toXZ = new Vector3(to.x, 0f, to.z);
        float distXZ = toXZ.magnitude;
        if (distXZ <= 0.001f)
            return false;

        float g = Mathf.Abs(Physics.gravity.y);
        if (g <= 0.001f)
            return false;

        float apexY = Mathf.Max(startPos.y, targetPos.y) + Mathf.Max(0f, config.arcHeight);
        float upHeight = Mathf.Max(0.01f, apexY - startPos.y);
        float downHeight = Mathf.Max(0.01f, apexY - targetPos.y);

        float vy = Mathf.Sqrt(2f * g * upHeight);
        float tUp = vy / g;
        float tDown = Mathf.Sqrt(2f * downHeight / g);
        float totalTime = Mathf.Max(0.1f, tUp + tDown);

        Vector3 vXZ = toXZ / totalTime;
        float maxHorizSpeed = Mathf.Max(0.1f, config.projectileSpeed);
        if (vXZ.magnitude > maxHorizSpeed)
            vXZ = vXZ.normalized * maxHorizSpeed;

        velocity = vXZ + Vector3.up * vy;
        return true;
    }

    private void Update()
    {
        if (!initialized || landed || config == null)
            return;

        AlignForwardToVelocity();

        lived += Time.deltaTime;
        if (lived >= Mathf.Max(0.2f, config.projectileMaxLifetime))
        {
            SpawnPoisonFieldAt(transform.position, Vector3.up);
            landed = true;
            Destroy(gameObject);
        }
    }

    private void AlignForwardToVelocity()
    {
        if (rb == null)
            return;

        Vector3 v = rb.linearVelocity;
        if (v.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(v.normalized, Vector3.up);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!initialized || landed || config == null)
            return;

        if ((config.groundLayers.value & (1 << collision.gameObject.layer)) == 0)
            return;

        Vector3 hitPoint = collision.GetContact(0).point;
        Vector3 hitNormal = collision.GetContact(0).normal;
        SpawnPoisonFieldAt(hitPoint, hitNormal);
        landed = true;
        Destroy(gameObject);
    }

    private void SpawnPoisonFieldAt(Vector3 worldPoint, Vector3 surfaceNormal)
    {
        var go = new GameObject("AngelCurse_PoisonField");
        go.transform.position = worldPoint;

        Vector3 up = surfaceNormal.sqrMagnitude > 0.001f ? surfaceNormal.normalized : Vector3.up;
        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, up);

        var field = go.AddComponent<AngelCursePoisonField>();
        field.Initialize(config);
    }
}
