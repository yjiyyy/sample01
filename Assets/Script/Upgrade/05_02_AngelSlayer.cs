using UnityEngine;

[CreateAssetMenu(
    fileName = "05_02_AngelSlayer",
    menuName = "Game/Upgrade/Effect/05_02_AngelSlayer",
    order = 51)]
public class Upgrade_05_02_AngelSlayer : UpgradeEffectSO
{
    [Header("보조무기")]
    [Tooltip("플레이어에 붙일 보조무기 루트 프리팹 (무기 본 하위에 HitBox_PC+Collider 필요)")]
    public GameObject companionPrefab;

    [Header("타겟")]
    [Tooltip("적으로 인식해 근접 공격을 시도할 최대 거리(미터)")]
    [Min(0.1f)]
    public float acquireRange = 3f;

    [Tooltip("적 재탐색 간격(초). 보조무기 위치 기준으로 탐색")]
    [Min(0.05f)]
    public float scanInterval = 0.12f;

    [Tooltip("Overlap 등에 사용할 적 레이어. 비어 있으면 Enemy 태그 사용")]
    public LayerMask enemyLayers;

    [Header("공격 타이밍")]
    [Tooltip("공격 간 최소 간격(초)")]
    [Min(0.05f)]
    public float attackCooldown = 0.7f;

    [Tooltip("애니메이터 트리거 이름")]
    public string attackTriggerName = "Attack";

    [Tooltip("공격 시작(트리거) 후 히트박스 활성화까지 지연(초)")]
    [Min(0f)]
    public float hitboxSpawnDelay = 0.12f;

    [Tooltip("히트박스 활성 유지 시간(초)")]
    [Min(0.01f)]
    public float hitBoxLifetime = 0.12f;

    [Header("피해/피격")]
    [Tooltip("명중 시 기본 피해량")]
    [Min(0f)]
    public float damage = 12f;

    [Header("넉백 / 저크")]
    [Min(0f)] public float knockbackDuration = 0.2f;
    [Min(0f)] public float knockbackPower = 4f;
    [Min(0f)] public float jerkIntensity = 1f;
    [Min(0f)] public float jerkDuration = 0.2f;

    [Header("스턴")]
    [Min(0f)] public float stunDuration = 0f;

    [Header("히트스톱 (타격감)")]
    [Min(0f)] public float targetHoldDuration = 0f;

    [Header("처치 연출")]
    public DeathMode deathMode = DeathMode.Animation;
    [Min(0f)] public float ragdollImpulse = 5f;
    public float ragdollUpImpulse = 0f;
    [Min(0f)] public float ragdollSpinTorque = 0f;

    [Header("피격 이펙트")]
    [Tooltip("적에게 명중 시 ClosestPoint에 스폰할 이펙트. 비우면 없음")]
    public GameObject hitEffectPrefab;
}
