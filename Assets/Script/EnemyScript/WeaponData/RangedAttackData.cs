using UnityEngine;

public enum RangedProjectileMovementType
{
    Straight = 0,   // 발사 시점 방향으로 등속 직진
    Parabolic = 1   // 2차 베지어로 목표까지 이동 후 마지막 접선 방향으로 계속 직진
}

[CreateAssetMenu(fileName = "RangedAttack", menuName = "Enemy/Attack/RangedAttackData")]
public class RangedAttackData : ScriptableObject
{
    [Header("공격 기본 정보")]
    public string attackName = "Ranged_Attack";
    public bool grantSuperArmor = false;
    public float damage = 10f;
    public float range = 10f;
    public float cooldown = 1.0f;

    [Header("준비/공격 시간")]
    [Tooltip("준비 동작 요구 시간(초). 0이면 스킵")]
    public float prepareTime = 0.2f;

    [Tooltip("공격 동작 요구 시간(초). 0 이하면 0.8초 기본")]
    public float attackTime = 0.6f;

    [Tooltip("공격 시작 후 몇 초에 투사체 발사할지 (0~attackTime로 clamp)")]
    public float fireAtTime = 0.2f;

    [Header("애니메이션 클립 (선택)")]
    [Tooltip("준비 동작 클립. 비워두면 재생 없이 대기만 수행")]
    public AnimationClip prepareClip;

    [Tooltip("공격 동작 클립. 비워두면 attackName으로 Animator.Play 시도")]
    public AnimationClip attackClip;

    [Header("투사체 설정")]
    public GameObject projectilePrefab;

    [Tooltip("발사 위치 자식 트랜스폼 이름(직접 입력). 못 찾으면 적 transform 사용")]
    public string firePointName = "Fire_Point";

    [Tooltip("투사체 이동 타입: 직선 또는 간이 포물(베지어)")]
    public RangedProjectileMovementType movementType = RangedProjectileMovementType.Straight;

    [Tooltip("투사체 속도(등속)")]
    public float projectileSpeed = 12f;

    [Tooltip("투사체 수명(초). 경과 시 자동 파괴")]
    public float projectileLifetime = 4f;

    [Tooltip("포물선(베지어) 중간 제어점 상승 높이")]
    public float arcHeight = 1.5f;

    [Header("넉백/스턴")]
    public float knockbackPower = 5f;
    public float knockbackDuration = 0.2f;
    public float stunDuration = 0f;

    [Header("중복 데미지 (참고: 투사체는 보통 히트 시 파괴)")]
    public bool allowDuplicateHit = false;
    public float duplicateHitInterval = 0.1f;

    [Header("장애물 충돌 처리")]
    [Tooltip("장애물(벽/바닥)과 부딪히면 파괴할지")]
    public bool destroyOnObstacle = true;

    [Tooltip("장애물 레이어 마스크. 비어있으면 비Trigger 충돌을 장애물로 간주(Enemy/Player 제외)")]
    public LayerMask obstacleLayers = 0;

    [Header("비행 중 회전 옵션")]
    [Tooltip("이동 방향을 바라보기(화살처럼)")]
    public bool faceToMovement = true;

    [Tooltip("비행 중 로컬 축 스핀")]
    public bool spinWhileFlying = false;

    [Tooltip("스핀 축(로컬 기준)")]
    public Vector3 spinAxis = new Vector3(0, 1, 0);

    [Tooltip("스핀 속도(도/초)")]
    public float spinSpeed = 360f;

}