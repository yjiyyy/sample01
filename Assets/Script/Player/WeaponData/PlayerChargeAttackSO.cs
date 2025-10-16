using UnityEngine;

[CreateAssetMenu(menuName = "Player/ChargeAttack")]
public class PlayerChargeAttackSO : ScriptableObject
{
    [Header("차지 성공 조건")]
    [Tooltip("공격 버튼을 누른(즉발 시점) 후, 이 시간 동안 계속 홀드하면 차지 성공")]
    public float holdSuccessTime = 1.5f;

    [Header("애니메이션")]
    [Tooltip("지정 시 이 클립 이름으로 재생(우선)")]
    public AnimationClip chargedClip;
    [Tooltip("클립이 비어 있으면 이 스테이트 이름으로 재생")]
    public string chargedStateName = "Attack_Charged01";

    [Header("히트박스 프리팹")]
    public GameObject hitBoxPrefab;

    [Header("전투 스탯(차지 전용)")]
    public float damage = 120f;
    public float range = 2.5f;
    public float hitBoxLifetime = 0.15f;

    [Header("넉백/스턴(EnemyImpact에서 SO로 읽음)")]
    public float knockbackPower = 5f;
    public float knockbackDuration = 0.3f;
    public float stunDuration = 0f;

    [Header("발동 무적 (A안: 차지 성공 즉시부터 적용)")]
    public float invincibilityDuration = 0.3f;

    [Header("스폰 포인트")]
    [Tooltip("없으면 플레이어 Transform 기준. 기본은 Root_dummy")]
    public string meleeSpawnPointName = "Root_dummy";

    [Header("히트박스 스폰 딜레이")]
    [Tooltip("차지 성공 후 히트박스 생성까지 대기 시간(초)")]
    public float spawnDelay = 0f;

    [Header("AoE DoT(틱 모드)")]
    [Tooltip("켜면 라이프타임 동안 주기적으로 피해를 줍니다(즉발 1회 타격 없음).")]
    public bool enableAreaDot = false;
    [Tooltip("틱마다 주는 피해량")]
    public float dotDamagePerTick = 10f;
    [Tooltip("틱 주기(초)")]
    public float dotTickInterval = 0.2f;

    private void OnValidate()
    {
        holdSuccessTime = Mathf.Max(0f, holdSuccessTime);
        damage = Mathf.Max(0f, damage);
        range = Mathf.Max(0f, range);
        hitBoxLifetime = Mathf.Max(0.01f, hitBoxLifetime);
        knockbackPower = Mathf.Max(0f, knockbackPower);
        knockbackDuration = Mathf.Max(0f, knockbackDuration);
        stunDuration = Mathf.Max(0f, stunDuration);
        invincibilityDuration = Mathf.Max(0f, invincibilityDuration);
        spawnDelay = Mathf.Max(0f, spawnDelay);
        dotDamagePerTick = Mathf.Max(0f, dotDamagePerTick);
        dotTickInterval = Mathf.Max(0.01f, dotTickInterval);
    }
}