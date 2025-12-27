using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Enemy + 관련 컴포넌트들에 EnemyConfig SO 값을 적용하는 facade.
/// - 파츠 시스템:  Start()에서 config.partSlots 기반으로 파츠 생성 및 부착.
/// </summary>
public class EnemyFacade : MonoBehaviour
{
    [Header("Core")]
    public EnemyConfig config;

    [Tooltip("If true, sync from SO to components automatically in OnValidate() (editor) and Awake() (runtime).")]
    public bool autoSync = true;

    [Header("Optional - override individual components")]
    public Enemy targetEnemy;
    public Component[] extraTargets;

    private EnemyConfig appliedConfig;
    private bool appliedOnce = false;

    // Parts System:  생성된 파츠 오브젝트들을 보관 (EnemyDie에서 참조)
    private List<GameObject> spawnedParts = new List<GameObject>();

    /// <summary>
    /// EnemyDie에서 접근할 수 있도록 public으로 노출.
    /// </summary>
    public List<GameObject> SpawnedParts => spawnedParts;

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

        // Parts System: 파츠 생성 (런타임에서만)
        if (Application.isPlaying && config != null)
        {
            SpawnParts();
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

        // 3) EnemyHealth
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

#if ! UNITY_EDITOR
                if (! applied)
                {
                    applied = TrySetPublicPropertyOrField(healthComp, "maxHP", config.maxHealth)
                           || TrySetPublicPropertyOrField(healthComp, "maxHealth", config.maxHealth);
                }
#endif
            }

            if (!applied)
                Debug.LogWarning($"[EnemyFacade] EnemyHealth present but no known health/shield field found on '{healthComp.GetType().Name}'.");
        }

        // 4) EnemyAI
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

        // 5) EnemyImpact
        var impactComp = GetComponent<EnemyImpact>();
        if (impactComp != null)
        {
            TrySetSerializedFloat(impactComp, "softKnockRatio", config.shieldRechargeRate);
        }

        // 6) EnemyAttackController
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
                TrySetPublicPropertyOrField(attackComp, "attackPatterns", config. attackPatterns);
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

        // 8) EnemyDeath
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

    /// <summary>
    /// Parts System: config.partSlots 기반으로 파츠 생성 및 부착.
    /// - boneName(문자열)으로 본을 검색해서 부착. 
    /// - 생성된 파츠는 spawnedParts 리스트에 보관.
    /// </summary>
    private void SpawnParts()
    {
        if (config == null || config.partSlots == null || config.partSlots.Length == 0)
            return;

        foreach (var slot in config.partSlots)
        {
            if (slot == null) continue;

            // boneName이 비어있으면 스킵
            if (string.IsNullOrEmpty(slot.boneName))
            {
                Debug.LogWarning($"[EnemyFacade] Part slot has empty boneName.  Skipping.");
                continue;
            }

            // partPrefab이 없으면 스킵
            if (slot.partPrefab == null)
            {
                Debug.LogWarning($"[EnemyFacade] Part slot (bone='{slot.boneName}') has no partPrefab assigned. Skipping.");
                continue;
            }

            // boneName으로 본 검색
            Transform attachBone = FindBoneByName(slot.boneName);
            if (attachBone == null)
            {
                Debug.LogWarning($"[EnemyFacade] Bone '{slot.boneName}' not found in '{gameObject.name}'. Skipping part.");
                continue;
            }

            // 파츠 생성
            GameObject partInstance = Instantiate(slot.partPrefab, attachBone);
            partInstance.name = slot.partPrefab.name;

            // 로컬 Transform 적용
            partInstance.transform.localPosition = slot.localOffset;
            partInstance.transform.localRotation = Quaternion.Euler(slot.localRotationEuler);
            partInstance.transform.localScale = slot.localScale;

            // ★★★ 생성 직후 파츠 물리 비활성화 ★★★
            InitializePartPhysics(partInstance);

            spawnedParts.Add(partInstance);

            Debug.Log($"[EnemyFacade] Spawned part '{partInstance.name}' on bone '{attachBone.name}'");
        }
    }
    private void InitializePartPhysics(GameObject partObj)
    {
        if (partObj == null) return;

        int rbCount = 0;
        int colCount = 0;

        // Rigidbody 초기화 (루트)
        Rigidbody partRb = partObj.GetComponent<Rigidbody>();
        if (partRb != null)
        {
            partRb.isKinematic = true;
            partRb.useGravity = true;
            partRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rbCount++;
        }

        // Collider 초기화 (루트 + 자식 모두)
        Collider[] colliders = partObj.GetComponentsInChildren<Collider>(true);
        foreach (var col in colliders)
        {
            if (col != null)
            {
                col.enabled = false;
                colCount++;
            }
        }

        Debug.Log($"[EnemyFacade] InitializePartPhysics: '{partObj.name}' - Rigidbody kinematic: {rbCount}, Colliders disabled: {colCount}");
    }

    /// <summary>
    /// 본 이름으로 Transform을 재귀 검색 (대소문자 구분).
    /// </summary>
    private Transform FindBoneByName(string boneName)
    {
        if (string.IsNullOrEmpty(boneName)) return null;

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child != null && child.name == boneName)
                return child;
        }

        return null;
    }

    // ---------- Helper methods ----------

    private int GetFirstLayerIndex(LayerMask mask)
    {
        int val = mask.value;
        for (int i = 0; i < 32; i++)
        {
            if ((val & (1 << i)) != 0) return i;
        }
        return -1;
    }

    private Component FindComponentByTypeName(string typeName)
    {
        foreach (var c in GetComponents<Component>())
        {
            if (c == null) continue;
            if (c.GetType().Name == typeName) return c;
        }
        return null;
    }

    private class ComponentWrapper
    {
        public Component component;
    }

    private ComponentWrapper FindComponentInChildrenByTypeName(string typeName)
    {
        foreach (var c in GetComponentsInChildren<Component>(true))
        {
            if (c == null) continue;
            if (c.GetType().Name == typeName)
                return new ComponentWrapper { component = c };
        }
        return null;
    }

    private bool TrySetPublicPropertyOrField(object target, string name, object value)
    {
        if (target == null) return false;
        var t = target.GetType();
        var prop = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(target, value);
            return true;
        }
        var field = t.GetField(name, BindingFlags.Instance | BindingFlags.Public);
        if (field != null)
        {
            field.SetValue(target, value);
            return true;
        }
        return false;
    }

    private void TryInvokeMethodIfExists(object target, string[] possibleNames, object[] args)
    {
        if (target == null) return;
        var t = target.GetType();
        foreach (var name in possibleNames)
        {
            var m = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (m != null)
            {
                m.Invoke(target, args);
                return;
            }
        }
    }

#if UNITY_EDITOR
    private bool TrySetSerializedFloat(Component comp, string propName, float value)
    {
        if (comp == null) return false;
        var so = new SerializedObject(comp);
        var prop = so.FindProperty(propName) ?? so.FindProperty("m_" + propName);
        if (prop != null && (prop.propertyType == SerializedPropertyType.Float || prop.propertyType == SerializedPropertyType.Integer))
        {
            if (prop.propertyType == SerializedPropertyType.Float)
                prop.floatValue = value;
            else
                prop.intValue = Mathf.RoundToInt(value);
            so.ApplyModifiedProperties();
            return true;
        }
        return false;
    }

    private bool TrySetSerializedObjectField(Component comp, string propName, UnityEngine.Object value)
    {
        if (comp == null) return false;
        var so = new SerializedObject(comp);
        var prop = so.FindProperty(propName) ?? so.FindProperty("m_" + propName);
        if (prop != null && prop.propertyType == SerializedPropertyType.ObjectReference)
        {
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
            return true;
        }
        return false;
    }
#endif

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
            {"shieldRechargeRate", cfg. shieldRechargeRate},
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
        }
        so.ApplyModifiedProperties();
#else
        TrySetPublicPropertyOrField(comp, "maxHealth", cfg. maxHealth);
        TrySetPublicPropertyOrField(comp, "baseMoveSpeed", cfg. baseMoveSpeed);
        TrySetPublicPropertyOrField(comp, "detectionRadius", cfg.detectionRadius);
        TrySetPublicPropertyOrField(comp, "shieldRechargeRate", cfg.shieldRechargeRate);
#endif
    }
}