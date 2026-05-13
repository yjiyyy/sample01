using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public class AngelLotteryArcProjectile : MonoBehaviour
{
    private const float SweepSkin = 0.02f;

    private Rigidbody rb;
    private Collider cachedCollider;
    private Upgrade_05_05_AngelLottery config;
    private bool initialized;
    private bool landed;
    private bool impactLocked;
    private float lived;
    private float sweepRadius;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        cachedCollider = GetComponent<Collider>();

        // 충돌 이벤트 기반 처리이므로 Trigger 상태면 OnCollisionEnter가 오지 않는다.
        if (cachedCollider != null && cachedCollider.isTrigger)
            cachedCollider.isTrigger = false;

        sweepRadius = ResolveSweepRadius(cachedCollider);
    }

    public void Initialize(Upgrade_05_05_AngelLottery sourceConfig, Vector3 spawnPos, Vector3 targetWorld)
    {
        config = sourceConfig;
        initialized = config != null && rb != null;
        landed = false;
        impactLocked = false;
        lived = 0f;

        if (!initialized)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = spawnPos;

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.detectCollisions = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (!TryBuildArcVelocity(spawnPos, targetWorld, out Vector3 initialVelocity))
            initialVelocity = transform.forward * Mathf.Max(0.1f, config.projectileSpeed);

        rb.linearVelocity = initialVelocity;
    }

    private bool TryBuildArcVelocity(Vector3 startPos, Vector3 targetPos, out Vector3 velocity)
    {
        velocity = Vector3.zero;

        Vector3 to = targetPos - startPos;
        Vector3 toXZ = new Vector3(to.x, 0f, to.z);
        if (toXZ.sqrMagnitude <= 0.0001f)
            return false;

        float g = Mathf.Abs(Physics.gravity.y);
        if (g <= 0.001f)
            return false;

        // TimeProjectile와 동일한 감각: 수평 속도는 projectileSpeed를 그대로 사용하고,
        // arcHeight로 초기 Y 속도만 추가한다.
        Vector3 dirXZ = toXZ.normalized;
        float speed = Mathf.Max(0.1f, config.projectileSpeed);
        float upVel = Mathf.Sqrt(2f * g * Mathf.Max(0f, config.arcHeight));
        velocity = dirXZ * speed;
        velocity.y = upVel;
        return true;
    }

    private void Update()
    {
        if (!initialized || landed || config == null)
            return;

        lived += Time.deltaTime;
        if (lived >= Mathf.Max(0.2f, config.projectileMaxLifetime))
        {
            landed = true;
            Destroy(gameObject);
        }
    }

    private void FixedUpdate()
    {
        if (!initialized || landed || config == null || impactLocked)
            return;

        Vector3 vel = rb != null ? rb.linearVelocity : Vector3.zero;
        float speed = vel.magnitude;
        if (speed > 0.0001f)
        {
            LayerMask mask = config.groundLayers.value != 0 ? config.groundLayers : ~0;
            Vector3 dir = vel / speed;
            float castDist = speed * Time.fixedDeltaTime + SweepSkin;

            if (Physics.SphereCast(
                transform.position,
                Mathf.Max(0.01f, sweepRadius),
                dir,
                out RaycastHit _,
                castDist,
                mask,
                QueryTriggerInteraction.Collide))
            {
                landed = true;
                LockOnImpact();
                return;
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!initialized || landed || config == null || collision == null)
            return;

        LayerMask mask = config.groundLayers.value != 0 ? config.groundLayers : ~0;
        if ((mask.value & (1 << collision.gameObject.layer)) == 0)
            return;

        landed = true;
        LockOnImpact();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!initialized || landed || config == null || other == null)
            return;

        // 프로젝트 설정/프리팹 세팅으로 Trigger 충돌만 들어오는 경우를 대비한 fallback.
        LayerMask mask = config.groundLayers.value != 0 ? config.groundLayers : ~0;
        if ((mask.value & (1 << other.gameObject.layer)) == 0)
            return;

        landed = true;
        LockOnImpact();
    }

    private static float ResolveSweepRadius(Collider col)
    {
        if (col == null)
            return 0.08f;

        if (col is SphereCollider s)
            return Mathf.Max(0.01f, s.radius);
        if (col is CapsuleCollider c)
            return Mathf.Max(0.01f, c.radius);

        Bounds b = col.bounds;
        float r = Mathf.Min(b.extents.x, b.extents.y, b.extents.z);
        return Mathf.Max(0.01f, r);
    }

    private void LockOnImpact()
    {
        if (impactLocked)
            return;

        impactLocked = true;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        if (cachedCollider != null)
            cachedCollider.enabled = false;
    }
}
