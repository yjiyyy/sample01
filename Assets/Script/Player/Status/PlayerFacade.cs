using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class PlayerFacade : MonoBehaviour
{
    [Header("Core")]
    public PlayerConfig config;

    [Tooltip("If true, sync from SO to components automatically in OnValidate() (editor) and Awake() (runtime).")]
    public bool autoSync = true;

    [Header("Optional - override individual components")]
    public GameObject targetPlayer; // optional reference to player root GameObject, if null use this.gameObject
    public Component[] extraTargets; // optional extra components to sync

    // internal: store last-applied config to avoid repeated work at runtime
    private PlayerConfig appliedConfig;
    private bool appliedOnce = false;

    // store original masses to avoid repeated multiplication / allow restore
    private Dictionary<Rigidbody, float> originalMasses = new Dictionary<Rigidbody, float>();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying && autoSync && config != null)
            ApplyToComponents();
    }
#endif

    private void Awake()
    {
        appliedOnce = false;
        if (config != null && autoSync)
            ApplyToComponents();
    }

    private void Start()
    {
        if (Application.isPlaying && autoSync && config != null)
        {
            if (!appliedOnce || appliedConfig != config)
                ApplyToComponents();
        }
    }

    public void ApplyToComponents()
    {
        if (config == null)
        {
            Debug.LogWarning("[PlayerFacade] No PlayerConfig assigned.");
            return;
        }

        appliedConfig = config;

        GameObject root = targetPlayer != null ? targetPlayer : this.gameObject;

        // Tag / Layer
        try
        {
            if (!string.IsNullOrEmpty(config.tagName) && root.tag != config.tagName)
            {
#if UNITY_EDITOR
                var tags = UnityEditorInternal.InternalEditorUtility.tags;
                if (Array.IndexOf(tags, config.tagName) >= 0)
                {
                    root.tag = config.tagName;
                }
#else
                root.tag = config.tagName;
#endif
            }
        }
        catch (Exception) { }

        if (config.layer != 0)
        {
            int layerIndex = GetFirstLayerIndex(config.layer);
            if (layerIndex >= 0 && root.layer != layerIndex)
                root.layer = layerIndex;
        }

        // 1) Rigidbody mass scaling (preserve originals)
        var rbs = root.GetComponentsInChildren<Rigidbody>(true);
        foreach (var rb in rbs)
        {
            if (rb == null) continue;

            // store original mass if not yet stored
            if (!originalMasses.ContainsKey(rb)) originalMasses[rb] = rb.mass;
            float orig = originalMasses[rb];

            if (config.useAbsoluteMass)
            {
                // Apply absolute mass (clamped)
                float applied = Mathf.Clamp(config.mass, 0.0001f, 500f);
                rb.mass = applied;
            }
            else
            {
                // multiplier mode (backwards compatible)
                rb.mass = Mathf.Max(0.0001f, orig * config.mass);
            }
        }

        // 2) PlayerMovement
        var pm = root.GetComponentInChildren<PlayerMovement>();
        if (pm != null)
        {
#if UNITY_EDITOR
            TrySetSerializedFloat(pm, "baseMoveSpeed", config.baseMoveSpeed);
            TrySetSerializedFloat(pm, "rotationSpeedDegPerSec", config.rotationSpeedDegPerSec);
            TrySetSerializedObjectField(pm, "movementSettings", config.movementSettings);
            TrySetSerializedFloat(pm, "stopWhenNoInput", config.stopWhenNoInput ? 1f : 0f);
#else
            TrySetPublicPropertyOrField(pm, "baseMoveSpeed", config.baseMoveSpeed);
            TrySetPublicPropertyOrField(pm, "rotationSpeedDegPerSec", config.rotationSpeedDegPerSec);
            TrySetPublicPropertyOrField(pm, "movementSettings", config.movementSettings);
            TrySetPublicPropertyOrField(pm, "stopWhenNoInput", config.stopWhenNoInput);
#endif
        }

        // 3) PlayerHealth
        var ph = root.GetComponentInChildren<PlayerHealth>();
        if (ph != null)
        {
#if UNITY_EDITOR
            TrySetSerializedFloat(ph, "maxHP", config.maxHealth);
            TrySetSerializedFloat(ph, "weight", config.mass); // map mass -> weight as legacy mapping
#else
            TrySetPublicPropertyOrField(ph, "maxHP", config.maxHealth);
            TrySetPublicPropertyOrField(ph, "weight", config.mass);
#endif
            var stats = ph.GetComponent<PlayerStats>() ?? ph.gameObject.AddComponent<PlayerStats>();
            stats.InitializeFromConfig(config);

            // Ensure currentHP matches new max immediately
            TryInvokeMethodIfExists(ph, new string[] { "SetHealth", "SetHP", "SetCurrentHP", "SetCurrentHp", "SetHp" }, new object[] { config.maxHealth });
        }
        else
        {
            var statsRoot = root.GetComponent<PlayerStats>() ?? root.AddComponent<PlayerStats>();
            statsRoot.InitializeFromConfig(config);
        }

        // 4) EnemyDetector - apply view settings if present on player
        var detector = root.GetComponentInChildren<EnemyDetector>();
        if (detector != null)
        {
            detector.viewAngle = config.detectorViewAngle;
            detector.viewDistance = config.detectorViewDistance;
        }

        // 5) PlayerWeaponController & PlayerEquipmentController - apply default weapon and evadeData
        var pwc = root.GetComponentInChildren<PlayerWeaponController>();
        var pec = root.GetComponentInChildren<PlayerEquipmentController>();

        if (pwc != null)
        {
            pwc.ApplyEvadeData(config.evadeData);
        }

        if (pec != null)
        {
            pec.ConfigureLoadout(config.GetSlotOrUnarmed(0), config.GetSlotOrUnarmed(1), config.GetUnarmedWeapon());

            if (Application.isPlaying)
                pec.EquipActive(root.transform);
        }

        // 6) Animator override
        var animator = root.GetComponentInChildren<Animator>();
        if (animator != null && config.overrideController != null)
        {
#if UNITY_EDITOR
            TrySetSerializedObjectField(animator, "runtimeAnimatorController", config.overrideController);
#else
            animator.runtimeAnimatorController = config.overrideController;
#endif
        }

        // 7) Extra targets
        if (extraTargets != null)
        {
            foreach (var c in extraTargets)
            {
                if (c == null) continue;
                TryApplyCommonFieldsToComponent(c, config);
            }
        }

        appliedOnce = true;
        if (Application.isPlaying)
            Debug.Log($"[PlayerFacade] Applied config '{config.displayName}' to '{root.name}'. (massMode={(config.useAbsoluteMass ? "absolute" : "multiplier")}, mass={config.mass})");
#if UNITY_EDITOR
        else
            Debug.Log($"[PlayerFacade] (Editor) Applied config '{config.displayName}' to '{root.name}'. (massMode={(config.useAbsoluteMass ? "absolute" : "multiplier")}, mass={config.mass})");
#endif
    }

    // ---------- Helper methods ----------
    private int GetFirstLayerIndex(LayerMask mask)
    {
        int v = mask;
        for (int i = 0; i < 32; ++i)
            if ((v & (1 << i)) != 0) return i;
        return -1;
    }

    private bool TrySetSerializedFloat(object compObj, string fieldName, float value)
    {
#if UNITY_EDITOR
        if (compObj == null) return false;
        UnityEngine.Object comp = compObj as UnityEngine.Object;
        if (comp == null) return false;

        var so = new SerializedObject(comp);
        var prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            prop = so.FindProperty("m_" + fieldName);
            if (prop == null)
                return false;
        }

        if (prop.propertyType == SerializedPropertyType.Float)
        {
            prop.floatValue = value;
            so.ApplyModifiedProperties();
            return true;
        }
        else if (prop.propertyType == SerializedPropertyType.Integer)
        {
            prop.intValue = Mathf.RoundToInt(value);
            so.ApplyModifiedProperties();
            return true;
        }
#endif
        return false;
    }

    private bool TrySetSerializedObjectField(UnityEngine.Object targetObject, string fieldName, UnityEngine.Object value)
    {
#if UNITY_EDITOR
        if (targetObject == null) return false;
        var so = new SerializedObject(targetObject);
        var prop = so.FindProperty(fieldName);
        if (prop == null)
            prop = so.FindProperty("m_" + fieldName);
        if (prop == null) return false;
        if (prop.propertyType == SerializedPropertyType.ObjectReference)
        {
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
            return true;
        }
#endif
        return false;
    }

    private void TryApplyCommonFieldsToComponent(Component comp, PlayerConfig cfg)
    {
#if UNITY_EDITOR
        if (comp == null || cfg == null) return;
        var so = new SerializedObject(comp);

        var attempts = new Dictionary<string, object>()
        {
            {"maxHP", cfg.maxHealth},
            {"baseMoveSpeed", cfg.baseMoveSpeed},
        };

        foreach (var kv in attempts)
        {
            var key = kv.Key;
            var val = kv.Value;
            var prop = so.FindProperty(key) ?? so.FindProperty("m_" + key);
            if (prop == null) continue;

            if (val is float f && (prop.propertyType == SerializedPropertyType.Float || prop.propertyType == SerializedPropertyType.Integer))
            {
                if (prop.propertyType == SerializedPropertyType.Float) prop.floatValue = f;
                else prop.intValue = Mathf.RoundToInt(f);
            }
            else if (val is string s && prop.propertyType == SerializedPropertyType.String)
            {
                prop.stringValue = s;
            }
        }
        so.ApplyModifiedProperties();
#endif
    }

    private bool TryInvokeMethodIfExists(object target, string[] candidateMethodNames, object[] args)
    {
        if (target == null || candidateMethodNames == null) return false;
        Type t = target.GetType();
        foreach (var name in candidateMethodNames)
        {
            var mi = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null) continue;
            try
            {
                var pars = mi.GetParameters();
                if ((args == null && pars.Length == 0) || (args != null && pars.Length == args.Length))
                {
                    mi.Invoke(target, args);
                    return true;
                }
            }
            catch (Exception) { }
        }
        return false;
    }

    private bool TrySetPublicPropertyOrField(object target, string name, object value)
    {
        if (target == null || string.IsNullOrEmpty(name)) return false;
        Type t = target.GetType();

        var pi = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (pi != null && pi.CanWrite)
        {
            try { pi.SetValue(target, Convert.ChangeType(value, pi.PropertyType)); return true; }
            catch { }
        }

        var fi = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (fi != null)
        {
            try { fi.SetValue(target, Convert.ChangeType(value, fi.FieldType)); return true; }
            catch { }
        }

        return false;
    }

    private ComponentWithName FindComponentInChildrenByTypeName(string typeName)
    {
        var comps = GetComponentsInChildren<Component>(true);
        foreach (var c in comps)
        {
            if (c == null) continue;
            if (c.GetType().Name == typeName)
                return new ComponentWithName { component = c };
        }
        return null;
    }

    private Component FindComponentByTypeName(string typeName)
    {
        var comps = GetComponents<Component>();
        foreach (var c in comps)
        {
            if (c == null) continue;
            if (c.GetType().Name == typeName) return c;
        }
        return null;
    }

    private class ComponentWithName
    {
        public Component component;
    }
}