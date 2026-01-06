using UnityEngine;

[CreateAssetMenu(menuName = "Player/PlayerConfig", fileName = "PlayerConfig_SO")]
public class PlayerConfig : ScriptableObject
{
    [Header("General")]
    public string displayName = "Player";
    public string tagName = "Player";
    public LayerMask layer = 0;

    [Header("Stats")]
    [Tooltip("Base maximum HP")]
    public float maxHealth = 100f;

    [Tooltip("Mass value (kg). If useAbsoluteMass = true, this is used as Rigidbody.mass directly. If false, this acts as a multiplier on original mass.")]
    public float mass = 1f;

    [Tooltip("If true, PlayerFacade will set Rigidbody.mass = mass (absolute). If false, Rigidbody.mass = originalMass * mass (multiplier).")]
    public bool useAbsoluteMass = true;

    [Header("Movement")]
    [Tooltip("Base move speed (m/s)")]
    public float baseMoveSpeed = 10f;
    [Tooltip("Rotation speed in degrees per second")]
    public float rotationSpeedDegPerSec = 720f;
    [Tooltip("Optional MovementSettings asset")]
    public MovementSettings movementSettings = null;
    [Tooltip("If true, stop when no input (maps to PlayerMovement.stopWhenNoInput)")]
    public bool stopWhenNoInput = true;

    [Header("Animation")]
    public AnimatorOverrideController overrideController = null;

    [Header("Default Weapon (SO)")]
    [Tooltip("기본 무기(공격) 데이터 SO.\n이 SO의 weaponPrefab이 실제 장착 프리팹으로 사용됩니다.")]
    public WeaponDataSO defaultWeaponData = null;

    [Header("Evade / 회피 (shared 설정)")]
    [Tooltip("회피 동작 설정 SO를 지정(옵션).")]
    public EvadeDataSO evadeData = null;

    [Header("EnemyDetector (applied by PlayerFacade)")]
    [Tooltip("EnemyDetector에 적용할 시야 각도(반각)")]
    public float detectorViewAngle = 45f;
    [Tooltip("EnemyDetector에 적용할 탐지 거리")]
    public float detectorViewDistance = 10f;

    [Header("Editor")]
    [Tooltip("If true, PlayerFacade will automatically sync SO -> components on OnValidate (editor) and Awake (runtime).")]
    public bool editorAutoApplyDefault = true;

    private void OnValidate()
    {
        maxHealth = Mathf.Max(0f, maxHealth);

        // mass: enforce safe range to avoid physics instability
        mass = Mathf.Clamp(mass, 0.0001f, 500f);

        baseMoveSpeed = Mathf.Max(0f, baseMoveSpeed);
        rotationSpeedDegPerSec = Mathf.Max(0f, rotationSpeedDegPerSec);
        detectorViewAngle = Mathf.Clamp(detectorViewAngle, 0f, 180f);
        detectorViewDistance = Mathf.Max(0f, detectorViewDistance);
    }
}