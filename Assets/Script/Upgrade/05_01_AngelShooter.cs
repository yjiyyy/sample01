using UnityEngine;

[CreateAssetMenu(
    fileName = "05_01_AngelShooter",
    menuName = "Game/Upgrade/Effect/05_01_AngelShooter",
    order = 50)]
public class Upgrade_05_01_AngelShooter : UpgradeEffectSO
{
    [Header("보조무기")]
    [Tooltip("플레이어에 붙일 보조무기 루트 프리팹 (발사 기준 Transform 포함)")]
    public GameObject companionPrefab;

    [Header("타겟")]
    [Tooltip("적으로 인식해 사격을 시도할 최대 거리(미터)")]
    [Min(0.1f)]
    public float acquireRange = 8f;

    [Tooltip("적을 다시 찾는 최소 간격(초). 너무 짧으면 모바일에서 부담")]
    [Min(0.05f)]
    public float reacquireInterval = 0.2f;

    [Tooltip("Overlap 등에 사용할 적 레이어")]
    public LayerMask enemyLayers;

    [Header("공격 연출")]
    [Tooltip("공격 시 재생할 애니메이션 클립. 비어 있으면 재생하지 않음. 프리팹에 Animator 필요")]
    public AnimationClip attackAnimationClip;

    [Tooltip("투사체·머즐 이펙트 스폰까지 대기 시간(초). 지연 중 타겟이 사라져도 이 방향으로 발사")]
    [Min(0f)]
    public float projectileSpawnDelay = 0f;

    [Tooltip("스폰 위치 우선: 이 이름의 자식 Transform (깊이 우선 탐색). 비어 있으면 Fire_Point 단계 생략 → Muzzle → 루트")]
    public string firePointChildName = "Fire_Point";

    [Header("발사")]
    [Tooltip("발사 간 최소 간격(초)")]
    [Min(0.05f)]
    public float fireCooldown = 0.5f;

    [Tooltip("날릴 투사체 프리팹")]
    public GameObject projectilePrefab;

    [Tooltip("투사체 이동 속도")]
    [Min(0f)]
    public float projectileSpeed = 20f;

    [Tooltip("명중 시 기본 피해량")]
    [Min(0f)]
    public float damage = 10f;

    [Header("넉백 / 저크")]
    [Tooltip("넉백 지속 시간(초). EnemyImpact에서 weapon 기준으로 사용")]
    [Min(0f)]
    public float knockbackDuration = 0.2f;

    [Tooltip("넉백/푸시 시 이동 세기(무기와 동일 의미)")]
    [Min(0f)]
    public float knockbackPower = 4f;

    [Tooltip("저크 강도(무기 SO와 동일 필드)")]
    [Min(0f)]
    public float jerkIntensity = 1f;

    [Tooltip("저크 지속(초)")]
    [Min(0f)]
    public float jerkDuration = 0.2f;

    [Header("Push(밀림)")]
    [Tooltip("true면 넉백 대신 Push 경로(Enemy.ApplyPush)")]
    public bool usePushInsteadOfKnockback = false;

    [Header("스턴")]
    [Tooltip("넉백 후 스턴 지속(초). 0이면 스턴 없음")]
    [Min(0f)]
    public float stunDuration = 0f;

    [Header("히트스톱 (타격감)")]
    [Tooltip("피격자 홀드(초). 0이면 미적용")]
    [Min(0f)]
    public float targetHoldDuration = 0f;

    [Tooltip("공격자(플레이어) 홀드(초). 0이면 미적용")]
    [Min(0f)]
    public float attackerHoldDuration = 0f;

    [Header("처치 연출 (데드)")]
    [Tooltip("적 사망 시 연출. Ragdoll이면 아래 임펄스 사용")]
    public DeathMode deathMode = DeathMode.Animation;

    [Header("Ragdoll 임펄스 (deathMode가 Ragdoll일 때)")]
    [Tooltip("사망 시 전방 임펄스")]
    [Min(0f)]
    public float ragdollImpulse = 5f;

    [Tooltip("사망 시 위쪽 추가 임펄스")]
    public float ragdollUpImpulse = 0f;

    [Tooltip("사망 시 스핀 토크")]
    [Min(0f)]
    public float ragdollSpinTorque = 0f;

    [Header("피격 이펙트 (적 표면)")]
    [Tooltip("적에게 명중 시 ClosestPoint에 스폰할 이펙트. 비우면 없음")]
    public GameObject hitEffectPrefab;

    [Header("머즐 이펙트")]
    [Tooltip("발사 순간 머즐 위치에 스폰할 이펙트 프리팹")]
    public GameObject muzzleEffectPrefab;

    [Tooltip("머즐 이펙트 자동 제거까지 시간(초). 파티클이 끝나기 전이면 조정")]
    [Min(0.05f)]
    public float muzzleEffectLifetime = 2f;
}
