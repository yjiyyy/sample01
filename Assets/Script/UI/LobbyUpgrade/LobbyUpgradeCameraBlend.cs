using UnityEngine;

/// <summary>
/// 업그레이드 화면에서 로비 카메라를 옮겨 캐릭터가 왼쪽에 서게 합니다.
/// 새 카메라를 만들지 않고, 지금 쓰는 카메라만 부드럽게 이동합니다.
/// </summary>
public class LobbyUpgradeCameraBlend : MonoBehaviour
{
    [SerializeField] private float blendSeconds = 0.4f;
    [Header("기본 로비 구도")]
    [SerializeField] private Vector3 restCameraOffset = new Vector3(0f, 0.83f, -7.69f);
    [SerializeField] private Vector3 restLookOffset = new Vector3(0f, 1.25f, 0f);
    [Header("상점 / 업그레이드 구도")]
    [Tooltip("캐릭터 기준 카메라 위치. X가 클수록 캐릭터가 더 왼쪽에 보입니다.")]
    [SerializeField] private Vector3 cameraOffsetFromCharacter = new Vector3(1.05f, 1f, -8.6f);
    [Tooltip("카메라가 바라보는 지점(캐릭터 기준). X가 클수록 캐릭터가 왼쪽으로 갑니다.")]
    [SerializeField] private Vector3 lookOffsetFromCharacter = new Vector3(1.7f, 1f, 0f);

    private Camera _camera;
    private Coroutine _routine;

    public static LobbyUpgradeCameraBlend Ensure()
    {
        var cam = Camera.main;
        if (cam == null)
            cam = FindFirstObjectByType<Camera>();
        if (cam == null)
            return null;

        var blend = cam.GetComponent<LobbyUpgradeCameraBlend>();
        if (blend == null)
            blend = cam.gameObject.AddComponent<LobbyUpgradeCameraBlend>();
        blend._camera = cam;
        return blend;
    }

    public void GoUpgrade(bool immediate)
    {
        if (!TryGetUpgradePose(out Vector3 pos, out Quaternion rot))
            return;

        StartBlend(pos, rot, immediate);
    }

    /// <summary>상점에서는 캐릭터 아래의 계정 정보가 가려지지 않도록 조금 더 멀리 보여줍니다.</summary>
    public void GoShop(bool immediate)
    {
        var character = FindCharacter(); if (character == null) return;
        Vector3 position = character.position + new Vector3(1.4f, 1.15f, -11.8f);
        Vector3 look = character.position + new Vector3(2.2f, .55f, 0f);
        StartBlend(position, Quaternion.LookRotation(look-position, Vector3.up), immediate);
    }

    public void GoRest(bool immediate)
    {
        Transform character = FindCharacter();
        if (character == null) return;
        Vector3 pos = character.position + restCameraOffset;
        Vector3 forward = character.position + restLookOffset - pos;
        if (forward.sqrMagnitude < 0.0001f) return;
        StartBlend(pos, Quaternion.LookRotation(forward, Vector3.up), immediate);
    }

    [ContextMenu("Preview Lobby Camera")]
    private void PreviewLobby() => GoRest(true);

    [ContextMenu("Preview Panel Camera")]
    private void PreviewPanel() => GoUpgrade(true);

    public static bool TryGetUpgradePose(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        var blend = Ensure();
        if (blend == null)
            return false;

        Transform character = FindCharacter();
        if (character == null)
        {
            var cam = blend._camera != null ? blend._camera : Camera.main;
            if (cam == null)
                return false;
            position = cam.transform.position + new Vector3(2.25f, 0f, 0.4f);
            rotation = Quaternion.LookRotation(
                (cam.transform.position + cam.transform.forward * 8f + Vector3.right * 1.75f) - position,
                Vector3.up);
            return true;
        }

        position = character.position + blend.cameraOffsetFromCharacter;
        Vector3 look = character.position + blend.lookOffsetFromCharacter;
        Vector3 forward = look - position;
        if (forward.sqrMagnitude < 0.0001f)
            forward = character.position - position;
        rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        return true;
    }

    private void StartBlend(Vector3 pos, Quaternion rot, bool immediate)
    {
        if (_camera == null)
            _camera = GetComponent<Camera>();
        if (_camera == null)
            return;

        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        if (immediate || !Application.isPlaying || blendSeconds <= 0.0001f)
        {
            _camera.transform.SetPositionAndRotation(pos, rot);
            return;
        }

        _routine = StartCoroutine(BlendRoutine(pos, rot));
    }

    private System.Collections.IEnumerator BlendRoutine(Vector3 pos, Quaternion rot)
    {
        Vector3 startPos = _camera.transform.position;
        Quaternion startRot = _camera.transform.rotation;
        float t = 0f;
        float duration = Mathf.Max(0.01f, blendSeconds);
        while (t < 1f)
        {
            t = Mathf.Clamp01(t + Time.unscaledDeltaTime / duration);
            float e = t * t * (3f - 2f * t);
            _camera.transform.SetPositionAndRotation(
                Vector3.Lerp(startPos, pos, e),
                Quaternion.Slerp(startRot, rot, e));
            yield return null;
        }

        _camera.transform.SetPositionAndRotation(pos, rot);
        _routine = null;
    }

    private static Transform FindCharacter()
    {
        var spawn = GameObject.Find("CharacterSpawnPoint");
        if (spawn != null)
        {
            for (int i = 0; i < spawn.transform.childCount; i++)
            {
                var child = spawn.transform.GetChild(i);
                if (child != null && child.name.StartsWith("Player_"))
                    return child;
            }
            return spawn.transform;
        }

        var facade = FindFirstObjectByType<PlayerFacade>(FindObjectsInactive.Include);
        return facade != null ? facade.transform : null;
    }
}
