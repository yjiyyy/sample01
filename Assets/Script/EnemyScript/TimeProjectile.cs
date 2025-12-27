using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class TimeProjectile : MonoBehaviour
{
    private Rigidbody rb;
    private TimeProjectileAttackData data;
    private Enemy enemyOwner;
    private Transform target;

    private float spawnTime;
    private bool exploded;

    // safety window to ignore immediate collisions with owner (avoid instant self-collision)
    private const float OWNER_IGNORE_WINDOW = 0.05f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogWarning("[TimeProjectile] Rigidbody가 없습니다.");
        }
    }

    /// <summary>
    /// 초기화: EnemyAttackController에서 호출
    /// 완전 물리 기반: rb.velocity, rb.angularVelocity로 초기 운동을 줍니다.
    /// </summary>
    public void Initialize(TimeProjectileAttackData data, Enemy owner, Transform target)
    {
        this.data = data;
        this.enemyOwner = owner;
        this.target = target;
        spawnTime = Time.time;
        exploded = false;

        if (rb == null) return;

        // Rigidbody 기본 세팅 권장값 적용 (SO 값 사용)
        rb.isKinematic = false;
        rb.useGravity = data.useGravity;
        rb.mass = Mathf.Max(0.001f, data.rigidbodyMass);
        rb.linearDamping = data.linearDrag;
        rb.angularDamping = data.angularDrag;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // 계산: 수평 방향(타깃 고정 Y) 우선
        Vector3 startPos = transform.position;

        Vector3 targetPos;
        if (target != null)
            targetPos = target.position;
        else
            targetPos = startPos + (owner != null ? owner.transform.forward : transform.forward) * data.range;

        Vector3 toTarget = targetPos - startPos;
        Vector3 toTargetXZ = new Vector3(toTarget.x, 0f, toTarget.z);
        Vector3 dirXZ = toTargetXZ.sqrMagnitude > 0.0001f ? toTargetXZ.normalized : (owner != null ? owner.transform.forward : transform.forward);

        // 수평 속도 설정
        Vector3 vel = dirXZ * data.projectileSpeed;

        // 위로 던지는 높이 근사 (arcHeight에 따라 초기 Y 속도 부여)
        float g = Physics.gravity.magnitude;
        float upVel = Mathf.Sqrt(2f * g * Mathf.Max(0f, data.arcHeight));
        vel.y = upVel;

        rb.linearVelocity = vel;

        // 초기 회전(스핀): 이동 방향의 수평 성분을 기준으로 '구르는' 축을 만들어 angularVelocity 설정
        Vector3 rollAxis = Vector3.Cross(Vector3.up, dirXZ);
        if (rollAxis.sqrMagnitude < 0.0001f)
            rollAxis = Vector3.right; // fallback
        float spinRad = data.spinSpeedDeg * Mathf.Deg2Rad;
        rb.angularVelocity = rollAxis.normalized * spinRad;
    }

    private void Update()
    {
        if (exploded || data == null) return;

        bool allowTimeout =
            data.explosionTrigger == TimeProjectileAttackData.ExplosionTriggerType.OnTimeoutOnly ||
            data.explosionTrigger == TimeProjectileAttackData.ExplosionTriggerType.OnCollisionOrTimeout;

        if (allowTimeout && Time.time >= spawnTime + data.projectileLifeTime)
        {
            Explode(transform.position);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (exploded || data == null) return;

        // Prevent immediate self-hit right after spawn (owner might have spawned projectile close)
        if (enemyOwner != null && collision.collider.transform.IsChildOf(enemyOwner.transform))
        {
            if (Time.time - spawnTime < OWNER_IGNORE_WINDOW)
            {
                // ignore very early collisions with owner
                if (Debug.isDebugBuild) Debug.Log("[TimeProjectile] Ignoring immediate collision with owner (safety window).");
                return;
            }
            // Otherwise, we DO NOT skip owner — owner is a valid target (per new rule).
        }

        bool allowCollision =
            data.explosionTrigger == TimeProjectileAttackData.ExplosionTriggerType.OnCollisionOnly ||
            data.explosionTrigger == TimeProjectileAttackData.ExplosionTriggerType.OnCollisionOrTimeout;

        if (!allowCollision)
        {
            // timeout-only: let physics continue (do not explode on collision)
            return;
        }

        Vector3 hitPoint = collision.GetContact(0).point;
        Explode(hitPoint);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (exploded || data == null) return;

        if (enemyOwner != null && other.transform.IsChildOf(enemyOwner.transform))
        {
            if (Time.time - spawnTime < OWNER_IGNORE_WINDOW)
            {
                if (Debug.isDebugBuild) Debug.Log("[TimeProjectile] Ignoring immediate trigger with owner (safety window).");
                return;
            }
        }

        bool allowCollision =
            data.explosionTrigger == TimeProjectileAttackData.ExplosionTriggerType.OnCollisionOnly ||
            data.explosionTrigger == TimeProjectileAttackData.ExplosionTriggerType.OnCollisionOrTimeout;

        if (!allowCollision) return;

        Explode(transform.position);
    }

    private void Explode(Vector3 pos)
    {
        if (exploded) return;
        exploded = true;

        GameObject go = new GameObject("EnemyExplosionHitbox");
        go.transform.position = pos;
        var hb = go.AddComponent<HitBox_Enemy_Explosion>();
        hb.Initialize(data, enemyOwner);

        Destroy(gameObject);
    }
}