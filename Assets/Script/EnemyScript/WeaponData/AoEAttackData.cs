using UnityEngine;

[CreateAssetMenu(menuName = "Enemy/Attack/AoE Attack Data")]
public class AoEAttackData : EnemyAttackDataBase
{
    [Header("Identification")]
    public string attackName = "AoE_Attack";

    [Header("Animation / Timings")]
    [Tooltip("공격 모션으로 재생할 클립 (선택). attackDuration과 길이가 달라도 패턴 시간은 아래 값을 따릅니다.")]
    public AnimationClip attackClip;
    [Tooltip("클립이 없을 때 재생할 Animator State 이름(선택)")]
    public string attackStateName = "AoE_Attack";
    [Tooltip("공격 전체 지속 시간(초). 이 시간이 지나면 패턴이 끝납니다(히트박스 스폰은 별도 코루틴으로 이어질 수 있음).")]
    public float attackDuration = 2.5f;
    [Tooltip("공격 종료 후 적용할 쿨타임(초)")]
    public float cooldown = 0f;

    [Header("Spawn")]
    [Tooltip("생성할 히트박스 개수")]
    public int spawnCount = 5;
    [Tooltip("적 주변 스폰 반경")]
    public float spawnRadius = 3f;
    [Tooltip("플레이어 주변 스폰 추가 반경(SpawnAtPlayerPosition 모드에서 사용)")]
    public float spawnAroundPlayerRadius = 1f;

    [Tooltip("첫 히트박스 스폰까지의 준비 시간(초).")]
    public float prepareDuration = 0.15f;

    [Tooltip("히트박스 스폰 간격(초). 0이면 동시에 스폰")]
    public float spawnInterval = 0.05f;
    [Tooltip("각 히트박스의 유지 시간(초)")]
    public float hitBoxLifetime = 0.25f;

    [Tooltip("스폰할 히트박스 (HitBox_Enemy 포함)")]
    public GameObject hitBoxPrefab;

    [Tooltip("히트박스 생성 전 추가로 기다릴 시간(초). 생성 후가 아니라 생성 자체를 미룹니다.")]
    public float hitboxActivationDelay = 0.0f;

    [Tooltip("히트박스를 적의 자식으로 붙일지 여부")]
    public bool attachHitboxToEnemy = false;

    [Tooltip("착지 위치를 찾을 지면 레이어 (0이면 기본 레이어)")]
    public LayerMask groundMask = 0;

    [Header("Debug / Visual")]
    [Tooltip("디버그용으로 스폰(히트 예정 위치)에 primitive sphere 마커를 표시할지")]
    public bool spawnDebugMarker = true;

    [Header("Damage / Knockback")]
    public float damage = 20f;
    public float knockbackPower = 6f;
    public float knockbackDuration = 0.3f;
    public float stunDuration = 0.4f;

    [Header("Push / Hitstop (플레이어 피격 시)")]
    [Tooltip("true면 넉백+스턴 대신 밀림(Push)만 적용. 플레이어 SO와 동일.")]
    public bool usePushInsteadOfKnockback = false;
    [Tooltip("피격 시 플레이어 Hitstop 시간(초). 0이면 비활성.")]
    public float targetHoldDuration = 0f;
    [Tooltip("공격 적중 시 공격자(몬스터) Hitstop 시간(초). 플레이어 SO attackerHoldDuration과 동일.")]
    public float attackerHoldDuration = 0f;

    [Header("Duplicate (중복) 피격")]
    public bool allowDuplicateHit = false;
    public float duplicateHitInterval = 0.2f;

    [Header("독 (플레이어)")]
    [Tooltip("true일 때만 독 공격으로 처리합니다(배리어 우회 등). false이면 Poison On Hit Status가 있어도 적용되지 않습니다.")]
    public bool isPoisonAttack;
    [Tooltip("맞을 때 플레이어 중독 상태를 갱신할 설정. 비우면 독 규칙만 적용되고 중독 틱·연출은 없습니다.")]
    public PoisonStatusConfigSO poisonOnHitStatus;

    [Header("Spawn Mode")]
    public SpawnMode spawnMode = SpawnMode.RandomAroundEnemy;

    public enum SpawnMode
    {
        RandomAroundEnemy,
        SpawnAtPlayerPosition
    }

    private void OnValidate()
    {
        spawnCount = Mathf.Max(1, spawnCount);
        spawnRadius = Mathf.Max(0f, spawnRadius);
        spawnAroundPlayerRadius = Mathf.Max(0f, spawnAroundPlayerRadius);
        prepareDuration = Mathf.Max(0f, prepareDuration);
        spawnInterval = Mathf.Max(0f, spawnInterval);
        hitBoxLifetime = Mathf.Max(0.01f, hitBoxLifetime);
        attackDuration = Mathf.Max(0.01f, attackDuration);
        cooldown = Mathf.Max(0f, cooldown);
        damage = Mathf.Max(0f, damage);
        duplicateHitInterval = Mathf.Max(0f, duplicateHitInterval);
        targetHoldDuration = Mathf.Max(0f, targetHoldDuration);
        attackerHoldDuration = Mathf.Max(0f, attackerHoldDuration);
    }
}
