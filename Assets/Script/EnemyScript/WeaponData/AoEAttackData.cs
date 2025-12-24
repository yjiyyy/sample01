using UnityEngine;

[CreateAssetMenu(menuName = "Enemy/Attack/AoE Attack Data")]
public class AoEAttackData : ScriptableObject
{
    [Header("Identification")]
    public string attackName = "AoE_Attack";

    [Header("Animation / Timings")]
    [Tooltip("공격 재생용 애니메이션 클립 (optional). attackDuration과 비교하여 프레임 유지 동작에 사용)")]
    public AnimationClip attackClip;
    [Tooltip("클립이 없을 때 재생할 Animator State 이름(옵션)")]
    public string attackStateName = "AoE_Attack";
    [Tooltip("전체 공격 지속 시간 (초). 이 시간이 지나면 공격이 강제 종료됩니다(애니메이션보다 길면 애니 마지막 프레임 유지).")]
    public float attackDuration = 2.5f;
    [Tooltip("공격 종료 후 적용될 쿨다운(초)")]
    public float cooldown = 0f;

    [Header("Spawn")]
    [Tooltip("생성할 히트박스 개수")]
    public int spawnCount = 5;
    [Tooltip("몬스터 기준 반경")]
    public float spawnRadius = 3f;
    [Tooltip("플레이어 주변 랜덤 오프셋(SpawnAtPlayerPosition 모드에서 사용)")]
    public float spawnAroundPlayerRadius = 1f;

    [Tooltip("첫 히트박스 스폰까지의 지연(초). (prepareDuration으로 이름 변경)")]
    public float prepareDuration = 0.15f;

    [Tooltip("스폰 간 간격(초). 0이면 동시에 스폰")]
    public float spawnInterval = 0.05f;
    [Tooltip("히트박스의 판정 지속시간(초)")]
    public float hitBoxLifetime = 0.25f;

    [Tooltip("히트박스 프리팹 (HitBox_Enemy 권장)")]
    public GameObject hitBoxPrefab;

    [Tooltip("히트박스 스폰 후 콜라이더 활성화까지의 지연(초) - 히트박스 내부 판정 활성화 딜레이")]
    public float hitboxActivationDelay = 0.0f;

    [Tooltip("히트박스를 몬스터의 자식으로 붙일지 여부")]
    public bool attachHitboxToEnemy = false;

    [Tooltip("지면 보정용 레이어 마스크 (0이면 보정 안함)")]
    public LayerMask groundMask = 0;

    [Header("Debug / Visual")]
    [Tooltip("플레이어 위치(또는 스폰 베이스)가 확정되면 그 위치에 디버그 마커(primitive sphere)를 생성합니다")]
    public bool spawnDebugMarker = true;

    [Header("Damage / Knockback")]
    public float damage = 20f;
    public float knockbackPower = 6f;
    public float knockbackDuration = 0.3f;
    public float stunDuration = 0.4f;

    [Header("Duplicate (드릴형) 옵션")]
    public bool allowDuplicateHit = false;
    public float duplicateHitInterval = 0.2f;

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
    }
}