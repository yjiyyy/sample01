using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Melee Combo 단일 스텝 정의(SO)
/// - 애니메이션(클립), 히트박스, 타이밍, 데미지/넉백/스턴/리코일/처치연출 등
/// - Step에 값이 설정되어 있으면 우선 사용하고, 비어 있으면 WeaponDataSO의 기본값을 사용합니다.
/// </summary>
[CreateAssetMenu(menuName = "Player/MeleeComboStep", fileName = "MeleeComboStep_SO")]
public class MeleeComboStepSO : ScriptableObject
{
    [Header("손 (히트 / 트레일)")]
    [Tooltip("이 스텝의 히트(콜라이더·프리팹 스폰)와 트레일을 어느 무기 기준으로 쓸지.\n" +
             "듀얼이 아니면 Sub/Both의 서브 쪽은 메인으로 폴백합니다.\n" +
             "기본 Both: 예전(듀얼 시 양손 콜라이더) 동작에 가깝습니다.")]
    public AttackVariantHandMode comboStepHandMode = AttackVariantHandMode.Both;

    [Header("Animation")]
    [Tooltip("참고용(선택). 실제 재생은 스텝 순서(0,1,2…) → AttackIndex → AOC의 None_Attack 오버라이드입니다.")]
    public AnimationClip animClip;

    [Header("전투 관련")]
    [Tooltip("스텝 자체의 쿨다운 (optional). 음수면 무기 기본값 사용.")]
    public float cooldown = -1f;
    [Tooltip("스텝 자체의 데미지(음수면 무기 기본값 사용)")]
    public float damage = -1f;
    [Tooltip("스텝 자체의 범위(음수면 무기 기본값 사용)")]
    public float range = -1f;
    [Tooltip("스텝별 스테미너 소모. 음수면 무기 기본값 사용.")]
    public float staminaCost = -1f;

    [Header("히트박스 타이밍 및 지속")]
    [Tooltip("히트박스 스폰 지연(초)")]
    public float hitboxSpawnDelay = 0.12f;
    [Tooltip("히트박스 생존시간(초)")]
    public float hitBoxLifetime = 0.15f;
    [Tooltip("이 스텝에서 사용할 히트박스 프리팹 (비어있으면 무기 SO의 meleeHitboxPrefab으로 폴백)")]
    public GameObject hitBoxPrefab;

    [Header("피격 이펙트")]
    [Tooltip("적 피격 시 표면에 스폰할 이펙트. 비어있으면 무기 SO의 hitEffectPrefab 사용.")]
    public GameObject hitEffectPrefab;

    [Header("공격 FX")]
    [Tooltip("이 스텝의 페이즈별 공격 FX. 콤보 단타는 Attack 페이즈를 주로 사용.")]
    public List<AttackFXPhaseSet> attackFXPhases = new List<AttackFXPhaseSet>();

    [Header("무기 트레일 (콤보 스텝)")]
    [Tooltip("스텝 시작 후 트레일 기록 시작까지 지연(초). trailEmitDuration>0일 때만 사용.")]
    public float trailEmitStartDelay = 0f;
    [Tooltip("트레일 기록 유지 시간(초). 0 이하면 이 스텝에서 트레일 없음. 콤보 무기는 스텝 값만 사용.")]
    public float trailEmitDuration = 0f;

    [Header("넉백 / 저크")]
    public float knockbackDuration = -1f;
    public float knockbackPower = -1f;
    public float jerkIntensity = -1f;
    public float jerkDuration = -1f;

    [Header("스턴")]
    public float stunDuration = -1f;

    [Header("Push(밀림) 옵션")]
    public bool usePushInsteadOfKnockback = false;

    [Header("Time Control (Hit Stop)")]
    [Tooltip("피격 대상 홀드 시간(초). 음수면 무기 기본값 사용")]
    public float targetHoldDuration = -1f;
    [Tooltip("공격자 홀드 시간(초). 음수면 무기 기본값 사용")]
    public float attackerHoldDuration = -1f;

    // Legacy split fields kept for compatibility.
    [HideInInspector] public float targetStateHoldDuration = -1f;
    [HideInInspector] public float targetAnimationHoldDuration = -1f;
    [HideInInspector] public float attackerStateHoldDuration = -1f;
    [HideInInspector] public float attackerAnimationHoldDuration = -1f;

    [Header("처치 연출 선택")]
    public DeathMode deathMode = DeathMode.Animation;
    [Header("Ragdoll 임펄스(죽음이 Ragdoll일 때만 사용)")]
    public float ragdollImpulse = -1f;
    public float ragdollUpImpulse = -1f;
    public float ragdollSpinTorque = -1f;
    [Header("Slice(본 분리)")]
    public List<SliceTarget> sliceTargets = new List<SliceTarget>();
    public float sliceImpulse = -1f;

    [Header("리코일 (자기 반동)")]
    [Tooltip("0 이상=지연(초). 음수면 0으로 처리")]
    public float recoilStartDelay = 0f;
    [Tooltip("0이면 리코일 없음. 그 외: 양수=뒤로, 음수=앞으로 (예: -1=앞으로 1)")]
    public float recoilPower = 0f;
    [Tooltip("0 이하면 0으로 처리. 리코일 쓰려면 0 초과로 설정")]
    public float recoilDuration = 0f;

    [Header("중복 히트 옵션")]
    public bool allowDuplicateHit = false;
    public float duplicateInterval = 0.2f;

    [Header("타이밍 - 콤보 제어")]
    [Tooltip("이 스텝 전체 지속시간(초). 이 시간이 지나면 다음 입력 없을 경우 콤보 종료.")]
    public float stepDuration = 0.6f;
    [Tooltip("스텝 시작 후 이 시간동안은 입력을 무시합니다. 이 시간이 지난 이후 ~ stepDuration 사이에 입력이 들어오면 다음 스텝.")]
    public float ignoreTimeAfterInput = 0.15f;

    private void OnValidate()
    {
        stepDuration = Mathf.Max(0.01f, stepDuration);
        ignoreTimeAfterInput = Mathf.Clamp(ignoreTimeAfterInput, 0f, Mathf.Max(0.001f, stepDuration - 0.001f));
        hitboxSpawnDelay = Mathf.Max(0f, hitboxSpawnDelay);
        hitBoxLifetime = Mathf.Max(0.01f, hitBoxLifetime);
        duplicateInterval = Mathf.Max(0.01f, duplicateInterval);

        trailEmitStartDelay = Mathf.Max(0f, trailEmitStartDelay);
        if (trailEmitDuration < 0f)
            trailEmitDuration = 0f;

        // 음수로 남긴 필드는 "미설정" 의미로 유지 (프로그램에서 무기 SO 값으로 폴백)
    }
}