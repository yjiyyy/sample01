using UnityEngine;

[CreateAssetMenu(menuName = "Player/SniperGun")]
public class WeaponDataSO_Sniper : WeaponDataSO_AR
{
    [Header("스나이퍼 조준")]
    [Tooltip("공격 버튼 홀드 후 완전 조준까지 걸리는 시간(초)")]
    public float fullAimTime = 1f;
    [Tooltip("완전 조준 성공 발사 시 데미지 배율 적용 여부")]
    public bool useFullAimDamageMultiplier = true;
    [Tooltip("완전 조준 성공 발사 시 데미지 배율")]
    public float fullAimDamageMultiplier = 2f;
    [Tooltip("완전 조준 성공 발사 시 자동 조준 보정 사용")]
    public bool useFullAimAutoAim = true;

    [Header("스나이퍼 탄환 반사")]
    [Tooltip("벽/경계 반사 레이어")]
    public LayerMask ricochetLayers = ~0;
    [Tooltip("반사 시 속도 배율 (요구사항상 기본 1)")]
    public float ricochetSpeedMultiplier = 1f;

    [Header("조준 레이 시각화")]
    [Tooltip("조준 레이 길이")]
    public float aimRayLength = 12f;
    public float aimRayWidth = 0.03f;
    public Color aimRayColor = new Color(1f, 0.3f, 0.3f, 0.9f);
    [Tooltip("완전 조준 성공 시 조준 레이 색 변경 여부")]
    public bool useFullAimRayColor = true;
    public Color aimRayFullAimColor = new Color(0.3f, 1f, 0.35f, 0.95f);

#if UNITY_EDITOR
    private void OnValidate()
    {
        fullAimTime = Mathf.Max(0.01f, fullAimTime);
        fullAimDamageMultiplier = Mathf.Max(0f, fullAimDamageMultiplier);
        ricochetSpeedMultiplier = Mathf.Max(0f, ricochetSpeedMultiplier);
        aimRayLength = Mathf.Max(0.1f, aimRayLength);
        aimRayWidth = Mathf.Max(0.001f, aimRayWidth);
    }
#endif
}
