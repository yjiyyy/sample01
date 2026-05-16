using UnityEngine;

/// <summary>
/// FX 프리팹 <b>최상위</b>에만 붙입니다. 매 프레임 이 오브젝트의 <b>회전만</b> 카메라 기준으로 맞추고,
/// 자식(파티클·메시·빈 부모 등)은 부모 회전을 그대로 따라가므로 한 번에 정면을 향하게 됩니다.
/// (자식 Transform에 동일 컴포넌트를 중복으로 붙이지 마세요.)
/// </summary>
[DisallowMultipleComponent]
public class CameraFacingBillboard : MonoBehaviour
{
    public enum FacingMode
    {
        [Tooltip("수평면(XZ)에서만 카메라 쪽으로 Yaw 회전. 쿼터뷰·탑다운에 자주 사용.")]
        YawTowardsCamera,

        [Tooltip("이 오브젝트 위치에서 카메라 위치를 향해 전방 축을 맞춤.")]
        LookAtCameraPosition,

        [Tooltip("카메라 시선에 수직인 면(화면에 평행)에 맞춤. 얇은 판·스프라이트 느낌에 적합.")]
        AlignWithCameraViewPlane,
    }

    [Tooltip("비우면 Camera.main 사용. 멀티 카메라·수동 제어 시 지정.")]
    [SerializeField] private Camera targetCamera;

    [SerializeField] private FacingMode mode = FacingMode.YawTowardsCamera;

    private Camera _cachedCamera;

    private void LateUpdate()
    {
        ApplyFacing();
    }

    private void OnEnable()
    {
        _cachedCamera = null;
    }

    private void ApplyFacing()
    {
        Camera cam = ResolveCamera();
        if (cam == null)
            return;

        switch (mode)
        {
            case FacingMode.YawTowardsCamera:
            {
                Vector3 toCam = cam.transform.position - transform.position;
                toCam.y = 0f;
                if (toCam.sqrMagnitude < 1e-8f)
                    return;
                transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
                break;
            }
            case FacingMode.LookAtCameraPosition:
            {
                Vector3 toCam = cam.transform.position - transform.position;
                if (toCam.sqrMagnitude < 1e-8f)
                    return;
                Vector3 up = cam.transform.up;
                if (Mathf.Abs(Vector3.Dot(toCam.normalized, up)) > 0.98f)
                    up = Vector3.up;
                transform.rotation = Quaternion.LookRotation(toCam.normalized, up);
                break;
            }
            case FacingMode.AlignWithCameraViewPlane:
            {
                Vector3 forward = -cam.transform.forward;
                if (forward.sqrMagnitude < 1e-8f)
                    return;
                transform.rotation = Quaternion.LookRotation(forward.normalized, cam.transform.up);
                break;
            }
        }
    }

    private Camera ResolveCamera()
    {
        if (targetCamera != null)
            return targetCamera;

        if (_cachedCamera != null && _cachedCamera.isActiveAndEnabled)
            return _cachedCamera;

        _cachedCamera = Camera.main;
        return _cachedCamera;
    }
}
