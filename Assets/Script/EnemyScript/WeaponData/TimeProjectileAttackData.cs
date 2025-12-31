using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Enemy/Attack/TimeProjectileAttackData")]
public class TimeProjectileAttackData : ScriptableObject
{
    [Header("기본 설정")]
    [Tooltip("패턴 이름(에디터용)")]
    public string attackName = "TimeProjectile";

    [Tooltip("사정 거리 (폭발 목표 예측에 사용)")]
    public float range = 10f;

    [Tooltip("공격 전체 시간(초)")]
    public float attackTime = 1.0f;

    [Tooltip("발사 타이밍(attackTime 기준)")]
    public float fireAtTime = 0.4f;

    [Tooltip("쿨다운(초)")]
    public float cooldown = 2.0f;

    [Header("애니메이션")]
    [Tooltip("공격 애니메이션 클립(옵션)")]
    public AnimationClip clip;

    [Header("발사 위치")]
    [Tooltip("무기/적의 뼈대 이름 (예: Fire_Point_Throw)")]
    public string muzzleBoneName = "";

    public enum ExplosionTriggerType
    {
        OnCollisionOnly,      // 충돌 시 폭발
        OnTimeoutOnly,        // 시간 만료 시 폭발
        OnCollisionOrTimeout  // 충돌 또는 시간 만료 시 폭발
    }

    [Header("폭발 트리거")]
    [Tooltip("폭발 발생 방식")]
    public ExplosionTriggerType explosionTrigger = ExplosionTriggerType.OnCollisionOrTimeout;

    [Header("발사체 설정")]
    [Tooltip("발사체 프리팹")]
    public GameObject projectilePrefab;

    [Tooltip("발사 속도 (m/s)")]
    public float projectileSpeed = 15f;

    [Tooltip("호를 만드는 높이")]
    public float arcHeight = 2f;

    [Tooltip("발사체 수명 (초)")]
    public float projectileLifeTime = 3f;

    [Tooltip("중력 사용 여부")]
    public bool useGravity = true;

    [Header("데미지 / 폭발")]
    [Tooltip("기본 데미지")]
    public float damage = 20f;

    [Tooltip("폭발 반경")]
    public float explosionRadius = 2f;

    [Tooltip("가장자리 데미지 곱 (0~1) - 1이면 균일, 0이면 에지에서 0")]
    [Range(0f, 1f)]
    public float edgeDamageMultiplier = 0.5f;

    [Tooltip("넉백 파워 (EnemyImpact/PlayerImpact에서 참조)")]
    public float knockbackPower = 5f;

    [Tooltip("넉백 지속시간 (초)")]
    public float knockbackDuration = 0.2f;

    [Tooltip("스턴 지속시간 (초)")]
    public float stunDuration = 0.0f;

    public enum ExplosionTargetType
    {
        PlayerOnly,
        EnemyOnly,
        Both
    }

    [Header("타겟 필터")]
    [Tooltip("폭발이 영향을 주는 대상 타입")]
    public ExplosionTargetType explosionTargets = ExplosionTargetType.PlayerOnly;

    [Header("디버그")]
    [Tooltip("폭발 시 디버그 구체 스폰")]
    public bool spawnDebugSphereOnExplode = false;

    [Header("물리(발사체)")]
    [Tooltip("Rigidbody mass")]
    public float rigidbodyMass = 1f;

    [Tooltip("선형 드래그")]
    public float linearDrag = 0.05f;

    [Tooltip("회전 드래그")]
    public float angularDrag = 0.4f;

    [Tooltip("회전(스핀) 속도 deg/s")]
    public float spinSpeedDeg = 720f;

    // ───────── Weapon-like death / ragdoll / slice 관련 필드 (Player WeaponDataSO와 동일 구성) ─────────
    [Header("데스 연출 (Weapon과 동일한 구성)")]
    [Tooltip("죽음 연출 방식: Animation 또는 Ragdoll")]
    public DeathMode deathMode = DeathMode.Animation;

    [Tooltip("랙돌 수평 임펄스 (ForceMode.VelocityChange 기준 m/s)")]
    public float ragdollImpulse = 5f;

    [Tooltip("랙돌 위로 임펄스 (m/s)")]
    public float ragdollUpImpulse = 0f;

    [Tooltip("랙돌 회전 토크(ForceMode.VelocityChange) - 전체 분배 기준값")]
    public float ragdollSpinTorque = 0f;

    [Tooltip("본 분리 대상 목록 (Slice) - WeaponDataSO와 동일 타입 사용")]
    public List<SliceTarget> sliceTargets = new List<SliceTarget>();

    [Tooltip("슬라이스에 적용되는 임펄스 (VelocityChange m/s)")]
    public float sliceImpulse = 0f;

    [Tooltip("애니메이션/힛스탑 지속시간(초) - death 연출 등에서 사용")]
    public float animationHoldDuration = 0f;

    [Tooltip("넉백 대신 push(밀림)을 사용할지 여부 (EnemyImpact.ApplyPush 사용)")]
    public bool usePushInsteadOfKnockback = false;

    [Tooltip("Jerk(짧은 흔들림) 세기 (weapon과 호환)")]
    public float jerkIntensity = 1f;

    [Tooltip("Jerk 지속시간")]
    public float jerkDuration = 0.2f;

    // ───────────────────────────────────────────────────────────────────────────────────────────────

    private void OnValidate()
    {
        range = Mathf.Max(0f, range);
        attackTime = Mathf.Max(0f, attackTime);
        fireAtTime = Mathf.Max(0f, fireAtTime);
        cooldown = Mathf.Max(0f, cooldown);

        projectileSpeed = Mathf.Max(0.1f, projectileSpeed);
        arcHeight = Mathf.Max(0f, arcHeight);
        projectileLifeTime = Mathf.Max(0.1f, projectileLifeTime);

        damage = Mathf.Max(0f, damage);
        explosionRadius = Mathf.Max(0.01f, explosionRadius);
        edgeDamageMultiplier = Mathf.Clamp01(edgeDamageMultiplier);
        knockbackPower = Mathf.Max(0f, knockbackPower);
        knockbackDuration = Mathf.Max(0f, knockbackDuration);
        stunDuration = Mathf.Max(0f, stunDuration);

        rigidbodyMass = Mathf.Max(0.001f, rigidbodyMass);
        linearDrag = Mathf.Max(0f, linearDrag);
        angularDrag = Mathf.Max(0f, angularDrag);
        spinSpeedDeg = Mathf.Max(0f, spinSpeedDeg);

        ragdollImpulse = Mathf.Max(0f, ragdollImpulse);
        ragdollUpImpulse = Mathf.Max(0f, ragdollUpImpulse);
        ragdollSpinTorque = Mathf.Max(0f, ragdollSpinTorque);
        sliceImpulse = Mathf.Max(0f, sliceImpulse);
        animationHoldDuration = Mathf.Max(0f, animationHoldDuration);
        jerkIntensity = Mathf.Max(0f, jerkIntensity);
        jerkDuration = Mathf.Max(0f, jerkDuration);
    }
}