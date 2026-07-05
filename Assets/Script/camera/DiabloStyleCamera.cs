using UnityEngine;

/// <summary>
/// 디아블로식 고정 각도 카메라. 회전·거리를 설정으로 고정하고 위치만 Dead Zone + 추적으로 이동합니다.
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
    [Tooltip("지정하면 이 Transform 위치를 Dead Zone 판정에 사용합니다. (캐릭터 자식 빈 오브젝트 권장)")]
    [SerializeField] private Transform followPoint;

    [Tooltip("followPoint가 없을 때 target 로컬 오프셋 (발=0, 가슴≈1)")]
    [SerializeField] private Vector3 targetLocalOffset = Vector3.zero;

    [Header("Dead Zone (화면 비율 0~1)")]
    [SerializeField] private Vector2 deadZoneSize = new Vector2(0.2f, 0.2f);

    [Tooltip("켜면 X/Y 값이 같을 때 화면 픽셀 기준으로 비슷한 크기 (세로는 항상 화면 높이 비율)")]
    [SerializeField] private bool uniformScreenDeadZone = false;

    [Tooltip("켜면 Dead Zone 밖으로 나가면 화면 정중앙까지 추적 (끄면 경계까지만)")]
    [SerializeField] private bool recenterToScreenCenter = false;

    [Header("추적")]
    [Tooltip("0이면 즉시 따라감. 0.15~0.3 권장")]
    [Min(0f)]
    [SerializeField] private float followSmoothTime = 0.2f;

    [Tooltip("화면 중앙 도달 판정 (viewport 단위)")]
    [Min(0.0001f)]
    [SerializeField] private float centerSnapThreshold = 0.008f;

    [Header("기즈모")]
    [SerializeField] private bool showDeadZoneGizmo = true;
    [SerializeField] private bool showDeadZoneInGameView = true;
    [SerializeField] private bool showFollowPointGizmo = true;
    [SerializeField] private float gameViewBorderThickness = 2f;
    [SerializeField] private Color deadZoneGizmoColor = new Color(1f, 0.85f, 0.1f, 0.9f);
    [SerializeField] private Color gameViewDeadZoneFillColor = new Color(1f, 0.85f, 0.1f, 0.08f);
    [SerializeField] private Color distanceGizmoColor = new Color(0.3f, 0.8f, 1f, 0.8f);
    [SerializeField] private Color followPointGizmoColor = new Color(1f, 0.35f, 0.85f, 0.95f);

    private Camera cam;
    private Transform target;
    private Vector3 anchorPosition;
    private bool initialized;
    private bool isRecentering;
    private DiabloStyleCameraDeadZoneOverlay gameViewOverlay;
    private float viewportFollowSensX = 1f;
    private float viewportFollowSensY = 1f;
    private float lastViewportSensDepth = -1f;
    private int lastViewportSensFrame = -1;

    public Transform Target => target;
    public float PitchDegrees => pitchDegrees;
    public float YawDegrees => yawDegrees;
    public float Distance => distance;
    public Vector2 DeadZoneSize => deadZoneSize;
    public bool ShowDeadZoneGizmo => showDeadZoneGizmo;
    public bool ShowDeadZoneInGameView => showDeadZoneInGameView;

    public void GetDeadZoneViewportHalfExtents(out float halfWidth, out float halfHeight)
    {
        halfWidth = deadZoneSize.x * 0.5f;
        halfHeight = deadZoneSize.y * 0.5f;

        if (uniformScreenDeadZone)
        {
            if (cam == null)
                cam = GetComponent<Camera>();

            if (cam != null)
                halfWidth /= cam.aspect;
        }

        halfWidth = Mathf.Min(halfWidth, 0.5f);
        halfHeight = Mathf.Min(halfHeight, 0.5f);
    }

    public void GetDeadZonePixelSize(out float widthPixels, out float heightPixels)
    {
        GetDeadZoneViewportHalfExtents(out float halfW, out float halfH);
        widthPixels = halfW * 2f * Screen.width;
        heightPixels = halfH * 2f * Screen.height;
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();
        ApplyLens();
        EnsureGameViewOverlay();
    }

    private void OnEnable()
    {
        if (cam == null)
            cam = GetComponent<Camera>();

        ApplyLens();
        if (!Application.isPlaying)
            ApplyRigToTransform(ResolveAnchorForGizmo());
    }

    private void OnValidate()
    {
        if (cam == null)
            cam = GetComponent<Camera>();

        pitchDegrees = Mathf.Clamp(pitchDegrees, 5f, 89.9f);
        distance = Mathf.Max(0.1f, distance);
        deadZoneSize.x = Mathf.Clamp(deadZoneSize.x, 0f, 1f);
        deadZoneSize.y = Mathf.Clamp(deadZoneSize.y, 0f, 1f);

        ApplyLens();

        if (!Application.isPlaying)
            ApplyRigToTransform(ResolveAnchorForGizmo());

        if (Application.isPlaying)
            RefreshGameViewOverlay();
        else if (gameViewOverlay != null)
            gameViewOverlay.RefreshVisibility();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
            return;

        transform.rotation = GetFixedRotation();

        if (target == null)
            return;

        if (!initialized)
        {
            SnapToTarget();
            return;
        }

        UpdateAnchorForDeadZone();
        ApplyRigToTransform(GetAnchorWithFollowHeight());
    }

    public void SetTarget(Transform newTarget, bool snap = true)
    {
        target = newTarget;
        initialized = false;
        isRecentering = false;

        if (target != null && snap)
            SnapToTarget();
    }

    public void SnapToTarget()
    {
        anchorPosition = GetFollowWorldPoint();
        initialized = true;
        isRecentering = false;
        ApplyRigToTransform(GetAnchorWithFollowHeight());
    }

    public Vector3 GetFollowWorldPoint()
    {
        if (followPoint != null)
            return followPoint.position;

        if (target != null)
            return target.TransformPoint(targetLocalOffset);

        return anchorPosition;
    }

    public Quaternion GetFixedRotation() => Quaternion.Euler(pitchDegrees, yawDegrees, 0f);

    public Vector3 GetCameraOffset() => GetFixedRotation() * new Vector3(0f, 0f, -distance);

    public Vector3 ResolveAnchorForGizmo()
    {
        if (target != null || followPoint != null)
            return GetFollowWorldPoint();

        if (initialized)
            return anchorPosition;

        return transform.position - GetCameraOffset();
    }

    public bool TryGetDeadZoneGroundCorners(out Vector3 bottomLeft, out Vector3 bottomRight, out Vector3 topRight, out Vector3 topLeft)
    {
        bottomLeft = bottomRight = topRight = topLeft = Vector3.zero;

        Camera camera = cam != null ? cam : GetComponent<Camera>();
        if (camera == null)
            return false;

        Vector3 followPointWorld = ResolveAnchorForGizmo();
        float groundY = followPointWorld.y;

        Vector3 viewport = camera.WorldToViewportPoint(followPointWorld);
        if (viewport.z <= 0f)
            return false;

        float depth = viewport.z;
        GetDeadZoneViewportHalfExtents(out float halfW, out float halfH);

        bottomLeft = ProjectToGround(camera.ViewportToWorldPoint(new Vector3(0.5f - halfW, 0.5f - halfH, depth)), groundY);
        bottomRight = ProjectToGround(camera.ViewportToWorldPoint(new Vector3(0.5f + halfW, 0.5f - halfH, depth)), groundY);
        topRight = ProjectToGround(camera.ViewportToWorldPoint(new Vector3(0.5f + halfW, 0.5f + halfH, depth)), groundY);
        topLeft = ProjectToGround(camera.ViewportToWorldPoint(new Vector3(0.5f - halfW, 0.5f + halfH, depth)), groundY);
        return true;
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

    private Vector3 GetAnchorWithFollowHeight()
    {
        float anchorY = GetFollowWorldPoint().y;
        return new Vector3(anchorPosition.x, anchorY, anchorPosition.z);
    }

    private void ApplyRigToTransform(Vector3 anchor)
    {
        transform.rotation = GetFixedRotation();
        transform.position = anchor + GetCameraOffset();
    }

    private void UpdateAnchorForDeadZone()
    {
        ApplyRigToTransform(GetAnchorWithFollowHeight());

        Vector3 followWorld = GetFollowWorldPoint();
        Vector3 viewport = cam.WorldToViewportPoint(followWorld);
        if (viewport.z <= 0f)
            return;

        GetViewportFollowSensitivity(viewport.z, followWorld, out float sensX, out float sensY);

        bool outsideDeadZone = IsOutsideDeadZone(viewport);
        if (outsideDeadZone)
            isRecentering = true;

        if (!isRecentering)
            return;

        Vector2 correction = ComputeFollowCorrection(viewport);
        if (correction.sqrMagnitude < 0.0000001f)
        {
            isRecentering = false;
            return;
        }

        float snapThresholdSqr = centerSnapThreshold * centerSnapThreshold;
        if (correction.sqrMagnitude <= snapThresholdSqr)
        {
            ApplyAnchorCorrection(correction, viewport.z, sensX, sensY, instant: true);
            isRecentering = false;
            return;
        }

        ApplyAnchorCorrection(correction, viewport.z, sensX, sensY);

        if (!recenterToScreenCenter && !IsOutsideDeadZone(cam.WorldToViewportPoint(followWorld)))
            isRecentering = false;
    }

    private Vector2 ComputeFollowCorrection(Vector3 viewport)
    {
        if (recenterToScreenCenter)
            return new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);

        GetDeadZoneViewportHalfExtents(out float halfW, out float halfH);
        float clampedX = Mathf.Clamp(viewport.x, 0.5f - halfW, 0.5f + halfW);
        float clampedY = Mathf.Clamp(viewport.y, 0.5f - halfH, 0.5f + halfH);
        return new Vector2(viewport.x - clampedX, viewport.y - clampedY);
    }

    private bool IsOutsideDeadZone(Vector3 viewport)
    {
        GetDeadZoneViewportHalfExtents(out float halfW, out float halfH);
        return viewport.x < 0.5f - halfW || viewport.x > 0.5f + halfW
            || viewport.y < 0.5f - halfH || viewport.y > 0.5f + halfH;
    }

    private void ApplyAnchorCorrection(Vector2 viewportOffset, float depth, float sensX, float sensY, bool instant = false)
    {
        if (viewportOffset.sqrMagnitude < 0.0000001f)
            return;

        float followFactor = 1f;
        if (!instant && followSmoothTime > 0.0001f && Application.isPlaying)
            followFactor = 1f - Mathf.Exp(-Time.deltaTime / followSmoothTime);

        float safeSensX = Mathf.Max(Mathf.Abs(sensX), 0.00001f);
        float safeSensY = Mathf.Max(Mathf.Abs(sensY), 0.00001f);

        // 축별 감도 보정: viewport에서 X/Y가 같은 followFactor로 줄어들도록 지면 이동량 조정
        Vector2 inputViewport = new Vector2(
            viewportOffset.x * followFactor / safeSensX,
            viewportOffset.y * followFactor / safeSensY);

        anchorPosition += ViewportDeltaToGroundDelta(inputViewport, depth);
        anchorPosition.y = GetFollowWorldPoint().y;
    }

    private void GetViewportFollowSensitivity(float depth, Vector3 followWorld, out float sensX, out float sensY)
    {
        int frame = Time.frameCount;
        if (frame == lastViewportSensFrame && Mathf.Approximately(depth, lastViewportSensDepth))
        {
            sensX = viewportFollowSensX;
            sensY = viewportFollowSensY;
            return;
        }

        const float probe = 0.01f;
        Vector2 baseViewport = cam.WorldToViewportPoint(followWorld);

        Vector3 probeDeltaX = ViewportDeltaToGroundDelta(new Vector2(probe, 0f), depth);
        Vector3 probeDeltaY = ViewportDeltaToGroundDelta(new Vector2(0f, probe), depth);

        float viewportDeltaX = MeasureViewportChangeForAnchorDelta(probeDeltaX, followWorld, baseViewport).x;
        float viewportDeltaY = MeasureViewportChangeForAnchorDelta(probeDeltaY, followWorld, baseViewport).y;

        viewportFollowSensX = Mathf.Abs(viewportDeltaX) > 0.000001f ? viewportDeltaX / probe : 1f;
        viewportFollowSensY = Mathf.Abs(viewportDeltaY) > 0.000001f ? viewportDeltaY / probe : 1f;
        lastViewportSensFrame = frame;
        lastViewportSensDepth = depth;
        sensX = viewportFollowSensX;
        sensY = viewportFollowSensY;
    }

    private Vector2 MeasureViewportChangeForAnchorDelta(Vector3 groundDelta, Vector3 followWorld, Vector2 baseViewport)
    {
        Vector3 savedAnchor = anchorPosition;
        Vector3 savedCameraPosition = transform.position;

        anchorPosition += groundDelta;
        anchorPosition.y = followWorld.y;
        transform.rotation = GetFixedRotation();
        transform.position = anchorPosition + GetCameraOffset();

        Vector3 viewport = cam.WorldToViewportPoint(followWorld);
        Vector2 delta = new Vector2(viewport.x - baseViewport.x, viewport.y - baseViewport.y);

        anchorPosition = savedAnchor;
        transform.position = savedCameraPosition;

        return delta;
    }

    private Vector3 ViewportDeltaToGroundDelta(Vector2 viewportDelta, float depth)
    {
        Vector3 center = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, depth));
        Vector3 shifted = cam.ViewportToWorldPoint(new Vector3(0.5f + viewportDelta.x, 0.5f + viewportDelta.y, depth));
        return ProjectWorldDeltaToGround(shifted - center);
    }

    private Vector3 ProjectWorldDeltaToGround(Vector3 worldDelta)
    {
        Vector3 right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up);
        Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        if (right.sqrMagnitude > 0.0001f)
            right.Normalize();
        if (forward.sqrMagnitude > 0.0001f)
            forward.Normalize();

        return right * Vector3.Dot(worldDelta, right) + forward * Vector3.Dot(worldDelta, forward);
    }

    private static Vector3 ProjectToGround(Vector3 worldPoint, float groundY)
    {
        return new Vector3(worldPoint.x, groundY, worldPoint.z);
    }

    private void EnsureGameViewOverlay()
    {
        if (!Application.isPlaying)
            return;

        if (gameViewOverlay == null)
            gameViewOverlay = GetComponent<DiabloStyleCameraDeadZoneOverlay>();

        if (gameViewOverlay == null)
            gameViewOverlay = gameObject.AddComponent<DiabloStyleCameraDeadZoneOverlay>();

        gameViewOverlay.Initialize(this);
        RefreshGameViewOverlay();
    }

    private void RefreshGameViewOverlay()
    {
        if (gameViewOverlay == null)
            return;

        gameViewOverlay.ApplyStyle(gameViewBorderThickness, deadZoneGizmoColor, gameViewDeadZoneFillColor);
        gameViewOverlay.RefreshVisibility();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDeadZoneGizmo && !showFollowPointGizmo)
            return;

        DrawEditorGizmos(0.65f);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDeadZoneGizmo && !showFollowPointGizmo)
            return;

        DrawEditorGizmos(1f);
    }

    private void DrawEditorGizmos(float alphaScale)
    {
        Vector3 anchor = ResolveAnchorForGizmo();
        Vector3 camPos = anchor + GetCameraOffset();

        if (showFollowPointGizmo && (target != null || followPoint != null))
        {
            Vector3 point = GetFollowWorldPoint();
            Gizmos.color = new Color(followPointGizmoColor.r, followPointGizmoColor.g, followPointGizmoColor.b, followPointGizmoColor.a * alphaScale);
            Gizmos.DrawSphere(point, 0.2f);
            Gizmos.DrawLine(point, new Vector3(point.x, point.y + 0.6f, point.z));
        }

        if (!showDeadZoneGizmo)
            return;

        Gizmos.color = new Color(distanceGizmoColor.r, distanceGizmoColor.g, distanceGizmoColor.b, distanceGizmoColor.a * alphaScale);
        Gizmos.DrawLine(camPos, anchor);
        Gizmos.DrawWireSphere(anchor, 0.25f);

        if (!TryGetDeadZoneGroundCorners(out Vector3 bl, out Vector3 br, out Vector3 tr, out Vector3 tl))
            return;

        Color zoneColor = deadZoneGizmoColor;
        zoneColor.a *= alphaScale;
        Gizmos.color = zoneColor;

        Gizmos.DrawLine(bl, br);
        Gizmos.DrawLine(br, tr);
        Gizmos.DrawLine(tr, tl);
        Gizmos.DrawLine(tl, bl);
    }
#endif
}
