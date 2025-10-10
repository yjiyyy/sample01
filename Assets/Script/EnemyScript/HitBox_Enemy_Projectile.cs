using UnityEngine;

/// <summary>
/// 적 원거리 투사체
/// - 이동: Straight(등속 직선) / Parabolic(간이 베지어, t=1 이후 마지막 접선 방향 직진)
/// - 충돌: Player 태그에 적중 시 데미지/넉백/스턴 적용 후 파괴
/// - 장애물: SO 옵션에 따라 레이어 또는 비Trigger 충돌 시 파괴
/// - 회전: faceToMovement(이동 방향 정면), spinWhileFlying(로컬축 스핀)
/// </summary>
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class HitBox_Enemy_Projectile : MonoBehaviour
{
    // SO에서 주입되는 전투 파라미터
    private float damage;
    private float knockbackPower;
    private float knockbackDuration;
    private float stunDuration;

    private bool allowDuplicateHit;
    private float duplicateHitInterval;

    // 이동/수명
    private float speed;
    private float lifetime;
    private float lifeTimer;

    private RangedProjectileMovementType movementType;
    private Vector3 startPos;
    private Vector3 targetPos; // 발사 시점의 플레이어 위치 스냅
    private float arcHeight;

    // Parabolic 용
    private float t;                   // 0~1 베지어 파라미터
    private float approxPathLen;
    private Vector3 p0, p1, p2;        // 베지어 제어점
    private Vector3 lastTangent;       // t→1 종료 시 접선

    // Straight/연속 이동용 고정 moveDir
    private Vector3 moveDir;

    // 회전 옵션
    private bool faceToMovement;
    private bool spinWhileFlying;
    private Vector3 spinAxisNormalized = Vector3.up;
    private float spinSpeed;

    // 장애물 처리
    private bool destroyOnObstacle;
    private LayerMask obstacleLayers;

    // 중복 히트 관리(현재는 플레이어 히트 시 파괴되므로 방어적만 남김)
    private GameObject lastHitObj;
    private float lastHitTime;

    private Rigidbody rb;
    private Collider col;

    private void Awake()
    {
        col = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();

        // Trigger + Kinematic 권장 설정
        if (col) col.isTrigger = true;
        if (rb)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
    }

    public void Initialize(
        float dmg,
        float spd,
        float life,
        float kbPower,
        float kbDuration,
        float stun,
        bool allowDup,
        float dupInterval,
        RangedProjectileMovementType moveType,
        Vector3 start,
        Vector3 target,
        float arcH,
        // 회전/장애물 옵션
        bool faceMove, bool spin, Vector3 spinAxis, float spinSpd,
        bool destroyObstacle, LayerMask obstacleMask
    )
    {
        damage = dmg;
        speed = Mathf.Max(0f, spd);
        lifetime = Mathf.Max(0.01f, life);
        knockbackPower = kbPower;
        knockbackDuration = kbDuration;
        stunDuration = stun;

        allowDuplicateHit = allowDup;
        duplicateHitInterval = dupInterval;

        movementType = moveType;
        startPos = start;
        targetPos = target;
        arcHeight = arcH;

        faceToMovement = faceMove;
        spinWhileFlying = spin;
        spinAxisNormalized = spinAxis.sqrMagnitude > 0.0001f ? spinAxis.normalized : Vector3.up;
        spinSpeed = spinSpd;

        destroyOnObstacle = destroyObstacle;
        obstacleLayers = obstacleMask;

        lifeTimer = 0f;
        t = 0f;

        switch (movementType)
        {
            case RangedProjectileMovementType.Straight:
                moveDir = (targetPos - startPos);
                if (moveDir.sqrMagnitude < 0.0001f) moveDir = Vector3.forward;
                moveDir.y = 0f; // 상면에서 운영
                moveDir.Normalize();
                break;

            case RangedProjectileMovementType.Parabolic:
                p0 = startPos;
                p2 = targetPos;
                p1 = (p0 + p2) * 0.5f + Vector3.up * arcHeight;

                // 근사 경로 길이: 두 선분 합
                approxPathLen = Vector3.Distance(p0, p1) + Vector3.Distance(p1, p2);
                lastTangent = (p2 - p1);
                if (lastTangent.sqrMagnitude < 0.0001f) lastTangent = (p2 - p0);
                if (lastTangent.sqrMagnitude < 0.0001f) lastTangent = Vector3.forward;
                lastTangent.Normalize();
                break;
        }

        // 스폰 즉시 1회 정렬(옵션 추가 없이)
        SetInitialFacingAtSpawn();

        // 수명 끝에 파괴
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        lifeTimer += Time.deltaTime;
        if (lifeTimer >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        switch (movementType)
        {
            case RangedProjectileMovementType.Straight:
                {
                    transform.position += moveDir * speed * Time.deltaTime;
                    ApplyFacing(moveDir);
                }
                break;

            case RangedProjectileMovementType.Parabolic:
                {
                    if (t < 1f)
                    {
                        // t가 실제 경로 길이에 비례하도록 등속 근사
                        float dt = (speed / Mathf.Max(approxPathLen, 0.01f)) * Time.deltaTime;
                        t = Mathf.Clamp01(t + dt);

                        Vector3 a = Vector3.Lerp(p0, p1, t);
                        Vector3 b = Vector3.Lerp(p1, p2, t);
                        Vector3 pos = Vector3.Lerp(a, b, t);
                        Vector3 tangent = (b - a);
                        if (tangent.sqrMagnitude > 0.0001f)
                            lastTangent = tangent.normalized;

                        transform.position = pos;
                        ApplyFacing(lastTangent);
                    }
                    else
                    {
                        // 목표점 지난 뒤엔 마지막 접선 방향으로 계속 직진
                        transform.position += lastTangent * speed * Time.deltaTime;
                        ApplyFacing(lastTangent);
                    }
                }
                break;
        }

        // 스핀
        if (spinWhileFlying && spinSpeed != 0f)
        {
            transform.Rotate(spinAxisNormalized, spinSpeed * Time.deltaTime, Space.Self);
        }
    }

    private void SetInitialFacingAtSpawn()
    {
        if (!faceToMovement) return;

        Vector3 dirToFace = Vector3.forward;

        if (movementType == RangedProjectileMovementType.Straight)
        {
            dirToFace = moveDir;
        }
        else // Parabolic
        {
            dirToFace = (p1 - p0);
            if (dirToFace.sqrMagnitude < 0.0001f) dirToFace = lastTangent;
        }

        dirToFace.y = 0f;
        if (dirToFace.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(dirToFace.normalized, Vector3.up);
        }
    }

    private void ApplyFacing(Vector3 dir)
    {
        if (!faceToMovement) return;
        if (dir.sqrMagnitude < 0.0001f) return;

        // "화살처럼" → 경로 접선 방향을 바라보게. 수평만 쓰려면 y=0 처리.
        Quaternion look = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = look;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 플레이어 피격
        if (other.CompareTag("Player"))
        {
            // 중복 히트 방지
            if (!allowDuplicateHit)
            {
                if (lastHitObj == other.gameObject) return;
            }
            else
            {
                if (lastHitObj == other.gameObject && Time.time - lastHitTime < duplicateHitInterval)
                    return;
            }
            lastHitObj = other.gameObject;
            lastHitTime = Time.time;

            // 무적 체크
            if (other.TryGetComponent(out PlayerWeaponController pwc) && pwc.IsInvincible())
            {
                Destroy(gameObject);
                return;
            }

            // 데미지
            if (other.TryGetComponent(out PlayerHealth hp))
            {
                hp.ApplyDamage(damage);
            }

            // 넉백/스턴
            Vector3 hitDir = (other.transform.position - transform.position);
            hitDir.y = 0f;
            if (hitDir.sqrMagnitude > 0.0001f) hitDir.Normalize();

            if (other.TryGetComponent(out PlayerWeaponController pwc2))
            {
                pwc2.ForceApplyKnockback(hitDir, knockbackPower, knockbackDuration, stunDuration);
            }
            else if (other.TryGetComponent(out PlayerMovement move))
            {
                move.ApplyKnockback(hitDir, knockbackPower, knockbackDuration, this.transform);
            }

            Destroy(gameObject);
            return;
        }

        // 장애물 처리
        if (destroyOnObstacle)
        {
            bool layerMatched = (obstacleLayers.value != 0) &&
                                ((obstacleLayers.value & (1 << other.gameObject.layer)) != 0);

            bool fallbackObstacle =
                (obstacleLayers.value == 0) &&
                (other.isTrigger == false) && // 비Trigger 충돌을 장애물로 간주
                !other.CompareTag("Enemy") &&
                !other.CompareTag("Player");

            if (layerMatched || fallbackObstacle)
            {
                Destroy(gameObject);
                return;
            }
        }
    }
}