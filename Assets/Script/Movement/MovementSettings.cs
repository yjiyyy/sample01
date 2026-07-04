using UnityEngine;

/// <summary>
/// 플레이어·몬스터 공용 배경 충돌 설정.
/// groundMask = 지면 판정·스텝, blockMask = Wall|Prop 막기, 슬라이드 캐스트는 solid(ground+block).
/// </summary>
[CreateAssetMenu(menuName = "Movement/MovementSettings")]
public class MovementSettings : ScriptableObject
{
    [Header("레이어")]
    [Tooltip("도로·인도·계단 등 '걸을 수 있는' 면의 레이어.\n발밑 지면 판정, 스텝 후 발 지지 확인, 경사면 슬라이드 판정에 사용합니다.")]
    public LayerMask groundMask;

    [Tooltip("벽·차량·고정 소품 등 '몸이 겹치면 안 되는' 배경 레이어.\n목표 위치에서 캡슐이 겹치면 이동을 막습니다. (Wall, Prop 권장)")]
    public LayerMask blockMask;

    [Header("충돌·슬라이드")]
    [Tooltip("벽/지면과의 여유 거리(m). 너무 크면 벽에서 멀어지고, 0에 가까우면 끼임이 늘 수 있습니다.")]
    public float collisionSkin = 0.02f;

    [Tooltip("충돌면 법선의 Y값이 이 이상이면 '완만한 바닥/경사'로 봅니다.\n대각선으로 경사를 밀 때 이 방향으로 미끄러집니다. (0.75 ? 41° 이하)")]
    [Range(0.5f, 1f)]
    public float floorSlopeThreshold = 0.75f;

    [Tooltip("벽·경사에 막혔을 때 미끄러짐을 반복 계산하는 횟수.\n0이면 사실상 미끄러지지 않고 벽 앞에서 멈춥니다. 대각선 슬라이드는 2~3 권장.")]
    [Range(0, 4)]
    public int slideIterations = 2;

    [Tooltip("이보다 짧은 이동은 무시합니다. 떨림 방지용이라 보통 0.002 전후면 충분합니다.")]
    public float tinyMoveThreshold = 0.002f;

    [Header("스텝 (턱·계단)")]
    [Tooltip("이 높이(m) 이하의 턱·계단은 자동으로 올라탑니다. 0.3이면 약 30cm까지.")]
    public float maxStepHeight = 0.3f;

    [Tooltip("스텝 높이를 찾을 때 이진 탐색 반복 횟수. 클수록 정밀하지만 비용이 조금 늘어납니다.")]
    [Range(1, 8)]
    public int stepSearchIterations = 5;

    [Tooltip("발밑 지면을 확인할 때 아래로 레이를 쏘는 깊이(m).")]
    public float floorCheckDepth = 0.15f;

    [Tooltip("스텝 시 앞쪽을 얼마나 멀리 살펴볼지(m). 너무 작으면 턱을 못 찾을 수 있습니다.")]
    public float minStepProbeDistance = 0.15f;

    [Header("머리 공간 (낮은 천장)")]
    [Tooltip("수평 이동 시 머리가 천장(Ground/Wall/Prop)에 닿기 전에 이동량을 줄입니다.")]
    public bool enableHeadroomClamp = true;

    [Tooltip("캡슐 상단에서 검사할 머리 원통 비율 (0.35 = 위쪽 35%).")]
    [Range(0.15f, 0.6f)]
    public float headPortion = 0.35f;

    [Tooltip("머리공간 이진 탐색 반복 횟수.")]
    [Range(2, 8)]
    public int headroomSearchIterations = 4;

    [Header("성능")]
    [Tooltip("겹침 검사용 버퍼 크기. 씬에 겹치는 콜라이더가 많으면 8~16으로 올리세요.")]
    public int overlapBufferSize = 16;

    /// <summary>슬라이드 CapsuleCast 대상 (Ground + Wall + Prop).</summary>
    public LayerMask SolidMask => groundMask | blockMask;

    private void OnValidate()
    {
        if (groundMask == 0)
        {
            int ground = LayerMask.NameToLayer("Ground");
            if (ground >= 0)
                groundMask = 1 << ground;
        }

        if (blockMask == 0)
        {
            int wall = LayerMask.NameToLayer("Wall");
            int prop = LayerMask.NameToLayer("Prop");
            if (wall >= 0)
                blockMask |= 1 << wall;
            if (prop >= 0)
                blockMask |= 1 << prop;
        }

        if (maxStepHeight < 0f)
            maxStepHeight = 0f;

        if (collisionSkin < 0f)
            collisionSkin = 0f;

        if (overlapBufferSize < 4)
            overlapBufferSize = 4;
    }
}
