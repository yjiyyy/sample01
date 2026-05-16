using UnityEngine;

[CreateAssetMenu(
    fileName = "SilhouetteGhostProfile",
    menuName = "Game/Upgrade/Effect/Silhouette Ghost Profile",
    order = 605)]
public class SilhouetteGhostProfile : ScriptableObject
{
    [Header("간격·수명 (GameplayTime 기준)")]
    [Tooltip("잔상을 찍는 최소 간격(초). 클수록 저렴합니다.")]
    [Min(0.02f)]
    public float snapshotIntervalSeconds = 0.08f;

    [Tooltip("한 잔상이 사라지기까지 걸리는 시간(초).")]
    [Min(0.05f)]
    public float ghostLifetimeSeconds = 0.35f;

    [Tooltip("동시에 유지할 최대 잔상 수(초과 시 가장 오래된 것부터 회수).")]
    [Min(1)]
    public int maxConcurrentGhosts = 4;

    [Tooltip("프레임당 최대 1회만 스냅샷(부하 상한).")]
    public bool limitOneSnapshotPerFrame = true;

    [Header("표시")]
    [Tooltip("URP Unlit 등 투명 쉐이더 머티리얼. 인스턴스 알파는 MaterialPropertyBlock으로만 바꿉니다.")]
    public Material ghostMaterial;

    [Tooltip("알파 구간. RGB는 tint와 곱해집니다.")]
    [Min(0f)] public float startAlpha = 0.4f;
    [Min(0f)] public float endAlpha = 0f;

    public Color tintRgb = Color.white;

    [Tooltip("URP: _BaseColor, Built-in: _Color 등")]
    public string colorPropertyName = "_BaseColor";

    [Header("배치")]
    [Tooltip("켜면 스냅샷이 찍힌 월드 위치에 남고, 플레이어가 움직여도 따라가지 않습니다.")]
    public bool leaveSnapshotsInWorldSpace = true;

    [Tooltip("월드 고정 시 부모로 쓸 빈 오브젝트(선택). 비우면 씬 루트(null)에 둡니다.")]
    public Transform worldSpaceContainer;

    [Header("소스 메시")]
    [Tooltip("비우면 이 컴포넌트가 붙은 루트 아래의 모든 SkinnedMeshRenderer를 사용합니다.")]
    public bool autoCollectSkinnedMeshes = true;

    [Header("레이어")]
    [Tooltip("-1이면 생성 시 레이어를 바꾸지 않습니다.")]
    public int ghostLayer = -1;
}
