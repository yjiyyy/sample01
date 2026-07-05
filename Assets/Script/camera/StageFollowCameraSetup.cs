using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;

/// <summary>
/// 스테이지 Follow 카메라에 Dead Zone + 부드러운 추적 값을 적용합니다.
/// </summary>
public static class StageFollowCameraSetup
{
    // 화면 중앙 자유 존 (0~1, 화면 비율)
    private static readonly Vector2 DeadZoneSize = new Vector2(0.35f, 0.25f);

    // 존 밖으로 나갔을 때 화면 프레이밍 복귀 속도 (작을수록 빠름)
    private static readonly Vector3 ComposerDamping = new Vector3(1.5f, 1.5f, 0f);

    // 월드 공간에서 카메라가 늦게 따라오는 정도 (작을수록 빠름)
    private static readonly Vector3 FollowPositionDamping = new Vector3(1.5f, 0.5f, 1.5f);

    public static void Apply(CinemachineCamera camera)
    {
        if (camera == null)
            return;

        var follow = camera.GetComponent<CinemachineFollow>();
        if (follow != null)
        {
            var tracker = follow.TrackerSettings;
            tracker.PositionDamping = FollowPositionDamping;
            follow.TrackerSettings = tracker;
        }

        var composer = camera.GetComponent<CinemachinePositionComposer>();
        if (composer != null)
        {
            var composition = composer.Composition;
            composition.ScreenPosition = Vector2.zero;
            composition.DeadZone.Enabled = true;
            composition.DeadZone.Size = DeadZoneSize;
            composer.Composition = composition;

            composer.Damping = ComposerDamping;
            composer.CenterOnActivate = false;
        }
    }
}
