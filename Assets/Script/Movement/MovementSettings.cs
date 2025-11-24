using UnityEngine;

[CreateAssetMenu(menuName = "Movement/MovementSettings")]
public class MovementSettings : ScriptableObject
{
    [Header("Headroom (머리) 검사")]
    public LayerMask headMask;
    [Range(0.2f, 0.6f)] public float headPortion = 0.4f;
    [Range(0f, 0.05f)] public float headMargin = 0.02f;
    [Range(1, 4)] public int headClampIterations = 2;

    [Header("Collision (분리된 레이어 마스크)")]
    public LayerMask obstacleMask;
    public LayerMask floorMask;
    public LayerMask movementBlockMask_fallback;

    [Header("슬라이딩 & 스텝")]
    public float collisionSkin = 0.03f;
    public float floorThreshold = 0.75f;
    [Range(0, 2)] public int slideIterations = 1;
    // NOTE: tinyDispThreshold here is the general "tiny displacement" threshold used by movement logic.
    public float tinyDispThreshold = 0.002f;

    [Header("스텝(자동 올라타기) 설정")]
    public float maxStepHeight = 0.6f;
    [Range(1, 8)] public int stepSearchIterations = 5;
    public float floorCheckDepth = 0.15f;
    public float minStepProbeDistance = 0.15f;

    [Header("Narrow-space (shared) tuning")]
    [Tooltip("Number of overlap iterations used by narrow-space filter (shared for Player & Enemy).")]
    [Range(1, 8)] public int overlapIterations = 2;
    [Tooltip("Minimum factor threshold used by narrow-space filter (shared).")]
    public float minFactorThreshold = 0.05f;
    // NOTE: tinyDispThreshold above is used for narrow-space tiny displacement as well.

    [Header("Headroom Strict Block")]
    public bool strictHeadroomBlock = true;

    [Header("Prop & Crowd push settings")]
    public float pushableMassMultiplier = 1f;
    public float pushImpulseFactor = 0.5f;
    public float crowdMassThresholdMultiplier = 1.2f;
    public int crowdCountThreshold = 3;

    [Header("Gizmos & debug")]
    public bool enableGizmos = false;

    [Header("Internal Performance Settings")]
    public int overlapBufferSize = 16;
}