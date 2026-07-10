using UnityEngine;

/// <summary>
/// 화면 기준 이동 입력 제공자. 카메라의 방향별 오프셋 판정에 사용됩니다.
/// </summary>
public interface ICameraMoveInputProvider
{
    /// <summary>화면 기준 이동 입력. x = 우(+)/좌(-), y = 상(+)/하(-), 대략 [-1, 1].</summary>
    Vector2 GetCameraMoveInput();
}

/// <summary>
/// 디아블로식 고정 각도 카메라. 회전·거리를 고정하고 캐릭터를 직접 추종합니다.
/// </summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[ExecuteAlways]
public class DiabloStyleCamera : MonoBehaviour
{
    [Header("카메라 각도·거리")]
    [Tooltip("0에 가까울수록 뒤에서, 90에 가까울수록 완전 탑뷰")]
    [Range(5f, 89.9f)]
    [SerializeField] private float pitchDegrees = 35f;

    [Tooltip("월드 Y축 기준 좌우 회전 (고정)")]
    [Range(0f, 360f)]
    [SerializeField] private float yawDegrees = 0f;

    [Tooltip("앵커(추적 기준점)와 카메라 사이 거리")]
    [Min(0.1f)]
    [SerializeField] private float distance = 15f;

    [Header("렌즈")]
    [Tooltip("켜면 원근 없음(탑뷰에 가깝게). 끄면 원근감 적용")]
    [SerializeField] private bool useOrthographic;

    [Min(0.1f)]
    [SerializeField] private float orthographicSize = 10f;

    [Range(10f, 90f)]
    [SerializeField] private float fieldOfView = 40f;

    [Header("추적 기준점")]
    [Tooltip("지정하면 이 Transform 위치를 추적 기준점으로 사용합니다. (캐릭터 자식 빈 오브젝트 권장)")]
    [SerializeField] private Transform followPoint;

    [Tooltip("followPoint가 없을 때 target 로컬 오프셋 (발=0, 가슴≈1)")]
    [SerializeField] private Vector3 targetLocalOffset = Vector3.zero;

    [Header("상/하/좌/우 이동 오프셋 (월드 고정)")]
    [Tooltip("켜면 캐릭터 이동 입력 방향에 따라 오프셋·Pitch·Distance를 적용합니다.")]
    [SerializeField] private bool enableDirectionalOffset = false;

    [Tooltip("위로 이동 시 카메라 기준점 월드 오프셋")]
    [SerializeField] private Vector3 moveUpOffset = Vector3.zero;
    [Tooltip("위로 이동 시 Pitch 오프셋")]
    [SerializeField] private float moveUpPitchOffset = 0f;
    [Tooltip("위로 이동 시 Distance 오프셋")]
    [SerializeField] private float moveUpDistanceOffset = 0f;

    [Tooltip("아래로 이동 시 카메라 기준점 월드 오프셋")]
    [SerializeField] private Vector3 moveDownOffset = Vector3.zero;
    [Tooltip("아래로 이동 시 Pitch 오프셋")]
    [SerializeField] private float moveDownPitchOffset = 0f;
    [Tooltip("아래로 이동 시 Distance 오프셋")]
    [SerializeField] private float moveDownDistanceOffset = 0f;

    [Tooltip("왼쪽으로 이동 시 카메라 기준점 월드 오프셋")]
    [SerializeField] private Vector3 moveLeftOffset = Vector3.zero;
    [Tooltip("왼쪽으로 이동 시 Pitch 오프셋")]
    [SerializeField] private float moveLeftPitchOffset = 0f;
    [Tooltip("왼쪽으로 이동 시 Distance 오프셋")]
    [SerializeField] private float moveLeftDistanceOffset = 0f;

    [Tooltip("오른쪽으로 이동 시 카메라 기준점 월드 오프셋")]
    [SerializeField] private Vector3 moveRightOffset = Vector3.zero;
    [Tooltip("오른쪽으로 이동 시 Pitch 오프셋")]
    [SerializeField] private float moveRightPitchOffset = 0f;
    [Tooltip("오른쪽으로 이동 시 Distance 오프셋")]
    [SerializeField] private float moveRightDistanceOffset = 0f;

    [Tooltip("입력이 들어올 때(시작) 오프셋이 목표로 블렌드되는 시간")]
    [Min(0f)]
    [SerializeField] private float offsetStartBlendTime = 0.2f;
    [Tooltip("입력이 없을 때(끝) 오프셋이 0으로 블렌드되는 시간")]
    [Min(0f)]
    [SerializeField] private float offsetEndBlendTime = 0.3f;
    [Tooltip("이 세기 이하의 입력은 무시")]
    [Range(0f, 1f)]
    [SerializeField] private float inputDeadZone = 0.1f;

    [Header("Occluder Fade (가림 반투명)")]
    [SerializeField] private bool enableOcclusionFade;
    [SerializeField] private bool enableRayOcclusionFade = true;
    [SerializeField] private bool enableInsideColliderFade = true;
    [SerializeField] private bool enableBuildingVolumeFade;
    [Tooltip("가릴 수 있는 오브젝트 레이어 (건물 Wall 등)")]
    [SerializeField] private LayerMask occluderLayers;
    [Range(0f, 1f)]
    [SerializeField] private float occlusionMinAlpha = 0.25f;
    [SerializeField] private float occlusionFadeInSpeed = 8f;
    [SerializeField] private float occlusionFadeOutSpeed = 6f;
    [Tooltip("0이면 얇은 Ray, 0.2~0.5면 SphereCast")]
    [Min(0f)]
    [SerializeField] private float occlusionCastRadius;
    [Tooltip("카메라가 Collider 안인지 검사할 OverlapSphere 반경")]
    [Min(0.01f)]
    [SerializeField] private float occlusionInsideCheckRadius = 0.05f;
    [Min(1)]
    [SerializeField] private int occlusionMaxCount = 16;

    private Camera cam;
    private Transform target;
    private DiabloStyleCameraOcclusionFade occlusionFade;
    private bool occlusionFadeWasEnabled;
    private ICameraMoveInputProvider moveInputProvider;
    private Vector3 blendedTargetOffset;
    private float blendedPitchOffset;
    private float blendedDistanceOffset;

    public Transform Target => target;
    public float PitchDegrees => pitchDegrees;
    public float YawDegrees => yawDegrees;
    public float Distance => distance;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        ApplyLens();
        EnsureOcclusionFade();
    }

    private void OnEnable()
    {
        if (cam == null)
            cam = GetComponent<Camera>();

        ApplyLens();
        if (!Application.isPlaying)
            ApplyRigToTransform(ResolveAnchorForGizmo());
    }

    private void OnDisable()
    {
        occlusionFade?.RestoreAll();
    }

    private void OnValidate()
    {
        if (cam == null)
            cam = GetComponent<Camera>();

        pitchDegrees = Mathf.Clamp(pitchDegrees, 5f, 89.9f);
        distance = Mathf.Max(0.1f, distance);

        ApplyLens();

        if (!Application.isPlaying)
            ApplyRigToTransform(ResolveAnchorForGizmo());

        if (Application.isPlaying)
            ApplyOcclusionFadeSettings();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
            return;

        transform.rotation = GetFixedRotation();

        if (target == null)
            return;

        UpdateDirectionalOffset();
        ApplyRigToTransform(GetFollowWorldPoint());
        UpdateOcclusionFade();
    }

    public void SetTarget(Transform newTarget, bool snap = true)
    {
        target = newTarget;
        occlusionFade?.SetIgnoreRoot(target);
        moveInputProvider = target != null ? target.GetComponentInChildren<ICameraMoveInputProvider>() : null;
        blendedTargetOffset = Vector3.zero;
        blendedPitchOffset = 0f;
        blendedDistanceOffset = 0f;

        if (target != null && snap)
            SnapToTarget();
    }

    public void SnapToTarget()
    {
        if (target == null)
            return;

        blendedTargetOffset = Vector3.zero;
        blendedPitchOffset = 0f;
        blendedDistanceOffset = 0f;
        ApplyRigToTransform(GetFollowWorldPoint());
    }

    public Vector3 GetFollowWorldPoint()
    {
        Vector3 basePoint;
        if (followPoint != null)
            basePoint = followPoint.position;
        else if (target != null)
            basePoint = target.TransformPoint(targetLocalOffset);
        else
            return transform.position - GetCameraOffset();

        if (Application.isPlaying)
            basePoint += blendedTargetOffset;

        return basePoint;
    }

    public Quaternion GetFixedRotation() => Quaternion.Euler(GetCurrentPitch(), yawDegrees, 0f);

    public Vector3 GetCameraOffset() => GetFixedRotation() * new Vector3(0f, 0f, -GetCurrentDistance());

    private float GetCurrentDistance()
    {
        if (!Application.isPlaying)
            return distance;

        return Mathf.Max(0.1f, distance + blendedDistanceOffset);
    }

    private float GetCurrentPitch()
    {
        if (!Application.isPlaying)
            return pitchDegrees;

        return Mathf.Clamp(pitchDegrees + blendedPitchOffset, 5f, 89.9f);
    }

    private void UpdateDirectionalOffset()
    {
        Vector2 input = Vector2.zero;
        if (enableDirectionalOffset)
        {
            if (moveInputProvider == null && target != null)
                moveInputProvider = target.GetComponentInChildren<ICameraMoveInputProvider>();

            if (moveInputProvider != null)
                input = moveInputProvider.GetCameraMoveInput();
        }

        if (input.magnitude > 1f)
            input.Normalize();

        bool hasInput = enableDirectionalOffset && input.magnitude > inputDeadZone;

        Vector3 targetOffset = Vector3.zero;
        float targetPitch = 0f;
        float targetDistance = 0f;
        if (hasInput)
        {
            float upWeight = Mathf.Max(0f, input.y);
            float downWeight = Mathf.Max(0f, -input.y);
            float rightWeight = Mathf.Max(0f, input.x);
            float leftWeight = Mathf.Max(0f, -input.x);

            targetOffset = moveUpOffset * upWeight + moveDownOffset * downWeight
                + moveRightOffset * rightWeight + moveLeftOffset * leftWeight;
            targetPitch = moveUpPitchOffset * upWeight + moveDownPitchOffset * downWeight
                + moveRightPitchOffset * rightWeight + moveLeftPitchOffset * leftWeight;
            targetDistance = moveUpDistanceOffset * upWeight + moveDownDistanceOffset * downWeight
                + moveRightDistanceOffset * rightWeight + moveLeftDistanceOffset * leftWeight;
        }

        float blendTime = hasInput ? offsetStartBlendTime : offsetEndBlendTime;
        float factor = blendTime <= 0.0001f ? 1f : 1f - Mathf.Exp(-Time.deltaTime / blendTime);

        blendedTargetOffset = Vector3.Lerp(blendedTargetOffset, targetOffset, factor);
        blendedPitchOffset = Mathf.Lerp(blendedPitchOffset, targetPitch, factor);
        blendedDistanceOffset = Mathf.Lerp(blendedDistanceOffset, targetDistance, factor);
    }

    public Vector3 ResolveAnchorForGizmo()
    {
        if (target != null || followPoint != null)
            return GetFollowWorldPoint();

        return transform.position - GetCameraOffset();
    }

    private void ApplyLens()
    {
        if (cam == null)
            return;

        cam.orthographic = useOrthographic;
        if (useOrthographic)
            cam.orthographicSize = orthographicSize;
        else
            cam.fieldOfView = fieldOfView;
    }

    private void ApplyRigToTransform(Vector3 anchor)
    {
        transform.rotation = GetFixedRotation();
        transform.position = anchor + GetCameraOffset();
    }

    private void EnsureOcclusionFade()
    {
        occlusionFade ??= new DiabloStyleCameraOcclusionFade();

        if (occluderLayers.value == 0)
            occluderLayers = LayerMask.GetMask("Wall", "Prop");

        ApplyOcclusionFadeSettings();
        occlusionFade.SetIgnoreRoot(target);
    }

    private void ApplyOcclusionFadeSettings()
    {
        if (occlusionFade == null)
            return;

        occlusionFade.Configure(
            occluderLayers,
            occlusionMinAlpha,
            occlusionFadeInSpeed,
            occlusionFadeOutSpeed,
            occlusionCastRadius,
            occlusionInsideCheckRadius,
            occlusionMaxCount,
            enableRayOcclusionFade,
            enableInsideColliderFade,
            enableBuildingVolumeFade);
        occlusionFade.SetIgnoreRoot(target);
    }

    private void UpdateOcclusionFade()
    {
        if (!enableOcclusionFade || occlusionFade == null)
        {
            if (occlusionFadeWasEnabled)
            {
                occlusionFade?.RestoreAll();
                occlusionFadeWasEnabled = false;
            }
            return;
        }

        occlusionFadeWasEnabled = true;
        occlusionFade.Update(transform.position, GetFollowWorldPoint());
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (target == null && followPoint == null)
            return;

        Vector3 anchor = ResolveAnchorForGizmo();
        Vector3 camPos = anchor + GetCameraOffset();

        Gizmos.color = new Color(1f, 0.35f, 0.85f, 0.95f);
        Gizmos.DrawSphere(anchor, 0.2f);

        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
        Gizmos.DrawLine(camPos, anchor);
    }
#endif
}
