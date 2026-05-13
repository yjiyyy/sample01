using UnityEngine;

[CreateAssetMenu(
    fileName = "05_04_AngelLightning",
    menuName = "Game/Upgrade/Effect/05_04_AngelLightning",
    order = 53)]
public class Upgrade_05_04_AngelLightning : UpgradeEffectSO
{
    [Header("보조무기")]
    [Tooltip("플레이어에 붙일 보조무기 루트 프리팹")]
    public GameObject companionPrefab;

    [Header("타겟 탐색")]
    [Tooltip("번개 타격 대상으로 인식할 최대 거리(미터)")]
    [Min(0.1f)]
    public float acquireRange = 8f;

    [Tooltip("탐색에 사용할 적 레이어 (비어 있으면 Enemy 태그 기준)")]
    public LayerMask enemyLayers;

    [Header("공격 주기")]
    [Tooltip("한 사이클(연쇄 타격 묶음) 시작 간격(초)")]
    [Min(0.05f)]
    public float cycleCooldown = 1.2f;

    [Tooltip("한 사이클에서 타격할 최대 적 수")]
    [Min(1)]
    public int targetsPerCycle = 3;

    [Tooltip("순차 타격 간격(초). 0이면 거의 동시")]
    [Min(0f)]
    public float strikeInterval = 0.12f;

    [Tooltip("사이클 시작 후 첫 타격까지 대기 시간(초)")]
    [Min(0f)]
    public float hitDelay = 0f;

    [Header("공격 연출 (클립 재생 방식)")]
    [Tooltip("사이클 시작 시 재생할 공격 애니메이션 클립")]
    public AnimationClip attackAnimationClip;

    [Header("CC (데미지 없음)")]
    [Tooltip("넉백 이동 세기")]
    [Min(0f)]
    public float knockbackPower = 4f;

    [Tooltip("넉백 지속 시간(초)")]
    [Min(0f)]
    public float knockbackDuration = 0.2f;

    [Tooltip("기본 스턴 시간(초). 최종값은 ±50% 랜덤 후 최소 1초 보정")]
    [Min(0f)]
    public float baseStunDuration = 1f;

    [Header("히트 이펙트 (타겟 루트 부착)")]
    [Tooltip("번개 타격 시 타겟 루트에 붙여 스폰할 이펙트 프리팹")]
    public GameObject hitAttachEffectPrefab;

    [Tooltip("부착 이펙트 자동 제거 시간(초). 0 이하면 자동 제거하지 않음")]
    [Min(0f)]
    public float hitAttachEffectLifetime = 1.5f;
}
