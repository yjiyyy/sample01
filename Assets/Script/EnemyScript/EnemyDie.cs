using UnityEngine;

[CreateAssetMenu(menuName = "Enemy/AoE Attack")]
public class AoEAttackData : ScriptableObject
{
    public string attackName = "AoE_Attack";

    [Header("Spawn")]
    [Tooltip("생성할 히트박스 개수")]
    public int spawnCount = 5;
    [Tooltip("몬스터(또는 플레이어) 기준 반경")]
    public float spawnRadius = 3f;
    [Tooltip("히트박스를 스폰한 뒤 판정(활성화)까지 대기시간(초)")]
    public float hitboxActivationDelay = 0.25f;
    [Tooltip("스폰 사이 간격(0이면 동시에 스폰)")]
    public float spawnInterval = 0.1f;
    [Tooltip("스폰 시, 플레이어 주변 랜덤 위치 반경(플레이어 추적 모드에서 사용)")]
    public float spawnAroundPlayerRadius = 1f;
    [Tooltip("지면 보정용 레이어 마스크 (0이면 y는 변하지 않음)")]
    public LayerMask groundMask = 0;

    [Header("HitBox")]
    [Tooltip("재사용할 HitBox 프리팹 (HitBox_Enemy 타입 권장)")]
    public GameObject hitBoxPrefab;
    [Tooltip("히트박스의 판정 지속시간(초)")]
    public float hitBoxLifetime = 0.5f;

    [Header("Damage / Knockback (HitBox_Enemy와 호환되는 값)")]
    public float damage = 20f;
    public float knockbackPower = 6f;
    public float knockbackDuration = 0.3f;
    public float stunDuration = 0.4f;

    [Header("Duplicate (드릴형) 옵션")]
    public bool allowDuplicateHit = false;
    public float duplicateHitInterval = 0.2f;

    [Header("Spawn Mode")]
    public SpawnMode spawnMode = SpawnMode.RandomAroundEnemy;

    [Tooltip("히트박스를 몬스터의 자식으로 붙일지 여부")]
    public bool attachToEnemy = false;

    [Header("Pooling (옵션)")]
    [Tooltip("플리핑: 풀링 사용 여부 (주의: 프리팹이 풀링 호환이어야 안정적)")]
    public bool usePooling = false;
    [Tooltip("풀 초기 크기 (usePooling=true일 때)")]
    public int poolInitialSize = 8;

    public enum SpawnMode
    {
        RandomAroundEnemy,
        SpawnAtPlayerPosition
    }

    private void OnValidate()
    {
        spawnCount = Mathf.Max(1, spawnCount);
        spawnRadius = Mathf.Max(0f, spawnRadius);
        hitboxActivationDelay = Mathf.Max(0f, hitboxActivationDelay);
        spawnInterval = Mathf.Max(0f, spawnInterval);
        spawnAroundPlayerRadius = Mathf.Max(0f, spawnAroundPlayerRadius);
        hitBoxLifetime = Mathf.Max(0.01f, hitBoxLifetime);
        damage = Mathf.Max(0f, damage);
        duplicateHitInterval = Mathf.Max(0f, duplicateHitInterval);
        poolInitialSize = Mathf.Max(0, poolInitialSize);
    }
}