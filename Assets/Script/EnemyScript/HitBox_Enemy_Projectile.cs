using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 적 원거리 발사체
/// - 이동: Straight(직선) / Parabolic(포물선, t=1 이후 접선 방향 직선 유지)
/// - 피격: Player 태그와 충돌 시 데미지/넉백/스턴 적용 후 처리
/// - 파괴: SO 옵션에 따라 장애물 / 비Trigger 충돌 시 파괴
/// - 회전: faceToMovement(이동 방향 정면), spinWhileFlying(자전 회전)
/// - 중복 히트: allowDuplicateHit=true면 겹치는 동안 duplicateHitInterval마다 반복 타격
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
    private float targetHoldDuration;
    private float attackerHoldDuration;
    private bool usePushInsteadOfKnockback;

    // 중복 히트 옵션
    private bool duplicateEnabled;
    private float duplicateInterval;

    // 이동/수명
    private float speed;
    private float lifetime;
    private float lifeTimer;

    private RangedProjectileMovementType movementType;
    private Vector3 startPos;
    private Vector3 targetPos; // 발사 시점의 플레이어 위치 스냅
    private float arcHeight;

    // Parabolic 용
    private float t;                   // 0~1 진행도
    private float approxPathLen;
    private Vector3 p0, p1, p2;        // 포물선 제어점
    private Vector3 lastTangent;       // t>=1 이후 직진 방향

    // Straight/직선 이동에서 쓰는 moveDir
    private Vector3 moveDir;

    // 회전 옵션
    private bool faceToMovement;
    private bool spinWhileFlying;
    private Vector3 spinAxisNormalized = Vector3.up;
    private float spinSpeed;

    // 장애물 처리
    private bool destroyOnObstacle;
    private LayerMask obstacleLayers;

    private WeaponDataSO playerDeathWeapon;

    // 드릴형 중복 히트 관리(플레이어 단위로 관리; 멀티 콜라이더 보호)
    private readonly HashSet<PlayerHealth> overlapping = new();
    private readonly HashSet<PlayerHealth> alreadyHit = new();
    private Coroutine dupRoutine;
    private Enemy ownerEnemy;

    private Rigidbody rb;
    private Collider col;

    private void Awake()
    {
        col = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();

        // Trigger + Kinematic 기본 세팅
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
        bool destroyObstacle, LayerMask obstacleMask,
        WeaponDataSO deathWeapon = null,
        float hitstopDuration = 0f, bool usePush = false, float attackerHitstop = 0f, Enemy owner = null
    )
    {
        damage = dmg;
        playerDeathWeapon = deathWeapon;
        speed = Mathf.Max(0f, spd);
        lifetime = Mathf.Max(0.01f, life);
        knockbackPower = kbPower;
        knockbackDuration = kbDuration;
        stunDuration = stun;
        targetHoldDuration = Mathf.Max(0f, hitstopDuration);
        attackerHoldDuration = Mathf.Max(0f, attackerHitstop);
        usePushInsteadOfKnockback = usePush;
        ownerEnemy = owner;

        duplicateEnabled = allowDup;
        duplicateInterval = Mathf.Max(0.01f, dupInterval);

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
        if (ownerEnemy == null)
            ownerEnemy = GetComponentInParent<Enemy>();

        switch (movementType)
        {
            case RangedProjectileMovementType.Straight:
                moveDir = (targetPos - startPos);
                if (moveDir.sqrMagnitude < 0.0001f) moveDir = Vector3.forward;
                moveDir.y = 0f; // 수평만 운용
                moveDir.Normalize();
                break;

            case RangedProjectileMovementType.Parabolic:
                p0 = startPos;
                p2 = targetPos;
                p1 = (p0 + p2) * 0.5f + Vector3.up * arcHeight;

                // 대략 길이: 선분 합
                approxPathLen = Vector3.Distance(p0, p1) + Vector3.Distance(p1, p2);
                lastTangent = (p2 - p1);
                if (lastTangent.sqrMagnitude < 0.0001f) lastTangent = (p2 - p0);
                if (lastTangent.sqrMagnitude < 0.0001f) lastTangent = Vector3.forward;
                lastTangent.Normalize();
                break;
        }

        // 스폰 직후 1회 정면 정렬(옵션)
        SetInitialFacingAtSpawn();

        // 수명 타이머 파괴 예약
        Destroy(gameObject, lifetime);

        // 드릴형이면 주기 코루틴 시작
        if (duplicateEnabled)
        {
            if (dupRoutine != null) StopCoroutine(dupRoutine);
            dupRoutine = StartCoroutine(DuplicateTickRoutine());
        }
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
                        // t 증가량을 속도/경로길이에 비례하도록 계산(프레임 독립)
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
                        // 목표점 통과 후 접선 방향 직선 유지
                        transform.position += lastTangent * speed * Time.deltaTime;
                        ApplyFacing(lastTangent);
                    }
                }
                break;
        }

        // 자전 회전
        if (spinWhileFlying && spinSpeed != 0f)
        {
            transform.Rotate(spinAxisNormalized, spinSpeed * Time.deltaTime, Space.Self);
        }
    }

    private void OnDisable()
    {
        if (dupRoutine != null)
        {
            StopCoroutine(dupRoutine);
            dupRoutine = null;
        }
        overlapping.Clear();
        alreadyHit.Clear();
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

        Quaternion look = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = look;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 플레이어 피격
        if (other.CompareTag("Player"))
        {
            var hp = other.GetComponentInParent<PlayerHealth>() ?? other.GetComponent<PlayerHealth>();
            if (hp == null) return;

            // 회피 무적 체크
            var pwcInv = other.GetComponentInParent<PlayerWeaponController>() ?? other.GetComponent<PlayerWeaponController>();
            if (pwcInv != null && pwcInv.IsInvincible())
            {
                // 기존 동작 유지: 무적이면 파괴
                Destroy(gameObject);
                return;
            }

            if (!duplicateEnabled)
            {
                // 즉발 1회: 같은 PlayerHealth 중복 방지(멀티 콜라이더 보호)
                if (alreadyHit.Contains(hp)) return;
                alreadyHit.Add(hp);

                ApplyHit(hp, other);
                Destroy(gameObject);
                return;
            }

            // 드릴형: 진입 즉시 1회 + 겹침 등록(마무리는 수명/장애물에서)
            ApplyHit(hp, other);
            overlapping.Add(hp);
            return;
        }

        // 장애물 처리
        if (destroyOnObstacle)
        {
            bool layerMatched = (obstacleLayers.value != 0) &&
                                ((obstacleLayers.value & (1 << other.gameObject.layer)) != 0);

            bool fallbackObstacle =
                (obstacleLayers.value == 0) &&
                (other.isTrigger == false) && // 비Trigger 충돌만 장애물로 간주
                !other.CompareTag("Enemy") &&
                !other.CompareTag("Player");

            if (layerMatched || fallbackObstacle)
            {
                Destroy(gameObject);
                return;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!duplicateEnabled) return;
        if (!other.CompareTag("Player")) return;

        var hp = other.GetComponentInParent<PlayerHealth>() ?? other.GetComponent<PlayerHealth>();
        if (hp != null)
            overlapping.Remove(hp);
    }

    private IEnumerator DuplicateTickRoutine()
    {
        while (true)
        {
            float elapsed = 0f;
            while (elapsed < duplicateInterval)
            {
                if (ownerEnemy != null && ownerEnemy.IsStateHoldActive)
                {
                    yield return null;
                    continue;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (overlapping.Count == 0) continue;

            // 스냅샷 순회(집합 변경 안전)
            var snapshot = new List<PlayerHealth>(overlapping);
            foreach (var hp in snapshot)
            {
                if (hp == null) continue;
                var col = hp.GetComponentInChildren<Collider>();
                ApplyHit(hp, col);
            }
        }
    }

    private void ApplyHit(PlayerHealth hp, Collider hitCollider)
    {
        if (hp == null) return;

        // 히트 시점 무적 재확인(회피 중 틱이면 스킵)
        var pwc = hp.GetComponent<PlayerWeaponController>() ?? hp.GetComponentInParent<PlayerWeaponController>();
        if (pwc != null && pwc.IsInvincible())
        {
            return;
        }

        // 1) 데미지 (플레이어 사망 시 랙돌 여부는 playerDeathWeapon.deathMode로 결정)
        Vector3 hitDir = (hp.transform.position - transform.position);
        hitDir.y = 0f;
        if (hitDir.sqrMagnitude < 0.0001f) hitDir = Vector3.forward;
        hitDir.Normalize();

        Vector3? hitPoint = hitCollider != null ? hitCollider.ClosestPoint(transform.position) : (Vector3?)null;
        float finalDamage = EnemyPlayerHitEffectApplier.ApplyIronBodyExtraDamageIfNeeded(pwc, damage);
        hp.ApplyDamage(finalDamage, hitDir, playerDeathWeapon, 1f, hitPoint);

        // ✅ 핵심: HP 0이면 넉백/스턴 스킵 (즉시 Death 우선)
        if (hp.GetCurrentHP() <= 0f)
            return;

        if (ownerEnemy == null)
            ownerEnemy = GetComponentInParent<Enemy>();

        var move = hp.GetComponent<PlayerMovement>() ?? hp.GetComponentInChildren<PlayerMovement>();

        EnemyPlayerHitEffectApplier.ApplyCrowdControlAndTargetHitstop(
            pwc,
            move,
            hitDir,
            knockbackPower,
            knockbackDuration,
            stunDuration,
            usePushInsteadOfKnockback,
            targetHoldDuration,
            transform,
            ownerEnemy,
            attackerHoldDuration);
    }
}