using UnityEngine;

public enum AngelCurseDamageTargetType
{
    EnemyOnly,
    Both
}

[CreateAssetMenu(
    fileName = "05_03_AngelCurse",
    menuName = "Game/Upgrade/Effect/05_03_AngelCurse",
    order = 52)]
public class Upgrade_05_03_AngelCurse : UpgradeEffectSO
{
    [Header("보조무기")]
    [Tooltip("플레이어에 붙일 보조무기 루트 프리팹 (발사 기준 Transform 포함)")]
    public GameObject companionPrefab;

    [Header("타겟")]
    [Tooltip("적으로 인식해 사격을 시도할 최대 거리(미터)")]
    [Min(0.1f)]
    public float acquireRange = 8f;

    [Tooltip("적을 다시 찾는 최소 간격(초)")]
    [Min(0.05f)]
    public float reacquireInterval = 0.2f;

    [Tooltip("Overlap 등에 사용할 적 레이어")]
    public LayerMask enemyLayers;

    [Header("공격 연출 (05_01과 동일 방식)")]
    [Tooltip("공격 시 재생할 애니메이션 클립. 비어 있으면 재생하지 않음.")]
    public AnimationClip attackAnimationClip;

    [Tooltip("투사체 스폰까지 대기 시간(초)")]
    [Min(0f)]
    public float projectileSpawnDelay = 0f;

    [Tooltip("스폰 위치 우선: 이 이름의 자식 Transform. 비어 있으면 Fire_Point 단계 생략 -> Muzzle -> 루트")]
    public string firePointChildName = "Fire_Point";

    [Header("발사")]
    [Tooltip("발사 간 최소 간격(초)")]
    [Min(0.05f)]
    public float fireCooldown = 0.9f;

    [Tooltip("날릴 낙하형 투사체 프리팹")]
    public GameObject projectilePrefab;

    [Tooltip("투사체 수평 속도 (m/s)")]
    [Min(0.1f)]
    public float projectileSpeed = 12f;

    [Tooltip("포물선 최고점 추가 높이 (m). 0이면 직선에 가까워짐")]
    [Min(0f)]
    public float arcHeight = 1.8f;

    [Tooltip("지면 충돌이 없을 때 안전 파괴 시간(초)")]
    [Min(0.2f)]
    public float projectileMaxLifetime = 5f;

    [Tooltip("지면으로 인정할 레이어")]
    public LayerMask groundLayers = ~0;

    [Header("독 필드 (순수 DoT)")]
    [Tooltip("독 필드 지속 시간(초)")]
    [Min(0.1f)]
    public float poisonFieldLifetime = 4f;

    [Tooltip("독 필드 반경(m)")]
    [Min(0.1f)]
    public float poisonFieldRadius = 2.2f;

    [Tooltip("독 필드 캡슐 높이(m). 층간 누수 방지용")]
    [Min(0.2f)]
    public float poisonFieldHeight = 1.0f;

    [Tooltip("독 필드 중심 오프셋")]
    public Vector3 poisonFieldCenterOffset = Vector3.zero;

    [Tooltip("독 틱 간격(초)")]
    [Min(0.05f)]
    public float poisonTickInterval = 0.5f;

    [Tooltip("독 틱당 피해량")]
    [Min(0f)]
    public float poisonDamagePerTick = 8f;

    [Tooltip("독 필드가 피해를 줄 대상")]
    public AngelCurseDamageTargetType poisonDamageTargets = AngelCurseDamageTargetType.EnemyOnly;

    [Header("게임뷰 시각화 (디버그/튜닝용)")]
    [Tooltip("활성 중인 독 필드 범위를 게임뷰에서 반투명으로 표시")]
    public bool showFieldInGame = true;

    [Tooltip("필드 시각화 색상(알파 포함)")]
    public Color fieldVisualColor = new Color(0.25f, 0.9f, 0.3f, 0.22f);

    [Tooltip("시각화 스케일 여유치. Z-fighting 방지")]
    [Min(0f)]
    public float fieldVisualPadding = 0.04f;

    [Header("처치 연출 (DoT 사망 시)")]
    public DeathMode deathMode = DeathMode.Animation;

    [Min(0f)] public float ragdollImpulse = 0f;
    public float ragdollUpImpulse = 0f;
    [Min(0f)] public float ragdollSpinTorque = 0f;
}
