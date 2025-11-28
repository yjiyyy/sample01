using UnityEngine;

[CreateAssetMenu(menuName = "Enemy/MeleeAttackData", fileName = "MeleeAttackData_SO")]
public class MeleeAttackData : ScriptableObject
{
    [Header("공격 기본 정보")]
    public string attackName = "Melee_Attack";
    public GameObject hitBoxPrefab;

    [Header("전투 스탯")]
    public bool grantSuperArmor = false;
    public float damage = 10f;
    public float range = 2f;
    public float cooldown = 1f;

    [Header("지속 시간")]
    [Tooltip("공격 모션 시간. 0 이하이면 컨트롤러 fallback 사용.")]
    public float attackTime = 0f;

    [Header("애니메이션 클립 (선택)")]
    public AnimationClip clip;

    [Header("히트박스 타이밍 및 지속")]
    public float hitboxSpawnDelay = 0f;
    public float hitBoxLifetime = 0.2f;

    [Header("넉백 / 저크")]
    public float knockbackPower = 4f;
    public float knockbackDuration = 0.25f;
    public float stunDuration = 0f;

    [Header("중복 데미지")]
    public bool allowDuplicateHit = false;
    public float duplicateHitInterval = 0.1f;

    // 패턴 홀드 Override 필드 완전 삭제 (이제 defaultPatternHoldDuration 만 사용)

    private void OnValidate()
    {
        hitboxSpawnDelay = Mathf.Max(0f, hitboxSpawnDelay);
        hitBoxLifetime = Mathf.Max(0f, hitBoxLifetime);
        knockbackDuration = Mathf.Max(0f, knockbackDuration);
        stunDuration = Mathf.Max(0f, stunDuration);
        range = Mathf.Max(0f, range);
        cooldown = Mathf.Max(0f, cooldown);
    }
}