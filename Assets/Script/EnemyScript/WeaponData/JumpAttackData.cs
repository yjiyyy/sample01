using UnityEngine;

[CreateAssetMenu(menuName = "Enemy/Attack/JumpAttackData")]
public class JumpAttackData : EnemyAttackDataBase
{
    [Header("General")]
    [Tooltip("패턴 이름(애니메이션 폴백 등)")]
    public string attackName;

    [Header("Timings")]
    [Tooltip("Prepare(점프 준비) 시간 (초) - 애니메이션 및 턴을 위한 대기 시간")]
    public float prepareDuration = 0.6f;

    [Tooltip("공중에서 머무르는 총 시간(초) — 이 시간 동안 포물선을 따라 이동")]
    public float duration = 0.9f;

    [Tooltip("End(착지 이후) 유지 시간 (초) - 착지 후 애니메이션 재생 등")]
    public float endDuration = 0.4f;

    [Header("Trajectory")]
    [Tooltip("포물선의 최대 높이(시작과 끝을 잇는 직선 기준으로의 상대 높이)")]
    public float height = 2.0f;

    [Tooltip("공격 쿨다운(성공 종료 후 적용되는 쿨다운, 초)")]
    public float cooldown = 1.2f;

    [Header("Animation Clips (optional)")]
    public AnimationClip prepareClip;
    public AnimationClip loopClip;   // 공중(Loop) 애니메이션
    public AnimationClip endClip;    // 착지/내려찍기 애니메이션

    [Header("Attack options")]
    public bool grantSuperArmor = true;
    public GameObject hitBoxPrefab;    // End 시작 시 스폰할 히트박스 프리팹 (HitBox_Enemy 타입 권장)
    public float hitBoxLifetime = 0.5f;

    [Tooltip("히트박스를 적의 자식으로 붙일지 여부. Rush는 true(부착형), Jump는 false(월드 고정)이 권장됩니다.")]
    public bool attachHitboxToEnemy = false;

    [Header("Damage / Knockback")]
    public float damage = 20f;
    public float range = 1.5f;
    public float knockbackPower = 6f;
    public float knockbackDuration = 0.2f;
    public float stunDuration = 0.4f;

    [Header("Push / Hitstop (플레이어 피격 시)")]
    [Tooltip("true면 넉백+스턴 대신 밀림(Push)만 적용. 플레이어 SO와 동일.")]
    public bool usePushInsteadOfKnockback = false;
    [Tooltip("피격 시 플레이어 Hitstop 시간(초). 0이면 비활성.")]
    public float targetHoldDuration = 0f;
    [Tooltip("공격 적중 시 공격자(몬스터) Hitstop 시간(초). 플레이어 SO attackerHoldDuration과 동일.")]
    public float attackerHoldDuration = 0f;

    [Header("Dup hit options (for area dot, drill etc.)")]
    public bool allowDuplicateHit = false;
    public float duplicateHitInterval = 0.2f;

    [Header("독 (플레이어)")]
    [Tooltip("true일 때만 독 공격으로 처리합니다(배리어 우회 등). false이면 Poison On Hit Status가 있어도 적용되지 않습니다.")]
    public bool isPoisonAttack;
    [Tooltip("맞을 때 플레이어 중독 상태를 갱신할 설정. 비우면 독 규칙만 적용되고 중독 틱·연출은 없습니다.")]
    public PoisonStatusConfigSO poisonOnHitStatus;

    [Header("Camera Shake")]
    public CameraShakeData cameraShake;

    private void Reset()
    {
        prepareDuration = 0.6f;
        duration = 0.9f;
        endDuration = 0.4f;
        height = 2.0f;
        cooldown = 1.2f;
        damage = 20f;
        range = 1.5f;
        knockbackPower = 6f;
        knockbackDuration = 0.2f;
        stunDuration = 0.4f;
        attachHitboxToEnemy = false;
    }
}