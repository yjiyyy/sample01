using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Enemy/Attack/TimeProjectileAttackData")]
public class TimeProjectileAttackData : EnemyAttackDataBase
{
    [Header("공격 기본")]
    [Tooltip("공격 이름(디버그/식별용)")]
    public string attackName = "TimeProjectile";

    [Tooltip("공격 사거리 (이 거리 안에 있으면 사용)")]
    public float range = 10f;

    [Tooltip("공격 모션 시간(초)")]
    public float attackTime = 1.0f;

    [Tooltip("발사 시점(attackTime 기준)")]
    public float fireAtTime = 0.4f;

    [Tooltip("쿨타임(초)")]
    public float cooldown = 2.0f;

    [Header("애니메이션")]
    [Tooltip("공격 애니메이션 클립(선택)")]
    public AnimationClip clip;

    [Header("발사 위치")]
    [Tooltip("발사/투척 위치 본 이름 (예: Fire_Point_Throw)")]
    public string muzzleBoneName = "";

    public enum ExplosionTriggerType
    {
        OnCollisionOnly,      // 충돌 시에만 폭발
        OnTimeoutOnly,        // 수명 종료 시에만 폭발
        OnCollisionOrTimeout  // 충돌 또는 수명 종료 시 폭발
    }

    [Header("폭발 조건")]
    [Tooltip("폭발이 일어나는 조건")]
    public ExplosionTriggerType explosionTrigger = ExplosionTriggerType.OnCollisionOrTimeout;

    [Header("투사체 설정")]
    [Tooltip("투사체 프리팹")]
    public GameObject projectilePrefab;

    [Tooltip("투사체 속도 (m/s)")]
    public float projectileSpeed = 15f;

    [Tooltip("포물선 최고점 높이")]
    public float arcHeight = 2f;

    [Tooltip("투사체 수명 (초)")]
    public float projectileLifeTime = 3f;

    [Tooltip("중력 영향 여부")]
    public bool useGravity = true;

    [Header("데미지 / 폭발")]
    [Tooltip("기본 데미지")]
    public float damage = 20f;

    [Tooltip("폭발 반경")]
    public float explosionRadius = 2f;

    [Tooltip("가장자리 데미지 배율 (0~1) - 1이면 동일, 0이면 가장자리는 0")]
    [Range(0f, 1f)]
    public float edgeDamageMultiplier = 0.5f;

    [Tooltip("넉백 세기 (EnemyImpact/PlayerImpact에서 사용)")]
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

    [Header("폭발 대상")]
    [Tooltip("폭발 데미지를 받을 대상 종류")]
    public ExplosionTargetType explosionTargets = ExplosionTargetType.PlayerOnly;

    [Header("디버그")]
    [Tooltip("폭발 시 디버그 구체를 생성할지 여부")]
    public bool spawnDebugSphereOnExplode = false;

    [Header("물리(투사체)")]
    [Tooltip("Rigidbody mass")]
    public float rigidbodyMass = 1f;

    [Tooltip("직선 저항")]
    public float linearDrag = 0.05f;

    [Tooltip("회전 저항")]
    public float angularDrag = 0.4f;

    [Tooltip("회전(스핀) 속도 deg/s")]
    public float spinSpeedDeg = 720f;

    [Header("처치 연출 (Weapon과 동일한 옵션)")]
    [Tooltip("처치 연출 방식: Animation 또는 Ragdoll")]
    public DeathMode deathMode = DeathMode.Animation;

    [Tooltip("래그돌 전방 임펄스 (ForceMode.VelocityChange 기준 m/s)")]
    public float ragdollImpulse = 5f;

    [Tooltip("래그돌 상승 임펄스 (m/s)")]
    public float ragdollUpImpulse = 0f;

    [Tooltip("래그돌 회전 토크(ForceMode.VelocityChange) - 스핀 세기")]
    public float ragdollSpinTorque = 0f;

    [Tooltip("자를 신체 부위 목록 (Slice) - WeaponDataSO와 같은 방식")]
    public List<SliceTarget> sliceTargets = new List<SliceTarget>();

    [Tooltip("슬라이스된 부위에 가할 임펄스 (VelocityChange m/s)")]
    public float sliceImpulse = 0f;

    [Tooltip("피격 대상 홀드 시간(초) - death 연출과 별개")]
    [FormerlySerializedAs("animationHoldDuration")]
    public float targetHoldDuration = 0f;
    [Tooltip("공격 적중 시 공격자(몬스터) Hitstop 시간(초). 플레이어 SO attackerHoldDuration과 동일.")]
    public float attackerHoldDuration = 0f;

    [Tooltip("넉백 대신 push(밀림)만 적용할지 여부 (EnemyImpact.ApplyPush 사용)")]
    public bool usePushInsteadOfKnockback = false;

    [Tooltip("Jerk(화면 흔들림) 강도 (weapon과 동일)")]
    public float jerkIntensity = 1f;

    [Tooltip("Jerk 지속시간")]
    public float jerkDuration = 0.2f;

    [Header("독 (플레이어)")]
    [Tooltip("true일 때만 독 공격으로 처리합니다(배리어 우회 등). false이면 Poison On Hit Status가 있어도 적용되지 않습니다.")]
    public bool isPoisonAttack;
    [Tooltip("맞을 때 플레이어 중독 상태를 갱신할 설정. 비우면 독 규칙만 적용되고 중독 틱·연출은 없습니다.")]
    public PoisonStatusConfigSO poisonOnHitStatus;

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
        targetHoldDuration = Mathf.Max(0f, targetHoldDuration);
        attackerHoldDuration = Mathf.Max(0f, attackerHoldDuration);
        jerkIntensity = Mathf.Max(0f, jerkIntensity);
        jerkDuration = Mathf.Max(0f, jerkDuration);
    }
}
