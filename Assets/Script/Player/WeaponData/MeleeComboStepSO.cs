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
    [Header("Animation")]
    [Tooltip("우선 재생할 애니메이션 클립(클립 이름을 재생에 사용).")]
    public AnimationClip animClip;
    // NOTE: 요청에 따라 fallbackStateName 필드는 제거되었습니다.
    // 추후에 상태명 기반 폴백이 필요하면 별도 로직으로 구현하세요.

    [Header("전투 관련")]
    [Tooltip("스텝 자체의 쿨다운 (optional). 음수면 무기 기본값 사용.")]
    public float cooldown = -1f;
    [Tooltip("스텝 자체의 데미지(음수면 무기 기본값 사용)")]
    public float damage = -1f;
    [Tooltip("스텝 자체의 범위(음수면 무기 기본값 사용)")]
    public float range = -1f;

    [Header("히트박스 타이밍 및 지속")]
    [Tooltip("히트박스 스폰 지연(초)")]
    public float hitboxSpawnDelay = 0.12f;
    [Tooltip("히트박스 생존시간(초)")]
    public float hitBoxLifetime = 0.15f;
    [Tooltip("이 스텝에서 사용할 히트박스 프리팹 (비어있으면 무기 SO의 meleeHitboxPrefab으로 폴백)")]
    public GameObject hitBoxPrefab;

    [Header("넉백 / 저크")]
    public float knockbackDuration = -1f;
    public float knockbackPower = -1f;
    public float jerkIntensity = -1f;
    public float jerkDuration = -1f;

    [Header("스턴")]
    public float stunDuration = -1f;

    [Header("Push(밀림) 옵션")]
    public bool usePushInsteadOfKnockback = false;

    [Header("애니메이션 홀드 (히트스탑)")]
    [Tooltip("피격 대상을 잠깐 멈추는 시간(초). 0이면 비활성")]
    public float animationHoldDuration = -1f;

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
    public float recoilStartDelay = -1f;
    public float recoilPower = -1f;
    public float recoilDuration = -1f;

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

        // 음수로 남긴 필드는 "미설정" 의미로 유지 (프로그램에서 무기 SO 값으로 폴백)
    }
}