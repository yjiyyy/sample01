using UnityEngine;

/// <summary>
/// Create Ragdoll 창과 동일한 옵션으로 랙돌을 만들 때 사용.
/// BIP 본 이름 매핑 + Total Mass, Strength, Flip Forward.
/// 에셋을 만들면 EnemyPrefabGenerator가 프로젝트에서 찾아 사용합니다.
/// </summary>
[CreateAssetMenu(menuName = "Enemy/Ragdoll Build Settings", fileName = "RagdollBuildSettings")]
public class RagdollBuildSettings : ScriptableObject
{
    [Header("Create Ragdoll 옵션")]
    [Tooltip("전체 랙돌 질량. 각 본에 분배됩니다.")]
    public float totalMass = 20f;

    [Tooltip("조인트 강도 (0 = 기본, 높을수록 더 단단함).")]
    public float strength = 0f;

    [Tooltip("캐릭터 전방 방향 반전 (파란 축).")]
    public bool flipForward = false;

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
}
