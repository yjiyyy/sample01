using UnityEngine;
using System;

/// <summary>
/// 적 파츠 슬롯 정의. 
/// - boneName: 파츠가 붙을 본 이름 (문자열). 예: "Bip001 R Hand"
/// - partPrefab: 생성할 파츠 프리펩 (드래그로 할당).
/// - localOffset/Rotation/Scale: 부착 시 로컬 Transform 조정값.
/// </summary>
[System.Serializable]
public class EnemyPartSlot
{
    [Tooltip("파츠가 붙을 본 이름 (정확히 입력). 예: 'Bip001 R Hand'")]
    public string boneName = "";

    [Tooltip("생성할 파츠 프리펩 (드래그로 할당).")]
    public GameObject partPrefab;

    [Tooltip("부착 후 로컬 위치 오프셋.")]
    public Vector3 localOffset = Vector3.zero;

    [Tooltip("부착 후 로컬 회전(오일러 각도).")]
    public Vector3 localRotationEuler = Vector3.zero;

    [Tooltip("부착 후 로컬 스케일.")]
    public Vector3 localScale = Vector3.one;
}

/// <summary>
/// 몬스터 사망 시 드랍 풀에 등록할 아이템 한 종류 정의.
/// - itemPrefab: 생성할 아이템 프리팹 (드래그로 할당).
/// - dropChance: 이 슬롯에서 이 아이템이 뽑힐 확률 (0~1).
/// 총 드랍 슬롯 수(totalDropCountMin~Max)만큼 확률 체크가 돌아가며, 여러 아이템이 동시에 드랍될 수 있음.
/// </summary>
[System.Serializable]
public class ItemDropEntry
{
    [Tooltip("생성할 아이템 프리팹 (드래그로 할당).")]
    public GameObject itemPrefab;

    [Tooltip("드랍 확률 (0~1). 각 슬롯마다 이 확률로 체크되어, 통과하면 드랍.")]
    [Range(0f, 1f)]
    public float dropChance = 0.5f;
}

/// <summary>
/// 전투 추적(이동) 시 플레이어 위치를 얼마나 자주 갱신할지.
/// 발견·공격 판정은 항상 실시간 위치를 사용한다.
/// </summary>
public enum EnemyChaseTrackingMode
{
    Realtime = 0,
    OneSecond = 1,
    TwoSeconds = 2,
    ThreeSeconds = 3,
}

[CreateAssetMenu(menuName = "Enemy/EnemyConfig", fileName = "EnemyConfig_SO")]
public class EnemyConfig : ScriptableObject
{
    [Header("General")]
    public string displayName = "NewEnemy";
    public string tagName = "Enemy";
    public LayerMask layer = 0;

    [Header("Stats")]
    public float maxHealth = 100f;

    [Tooltip("Mass multiplier applied to ragdoll rigidbodies and used to scale knockback distance (1 = default).")]
    public float mass = 1f;

    [Header("Shield / Health (optional)")]
    [Tooltip("Enable shield behavior on this enemy (if EnemyHealth supports shields).")]
    public bool useShield = false;
    [Tooltip("Maximum shield value (if useShield=true).")]
    public float maxShield = 50f;
    [Tooltip("Duration the shield remains broken (seconds) when broken).")]
    public float shieldBreakDuration = 2f;
    [Tooltip("Delay (seconds) before shield begins to recharge after taking damage).")]
    public float shieldRechargeDelay = 3f;
    [Tooltip("Shield recharge rate (points per second). If 0 => no recharge.")]
    public float shieldRechargeRate = 10f;

    [Header("Movement / AI")]
    [Tooltip("Base move speed (m/s) used by Enemy. moveSpeed.")]
    public float baseMoveSpeed = 3.5f;
    [Tooltip("MovementSettings asset to assign to Enemy/movement components.")]
    public MovementSettings movementSettings = null;

    [Tooltip("Distance to detect player and trigger finding/aggro (units).")]
    public float detectionRadius = 5f;

    [Tooltip("Find animation duration (sec) - AI will play find then transition to combat.")]
    public float findDuration = 2f;

    [Header("AI - detailed tuning")]
    [Tooltip("Target distance for backstep behaviour (center of band).")]
    public float backstepDistance = 5f;
    [Tooltip("Backstep speed multiplier (1.0 = base move speed).")]
    public float backstepSpeedMultiplier = 1.0f;
    [Tooltip("쿨타임(거리 유지) 중 너무 가까울 때 백스텝을 고를 확률. 1=항상 백스텝, 0=항상 제자리. 쿨타임 진입 시 1회만 결정.")]
    [Range(0f, 1f)]
    public float cooldownBackstepChance = 1f;
    [Tooltip("Forward speed normalization time used by EnemyAI (seconds).")]
    [Range(0.05f, 2f)]
    public float forwardSpeedNormalizeTime = 0.25f;
    [Tooltip("Roam radius around spawn when in Peace mode. ")]
    public float roamRadius = 3f;
    [Tooltip("Peace mode movement speed multiplier (percentage of baseMoveSpeed).")]
    [Range(0.05f, 1f)]
    public float peaceMoveSpeedMultiplier = 0.6f;
    [Tooltip("Idle wait time minimum in Peace mode. ")]
    public float idleMin = 1f;
    [Tooltip("Idle wait time maximum in Peace mode.")]
    public float idleMax = 3f;

    [Tooltip("전투 추적 이동 시 플레이어 위치 갱신 주기. 발견·공격은 실시간.")]
    public EnemyChaseTrackingMode chaseTrackingMode = EnemyChaseTrackingMode.ThreeSeconds;

    [Header("Combat")]

    [Header("Attack patterns (EnemyAttackController)")]
    [Tooltip("Array of attack pattern ScriptableObjects (MeleeAttackData/RushAttackData/RangedAttackData).")]
    public ScriptableObject[] attackPatterns = null;
    [Tooltip("Global cooldown applied after successful attack (seconds).")]
    public float globalPatternCooldown = 0.35f;
    [Tooltip("Default hold duration for selected pattern (seconds). Per-pattern override is supported if pattern SO has 'holdOverride' field).")]
    public float defaultPatternHoldDuration = 1.0f;
    [Tooltip("If true, EnemyAttackController will honor per-pattern holdOverride field when present.")]
    public bool enablePerPatternHoldOverride = true;

    [Header("Item Drop")]
    [Tooltip("드랍할 총 아이템 개수 최소값. totalDropCountMin~Max 사이 랜덤.")]
    public int totalDropCountMin = 1;
    [Tooltip("드랍할 총 아이템 개수 최대값 (슬롯 수). 각 슬롯마다 등록된 아이템들이 확률 체크됨.")]
    public int totalDropCountMax = 3;
    [Tooltip("드랍 풀에 등록할 아이템 목록. (프리팹, 드랍 확률) 쌍. 추가 가능.")]
    public ItemDropEntry[] dropEntries = new ItemDropEntry[0];
    [Tooltip("착지 레이캐스트용 지면 레이어. 비탈/계단 포함. 0이면 DefaultRaycastLayers 사용.")]
    public LayerMask dropGroundLayerMask = 0;

    [Header("Appearance Pool (스폰 시 균등 랜덤)")]
    [Tooltip("공유 골격 바디 프리팹 목록. 스포너는 이 중에서 하나를 골라 Instantiate합니다. 비어 있으면 스폰 불가.")]
    public GameObject[] bodyPrefabs = new GameObject[0];

    [Tooltip("Head 파츠 후보. 비어 있으면 바디 프리팹에 설정된 Head를 그대로 씁니다.")]
    public GameObject[] headPrefabs = new GameObject[0];

    [Tooltip("Hair 파츠 후보. 비어 있으면 바디 프리팹에 설정된 Hair를 그대로 씁니다.")]
    public GameObject[] hairPrefabs = new GameObject[0];

    [Tooltip("피부색 후보. 비어 있으면 바디 프리팹에 설정된 피부색을 그대로 씁니다.")]
    public Color[] skinColors = new Color[0];

    [Header("Parts System")]
    [Tooltip("비무기 파츠 슬롯 개수(장식 등). 무기는 Attack SO의 Weapon Parts에 설정합니다.")]
    public int partSlotCount = 0;
    [Tooltip("각 슬롯에 붙을 본 이름(문자열)과 파츠 프리펩을 설정합니다.")]
    public EnemyPartSlot[] partSlots = new EnemyPartSlot[0];

    [Header("References")]
    public AnimatorOverrideController overrideController = null;

    [Header("Spawn Intro")]
    [Tooltip("스폰 시작 시 Spawn 상태에 재생할 클립. 비어 있으면 Animator 기본 Spawn 모션 사용.")]
    public AnimationClip spawnAnimationClip = null;
    [Tooltip("스폰 시작 시 재생할 이펙트 프리팹. 비어 있으면 이펙트 없음.")]
    public GameObject spawnEffectPrefab = null;

    [Header("Editor only")]
    [Tooltip("If true, EnemyFacade will automatically sync SO -> components on OnValidate.")]
    public bool editorAutoApplyDefault = true;

    /// <summary>외형 풀에서 바디 프리팹을 균등 랜덤으로 고릅니다.</summary>
    public bool TryPickBodyPrefab(out GameObject bodyPrefab)
    {
        bodyPrefab = PickRandomPrefab(bodyPrefabs);
        return bodyPrefab != null;
    }

    /// <summary>
    /// 인스턴스의 EnemyBodyPartSlots에 Head/Hair/피부색을 풀에서 골라 넣습니다.
    /// Start의 파츠 부착보다 먼저 호출해야 합니다.
    /// </summary>
    public void ApplyRandomAppearance(GameObject enemyInstance)
    {
        if (enemyInstance == null)
            return;

        EnemyBodyPartSlots slots = enemyInstance.GetComponentInChildren<EnemyBodyPartSlots>(true);
        if (slots == null)
            return;

        GameObject head = PickRandomPrefab(headPrefabs);
        if (head != null)
            slots.headPartPrefab = head;

        GameObject hair = PickRandomPrefab(hairPrefabs);
        if (hair != null)
            slots.hairPartPrefab = hair;

        if (TryPickSkinColor(out Color skin))
            slots.bodySkinColor = skin;
    }

    /// <summary>배열에서 null이 아닌 항목만 모아 균등 랜덤으로 고릅니다.</summary>
    public static GameObject PickRandomPrefab(GameObject[] prefabs)
    {
        if (prefabs == null || prefabs.Length == 0)
            return null;

        int validCount = 0;
        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] != null)
                validCount++;
        }

        if (validCount == 0)
            return null;

        int pick = UnityEngine.Random.Range(0, validCount);
        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] == null)
                continue;
            if (pick == 0)
                return prefabs[i];
            pick--;
        }

        return null;
    }

    private bool TryPickSkinColor(out Color color)
    {
        color = default;
        if (skinColors == null || skinColors.Length == 0)
            return false;

        color = skinColors[UnityEngine.Random.Range(0, skinColors.Length)];
        return true;
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(0f, maxHealth);
        maxShield = Mathf.Max(0f, maxShield);
        baseMoveSpeed = Mathf.Max(0f, baseMoveSpeed);
        detectionRadius = Mathf.Max(0f, detectionRadius);
        findDuration = Mathf.Max(0f, findDuration);
        backstepDistance = Mathf.Max(0f, backstepDistance);
        backstepSpeedMultiplier = Mathf.Max(0f, backstepSpeedMultiplier);
        cooldownBackstepChance = Mathf.Clamp01(cooldownBackstepChance);
        forwardSpeedNormalizeTime = Mathf.Max(0.01f, forwardSpeedNormalizeTime);
        roamRadius = Mathf.Max(0f, roamRadius);
        peaceMoveSpeedMultiplier = Mathf.Clamp01(peaceMoveSpeedMultiplier);
        idleMin = Mathf.Max(0f, idleMin);
        idleMax = Mathf.Max(idleMin, idleMax);
        globalPatternCooldown = Mathf.Max(0f, globalPatternCooldown);
        defaultPatternHoldDuration = Mathf.Max(0f, defaultPatternHoldDuration);

        mass = Mathf.Max(0.0001f, mass);

        totalDropCountMin = Mathf.Max(0, totalDropCountMin);
        totalDropCountMax = Mathf.Max(totalDropCountMin, totalDropCountMax);

        // Parts System:  partSlotCount 변경 시 배열 크기 자동 조정
        partSlotCount = Mathf.Max(0, partSlotCount);
        if (partSlots == null || partSlots.Length != partSlotCount)
        {
            Array.Resize(ref partSlots, partSlotCount);
            for (int i = 0; i < partSlots.Length; i++)
            {
                if (partSlots[i] == null)
                    partSlots[i] = new EnemyPartSlot();
            }
        }

        if (partSlots != null)
        {
            for (int i = 0; i < partSlots.Length; i++)
            {
                if (partSlots[i] == null)
                    partSlots[i] = new EnemyPartSlot();
            }
        }
    }
}