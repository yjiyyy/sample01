using UnityEngine;

/// <summary>
/// BIP 랙돌 빌드 시 E_LV01_New04에서 추출한 수치를 초기값으로 사용. (프리팹 참조 없이 코드에 고정된 값)
/// 캐릭터 생성 시마다 이 초기값을 적용. boneOverrides로 특정 본만 선택적 덮어쓰기 가능.
/// totalMass/strength 등은 Humanoid 모드 또는 폴백용.
/// </summary>
[CreateAssetMenu(menuName = "Enemy/Ragdoll Build Settings", fileName = "RagdollBuildSettings")]
public class RagdollBuildSettings : ScriptableObject
{
    [System.Serializable]
    public class BoneOverride
    {
        public string boneKey;
        public float mass = -1f;
        public float colliderRadius = -1f;
        public float colliderHeight = -1f;
        public int colliderDirection;
        public Vector3 colliderCenter;
        public Vector3 jointAxis = new Vector3(0, 0, 1);
        public Vector3 jointSwingAxis = new Vector3(1, 0, 0);
        public float lowTwistLimit = float.NaN;
        public float highTwistLimit = float.NaN;
        public float swing1Limit = float.NaN;
        public float swing2Limit = float.NaN;
    }

    [Header("Humanoid/폴백용 (BIP는 아래 preset이 초기값)")]
    [Tooltip("전체 랙돌 질량. Humanoid 모드 또는 preset 없는 본에만 사용.")]
    public float totalMass = 20f;

    [Tooltip("조인트 강도. Humanoid 모드에서 twistLimit/swingLimit 0일 때 사용.")]
    public float strength = 0f;

    [Header("Character Joint (Humanoid/폴백용)")]
    [Tooltip("Twist 한계(도). BIP는 preset 사용.")]
    public float twistLimit = 0f;
    [Tooltip("Swing 한계(도). BIP는 preset 사용.")]
    public float swingLimit = 0f;

    [Tooltip("캐릭터 전방 방향 반전 (파란 축).")]
    public bool flipForward = false;

    [Header("본별 수치 (편집 가능, 빌드 시 이 값 사용)")]
    [Tooltip("비어 있으면 코드 preset 사용. '초기화' 버튼으로 채운 뒤 Inspector에서 각 본별 값 편집 가능.")]
    public BoneOverride[] boneOverrides;

    [System.Serializable]
    public class FXBloodDummyEntry
    {
        [Tooltip("더미 이름. 예: FX_Blood_Head01")]
        public string dummyName;
        [Tooltip("부모가 될 본 이름")]
        public string parentBoneName;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
    }

    [Header("FX Blood (슬라이스 피 이펙트 더미)")]
    [Tooltip("비어 있으면 기본값(10개) 사용. 빌드 시 각 본 아래 더미 생성.")]
    public FXBloodDummyEntry[] fxBloodDummies;
    [Tooltip("SliceBloodEffectSpawner에 할당할 피 이펙트 프리팹")]
    public GameObject bloodGushPrefab;

    [Header("BIP 본 이름 (Transform 이름과 정확히 일치)")]
    public string pelvis = "Bip001";
    public string leftHips = "Bip001 L Thigh";
    public string leftKnee = "Bip001 L Calf";
    public string leftFoot = "Bip001 L Foot";
    public string rightHips = "Bip001 R Thigh";
    public string rightKnee = "Bip001 R Calf";
    public string rightFoot = "Bip001 R Foot";
    public string leftArm = "Bip001 L UpperArm";
    public string leftElbow = "Bip001 L Forearm";
    public string rightArm = "Bip001 R UpperArm";
    public string rightElbow = "Bip001 R Forearm";
    public string middleSpine = "Bip001 Spine1";
    public string head = "Bip001 Head";

    public BoneOverride GetOverride(string boneKey)
    {
        if (boneOverrides == null) return null;
        foreach (var o in boneOverrides)
        {
            if (o != null && o.boneKey == boneKey) return o;
        }
        return null;
    }

    /// <summary>base 위에 so 값으로 덮어쓰기. so에 유효한 값이 있을 때만 적용. axis/swingAxis는 preset 유지.</summary>
    public static BoneOverride Merge(BoneOverride baseVal, BoneOverride so)
    {
        if (baseVal == null) return so;
        if (so == null) return baseVal;
        return new BoneOverride
        {
            boneKey = baseVal.boneKey,
            mass = so.mass > 0f ? so.mass : baseVal.mass,
            colliderRadius = so.colliderRadius >= 0f ? so.colliderRadius : baseVal.colliderRadius,
            colliderHeight = so.colliderHeight >= 0f ? so.colliderHeight : baseVal.colliderHeight,
            colliderDirection = so.colliderRadius >= 0f ? so.colliderDirection : baseVal.colliderDirection,
            colliderCenter = so.colliderRadius >= 0f ? so.colliderCenter : baseVal.colliderCenter,
            jointAxis = baseVal.jointAxis,
            jointSwingAxis = baseVal.jointSwingAxis,
            lowTwistLimit = !float.IsNaN(so.lowTwistLimit) ? so.lowTwistLimit : baseVal.lowTwistLimit,
            highTwistLimit = !float.IsNaN(so.highTwistLimit) ? so.highTwistLimit : baseVal.highTwistLimit,
            swing1Limit = !float.IsNaN(so.swing1Limit) ? so.swing1Limit : baseVal.swing1Limit,
            swing2Limit = !float.IsNaN(so.swing2Limit) ? so.swing2Limit : baseVal.swing2Limit,
        };
    }

    /// <summary>
    /// E_LV01_New04에서 추출한 초기값(코드에 고정). 캐릭터 생성 시 이 값을 사용.
    /// 프리팹 참조 없이 항상 이 수치 적용. SO boneOverrides로 선택적 덮어쓰기 가능.
    /// </summary>
    public static BoneOverride[] GetE_LV01PresetOverrides()
    {
        return new BoneOverride[]
        {
            new BoneOverride { boneKey = "Pelvis", mass = 3f, colliderRadius = 0.16f, colliderHeight = 0.41f, colliderDirection = 1, colliderCenter = new Vector3(-0.03f, 0f, 0.08f) },
            new BoneOverride { boneKey = "LeftHips", mass = 5f, colliderRadius = 0.18f, colliderHeight = 0.54f, colliderDirection = 0, colliderCenter = new Vector3(-0.15f, -0.04f, 0f), jointAxis = new Vector3(0f, 0f, 1f), jointSwingAxis = new Vector3(1f, 0f, 0f), lowTwistLimit = -20f, highTwistLimit = 20f, swing1Limit = 30f, swing2Limit = 30f },
            new BoneOverride { boneKey = "LeftKnee", mass = 3f, colliderRadius = 0.08f, colliderHeight = 0.2f, colliderDirection = 0, colliderCenter = new Vector3(-0.15f, 0f, 0f), jointAxis = new Vector3(0f, 0.5f, 1f), jointSwingAxis = new Vector3(1f, 0f, 0f), lowTwistLimit = -90f, highTwistLimit = 90f, swing1Limit = 30f, swing2Limit = 30f },
            new BoneOverride { boneKey = "LeftFoot", mass = 1f, colliderRadius = 0.06f, colliderHeight = 0.3f, colliderDirection = 1, colliderCenter = new Vector3(-0.05f, 0.05f, 0f), jointAxis = new Vector3(0f, 0f, 1f), jointSwingAxis = new Vector3(1f, 0f, 0f), lowTwistLimit = -20f, highTwistLimit = 20f, swing1Limit = 30f, swing2Limit = 30f },
            new BoneOverride { boneKey = "RightHips", mass = 3f, colliderRadius = 0.08f, colliderHeight = 0.3f, colliderDirection = 0, colliderCenter = new Vector3(-0.1f, 0f, 0f), jointAxis = new Vector3(0f, -0.5f, 1f), jointSwingAxis = new Vector3(1f, 0f, 0f), lowTwistLimit = -90f, highTwistLimit = 90f, swing1Limit = 30f, swing2Limit = 30f },
            new BoneOverride { boneKey = "RightKnee", mass = 3f, colliderRadius = 0.08f, colliderHeight = 0.3f, colliderDirection = 0, colliderCenter = new Vector3(-0.1f, 0f, 0f), jointAxis = new Vector3(0f, 0f, -1f), jointSwingAxis = new Vector3(1f, 0f, 0f), lowTwistLimit = -90f, highTwistLimit = 30f, swing1Limit = 45f, swing2Limit = 90f },
            new BoneOverride { boneKey = "RightFoot", mass = 2f, colliderRadius = 0.07f, colliderHeight = 0.2f, colliderDirection = 0, colliderCenter = new Vector3(-0.1f, 0f, 0f), jointAxis = new Vector3(0f, 0f, 1f), jointSwingAxis = new Vector3(1f, 0f, 0f), lowTwistLimit = -88.55f, highTwistLimit = -0.24f, swing1Limit = 30f, swing2Limit = 30f },
            new BoneOverride { boneKey = "MiddleSpine", mass = 5f, colliderRadius = 0.21f, colliderHeight = 0.51f, colliderDirection = 0, colliderCenter = new Vector3(-0.18f, 0f, 0f), jointAxis = new Vector3(0f, 0f, 1f), jointSwingAxis = new Vector3(1f, 0f, 0f), lowTwistLimit = -20f, highTwistLimit = 20f, swing1Limit = 30f, swing2Limit = 10f },
            new BoneOverride { boneKey = "Head", mass = 1f, colliderRadius = 0.06f, colliderHeight = 0.3f, colliderDirection = 1, colliderCenter = new Vector3(-0.05f, 0.05f, 0f), jointAxis = new Vector3(0f, 0f, 1f), jointSwingAxis = new Vector3(1f, 0f, 0f), lowTwistLimit = -20f, highTwistLimit = 20f, swing1Limit = 30f, swing2Limit = 30f },
            new BoneOverride { boneKey = "LeftArm", mass = 3f, colliderRadius = 0.08f, colliderHeight = 0.2f, colliderDirection = 0, colliderCenter = new Vector3(-0.15f, 0f, 0f), jointAxis = new Vector3(0f, 0.5f, 1f), jointSwingAxis = new Vector3(1f, 0f, 0f), lowTwistLimit = -90f, highTwistLimit = 90f, swing1Limit = 30f, swing2Limit = 30f },
            new BoneOverride { boneKey = "LeftElbow", mass = 2f, colliderRadius = 0.07f, colliderHeight = 0.2f, colliderDirection = 0, colliderCenter = new Vector3(-0.14f, 0f, 0f), jointAxis = new Vector3(0f, 0f, -1f), jointSwingAxis = new Vector3(1f, 0f, 0f), lowTwistLimit = 34.96f, highTwistLimit = 130.71f, swing1Limit = 30f, swing2Limit = 30f },
            new BoneOverride { boneKey = "RightArm", mass = 3f, colliderRadius = 0.08f, colliderHeight = 0.2f, colliderDirection = 0, colliderCenter = new Vector3(-0.15f, 0f, 0f), jointAxis = new Vector3(0f, -0.5f, 1f), jointSwingAxis = new Vector3(1f, 0f, 0f), lowTwistLimit = -90f, highTwistLimit = 90f, swing1Limit = 30f, swing2Limit = 30f },
            new BoneOverride { boneKey = "RightElbow", mass = 2f, colliderRadius = 0.07f, colliderHeight = 0.2f, colliderDirection = 0, colliderCenter = new Vector3(-0.1f, 0f, 0f), jointAxis = new Vector3(0f, 0f, 1f), jointSwingAxis = new Vector3(1f, 0f, 0f), lowTwistLimit = -89.07f, highTwistLimit = 2f, swing1Limit = 30f, swing2Limit = 30f },
        };
    }

    /// <summary>FX_Blood 더미 기본값. 01=잘린 쪽, 02=몸통 쪽</summary>
    public static FXBloodDummyEntry[] GetDefaultFXBloodDummies()
    {
        return new FXBloodDummyEntry[]
        {
            new FXBloodDummyEntry { dummyName = "FX_Blood_Head01", parentBoneName = "Bip001 HeadNub", localPosition = Vector3.zero, localEulerAngles = Vector3.zero },
            new FXBloodDummyEntry { dummyName = "FX_Blood_Head02", parentBoneName = "Bip001 Neck", localPosition = Vector3.zero, localEulerAngles = Vector3.zero },
            new FXBloodDummyEntry { dummyName = "FX_Blood_L_Arm01", parentBoneName = "Bip001 L Forearm", localPosition = Vector3.zero, localEulerAngles = Vector3.zero },
            new FXBloodDummyEntry { dummyName = "FX_Blood_L_Arm02", parentBoneName = "Bip001", localPosition = Vector3.zero, localEulerAngles = Vector3.zero },
            new FXBloodDummyEntry { dummyName = "FX_Blood_R_Arm01", parentBoneName = "Bip001 R Forearm", localPosition = Vector3.zero, localEulerAngles = Vector3.zero },
            new FXBloodDummyEntry { dummyName = "FX_Blood_R_Arm02", parentBoneName = "Bip001", localPosition = Vector3.zero, localEulerAngles = Vector3.zero },
            new FXBloodDummyEntry { dummyName = "FX_Blood_L_Leg01", parentBoneName = "Bip001 L Calf", localPosition = Vector3.zero, localEulerAngles = Vector3.zero },
            new FXBloodDummyEntry { dummyName = "FX_Blood_L_Leg02", parentBoneName = "Bip001", localPosition = Vector3.zero, localEulerAngles = Vector3.zero },
            new FXBloodDummyEntry { dummyName = "FX_Blood_R_Leg01", parentBoneName = "Bip001 R Calf", localPosition = Vector3.zero, localEulerAngles = Vector3.zero },
            new FXBloodDummyEntry { dummyName = "FX_Blood_R_Leg02", parentBoneName = "Bip001", localPosition = Vector3.zero, localEulerAngles = Vector3.zero },
        };
    }
}
