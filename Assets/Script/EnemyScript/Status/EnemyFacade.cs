using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class EnemyFacade : MonoBehaviour
{
    [Header("Core")]
    public EnemyConfig config;

    [Tooltip("If true, sync from SO to components automatically in OnValidate() (editor) and Awake() (runtime).")]
    public bool autoSync = true;

    [Header("Optional - override individual components")]
    public Enemy targetEnemy; // optional reference, if null GetComponent will be used
    public Component[] extraTargets; // optional extra components to sync (inspector convenience)

    // internal: store last-applied config to avoid repeated work at runtime
    private EnemyConfig appliedConfig;
    private bool appliedOnce = false;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying && autoSync)
        {
            ApplyToComponents();
        }
    }
#endif

    private void Awake()
    {
        // runtime one-time sync for safety (also we will re-try in Start to avoid Awake ordering issues)
        appliedOnce = false;
        if (config != null && autoSync)
            ApplyToComponents();
    }

    private void Start()
    {
        // Re-apply in Start as a safety for components that initialize in Awake/OnEnable after facades Awake
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
            Debug.LogWarning("[EnemyFacade] No EnemyConfig assigned.");
            return;
        }

        appliedConfig = config;

        // 1) Tag / Layer
        try
        {
            if (!string.IsNullOrEmpty(config.tagName) && gameObject.tag != config.tagName)
            {
#if UNITY_EDITOR
                var tags = UnityEditorInternal.InternalEditorUtility.tags;
                if (Array.IndexOf(tags, config.tagName) >= 0)
                {
                    gameObject.tag = config.tagName;
                }
#else
                gameObject.tag = config.tagName;
#endif
            }
        }
        catch (Exception) { }

        if (config.layer != 0)
        {
            int layerIndex = GetFirstLayerIndex(config.layer);
            if (layerIndex >= 0 && gameObject.layer != layerIndex)
                gameObject.layer = layerIndex;
        }

        // 2) Enemy component
        Enemy enemy = targetEnemy != null ? targetEnemy : GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.moveSpeed = config.baseMoveSpeed;

            if (config.movementSettings != null)
            {
#if UNITY_EDITOR
                TrySetSerializedObjectField(enemy, "movementSettings", config.movementSettings);
#else
                TrySetPublicPropertyOrField(enemy, "movementSettings", config.movementSettings);
                var f = enemy.GetType().GetField("movementSettings", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (f != null) f.SetValue(enemy, config.movementSettings);
#endif
            }
        }
        else
        {
            Debug.LogWarning($"[EnemyFacade] Enemy component not found on '{gameObject.name}'.");
        }

        // 3) EnemyHealth - map health & shield fields
        var healthWrapper = FindComponentInChildrenByTypeName("EnemyHealth");
        if (healthWrapper != null && healthWrapper.component != null)
        {
            var healthComp = healthWrapper.component;
            var concrete = healthComp as EnemyHealth;
            bool applied = false;

            if (concrete != null)
            {
#if UNITY_EDITOR
                applied = TrySetSerializedFloat(concrete, "maxHP", config.maxHealth) || applied;
                applied = TrySetSerializedFloat(concrete, "maxShield", config.maxShield) || applied;
                applied = TrySetSerializedFloat(concrete, "shieldBreakDuration", config.shieldBreakDuration) || applied;
                applied = TrySetSerializedFloat(concrete, "shieldRechargeDelay", config.shieldRechargeDelay) || applied;
                applied = TrySetSerializedFloat(concrete, "useShield", config.useShield ? 1f : 0f) || applied;
#endif
                try
                {
                    concrete.maxHP = config.maxHealth;
                    concrete.useShield = config.useShield;
                    concrete.maxShield = config.maxShield;
                    concrete.shieldBreakDuration = config.shieldBreakDuration;
                    concrete.shieldRechargeDelay = config.shieldRechargeDelay;
                    applied = true;

                    TryInvokeMethodIfExists(concrete, new string[] { "SetHealth", "SetHP", "SetCurrentHP", "SetCurrentHp", "SetHp" }, new object[] { config.maxHealth });
                    TrySetPublicPropertyOrField(concrete, "shieldRechargeRate", config.shieldRechargeRate);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[EnemyFacade] Failed to apply EnemyHealth runtime values: {ex.Message}");
                }
            }
            else
            {
                applied = TrySetSerializedFloat(healthComp, "maxHealth", config.maxHealth)
                       || TrySetSerializedFloat(healthComp, "maxHp", config.maxHealth)
                       || TrySetSerializedFloat(healthComp, "hpMax", config.maxHealth)
                       || TrySetSerializedFloat(healthComp, "maxHP", config.maxHealth);

#if !UNITY_EDITOR
                if (!applied)
                {
                    applied = TrySetPublicPropertyOrField(healthComp, "maxHP", config.maxHealth)
                           || TrySetPublicPropertyOrField(healthComp, "maxHealth", config.maxHealth);
                }
#endif
            }

            if (!applied)
                Debug.LogWarning($"[EnemyFacade] EnemyHealth present but no known health/shield field found on '{healthComp.GetType().Name}'.");
        }

        // 4) EnemyAI - apply AI tuning fields
        var aiComp = FindComponentByTypeName("EnemyAI");
        if (aiComp != null)
        {
#if UNITY_EDITOR
            bool ok = false;
            ok |= TrySetSerializedFloat(aiComp, "backstepDistance", config.backstepDistance);
            ok |= TrySetSerializedFloat(aiComp, "backstepSpeedMultiplier", config.backstepSpeedMultiplier);
            ok |= TrySetSerializedFloat(aiComp, "forwardSpeedNormalizeTime", config.forwardSpeedNormalizeTime);
            ok |= TrySetSerializedFloat(aiComp, "detectionRadius", config.detectionRadius);
            ok |= TrySetSerializedFloat(aiComp, "findDuration", config.findDuration);
            ok |= TrySetSerializedFloat(aiComp, "roamRadius", config.roamRadius);
            ok |= TrySetSerializedFloat(aiComp, "peaceMoveSpeedMultiplier", config.peaceMoveSpeedMultiplier);
            ok |= TrySetSerializedFloat(aiComp, "idleMin", config.idleMin);
            ok |= TrySetSerializedFloat(aiComp, "idleMax", config.idleMax);
            if (!ok)
                Debug.LogWarning($"[EnemyFacade] EnemyAI present but some AI fields were not found on '{aiComp.GetType().Name}'.");
#else
            TrySetPublicPropertyOrField(aiComp, "backstepDistance", config.backstepDistance);
            TrySetPublicPropertyOrField(aiComp, "backstepSpeedMultiplier", config.backstepSpeedMultiplier);
            TrySetPublicPropertyOrField(aiComp, "forwardSpeedNormalizeTime", config.forwardSpeedNormalizeTime);
            TrySetPublicPropertyOrField(aiComp, "detectionRadius", config.detectionRadius);
            TrySetPublicPropertyOrField(aiComp, "findDuration", config.findDuration);
            TrySetPublicPropertyOrField(aiComp, "roamRadius", config.roamRadius);
            TrySetPublicPropertyOrField(aiComp, "peaceMoveSpeedMultiplier", config.peaceMoveSpeedMultiplier);
            TrySetPublicPropertyOrField(aiComp, "idleMin", config.idleMin);
            TrySetPublicPropertyOrField(aiComp, "idleMax", config.idleMax);
#endif
        }

        // 5) EnemyImpact - kept best-effort but no global config knockback fields now
        var impactComp = GetComponent<EnemyImpact>();
        if (impactComp != null)
        {
            TrySetSerializedFloat(impactComp, "softKnockRatio", config.shieldRechargeRate); // No direct mapping; kept minimal
        }

        // 6) EnemyAttackController - patterns, global cooldown, hold defaults
        var attackComp = FindComponentByTypeName("EnemyAttackController");
        if (attackComp != null)
        {
#if UNITY_EDITOR
            var so = new SerializedObject((UnityEngine.Object)attackComp);
            var arr = so.FindProperty("attackPatterns") ?? so.FindProperty("m_attackPatterns");
            if (arr != null && config.attackPatterns != null)
            {
                arr.arraySize = config.attackPatterns.Length;
                for (int i = 0; i < arr.arraySize; ++i)
                    arr.GetArrayElementAtIndex(i).objectReferenceValue = config.attackPatterns[i];
            }

            var p1 = so.FindProperty("defaultPatternHoldDuration") ?? so.FindProperty("m_defaultPatternHoldDuration");
            if (p1 != null) p1.floatValue = config.defaultPatternHoldDuration;

            var p2 = so.FindProperty("enablePerPatternHoldOverride") ?? so.FindProperty("m_enablePerPatternHoldOverride");
            if (p2 != null) p2.boolValue = config.enablePerPatternHoldOverride;

            var p3 = so.FindProperty("글로벌쿨타임") ?? so.FindProperty("globalReadyTime") ?? so.FindProperty("m_글로벌쿨타임");
            if (p3 != null && p3.propertyType == SerializedPropertyType.Float)
                p3.floatValue = config.globalPatternCooldown;

            so.ApplyModifiedProperties();
#else
            try
            {
                var ac = attackComp as dynamic;
                try { ac.attackPatterns = config.attackPatterns; } catch { }
                TrySetPublicPropertyOrField(attackComp, "defaultPatternHoldDuration", config.defaultPatternHoldDuration);
                TrySetPublicPropertyOrField(attackComp, "enablePerPatternHoldOverride", config.enablePerPatternHoldOverride);
                TrySetPublicPropertyOrField(attackComp, "글로벌쿨타임", config.globalPatternCooldown);
                TrySetPublicPropertyOrField(attackComp, "globalPatternCooldown", config.globalPatternCooldown);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[EnemyFacade] Failed to apply EnemyAttackController runtime fields: {ex.Message}");
            }
#endif
        }

        // 7) Animator override
        var animator = GetComponent<Animator>();
        if (animator != null && config.overrideController != null)
        {
#if UNITY_EDITOR
            TrySetSerializedObjectField(animator, "runtimeAnimatorController", config.overrideController);
#else
            animator.runtimeAnimatorController = config.overrideController;
#endif
        }

        // 8) EnemyDeath - map mass -> weight
        var deathComp = FindComponentByTypeName("EnemyDeath");
        if (deathComp != null)
        {
#if UNITY_EDITOR
            TrySetSerializedFloat(deathComp, "weight", config.mass);
#else
            TrySetPublicPropertyOrField(deathComp, "weight", config.mass);
#endif
        }

        // 9) Extra user-specified components
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
            Debug.Log($"[EnemyFacade] Applied config '{config.displayName}' to '{gameObject.name}'.");
#if UNITY_EDITOR
        else
            Debug.Log($"[EnemyFacade] (Editor) Applied config '{config.displayName}' to '{gameObject.name}'.");
#endif
    }

    public void RevertFromComponents()
    {
        Debug.Log("[EnemyFacade] RevertFromComponents called - this function is a no-op in current simple implementation.");
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

    private bool TrySetSerializedString(object compObj, string fieldName, string value)
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

        if (prop.propertyType == SerializedPropertyType.String)
        {
            prop.stringValue = value;
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

    private void TryApplyCommonFieldsToComponent(Component comp, EnemyConfig cfg)
    {
#if UNITY_EDITOR
        if (comp == null || cfg == null) return;
        var so = new SerializedObject(comp);

        var attempts = new Dictionary<string, object>()
        {
            {"maxHealth", cfg.maxHealth},
            {"baseMoveSpeed", cfg.baseMoveSpeed},
            {"detectionRadius", cfg.detectionRadius},
            {"shieldRechargeRate", cfg.shieldRechargeRate},
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