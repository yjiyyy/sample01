using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Player/PlayerConfig", fileName = "PlayerConfig_SO")]
public class PlayerConfig : ScriptableObject
{
    [Header("General")]
    public string displayName = "Player";
    [Tooltip("인게임 HP HUD에 표시할 캐릭터 초상화")]
    public Sprite portrait;
    public string tagName = "Player";
    public LayerMask layer = 0;

    [Header("Stats")]
    [Tooltip("Base maximum HP")]
    public float maxHealth = 100f;
    [Tooltip("Base maximum stamina (evade gauge)")]
    public float maxStamina = 100f;
    [Tooltip("Stamina recharge per second")]
    public float staminaRechargeRate = 20f;

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

    [Header("Weapon Slots (SO)")]
    [Tooltip("?? ????? ??????? ???? ???(None) SO.")]
    public WeaponDataSO unarmedWeaponData = null;

    [FormerlySerializedAs("defaultWeaponData")]
    [Tooltip("???? ???? 1. ??? ?????? None?? ???????.")]
    public WeaponDataSO weaponSlot0 = null;

    [Tooltip("???? ???? 2. ??? ?????? None?? ???????.")]
    public WeaponDataSO weaponSlot1 = null;

    [Header("Evade / ??? (shared ????)")]
    [Tooltip("??? ???? ???? SO?? ????(???).")]
    public EvadeDataSO evadeData = null;

    [Header("EnemyDetector (applied by PlayerFacade)")]
    [Tooltip("EnemyDetector?? ?????? ?þ? ????(???)")]
    public float detectorViewAngle = 45f;
    [Tooltip("EnemyDetector?? ?????? ??? ???")]
    public float detectorViewDistance = 10f;

    [Header("Editor")]
    [Tooltip("If true, PlayerFacade will automatically sync SO -> components on OnValidate (editor) and Awake (runtime).")]
    public bool editorAutoApplyDefault = true;

    public WeaponDataSO GetUnarmedWeapon()
    {
        if (unarmedWeaponData != null)
            return unarmedWeaponData;
        if (IsUnarmedAsset(weaponSlot0))
            return weaponSlot0;
        if (IsUnarmedAsset(weaponSlot1))
            return weaponSlot1;
        return null;
    }

    public WeaponDataSO GetSlotOrUnarmed(int index)
    {
        WeaponDataSO slot = index == 0 ? weaponSlot0 : weaponSlot1;
        return slot != null ? slot : GetUnarmedWeapon();
    }

    public static bool IsUnarmedAsset(WeaponDataSO so)
    {
        if (so == null)
            return true;
        if (!string.IsNullOrEmpty(so.id) && so.id.IndexOf("None", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        return !string.IsNullOrEmpty(so.name) && so.name.IndexOf("None", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(0f, maxHealth);
        maxStamina = Mathf.Max(1f, maxStamina);
        staminaRechargeRate = Mathf.Max(0f, staminaRechargeRate);

        // mass: enforce safe range to avoid physics instability
        mass = Mathf.Clamp(mass, 0.0001f, 500f);

        baseMoveSpeed = Mathf.Max(0f, baseMoveSpeed);
        rotationSpeedDegPerSec = Mathf.Max(0f, rotationSpeedDegPerSec);
        detectorViewAngle = Mathf.Clamp(detectorViewAngle, 0f, 180f);
        detectorViewDistance = Mathf.Max(0f, detectorViewDistance);
    }
}
