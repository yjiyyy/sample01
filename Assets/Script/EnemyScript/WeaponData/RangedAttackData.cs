using UnityEngine;
using System.Collections.Generic;

public enum RangedProjectileMovementType
{
    Straight = 0,   // 직선으로 날아가는 투사체
    Parabolic = 1   // 포물선으로 날아가며 목표 지점을 향해 떨어지는 투사체
}

[CreateAssetMenu(fileName = "RangedAttack", menuName = "Enemy/Attack/RangedAttackData")]
public class RangedAttackData : EnemyAttackDataBase
{
    [Header("공격 기본 정보")]
    public string attackName = "Ranged_Attack";
    public bool grantSuperArmor = false;
    public float damage = 10f;
    public float range = 10f;
    public float cooldown = 1.0f;

    [Header("준비/공격 타이밍")]
    [Tooltip("준비 동작 대기 시간(초). 0이면 생략")]
    public float prepareTime = 0.2f;

    [Tooltip("공격 동작 전체 시간(초). 0 이하면 0.8초로 처리")]
    public float attackTime = 0.6f;

    [Tooltip("공격 시작 후 몇 초에 발사할지 (0~attackTime으로 clamp)")]
    public float fireAtTime = 0.2f;

    [Header("애니메이션 클립 (선택)")]
    [Tooltip("준비 동작 클립. 없으면 준비 시간만 대기")]
    public AnimationClip prepareClip;

    [Tooltip("공격 동작 클립. 없으면 attackName으로 Animator.Play 시도")]
    public AnimationClip attackClip;

    [Header("투사체 설정")]
    public GameObject projectilePrefab;

    [Tooltip("발사 위치 본/오브젝트 이름(없으면 루트). 이 이름의 자식 transform 검색")]
    public string firePointName = "Fire_Point";

    [Tooltip("투사체 이동 방식: 직선 또는 포물선")]
    public RangedProjectileMovementType movementType = RangedProjectileMovementType.Straight;

    [Tooltip("투사체 속도(m/s)")]
    public float projectileSpeed = 12f;

    [Tooltip("투사체 수명(초). 이 시간 후 제거")]
    public float projectileLifetime = 4f;

    [Tooltip("포물선 이동 시 추가할 최고 높이")]
    public float arcHeight = 1.5f;

    [Header("넉백/스턴")]
    public float knockbackPower = 5f;
    public float knockbackDuration = 0.2f;
    public float stunDuration = 0f;

    [Header("Push / Hitstop (플레이어 피격 시)")]
    [Tooltip("true면 넉백+스턴 대신 밀림(Push)만 적용. 플레이어 SO와 동일.")]
    public bool usePushInsteadOfKnockback = false;
    [Tooltip("피격 시 플레이어 Hitstop 시간(초). 0이면 비활성.")]
    public float targetHoldDuration = 0f;
    [Tooltip("공격 적중 시 공격자(몬스터) Hitstop 시간(초). 플레이어 SO attackerHoldDuration과 동일.")]
    public float attackerHoldDuration = 0f;

    [Header("중복 데미지 옵션")]
    public bool allowDuplicateHit = false;
    public float duplicateHitInterval = 0.1f;

    [Header("처치 연출 (선택)")]
    public DeathMode deathMode = DeathMode.Animation;
    public float ragdollImpulse = 5f;
    public float ragdollUpImpulse = 0f;
    public float ragdollSpinTorque = 0f;
    public List<SliceTarget> sliceTargets = new List<SliceTarget>();
    public float sliceImpulse = 0f;

    [Header("독 (플레이어)")]
    [Tooltip("true일 때만 독 공격으로 처리합니다(배리어 우회 등). false이면 Poison On Hit Status가 있어도 적용되지 않습니다.")]
    public bool isPoisonAttack;
    [Tooltip("맞을 때 플레이어 중독 상태를 갱신할 설정. 비우면 독 규칙만 적용되고 중독 틱·연출은 없습니다.")]
    public PoisonStatusConfigSO poisonOnHitStatus;

    [Header("장애물 충돌")]
    [Tooltip("장애물(벽/지면)에 맞으면 투사체 파괴")]
    public bool destroyOnObstacle = true;

    [Tooltip("장애물로 판정할 레이어. 비어 있으면 논Trigger 레이어만 기본 처리(Enemy/Player 제외)")]
    public LayerMask obstacleLayers = 0;

    [Header("비행 중 회전 연출")]
    [Tooltip("이동 방향을 바라보게(정렬)")]
    public bool faceToMovement = true;

    [Tooltip("비행 중 자체 축으로 회전")]
    public bool spinWhileFlying = false;

    [Tooltip("회전 축(로컬 기준)")]
    public Vector3 spinAxis = new Vector3(0, 1, 0);

    [Tooltip("회전 속도(도/초)")]
    public float spinSpeed = 360f;

}
