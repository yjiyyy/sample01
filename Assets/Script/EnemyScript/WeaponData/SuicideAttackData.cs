using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Enemy/Attack/SuicideAttackData", fileName = "SuicideAttackData_SO")]
public class SuicideAttackData : ScriptableObject
{
    [Header("General")]
    [Tooltip("패턴 이름(디버그/애니 폴백)")]
    public string attackName = "Suicide";

    [Tooltip("쿨다운(초). 자폭은 보통 1회성이지만 구조 통일용")]
    public float cooldown = 0f;

    [Header("Ranges")]
    [Tooltip("자폭 패턴을 '시작'할 수 있는 거리(AI 사거리). explodeDistance보다 크게 두는 것을 권장")]
    public float startRange = 6f;

    [Header("Timings")]
    [Tooltip("Prepare 단계 길이(초)")]
    public float prepareDuration = 0.5f;

    [Tooltip("플레이어를 인식(자폭 시작)한 뒤 이 시간이 다하면 폭발(타임아웃 폭발)")]
    public float maxChaseTime = 3f;

    [Header("Explode conditions")]
    [Tooltip("플레이어와 이 거리 이내가 되면 폭발")]
    public float explodeDistance = 1.6f;

    [Tooltip("추적 도중 Owner HP(=에너지)가 0이 되면 폭발(기존 옵션)")]
    public bool explodeWhenOwnerHPZero = true;

    [Header("Death during suicide (drop bomb)")]
    [Tooltip("자폭 공격 도중 플레이어에게 죽었을 때 떨어질 폭탄 프리팹(루트에 SuicideDroppedBomb 스크립트 필요).")]
    public GameObject droppedBombPrefab;

    [Tooltip("폭탄 스폰 위치를 위로 올리는 오프셋(미터).")]
    public float droppedBombSpawnHeightOffset = 0.2f;

    [Tooltip("스폰 시 위로 가해지는 속도 변화(m/s). Rigidbody가 있을 때 VelocityChange로 적용.")]
    public float droppedBombUpVelocity = 1.5f;

    [Tooltip("스폰 시 회전(각속도) 변화. Rigidbody가 있으면 Torque(VelocityChange), 없으면 회전 오프셋으로 1회 적용.")]
    public Vector3 droppedBombSpinVelocity = new Vector3(0f, 5f, 0f);

    [Header("Animations (optional)")]
    public AnimationClip prepareClip;
    public AnimationClip chaseLoopClip;

    [Header("Chase movement (Rush style, FixedUpdate)")]
    [Tooltip("추적 속도(m/s)")]
    public float chaseSpeed = 5f;

    [Tooltip("러시처럼 목표 방향으로 휘어지는 보정 여부")]
    public bool allowDirectionDeviation = true;

    [Range(0f, 1f)]
    [Tooltip("방향 보정 강도(0~1). 클수록 더 목표를 강하게 따라감")]
    public float directionDeviationAmount = 0.35f;

    [Tooltip("목표 방향 재획득 간격(초). 0이면 매 FixedUpdate")]
    public float retargetInterval = 0.12f;

    [Header("Explosion")]
    [Tooltip("폭발 반경")]
    public float explosionRadius = 2f;

    [Range(0f, 1f)]
    [Tooltip("거리 감쇠(가장자리 배율). 1이면 균일 데미지")]
    public float edgeDamageMultiplier = 0.5f;

    public enum SuicideExplosionTargetType
    {
        PlayerOnly,
        PlayerAndEnemies
    }

    [Tooltip("폭발 대상 선택")]
    public SuicideExplosionTargetType explosionTargets = SuicideExplosionTargetType.PlayerOnly;

    [Header("Damage / Knockback (Player/Enemy 공통)")]
    public float damage = 20f;
    public float knockbackPower = 5f;
    public float knockbackDuration = 0.2f;
    public float stunDuration = 0f;

    [Header("독 (플레이어)")]
    [Tooltip("true일 때만 독 공격으로 처리합니다(배리어 우회 등). false이면 Poison On Hit Status가 있어도 적용되지 않습니다.")]
    public bool isPoisonAttack;
    [Tooltip("맞을 때 플레이어 중독 상태를 갱신할 설정. 비우면 독 규칙만 적용되고 중독 틱·연출은 없습니다.")]
    public PoisonStatusConfigSO poisonOnHitStatus;

    [Header("Debug")]
    [Tooltip("체크하면 폭발 시 범위 확인용 더미 스피어를 잠깐 생성합니다.")]
    public bool spawnDebugSphereOnExplode = false;

    [Tooltip("디버그 스피어 유지시간(초)")]
    public float debugSphereLifetime = 0.5f;

    [Header("Owner death (Ragdoll/Slice) - 주변 Enemy도 동일 적용")]
    public DeathMode deathMode = DeathMode.Ragdoll;
    public float ragdollImpulse = 5f;
    public float ragdollUpImpulse = 0f;
    public float ragdollSpinTorque = 0f;

    public List<SliceTarget> sliceTargets = new List<SliceTarget>();
    public float sliceImpulse = 0f;

    [FormerlySerializedAs("animationHoldDuration")]
    public float targetHoldDuration = 0f;
    [Tooltip("공격 적중 시 공격자(몬스터) Hitstop 시간(초). 플레이어 SO attackerHoldDuration과 동일.")]
    public float attackerHoldDuration = 0f;
    public bool usePushInsteadOfKnockback = false;
    public float jerkIntensity = 1f;
    public float jerkDuration = 0.2f;

    [Header("Logs")]
    public bool debugLogs = true;

    private void OnValidate()
    {
        cooldown = Mathf.Max(0f, cooldown);

        startRange = Mathf.Max(0f, startRange);
        explodeDistance = Mathf.Max(0.01f, explodeDistance);
        if (startRange < explodeDistance)
            startRange = explodeDistance;

        prepareDuration = Mathf.Max(0f, prepareDuration);
        maxChaseTime = Mathf.Max(0f, maxChaseTime);

        chaseSpeed = Mathf.Max(0f, chaseSpeed);
        directionDeviationAmount = Mathf.Clamp01(directionDeviationAmount);
        retargetInterval = Mathf.Max(0f, retargetInterval);

        explosionRadius = Mathf.Max(0.01f, explosionRadius);
        edgeDamageMultiplier = Mathf.Clamp01(edgeDamageMultiplier);

        damage = Mathf.Max(0f, damage);
        knockbackPower = Mathf.Max(0f, knockbackPower);
        knockbackDuration = Mathf.Max(0f, knockbackDuration);
        stunDuration = Mathf.Max(0f, stunDuration);

        debugSphereLifetime = Mathf.Max(0f, debugSphereLifetime);

        ragdollImpulse = Mathf.Max(0f, ragdollImpulse);
        ragdollUpImpulse = Mathf.Max(0f, ragdollUpImpulse);
        ragdollSpinTorque = Mathf.Max(0f, ragdollSpinTorque);
        sliceImpulse = Mathf.Max(0f, sliceImpulse);

        targetHoldDuration = Mathf.Max(0f, targetHoldDuration);
        attackerHoldDuration = Mathf.Max(0f, attackerHoldDuration);
        jerkIntensity = Mathf.Max(0f, jerkIntensity);
        jerkDuration = Mathf.Max(0f, jerkDuration);

        // drop bomb params
        droppedBombSpawnHeightOffset = Mathf.Max(0f, droppedBombSpawnHeightOffset);
        droppedBombUpVelocity = Mathf.Max(0f, droppedBombUpVelocity);
        // droppedBombSpinVelocity는 Vector3라 clamp 불필요(원하면 여기서 제한 가능)
    }
}