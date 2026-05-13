using UnityEngine;

[CreateAssetMenu(
    fileName = "05_05_AngelLottery",
    menuName = "Game/Upgrade/Effect/05_05_AngelLottery",
    order = 55)]
public class Upgrade_05_05_AngelLottery : UpgradeEffectSO
{
    [Header("보조무기")]
    [Tooltip("플레이어에 붙일 보조무기 루트 프리팹 (발사 기준 Transform 포함)")]
    public GameObject companionPrefab;

    [Header("공격 연출 (클립 재생 방식)")]
    [Tooltip("공격 시 재생할 애니메이션 클립")]
    public AnimationClip attackAnimationClip;

    [Tooltip("투사체 스폰까지 대기 시간(초)")]
    [Min(0f)]
    public float projectileSpawnDelay = 0f;

    [Tooltip("스폰 위치 우선: 이 이름의 자식 Transform (비어 있으면 Muzzle -> 루트)")]
    public string firePointChildName = "Fire_Point";

    [Header("발사 주기")]
    [Tooltip("발사 간격(초). 활성 중 반복")]
    [Min(0.05f)]
    public float fireCooldown = 1f;

    [Header("투사체 풀")]
    [Tooltip("던질 아이템 프리팹 목록. 여러 개면 랜덤 선택")]
    public GameObject[] projectilePrefabs;

    [Header("랜덤 투척 목표 (플레이어 주변)")]
    [Tooltip("플레이어 주변 랜덤 목표 반경 (XZ)")]
    [Min(0.1f)]
    public float randomThrowRange = 8f;

    [Tooltip("바닥 판정 레이어")]
    public LayerMask groundLayers = ~0;

    [Tooltip("고저차 대응용 수직 탐색 높이(위/아래)")]
    [Min(1f)]
    public float groundProbeHeight = 20f;

    [Header("아크 공통값")]
    [Tooltip("투사체 수평 속도 상한 (m/s)")]
    [Min(0.1f)]
    public float projectileSpeed = 12f;

    [Tooltip("포물선 최고점 추가 높이 (m)")]
    [Min(0f)]
    public float arcHeight = 1.8f;

    [Tooltip("안전 파괴 시간(초)")]
    [Min(0.2f)]
    public float projectileMaxLifetime = 6f;
}
