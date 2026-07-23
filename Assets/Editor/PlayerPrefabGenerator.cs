using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Player용 FBX를 선택한 뒤 메뉴에서 실행하면
/// Assets/Arts/Player/Player001.prefab 과 동일한 루트 컴포넌트·기본값으로 Player 전용 프리팹을 생성합니다.
/// </summary>
public static class PlayerPrefabGenerator
{
    private const string MenuName = "Assets/Create Player Prefab from FBX";

    private static readonly HumanBodyBones[] RagdollBones = new[]
    {
        HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest,
        HumanBodyBones.Neck, HumanBodyBones.Head,
        HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
        HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
        HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
        HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
    };

    [MenuItem(MenuName, true)]
    private static bool ValidateCreatePlayerPrefab()
    {
        if (Selection.activeObject == null) return false;
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        return !string.IsNullOrEmpty(path) && path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);
    }

    [MenuItem(MenuName, false, 3)]
    private static void CreatePlayerPrefab()
    {
        var fbx = Selection.activeObject as GameObject;
        if (fbx == null) return;
        string assetPath = AssetDatabase.GetAssetPath(fbx);
        GenerateAndSave(fbx, assetPath);
    }

    private static void GenerateAndSave(GameObject fbxSource, string fbxAssetPath)
    {
        string folderPath = Path.GetDirectoryName(fbxAssetPath).Replace("\\", "/");
        string prefabName = Path.GetFileNameWithoutExtension(fbxAssetPath);
        string savePath = $"{folderPath}/{prefabName}.prefab";

        GameObject fbxInstance = PrefabUtility.InstantiatePrefab(fbxSource) as GameObject;
        if (fbxInstance == null)
        {
            Debug.LogError("[PlayerPrefabGenerator] FBX 인스턴스 생성 실패: " + fbxAssetPath);
            return;
        }

        GameObject root = new GameObject(prefabName);
        fbxInstance.transform.SetParent(root.transform, false);
        fbxInstance.transform.localPosition = Vector3.zero;
        fbxInstance.transform.localRotation = Quaternion.identity;
        fbxInstance.transform.localScale = Vector3.one;

        // Player001.prefab 과 동일한 컴포넌트 순서. (PlayerMovement가 RequireComponent<Rigidbody>이므로 Rigidbody를 Movement보다 먼저 추가)
        Animator sourceAnimator = fbxInstance.GetComponent<Animator>();
        if (sourceAnimator != null)
        {
            Animator rootAnimator = root.AddComponent<Animator>();
            rootAnimator.avatar = sourceAnimator.avatar;
            rootAnimator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;
            rootAnimator.applyRootMotion = false;
            rootAnimator.updateMode = AnimatorUpdateMode.Normal;
            rootAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Object.DestroyImmediate(sourceAnimator);
        }
        Animator rootAnim = root.GetComponent<Animator>();
        if (rootAnim == null)
        {
            rootAnim = root.AddComponent<Animator>();
            rootAnim.applyRootMotion = false;
            rootAnim.updateMode = AnimatorUpdateMode.Normal;
            rootAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        ApplyDefaultAnimatorIfNeeded(rootAnim);

        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.mass = 1f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.05f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        root.AddComponent<PlayerMovement>();
        root.AddComponent<PlayerAnimationController>();
        root.AddComponent<PlayerWeaponController>();
        root.AddComponent<MeshFilter>();
        var meshRenderer = root.AddComponent<MeshRenderer>();
        ApplyDetectorMaterial(meshRenderer);
        root.AddComponent<InputManager>();
        root.AddComponent<PlayerHealth>();
        root.AddComponent<EnemyDetector>();
        CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
        capsule.height = 1.6f;
        capsule.radius = 0.28f;
        capsule.center = new Vector3(0f, 0.8f, 0f);
        capsule.direction = 1;
        root.AddComponent<PlayerFacade>();
        root.AddComponent<PlayerStats>();
        root.AddComponent<PlayerEquipmentController>();
        var sliceBlood = root.AddComponent<SliceBloodEffectSpawner>();
        root.AddComponent<SubWeaponController>();
        root.AddComponent<Upgrade>();
        root.AddComponent<UpgradeEffectRuntime>();

        SetModelHierarchyLayer(fbxInstance.transform, LayerMask.NameToLayer("Ragdoll"));

        AddPlayerComponentProperties(root, rootAnim, rb, capsule);
        if (!BuildRagdoll(root, rootAnim, rb, fbxInstance.transform))
        {
            Object.DestroyImmediate(root);
            return;
        }

        var settings = FindRagdollBuildSettings();
        if (settings != null)
        {
            BuildFXBloodDummies(fbxInstance.transform, settings);
            if (settings.bloodGushPrefab != null)
                sliceBlood.bloodGushPrefab = settings.bloodGushPrefab;
        }

        PrefabUtility.SaveAsPrefabAssetAndConnect(root, savePath, InteractionMode.AutomatedAction);
        Object.DestroyImmediate(root);
        Debug.Log($"[PlayerPrefabGenerator] Player 프리팹 생성 완료: {savePath}");
    }

    private static void AddPlayerComponentProperties(GameObject root, Animator animator, Rigidbody rb, CapsuleCollider capsule)
    {
        root.tag = "Player";
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0) root.layer = playerLayer;

        var movement = root.GetComponent<PlayerMovement>();
        var weaponCtrl = root.GetComponent<PlayerWeaponController>();
        var animCtrl = root.GetComponent<PlayerAnimationController>();
        var detector = root.GetComponent<EnemyDetector>();
        var health = root.GetComponent<PlayerHealth>();
        var stats = root.GetComponent<PlayerStats>();
        var subWeapon = root.GetComponent<SubWeaponController>();
        var upgradeRuntime = root.GetComponent<UpgradeEffectRuntime>();

        if (movement != null)
        {
            var so = new SerializedObject(movement);
            SetFloat(so, "baseMoveSpeed", 10f);
            var propStop = so.FindProperty("stopWhenNoInput") ?? so.FindProperty("m_stopWhenNoInput");
            if (propStop != null) propStop.boolValue = true;
            SetFloat(so, "rotationSpeedDegPerSec", 1080f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        if (weaponCtrl != null)
        {
            var so = new SerializedObject(weaponCtrl);
            var propAnim = so.FindProperty("animationController") ?? so.FindProperty("m_animationController");
            if (propAnim != null && animCtrl != null) propAnim.objectReferenceValue = animCtrl;
            var propDet = so.FindProperty("enemyDetector") ?? so.FindProperty("m_enemyDetector");
            if (propDet != null && detector != null) propDet.objectReferenceValue = detector;
            var propDebug = so.FindProperty("debugMode") ?? so.FindProperty("m_debugMode");
            if (propDebug != null) propDebug.boolValue = true;
            var propCharge = so.FindProperty("enableChargeMessages") ?? so.FindProperty("m_enableChargeMessages");
            if (propCharge != null) propCharge.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        if (health != null)
        {
            health.maxHP = 90f;
            health.weight = 1f;
        }

        if (detector != null)
        {
            detector.viewAngle = 45f;
            detector.viewDistance = 10f;
            detector.segmentCount = 30;
            detector.height = 0.8f;
            detector.usePhysicsScan = true;
        }

        if (stats != null)
        {
            stats.maxHealth = 90f;
            stats.currentHealth = 90f;
            stats.massMultiplier = 1f;
            stats.baseMoveSpeed = 10f;
            stats.rotationSpeedDegPerSec = 1080f;
            stats.maxStamina = 10f;
            stats.currentStamina = 10f;
            stats.staminaRechargeRate = 0.5f;
            stats.staminaRechargeDelay = 3f;
            stats.level = 1;
            stats.experience = 0;
            stats.expToNextLevel = 100;
        }

        if (subWeapon != null)
        {
            var so = new SerializedObject(subWeapon);
            SetFloat(so, "orbitDiameter", 2f);
            SetFloat(so, "rotationSpeedDegPerSec", 120f);
            SetFloat(so, "orbitHeightOffset", 0.6f);
            SetString(so, "orbitCenterBoneName", "Root_Dummy");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        if (upgradeRuntime != null)
        {
            var so = new SerializedObject(upgradeRuntime);
            var propLog = so.FindProperty("enableDebugLog") ?? so.FindProperty("m_enableDebugLog");
            if (propLog != null) propLog.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        MovementSettings movementSettings = FindDefaultMovementSettings();
        if (movementSettings != null && movement != null)
        {
            var so = new SerializedObject(movement);
            var prop = so.FindProperty("movementSettings") ?? so.FindProperty("m_movementSettings");
            if (prop != null) prop.objectReferenceValue = movementSettings;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else
            Debug.LogWarning("[PlayerPrefabGenerator] MovementSettings를 찾지 못했습니다. 프리팹에서 PlayerMovement에 수동 할당해 주세요.");

        // Player Facade: Config = PlayerConfig_SO, Auto Sync 켜기 (Player001.prefab 과 동일)
        PlayerConfig playerConfig = FindDefaultPlayerConfig();
        if (playerConfig != null)
        {
            var facade = root.GetComponent<PlayerFacade>();
            if (facade != null)
            {
                facade.config = playerConfig;
                facade.autoSync = true;
                facade.targetPlayer = null;
                EditorUtility.SetDirty(facade);
            }
        }
        else
            Debug.LogWarning("[PlayerPrefabGenerator] PlayerConfig_SO를 찾지 못했습니다. 프리팹에서 Player Facade에 수동 할당해 주세요.");
    }

    private static void SetFloat(SerializedObject so, string name, float value)
    {
        var prop = so.FindProperty(name) ?? so.FindProperty("m_" + name);
        if (prop != null) prop.floatValue = value;
    }

    private static void SetString(SerializedObject so, string name, string value)
    {
        var prop = so.FindProperty(name) ?? so.FindProperty("m_" + name);
        if (prop != null) prop.stringValue = value;
    }

    /// <summary>Player001.prefab: FBX 하위 메시 계층을 Ragdoll 레이어(9)로 설정</summary>
    private static void SetModelHierarchyLayer(Transform modelRoot, int layer)
    {
        if (modelRoot == null || layer < 0) return;
        foreach (Transform child in modelRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child == modelRoot) continue;
            child.gameObject.layer = layer;
        }
    }

    private static void ApplyDetectorMaterial(MeshRenderer meshRenderer)
    {
        if (meshRenderer == null) return;
        const string detectorMatPath = "Assets/Arts/FX/Helper/Detector.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(detectorMatPath);
        if (mat != null)
            meshRenderer.sharedMaterial = mat;
    }

    private static PlayerConfig FindDefaultPlayerConfig()
    {
        foreach (string guid in AssetDatabase.FindAssets("PlayerConfig_SO t:PlayerConfig"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<PlayerConfig>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) return asset;
        }
        foreach (string guid in AssetDatabase.FindAssets("t:PlayerConfig"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (path != null && path.Contains("PlayerConfig"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<PlayerConfig>(path);
                if (asset != null) return asset;
            }
        }
        return null;
    }

    /// <summary>Player001 기준: Controller=PC_001, Avatar=Player001.fbx, Apply Root Motion/Animate Physics 끄기, Normal, Always Animate</summary>
    private static void ApplyDefaultAnimatorIfNeeded(Animator animator)
    {
        if (animator == null) return;
        RuntimeAnimatorController defaultController = FindDefaultAnimatorController();
        Avatar defaultAvatar = FindDefaultAvatar();
        if (animator.runtimeAnimatorController == null && defaultController != null)
            animator.runtimeAnimatorController = defaultController;
        if (animator.avatar == null && defaultAvatar != null)
            animator.avatar = defaultAvatar;
        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    private static RuntimeAnimatorController FindDefaultAnimatorController()
    {
        string[] guids = AssetDatabase.FindAssets("PC_001 t:AnimatorController");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == null || path.Contains("/Old/")) continue;
            var asset = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
            if (asset != null && asset.name == "PC_001") return asset;
        }
        return null;
    }

    private static Avatar FindDefaultAvatar()
    {
        string[] pathsToTry = new[]
        {
            "Assets/Arts/Old/Model/Player001.fbx",
            "Assets/Arts/Player/FBX/Body/PC_M_Normal_Skin.fbx"
        };
        foreach (string path in pathsToTry)
        {
            if (string.IsNullOrEmpty(path)) continue;
            Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (Object o in all)
            {
                if (o is Avatar av && av.isHuman) return av;
            }
        }
        foreach (string guid in AssetDatabase.FindAssets("Player001 t:Model"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == null || !path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
            Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (Object o in all)
            {
                if (o is Avatar av && av.isHuman) return av;
            }
        }
        return null;
    }

    private static MovementSettings FindDefaultMovementSettings()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:MovementSettings"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<MovementSettings>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) return asset;
        }
        return null;
    }

    private static bool BuildRagdoll(GameObject root, Animator animator, Rigidbody rootRb, Transform modelRoot)
    {
        RagdollBuildSettings settings = FindRagdollBuildSettings();
        // 에너미와 동일: Bip001이 있으면 BIP 경로 우선 (Humanoid여도 BIP 본 구조이므로 Pelvis에 조인트 안 붙음)
        Transform pelvisTr = FindBoneInChildren(modelRoot, "Bip001");
        if (pelvisTr != null)
        {
            if (settings == null)
            {
                Debug.LogError("[PlayerPrefabGenerator] BIP 랙돌 빌드 실패: RagdollBuildSettings SO를 찾을 수 없습니다. Project에 RagdollBuildSettings를 생성·할당한 뒤 다시 시도하세요.");
                return false;
            }
            if (settings.boneOverrides == null || settings.boneOverrides.Length == 0)
            {
                Debug.LogError("[PlayerPrefabGenerator] BIP 랙돌 빌드 실패: RagdollBuildSettings의 boneOverrides가 비어 있습니다. boneOverrides에 값을 채운 뒤 다시 시도하세요.");
                return false;
            }
            var playerSettings = ScriptableObject.CreateInstance<RagdollBuildSettings>();
            playerSettings.totalMass = settings.totalMass;
            playerSettings.strength = settings.strength;
            playerSettings.flipForward = settings.flipForward;
            playerSettings.leftHips = settings.leftHips;
            playerSettings.leftKnee = settings.leftKnee;
            playerSettings.leftFoot = settings.leftFoot;
            playerSettings.rightHips = settings.rightHips;
            playerSettings.rightKnee = settings.rightKnee;
            playerSettings.rightFoot = settings.rightFoot;
            playerSettings.leftArm = settings.leftArm;
            playerSettings.leftElbow = settings.leftElbow;
            playerSettings.rightArm = settings.rightArm;
            playerSettings.rightElbow = settings.rightElbow;
            playerSettings.middleSpine = settings.middleSpine;
            playerSettings.head = settings.head;
            playerSettings.pelvis = "Bip001";
            playerSettings.boneOverrides = settings.boneOverrides;
            BuildRagdollBIP(root, rootRb, modelRoot, playerSettings);
            RemoveCharacterJointFromBip001(modelRoot);
            EnsureRagdollOffState(root, rootRb, root.GetComponent<Collider>());
            Object.DestroyImmediate(playerSettings);
            return true;
        }

        // Bip001 없을 때만 Humanoid 경로 (fallback)
        bool useHumanoid = animator != null && animator.avatar != null && animator.isHuman;
        Transform hipsTr = useHumanoid ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
        if (useHumanoid && hipsTr != null)
        {
            BuildRagdollHumanoid(root, animator, rootRb, hipsTr, settings);
            RemoveCharacterJointFromBip001(modelRoot);
            EnsureRagdollOffState(root, rootRb, root.GetComponent<Collider>());
            return true;
        }

        Debug.LogWarning("[PlayerPrefabGenerator] Humanoid도 BIP 본(Bip001)을 찾지 못해 랙돌을 생성하지 않습니다.");
        return true;
    }

    /// <summary> Bip001에 붙은 Character Joint를 제거 (자동 세팅이 되지 않을 경우 대비) </summary>
    private static void RemoveCharacterJointFromBip001(Transform modelRoot)
    {
        Transform bip001 = FindBoneInChildren(modelRoot, "Bip001");
        if (bip001 == null) return;
        var cj = bip001.GetComponent<CharacterJoint>();
        if (cj != null) Object.DestroyImmediate(cj);
    }

    /// <summary> 랙돌에 포함된 모든 본의 레이어를 Ragdoll로 설정 </summary>
    private static void SetRagdollBonesLayer(GameObject root, IEnumerable<Transform> ragdollBones)
    {
        int ragdollLayer = LayerMask.NameToLayer("Ragdoll");
        if (ragdollLayer < 0) return;
        foreach (Transform tr in ragdollBones)
        {
            if (tr != null && tr != root.transform)
                tr.gameObject.layer = ragdollLayer;
        }
    }

    /// <summary> 랙돌 본은 평상시 kinematic + Collider 비활성화 상태로 유지 </summary>
    private static void EnsureRagdollOffState(GameObject root, Rigidbody rootRb, Collider rootCollider)
    {
        if (root == null) return;
        foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true))
        {
            if (rb == null || rb.transform == root.transform) continue;
            rb.isKinematic = true;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
        foreach (var col in root.GetComponentsInChildren<Collider>(true))
        {
            if (col == null || col.transform == root.transform || col == rootCollider) continue;
            col.enabled = false;
        }
    }

    private static RagdollBuildSettings FindRagdollBuildSettings()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:RagdollBuildSettings"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<RagdollBuildSettings>(AssetDatabase.GUIDToAssetPath(guid));
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

    private static void BuildRagdollHumanoid(GameObject root, Animator animator, Rigidbody rootRb, Transform hipsTr, RagdollBuildSettings settings)
    {
        float totalMass = settings != null ? settings.totalMass : 20f;
        float strength = settings != null ? settings.strength : 0f;
        float massPerBone = Mathf.Max(0.1f, totalMass / RagdollBones.Length);
        float twist = (settings != null && settings.twistLimit > 0f) ? settings.twistLimit : (20f + strength);
        float swing = (settings != null && settings.swingLimit > 0f) ? settings.swingLimit : Mathf.Min(90f, 30f + strength);

        var boneToRb = new Dictionary<Transform, Rigidbody>();
        foreach (HumanBodyBones boneType in RagdollBones)
        {
            Transform tr = animator.GetBoneTransform(boneType);
            if (tr == null || tr == root.transform) continue;
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
        RagdollAttachmentBoneBuilder.Build(animator.transform, boneToRb, settings, "[PlayerPrefabGenerator]");
        SetRagdollBonesLayer(root, boneToRb.Keys);
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
            Debug.LogWarning("[PlayerPrefabGenerator] BIP Pelvis 본을 찾지 못했습니다. RagdollBuildSettings의 본 이름을 확인하세요.");
            return;
        }

        int count = bones.Count;
        var boneToRb = new Dictionary<Transform, Rigidbody>();

        foreach (var kv in bones)
        {
            Transform tr = kv.Value;
            if (tr.gameObject == root) continue;
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

        var pelvisTr = bones["Pelvis"];
        var existingCj = pelvisTr.GetComponent<CharacterJoint>();
        if (existingCj != null) Object.DestroyImmediate(existingCj);

        RagdollAttachmentBoneBuilder.Build(modelRoot, boneToRb, settings, "[PlayerPrefabGenerator]");
        SetRagdollBonesLayer(root, boneToRb.Keys);
        Debug.Log($"[PlayerPrefabGenerator] BIP 랙돌 생성 완료 (본 수={count})");
    }

    private static Transform GetRagdollParentBone(Animator animator, HumanBodyBones bone)
    {
        switch (bone)
        {
            case HumanBodyBones.Spine:
            case HumanBodyBones.LeftUpperLeg:
            case HumanBodyBones.RightUpperLeg: return animator.GetBoneTransform(HumanBodyBones.Hips);
            case HumanBodyBones.Chest:
            case HumanBodyBones.LeftUpperArm:
            case HumanBodyBones.RightUpperArm: return animator.GetBoneTransform(HumanBodyBones.Spine) ?? animator.GetBoneTransform(HumanBodyBones.Hips);
            case HumanBodyBones.UpperChest:
            case HumanBodyBones.Neck: return animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine);
            case HumanBodyBones.Head: return animator.GetBoneTransform(HumanBodyBones.Neck) ?? animator.GetBoneTransform(HumanBodyBones.Chest);
            case HumanBodyBones.LeftLowerArm: return animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            case HumanBodyBones.LeftHand: return animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            case HumanBodyBones.RightLowerArm: return animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            case HumanBodyBones.RightHand: return animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            case HumanBodyBones.LeftLowerLeg: return animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            case HumanBodyBones.LeftFoot: return animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            case HumanBodyBones.RightLowerLeg: return animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            case HumanBodyBones.RightFoot: return animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            default: return null;
        }
    }

    /// <summary>
    /// 기존 프리팹에서 Bip001(루트 펠비스)에 붙어 있는 Character Joint를 제거합니다.
    /// 수동 랙돌 세팅과 동일하게 Bip001에는 조인트가 없어야 이동 오류가 나지 않습니다.
    /// </summary>
    [MenuItem("Tools/Ragdoll/Remove CharacterJoint from Bip001 in all Prefabs")]
    public static void RemoveCharacterJointFromBip001InAllPrefabs()
    {
        const string bip001Name = "Bip001";
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        int removed = 0;
        int prefabsTouched = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) continue;
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
            if (prefabRoot == null) continue;
            Transform[] all = prefabRoot.GetComponentsInChildren<Transform>(true);
            bool anyRemoved = false;
            foreach (Transform t in all)
            {
                if (t.name != bip001Name) continue;
                var joint = t.GetComponent<CharacterJoint>();
                if (joint == null) continue;
                Object.DestroyImmediate(joint, true);
                anyRemoved = true;
                removed++;
            }
            if (anyRemoved)
            {
                prefabsTouched++;
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
            }
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Ragdoll] Bip001 CharacterJoint 제거 완료: {removed}개 제거, {prefabsTouched}개 프리팹 수정");
    }
}
