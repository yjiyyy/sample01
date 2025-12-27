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

    [Header("Parts System")]
    [Tooltip("파츠 슬롯 개수.  이 값을 변경하면 OnValidate에서 배열 크기를 자동 조정합니다.")]
    public int partSlotCount = 0;
    [Tooltip("각 슬롯에 붙을 본 이름(문자열)과 파츠 프리펩을 설정합니다.")]
    public EnemyPartSlot[] partSlots = new EnemyPartSlot[0];

    [Header("References")]
    public AnimatorOverrideController overrideController = null;

    [Header("Editor only")]
    [Tooltip("If true, EnemyFacade will automatically sync SO -> components on OnValidate.")]
    public bool editorAutoApplyDefault = true;

    private void OnValidate()
    {
        maxHealth = Mathf.Max(0f, maxHealth);
        maxShield = Mathf.Max(0f, maxShield);
        baseMoveSpeed = Mathf.Max(0f, baseMoveSpeed);
        detectionRadius = Mathf.Max(0f, detectionRadius);
        findDuration = Mathf.Max(0f, findDuration);
        backstepDistance = Mathf.Max(0f, backstepDistance);
        backstepSpeedMultiplier = Mathf.Max(0f, backstepSpeedMultiplier);
        forwardSpeedNormalizeTime = Mathf.Max(0.01f, forwardSpeedNormalizeTime);
        roamRadius = Mathf.Max(0f, roamRadius);
        peaceMoveSpeedMultiplier = Mathf.Clamp01(peaceMoveSpeedMultiplier);
        idleMin = Mathf.Max(0f, idleMin);
        idleMax = Mathf.Max(idleMin, idleMax);
        globalPatternCooldown = Mathf.Max(0f, globalPatternCooldown);
        defaultPatternHoldDuration = Mathf.Max(0f, defaultPatternHoldDuration);

        mass = Mathf.Max(0.0001f, mass);

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
    }
}