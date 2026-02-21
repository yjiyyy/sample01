using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Enemy용 FBX를 프로젝트에 넣은 뒤, 해당 FBX를 선택하고 메뉴에서 실행하면
/// Enemy 전용 프리팹(Enemy, Animator, Rigidbody, CapsuleCollider, EnemyDie 등 + 랙돌)을 생성합니다.
/// Player는 별도로 만들 예정이면 이 메뉴를 쓰지 마세요.
/// </summary>
public static class EnemyPrefabGenerator
{
    private const string MenuName = "Assets/Create Enemy Prefab from FBX";
    private const string MenuNameBatch = "Assets/Create Enemy Prefab from FBX (Batch)";

    [MenuItem(MenuName, true)]
    private static bool ValidateCreateEnemyPrefab()
    {
        if (Selection.activeObject == null) return false;
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        return !string.IsNullOrEmpty(path) && path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);
    }

    [MenuItem(MenuName, false, 1)]
    private static void CreateEnemyPrefab()
    {
        var fbx = Selection.activeObject as GameObject;
        if (fbx == null) return;
        string assetPath = AssetDatabase.GetAssetPath(fbx);
        GenerateAndSave(fbx, assetPath);
    }

    [MenuItem(MenuNameBatch, true)]
    private static bool ValidateCreateEnemyPrefabBatch()
    {
        foreach (var obj in Selection.objects)
        {
            if (obj is GameObject go)
            {
                string path = AssetDatabase.GetAssetPath(go);
                if (!string.IsNullOrEmpty(path) && path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }

    [MenuItem(MenuNameBatch, false, 2)]
    private static void CreateEnemyPrefabBatch()
    {
        foreach (var obj in Selection.objects)
        {
            if (!(obj is GameObject fbx)) continue;
            string path = AssetDatabase.GetAssetPath(fbx);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
            GenerateAndSave(fbx, path);
        }
    }

    private static void GenerateAndSave(GameObject fbxSource, string fbxAssetPath)
    {
        string folderPath = Path.GetDirectoryName(fbxAssetPath).Replace("\\", "/");
        string prefabName = Path.GetFileNameWithoutExtension(fbxAssetPath);
        string savePath = $"{folderPath}/{prefabName}.prefab";

        GameObject fbxInstance = PrefabUtility.InstantiatePrefab(fbxSource) as GameObject;
        if (fbxInstance == null)
        {
            Debug.LogError("[EnemyPrefabGenerator] FBX 인스턴스 생성 실패: " + fbxAssetPath);
            return;
        }

        GameObject root = new GameObject(prefabName);
        fbxInstance.transform.SetParent(root.transform, false);
        fbxInstance.transform.localPosition = Vector3.zero;
        fbxInstance.transform.localRotation = Quaternion.identity;
        fbxInstance.transform.localScale = Vector3.one;

        Animator sourceAnimator = fbxInstance.GetComponent<Animator>();
        Animator rootAnimator = root.AddComponent<Animator>();

        // Enemy 공통: Controller=E_Animator, Avatar=PC001_newAvatar, Apply Root Motion 끄기, Normal, Always Animate
        RuntimeAnimatorController enemyController = FindEnemyAnimatorController();
        Avatar enemyAvatar = FindEnemyAvatar(fbxAssetPath, sourceAnimator);

        RuntimeAnimatorController ctrlToUse = enemyController ?? sourceAnimator?.runtimeAnimatorController;
        Avatar avaToUse = enemyAvatar ?? sourceAnimator?.avatar;

        if (ctrlToUse == null)
            Debug.LogWarning("[EnemyPrefabGenerator] E_Animator를 찾지 못했습니다. " + EnemyAnimatorControllerPath);
        if (avaToUse == null)
            Debug.LogWarning("[EnemyPrefabGenerator] Avatar를 찾지 못했습니다. " + fbxAssetPath + " / " + EnemyAvatarSourcePath);

        // SerializedObject로 할당하여 프리팹에 확실히 반영
        var soAnim = new SerializedObject(rootAnimator);
        soAnim.FindProperty("m_Controller").objectReferenceValue = ctrlToUse;
        soAnim.FindProperty("m_Avatar").objectReferenceValue = avaToUse;
        soAnim.FindProperty("m_ApplyRootMotion").boolValue = false;
        soAnim.FindProperty("m_UpdateMode").intValue = (int)AnimatorUpdateMode.Normal;
        soAnim.FindProperty("m_CullingMode").intValue = (int)AnimatorCullingMode.AlwaysAnimate;
        soAnim.ApplyModifiedPropertiesWithoutUndo();

        if (sourceAnimator != null)
            Object.DestroyImmediate(sourceAnimator);

        Animator rootAnim = root.GetComponent<Animator>();

        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.mass = 50f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.05f;

        CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
        capsule.height = 2f;
        capsule.radius = 0.35f;
        capsule.center = new Vector3(0f, 1f, 0f);
        capsule.direction = 1;

        AddEnemyComponents(root, rootAnim, rb, capsule);
        if (!BuildRagdoll(root, rootAnim, rb, fbxInstance.transform))
        {
            Object.DestroyImmediate(root);
            return;
        }

        var settings = FindRagdollBuildSettings();
        if (settings != null)
        {
            BuildFXBloodDummies(fbxInstance.transform, settings);
            AddSliceBloodEffectSpawner(root, settings);
        }

        PrefabUtility.SaveAsPrefabAssetAndConnect(root, savePath, InteractionMode.AutomatedAction);
        Object.DestroyImmediate(root);
        Debug.Log($"[EnemyPrefabGenerator] Enemy 프리팹 생성 완료: {savePath}");
    }

    private static void AddEnemyComponents(GameObject root, Animator animator, Rigidbody rb, CapsuleCollider capsule)
    {
        root.tag = "Enemy";
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0) root.layer = enemyLayer;

        // Enemy.OnValidate()가 EnemyAI, EnemyImpact, EnemyDie를 추가하므로, Enemy 추가 전에 먼저 붙여 둠.
        // (순서가 뒤면 OnValidate에서 AddComponent 호출 → SendMessage 오류 및 중복 추가 발생)
        root.AddComponent<EnemyAI>();
        root.AddComponent<EnemyImpact>();
        root.AddComponent<EnemyDie>();

        var enemy = root.AddComponent<Enemy>();
        root.AddComponent<EnemyAnimationController>();
        root.AddComponent<EnemyAttackController>();
        root.AddComponent<MultiBoneJerkController>();
        root.AddComponent<EnemyHealth>();
        root.AddComponent<EnemyFacade>();

        var die = root.GetComponent<EnemyDie>();
        if (die != null)
        {
            die.animator = animator;
            die.rootRb = rb;
            die.rootCollider = capsule;
            die.excludeRoot = root.transform;
        }
        if (animator != null)
        {
            var soEnemy = new SerializedObject(enemy);
            var propAnim = soEnemy.FindProperty("animator");
            if (propAnim != null) propAnim.objectReferenceValue = animator;
            soEnemy.ApplyModifiedPropertiesWithoutUndo();
        }

        MovementSettings defaultMovement = FindDefaultMovementSettings();
        if (defaultMovement != null)
        {
            var soEnemy = new SerializedObject(enemy);
            var prop = soEnemy.FindProperty("movementSettings");
            if (prop == null) prop = soEnemy.FindProperty("m_movementSettings");
            if (prop != null) prop.objectReferenceValue = defaultMovement;
            soEnemy.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogWarning("[EnemyPrefabGenerator] MovementSettings를 찾지 못했습니다. 프리팹에서 Enemy에 수동 할당해 주세요.");
        }
    }

    private const string EnemyAnimatorControllerPath = "Assets/Arts/Enemy/Ani/E_Animator.controller";
    private const string EnemyAvatarSourcePath = "Assets/Arts/Player/New01/Model/PC001_new.fbx";
    private const string E_AnimatorGUID = "0c585f06e6c97534cac89d91d9e0726d";
    private const string PC001_newGUID = "8660d3d865b72a241877d8d4e0773df0";

    /// <summary>Enemy 공통 Animator Controller (E_Animator) 찾기</summary>
    private static RuntimeAnimatorController FindEnemyAnimatorController()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AssetDatabase.GUIDToAssetPath(E_AnimatorGUID));
        if (ctrl != null) return ctrl;
        ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(EnemyAnimatorControllerPath);
        if (ctrl != null) return ctrl;
        foreach (string guid in AssetDatabase.FindAssets("E_Animator t:AnimatorController"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == null || path.Contains("/Old/")) continue;
            ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
            if (ctrl != null) return ctrl;
        }
        return null;
    }

    /// <summary>Enemy 공통 Avatar (PC001_newAvatar) 찾기</summary>
    private static Avatar FindEnemyAvatar(string fbxAssetPath, Animator sourceAnimator)
    {
        foreach (string path in new[]
        {
            fbxAssetPath,
            AssetDatabase.GUIDToAssetPath(PC001_newGUID),
            EnemyAvatarSourcePath
        })
        {
            if (string.IsNullOrEmpty(path)) continue;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (o is Avatar av && av.isHuman) return av;
            }
        }
        return sourceAnimator?.avatar != null && sourceAnimator.isHuman ? sourceAnimator.avatar : null;
    }

    private static MovementSettings FindDefaultMovementSettings()
    {
        string[] guids = AssetDatabase.FindAssets("t:MovementSettings");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<MovementSettings>(path);
            if (asset != null) return asset;
        }
        return null;
    }

    private static readonly HumanBodyBones[] RagdollBones = new[]
    {
        HumanBodyBones.Hips,
        HumanBodyBones.Spine,
        HumanBodyBones.Chest,
        HumanBodyBones.UpperChest,
        HumanBodyBones.Neck,
        HumanBodyBones.Head,
        HumanBodyBones.LeftUpperArm,
        HumanBodyBones.LeftLowerArm,
        HumanBodyBones.LeftHand,
        HumanBodyBones.RightUpperArm,
        HumanBodyBones.RightLowerArm,
        HumanBodyBones.RightHand,
        HumanBodyBones.LeftUpperLeg,
        HumanBodyBones.LeftLowerLeg,
        HumanBodyBones.LeftFoot,
        HumanBodyBones.RightUpperLeg,
        HumanBodyBones.RightLowerLeg,
        HumanBodyBones.RightFoot,
    };

    private static bool BuildRagdoll(GameObject root, Animator animator, Rigidbody rootRb, Transform modelRoot)
    {
        RagdollBuildSettings settings = FindRagdollBuildSettings();
        // PC 제너레이터와 동일: Bip001이 있으면 BIP 경로 우선 (최상위 본 Bip001, Character Joint 없음)
        Transform pelvisTr = FindBoneInChildren(modelRoot, "Bip001");
        if (pelvisTr != null)
        {
            if (settings == null)
            {
                Debug.LogError("[EnemyPrefabGenerator] BIP 랙돌 빌드 실패: RagdollBuildSettings SO를 찾을 수 없습니다. Project에 RagdollBuildSettings를 생성·할당한 뒤 다시 시도하세요.");
                return false;
            }
            if (settings.boneOverrides == null || settings.boneOverrides.Length == 0)
            {
                Debug.LogError("[EnemyPrefabGenerator] BIP 랙돌 빌드 실패: RagdollBuildSettings의 boneOverrides가 비어 있습니다. boneOverrides에 값을 채운 뒤 다시 시도하세요.");
                return false;
            }
            var enemySettings = settings;
            if (string.IsNullOrEmpty(enemySettings.pelvis)) enemySettings.pelvis = "Bip001";
            BuildRagdollBIP(root, rootRb, modelRoot, enemySettings);
            RemoveCharacterJointFromBip001(modelRoot);
            return true;
        }

        bool useHumanoid = animator != null && animator.avatar != null && animator.isHuman;
        Transform hipsTr = useHumanoid ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
        if (useHumanoid && hipsTr != null)
        {
            BuildRagdollHumanoid(root, animator, rootRb, hipsTr, settings);
            RemoveCharacterJointFromBip001(modelRoot);
            return true;
        }

        Debug.LogWarning("[EnemyPrefabGenerator] BIP 본(Bip001)도 Humanoid도 찾지 못해 랙돌을 생성하지 않습니다.");
        return true;
    }

    /// <summary>Bip001(최상위 본)에 붙은 Character Joint 제거. PC 제너레이터와 동일.</summary>
    private static void RemoveCharacterJointFromBip001(Transform modelRoot)
    {
        Transform bip001 = FindBoneInChildren(modelRoot, "Bip001");
        if (bip001 == null) return;
        var cj = bip001.GetComponent<CharacterJoint>();
        if (cj != null) Object.DestroyImmediate(cj);
    }

    private static RagdollBuildSettings FindRagdollBuildSettings()
    {
        string[] guids = AssetDatabase.FindAssets("t:RagdollBuildSettings");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<RagdollBuildSettings>(path);
            if (asset != null) return asset;
        }
        return null;
    }

    private static Transform FindBoneInChildren(Transform root, string boneName)
    {
        if (root == null || string.IsNullOrEmpty(boneName)) return null;
        if (root.name == boneName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindBoneInChildren(root.GetChild(i), boneName);
            if (found != null) return found;
        }
        return null;
    }

    private static void BuildRagdollHumanoid(GameObject root, Animator animator, Rigidbody rootRb, Transform hipsTr, RagdollBuildSettings settings)
    {
        float totalMass = settings != null ? settings.totalMass : 20f;
        float strength = settings != null ? settings.strength : 0f;
        int boneCount = RagdollBones.Length;
        float massPerBone = Mathf.Max(0.1f, totalMass / boneCount);
        float twist = (settings != null && settings.twistLimit > 0f) ? settings.twistLimit : (20f + strength);
        float swing = (settings != null && settings.swingLimit > 0f) ? settings.swingLimit : Mathf.Min(90f, 30f + strength);

        var boneToRb = new Dictionary<Transform, Rigidbody>();
        foreach (HumanBodyBones boneType in RagdollBones)
        {
            Transform tr = animator.GetBoneTransform(boneType);
            if (tr == null) continue;
            if (tr == root.transform) continue;

            Rigidbody brb = tr.GetComponent<Rigidbody>();
            if (brb == null) brb = tr.gameObject.AddComponent<Rigidbody>();
            brb.useGravity = true;
            brb.isKinematic = true;
            brb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            brb.mass = massPerBone;
            brb.linearDamping = 0f;
            brb.angularDamping = 0.05f;
            boneToRb[tr] = brb;

            if (tr.GetComponent<Collider>() == null)
            {
                float height = 0.2f;
                float radius = 0.08f;
                switch (boneType)
                {
                    case HumanBodyBones.Head:
                        radius = 0.12f;
                        height = 0.2f;
                        break;
                    case HumanBodyBones.Chest:
                    case HumanBodyBones.Spine:
                    case HumanBodyBones.UpperChest:
                        radius = 0.12f;
                        height = 0.25f;
                        break;
                    case HumanBodyBones.LeftUpperLeg:
                    case HumanBodyBones.RightUpperLeg:
                    case HumanBodyBones.LeftLowerLeg:
                    case HumanBodyBones.RightLowerLeg:
                        radius = 0.07f;
                        height = 0.35f;
                        break;
                    case HumanBodyBones.LeftUpperArm:
                    case HumanBodyBones.RightUpperArm:
                    case HumanBodyBones.LeftLowerArm:
                    case HumanBodyBones.RightLowerArm:
                        radius = 0.05f;
                        height = 0.2f;
                        break;
                }

                CapsuleCollider col = tr.gameObject.AddComponent<CapsuleCollider>();
                col.height = height;
                col.radius = radius;
                col.direction = 0;
                col.center = Vector3.zero;
                col.enabled = false;
            }
        }

        foreach (HumanBodyBones boneType in RagdollBones)
        {
            Transform tr = animator.GetBoneTransform(boneType);
            if (tr == null || tr == root.transform) continue;
            if (!boneToRb.TryGetValue(tr, out Rigidbody bodyRb)) continue;

            Rigidbody connected = null;
            if (boneType == HumanBodyBones.Hips)
            {
                connected = rootRb;
            }
            else
            {
                Transform parent = GetRagdollParentBone(animator, boneType);
                if (parent != null && parent != root.transform)
                    boneToRb.TryGetValue(parent, out connected);
                else if (parent == root.transform)
                    connected = rootRb;
            }

            if (connected == null) continue;

            CharacterJoint joint = tr.GetComponent<CharacterJoint>();
            if (joint == null) joint = tr.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = connected;
            joint.anchor = Vector3.zero;
            joint.axis = new Vector3(0, 0, 1);
            joint.swingAxis = new Vector3(1, 0, 0);
            joint.lowTwistLimit = new SoftJointLimit { limit = -twist };
            joint.highTwistLimit = new SoftJointLimit { limit = twist };
            joint.swing1Limit = new SoftJointLimit { limit = swing };
            joint.swing2Limit = new SoftJointLimit { limit = swing };
            joint.enableProjection = true;
        }

        SetRagdollBonesLayer(root, boneToRb.Keys);
        SetEnemyDieHipsBody(root, hipsTr, boneToRb);
    }

    private static void BuildRagdollBIP(GameObject root, Rigidbody rootRb, Transform modelRoot, RagdollBuildSettings settings)
    {
        // SO의 boneOverrides만 사용. (caller에서 SO·boneOverrides 검증 후 호출)
        var overrideDict = new Dictionary<string, RagdollBuildSettings.BoneOverride>();
        if (settings.boneOverrides != null)
        {
            foreach (var o in settings.boneOverrides)
            {
                if (o != null && !string.IsNullOrEmpty(o.boneKey))
                    overrideDict[o.boneKey] = o;
            }
        }

        var bones = new Dictionary<string, Transform>();
        string[] keys = { "Pelvis", "LeftHips", "LeftKnee", "LeftFoot", "RightHips", "RightKnee", "RightFoot", "LeftArm", "LeftElbow", "RightArm", "RightElbow", "MiddleSpine", "Head" };
        string pelvisName = !string.IsNullOrEmpty(settings.pelvis) ? settings.pelvis : "Bip001";
        string[] names = { pelvisName, settings.leftHips, settings.leftKnee, settings.leftFoot, settings.rightHips, settings.rightKnee, settings.rightFoot, settings.leftArm, settings.leftElbow, settings.rightArm, settings.rightElbow, settings.middleSpine, settings.head };
        for (int i = 0; i < keys.Length; i++)
        {
            var tr = FindBoneInChildren(modelRoot, names[i]);
            if (tr != null) bones[keys[i]] = tr;
        }

        if (!bones.ContainsKey("Pelvis"))
        {
            Debug.LogWarning("[EnemyPrefabGenerator] BIP Pelvis 본을 찾지 못했습니다. RagdollBuildSettings의 본 이름을 확인하세요.");
            return;
        }

        int count = bones.Count;
        var boneToRb = new Dictionary<Transform, Rigidbody>();

        foreach (var kv in bones)
        {
            Transform tr = kv.Value;
            if (tr == root) continue;
            var o = overrideDict.TryGetValue(kv.Key, out var ov) ? ov : null;

            Rigidbody brb = tr.GetComponent<Rigidbody>();
            if (brb == null) brb = tr.gameObject.AddComponent<Rigidbody>();
            brb.useGravity = true;
            brb.isKinematic = true;
            brb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            brb.mass = (o != null && o.mass > 0f) ? o.mass : 1f;
            brb.linearDamping = 0f;
            brb.angularDamping = 0.05f;
            boneToRb[tr] = brb;

            // E_LV01_New04 기준: override가 있으면 무조건 적용 (기존 콜리더도 덮어씀)
            float radius = 0.08f, height = 0.2f;
            Vector3 center = Vector3.zero;
            int dir = 0;
            bool hasOverride = o != null && o.colliderRadius >= 0f && o.colliderHeight >= 0f;
            if (hasOverride)
            {
                radius = o.colliderRadius;
                height = o.colliderHeight;
                center = o.colliderCenter;
                dir = o.colliderDirection;
            }
            else if (tr.GetComponent<Collider>() == null)
            {
                switch (kv.Key)
                {
                    case "Head": radius = 0.12f; height = 0.2f; break;
                    case "MiddleSpine": radius = 0.12f; height = 0.25f; break;
                    case "LeftHips": case "RightHips": case "LeftKnee": case "RightKnee": radius = 0.07f; height = 0.35f; break;
                    case "LeftArm": case "RightArm": case "LeftElbow": case "RightElbow": radius = 0.05f; height = 0.2f; break;
                    case "Pelvis": radius = 0.12f; height = 0.2f; break;
                }
            }

            CapsuleCollider col = tr.GetComponent<CapsuleCollider>();
            if (col == null)
            {
                var existing = tr.GetComponent<Collider>();
                if (existing != null) Object.DestroyImmediate(existing);
                col = tr.gameObject.AddComponent<CapsuleCollider>();
            }
            col.radius = radius;
            col.height = height;
            col.direction = dir;
            col.center = center;
            col.enabled = false;
        }

        var parentMap = new Dictionary<string, string>
        {
            { "Pelvis", null },
            { "LeftHips", "Pelvis" }, { "LeftKnee", "LeftHips" }, { "LeftFoot", "LeftKnee" },
            { "RightHips", "Pelvis" }, { "RightKnee", "RightHips" }, { "RightFoot", "RightKnee" },
            { "MiddleSpine", "Pelvis" }, { "Head", "MiddleSpine" },
            { "LeftArm", "MiddleSpine" }, { "LeftElbow", "LeftArm" },
            { "RightArm", "MiddleSpine" }, { "RightElbow", "RightArm" }
        };

        foreach (var kv in bones)
        {
            string key = kv.Key;
            Transform tr = kv.Value;
            if (!boneToRb.TryGetValue(tr, out Rigidbody bodyRb)) continue;
            string parentKey = parentMap.TryGetValue(key, out var p) ? p : null;
            if (parentKey == null) continue;
            Rigidbody connected = null;
            if (bones.TryGetValue(parentKey, out Transform parentTr) && boneToRb.TryGetValue(parentTr, out connected)) { }
            if (connected == null) continue;

            var o = overrideDict.TryGetValue(key, out var ov) ? ov : null;
            float lowTwist = o != null && !float.IsNaN(o.lowTwistLimit) ? o.lowTwistLimit : -20f;
            float highTwist = o != null && !float.IsNaN(o.highTwistLimit) ? o.highTwistLimit : 20f;
            float s1 = o != null && !float.IsNaN(o.swing1Limit) ? o.swing1Limit : 30f;
            float s2 = o != null && !float.IsNaN(o.swing2Limit) ? o.swing2Limit : 30f;
            Vector3 axis = (o != null) ? o.jointAxis : new Vector3(0, 0, 1);
            Vector3 swingAxis = (o != null) ? o.jointSwingAxis : new Vector3(1, 0, 0);

            CharacterJoint joint = tr.GetComponent<CharacterJoint>();
            if (joint == null) joint = tr.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = connected;
            joint.anchor = Vector3.zero;
            joint.axis = axis;
            joint.swingAxis = swingAxis;
            joint.lowTwistLimit = new SoftJointLimit { limit = lowTwist };
            joint.highTwistLimit = new SoftJointLimit { limit = highTwist };
            joint.swing1Limit = new SoftJointLimit { limit = s1 };
            joint.swing2Limit = new SoftJointLimit { limit = s2 };
            joint.enableProjection = true;
        }

        SetRagdollBonesLayer(root, boneToRb.Keys);
        SetEnemyDieHipsBody(root, bones["Pelvis"], boneToRb);
        Debug.Log($"[EnemyPrefabGenerator] BIP 랙돌 생성 완료 (본 수={count})");
    }

    /// <summary> 랙돌에 포함된 모든 본의 레이어를 Ragdoll로 설정 </summary>
    private static void SetRagdollBonesLayer(GameObject root, IEnumerable<Transform> ragdollBones)
    {
        int ragdollLayer = LayerMask.NameToLayer("Ragdoll");
        if (ragdollLayer < 0) return;
        foreach (Transform tr in ragdollBones)
        {
            if (tr != null && tr.gameObject != root)
                tr.gameObject.layer = ragdollLayer;
        }
    }

    private static void SetEnemyDieHipsBody(GameObject root, Transform hipsTr, Dictionary<Transform, Rigidbody> boneToRb)
    {
        var die = root.GetComponent<EnemyDie>();
        if (die == null || hipsTr == null) return;
        if (!boneToRb.TryGetValue(hipsTr, out Rigidbody hipsRb)) return;
        var so = new SerializedObject(die);
        var hipsProp = so.FindProperty("hipsBody");
        if (hipsProp != null) hipsProp.objectReferenceValue = hipsRb;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildFXBloodDummies(Transform modelRoot, RagdollBuildSettings settings)
    {
        var entries = settings.fxBloodDummies != null && settings.fxBloodDummies.Length > 0
            ? settings.fxBloodDummies
            : RagdollBuildSettings.GetDefaultFXBloodDummies();
        foreach (var e in entries)
        {
            if (string.IsNullOrEmpty(e.dummyName) || string.IsNullOrEmpty(e.parentBoneName)) continue;
            Transform parent = FindBoneInChildren(modelRoot, e.parentBoneName);
            if (parent == null) continue;
            var existing = FindInChildrenByName(modelRoot, e.dummyName);
            if (existing != null) continue;
            var go = new GameObject(e.dummyName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = e.localPosition;
            go.transform.localEulerAngles = e.localEulerAngles;
        }
    }

    private static Transform FindInChildrenByName(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var f = FindInChildrenByName(root.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }

    private static void AddSliceBloodEffectSpawner(GameObject root, RagdollBuildSettings settings)
    {
        var spawner = root.AddComponent<SliceBloodEffectSpawner>();
        if (settings.bloodGushPrefab != null)
            spawner.bloodGushPrefab = settings.bloodGushPrefab;
    }

    private static Transform GetRagdollParentBone(Animator animator, HumanBodyBones bone)
    {
        switch (bone)
        {
            case HumanBodyBones.Spine:
            case HumanBodyBones.LeftUpperLeg:
            case HumanBodyBones.RightUpperLeg:
                return animator.GetBoneTransform(HumanBodyBones.Hips);
            case HumanBodyBones.Chest:
            case HumanBodyBones.LeftUpperArm:
            case HumanBodyBones.RightUpperArm:
                return animator.GetBoneTransform(HumanBodyBones.Spine) ?? animator.GetBoneTransform(HumanBodyBones.Hips);
            case HumanBodyBones.UpperChest:
            case HumanBodyBones.Neck:
                return animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine);
            case HumanBodyBones.Head:
                return animator.GetBoneTransform(HumanBodyBones.Neck) ?? animator.GetBoneTransform(HumanBodyBones.Chest);
            case HumanBodyBones.LeftLowerArm:
                return animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            case HumanBodyBones.LeftHand:
                return animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            case HumanBodyBones.RightLowerArm:
                return animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            case HumanBodyBones.RightHand:
                return animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            case HumanBodyBones.LeftLowerLeg:
                return animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            case HumanBodyBones.LeftFoot:
                return animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            case HumanBodyBones.RightLowerLeg:
                return animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            case HumanBodyBones.RightFoot:
                return animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            case HumanBodyBones.Hips:
                return null;
            default:
                return null;
        }
    }
}
