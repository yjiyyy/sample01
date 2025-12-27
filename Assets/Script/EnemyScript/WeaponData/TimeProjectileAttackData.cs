using UnityEngine;

[CreateAssetMenu(menuName = "Enemy/Attack/TimeProjectileAttackData")]
public class TimeProjectileAttackData : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("패턴 이름(디버그용)")]
    public string attackName = "TimeProjectile";

    [Tooltip("이 공격을 시도할 최대 거리 (몬스터-플레이어 거리)")]
    public float range = 10f;

    [Tooltip("공격 전체 모션 시간 (초). 이 시간 동안 공격 중으로 유지됩니다.")]
    public float attackTime = 1.0f;

    [Tooltip("공격 시작 후 몇 초 뒤에 투사체를 발사할지 (초). attackTime보다 크면 발사하지 않습니다(애니메이션만 재생).")]
    public float fireAtTime = 0.4f;

    [Tooltip("공격 쿨다운 (성공 종료 후 적용, 초)")]
    public float cooldown = 2.0f;

    [Header("애니메이션 (선택)")]
    [Tooltip("이 공격에서 재생할 애니메이션 클립 (선택). 지정하지 않으면 컨트롤러 기본 애니메이션 사용.")]
    public AnimationClip clip;

    [Header("발사 위치 설정")]
    [Tooltip("발사 위치로 사용할 본/더미의 이름 (Enemy 루트 아래에서 FindChildRecursive로 검색)")]
    public string muzzleBoneName = "";

    public enum ExplosionTriggerType
    {
        OnCollisionOnly,      // 첫 충돌에서만 폭발
        OnTimeoutOnly,        // LifeTime 만료에서만 폭발
        OnCollisionOrTimeout  // 둘 중 먼저 일어나는 쪽에서 폭발
    }

    [Header("폭발 시작 조건")]
    [Tooltip("폭발을 언제 시작할지 결정합니다.")]
    public ExplosionTriggerType explosionTrigger = ExplosionTriggerType.OnCollisionOrTimeout;

    [Header("투사체 설정")]
    [Tooltip("TimeProjectile 컴포넌트와 Rigidbody, Collider가 붙어있는 프리팹")]
    public GameObject projectilePrefab;

    [Tooltip("투사체의 기본 속도 크기 (m/s) - 수평 방향 속도에 사용됩니다.")]
    public float projectileSpeed = 15f;

    [Tooltip("위로 던지는 높이 감각. 값이 클수록 더 높이 포물선으로 날아갑니다.")]
    public float arcHeight = 2f;

    [Tooltip("투사체 최대 생존 시간(초). 이 시간이 지나면 (옵션에 따라) 자동 폭발합니다.")]
    public float projectileLifeTime = 3f;

    [Tooltip("리지드바디에서 중력을 사용할지 여부")]
    public bool useGravity = true;

    [Header("폭발/피해 설정")]
    [Tooltip("폭발 기본 피해량 (중심 기준)")]
    public float damage = 20f;

    [Tooltip("폭발 반경 (OverlapSphere에 사용)")]
    public float explosionRadius = 2f;

    [Tooltip("폭발 반경 끝에서의 피해 배율 (0~1). 1이면 감쇠 없음, 0이면 끝에서는 피해 0.")]
    [Range(0f, 1f)]
    public float edgeDamageMultiplier = 0.5f;

    [Tooltip("넉백 힘 (프로젝트의 EnemyImpact/PlayerImpact 시스템에 맞게 사용 예정)")]
    public float knockbackPower = 5f;

    [Tooltip("넉백 지속 시간(초)")]
    public float knockbackDuration = 0.2f;

    [Tooltip("기절(스턴) 지속 시간(초)")]
    public float stunDuration = 0.0f;

    public enum ExplosionTargetType
    {
        PlayerOnly,
        EnemyOnly,
        Both
    }

    [Header("폭발 타겟 설정")]
    [Tooltip("폭발 피해가 적용될 대상 타입")]
    public ExplosionTargetType explosionTargets = ExplosionTargetType.PlayerOnly;

    [Header("폭발 시 디버그/범위 표시")]
    [Tooltip("폭발 시 범위 표시용 더미 스피어를 소환할지 여부 (시각 Only)")]
    public bool spawnDebugSphereOnExplode = false;

    [Header("물리(발사/굴림) 튜닝")]
    [Tooltip("Rigidbody mass (권장 0.5~2)")]
    public float rigidbodyMass = 1f;

    [Tooltip("선형 저항(Drag). 공중 이동 조절용 (권장 0~0.3)")]
    public float linearDrag = 0.05f;

    [Tooltip("회전 저항(Angular Drag). 굴림 감쇠용 (권장 0.2~1.0)")]
    public float angularDrag = 0.4f;

    [Tooltip("초기 스핀 속도(도/초). 발사 후 투사체가 회전(구르기)하도록 하는 시각 요소")]
    public float spinSpeedDeg = 720f;

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
    }
}