using System.Collections.Generic;
using UnityEngine;

public class HitBox_PC_Projectile : MonoBehaviour
{
    private float speed;
    private float lifetime;
    private float damage;
    private Vector3 moveDir;

    private WeaponDataSO weapon;

    // 관통 관련
    // remainingPierce: 남은 '히트(데미지 적용) 가능 횟수'
    private int remainingPierce = 0;
    private readonly HashSet<EnemyHealth> hitSet = new HashSet<EnemyHealth>();

    // --- New: defensive components refs ---
    private Collider _coll;
    private Rigidbody _rb;

    // Small tolerance for arrival snapping (unused for normal straight movement)
    private const float ARRIVAL_EPS = 0.05f;

    public void SetWeapon(WeaponDataSO w)
    {
        weapon = w;

        // If weapon SO provides pierce count and we haven't set remainingPierce explicitly,
        // use it as default so missing params from caller won't break piercing.
        // Read common weapon SOs that define pierceCount (Gun, AR)
        if (weapon is WeaponDataSO_Gun g)
        {
            remainingPierce = Mathf.Max(0, g.pierceCount);
        }
        else if (weapon is WeaponDataSO_AR ar)
        {
            remainingPierce = Mathf.Max(0, ar.pierceCount);
        }
        // NOTE: WeaponDataSO_Launcher does not define pierceCount in this project,
        // so we must NOT attempt to read it (would cause CS1061).
    }

    private void Awake()
    {
        // Defensive: ensure Collider is trigger so physics doesn't 'trap' transform-based movement.
        _coll = GetComponent<Collider>();
        if (_coll != null)
        {
            if (!_coll.isTrigger)
            {
                Debug.Log($"[Projectile] Forcing collider.isTrigger=true on {name} to avoid physics-stopping.");
                _coll.isTrigger = true;
            }
        }

        // If a Rigidbody exists, set it kinematic so physics won't override transform moves.
        _rb = GetComponent<Rigidbody>();
        if (_rb != null && !_rb.isKinematic)
        {
            Debug.Log($"[Projectile] Forcing Rigidbody.isKinematic=true on {name} to allow transform movement.");
            _rb.isKinematic = true;
        }
    }

    /* ───────── 기존 API: 방향으로 즉시 발사(호환 유지) ───────── */
    public void InitializeTowards(Vector3 direction, float dmg, float spd, float life)
    {
        // If remainingPierce wasn't set by SetWeapon, keep it as 0 (no pierce)
        damage = dmg;
        speed = spd;
        lifetime = life;
        moveDir = direction.normalized;

        Destroy(gameObject, lifetime);
    }

    // 오버로드: 피어스 카운트까지 설정 (명시적 값이 있으면 우선 적용)
    public void InitializeTowards(Vector3 direction, float dmg, float spd, float life, int pierceCount)
    {
        InitializeTowards(direction, dmg, spd, life);
        remainingPierce = Mathf.Max(0, pierceCount);
        if (remainingPierce > 0)
            Debug.Log($"[Projectile] InitializeTowards set pierce={remainingPierce}");
    }

    /* ───────── New API: 발사 시점의 타깃 위치를 고정해서 발사 ───────── */
    // targetPos: world-space 고정 좌표
    // maintainTargetHeight: true이면 발사체의 y를 targetPos.y로 고정하고 XZ 평면으로만 이동
    public void InitializeTowardsTargetPosition(Vector3 targetPos, float dmg, float spd, float life, bool maintainTargetHeight = true)
    {
        damage = dmg;
        speed = spd;
        lifetime = life;

        if (maintainTargetHeight)
        {
            // fix y to our current spawn y
            Vector3 p = transform.position;
            transform.position = new Vector3(p.x, targetPos.y, p.z);

            Vector3 horiz = new Vector3(targetPos.x - transform.position.x, 0f, targetPos.z - transform.position.z);
            if (horiz.sqrMagnitude < 0.0001f) horiz = transform.forward;
            moveDir = horiz.normalized;
        }
        else
        {
            Vector3 dir3 = (targetPos - transform.position);
            if (dir3.sqrMagnitude < 0.0001f) dir3 = transform.forward;
            moveDir = dir3.normalized;
        }

        Destroy(gameObject, lifetime);
    }

    // 오버로드: 피어스 카운트까지 설정
    public void InitializeTowardsTargetPosition(Vector3 targetPos, float dmg, float spd, float life, int pierceCount, bool maintainTargetHeight = true)
    {
        InitializeTowardsTargetPosition(targetPos, dmg, spd, life, maintainTargetHeight);
        remainingPierce = Mathf.Max(0, pierceCount);
        if (remainingPierce > 0)
            Debug.Log($"[Projectile] InitializeTowardsTargetPosition set pierce={remainingPierce}");
    }

    void Update()
    {
        // simple transform-based movement (frame-rate independent)
        transform.position += moveDir * speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Enemy")) return;

        // 대상 Health 찾기(중복 타격 방지용 키)
        var hp = other.GetComponentInParent<EnemyHealth>();
        if (hp == null)
        {
            Debug.LogWarning($"❌ [Projectile] {other.name}에서 EnemyHealth를 찾지 못했습니다.");
            return;
        }

        // 이미 같은 EnemyHealth에 타격했으면 중복 무시
        if (hitSet.Contains(hp))
            return;

        // 넉백 / Push 분기 처리
        if (other.GetComponentInParent<Enemy>() is Enemy enemy)
        {
            Vector3 knockbackDir = moveDir;
            knockbackDir.y = 0f;
            if (knockbackDir.sqrMagnitude < 0.0001f) knockbackDir = Vector3.back;
            knockbackDir = knockbackDir.normalized;

            if (weapon != null && weapon.usePushInsteadOfKnockback)
            {
                enemy.ApplyPush(knockbackDir, weapon);
                Debug.Log($"💥 Projectile 충돌 │ Push 방향: {knockbackDir}");
            }
            else
            {
                enemy.ApplyKnockback(knockbackDir, weapon);
                Debug.Log($"💥 Projectile 충돌 │ 넉백 방향: {knockbackDir}");
            }
        }

        // 데미지 1회 적용(Projectile 기준)
        {
            Vector3 damageDir = moveDir;
            damageDir.y = 0f;
            if (damageDir.sqrMagnitude < 0.0001f) damageDir = Vector3.back;
            damageDir = damageDir.normalized;

            hp.ApplyDamage(damage, damageDir, weapon);
            Debug.Log($"✅ [Projectile] EnemyHealth에 {damage} 데미지 적용!");
        }

        // 관통 처리(단순화)
        hitSet.Add(hp);

        // If remainingPierce <= 0 means no pierce allowed -> destroy immediately
        // If remainingPierce > 0, decrement and continue flying.
        if (remainingPierce <= 0)
        {
            Debug.Log($"[Projectile] No pierce left -> Destroying on hit. (hp:{hp.name})");
            Destroy(gameObject);
            return;
        }
        else
        {
            remainingPierce--;
            Debug.Log($"[Projectile] Pierce consumed -> remainingPierce={remainingPierce}. Continuing flight.");
            if (remainingPierce <= 0)
            {
                // If this hit consumed the last allowed hit, destroy now
                Destroy(gameObject);
                return;
            }
        }
    }
}