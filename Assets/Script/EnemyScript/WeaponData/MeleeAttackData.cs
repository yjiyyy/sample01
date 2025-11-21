using UnityEngine;

[CreateAssetMenu(fileName = "MeleeAttack", menuName = "Enemy/Attack/MeleeAttackData")]
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
    [Tooltip("여기에 지정하면 실제 클립 길이를 정확히 사용할 수 있습니다. 비워두면 Animator에서 attackName과 같은 이름의 클립을 탐색합니다.")]
    public AnimationClip clip;

    [Header("넉백")]
    public float knockbackPower = 5f;
    public float knockbackDuration = 0.2f;
    public float stunDuration = 0f;

    [Header("히트박스")]
    [Tooltip("공격 시작 후 히트박스를 생성하기까지의 지연 시간(초). 0이면 즉시 생성")]
    public float hitboxSpawnDelay = 0f;
    public float hitBoxLifetime = 0.1f;

    [Header("중복 데미지")]
    public bool allowDuplicateHit = false;
    public float duplicateHitInterval = 0.1f;

    [Header("패턴 홀드 Override")]
    public float holdOverride = -1f;

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