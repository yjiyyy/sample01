using UnityEngine;

/// <summary>
/// 아이템 드랍 시 공중에서 흩뿌려지며 나오는 연출.
/// - 가짜 물리 아크 (Time.deltaTime으로 프레임 독립).
/// - 착지 시 레이캐스트로 지면(비탈, 계단 포함)에 스냅.
/// - StartArc() 호출로 사용. 호출 후 자동으로 착지 시 비활성화.
/// </summary>
[DisallowMultipleComponent]
public class ItemDropArc : MonoBehaviour
{
    private Vector3 velocity;
    private float gravity = 13.5f;
    private LayerMask groundLayer;
    private float groundOffset = 0.02f;
    private float maxDuration = 3f;
    private float elapsed;

    private bool active;

    /// <summary>
    /// 드랍 아크 시작. 상대적 위치에서 Instantiate 직후 호출.
    /// </summary>
    /// <param name="initialVelocity">초기 속도 (위+바깥 방향 권장).</param>
    /// <param name="groundLayerMask">레이캐스트할 지면 레이어. 0이면 DefaultRaycastLayers 사용.</param>
    /// <param name="groundOffsetY">착지 시 지면 위 오프셋 (z-fighting 방지).</param>
    public void StartArc(Vector3 initialVelocity, LayerMask groundLayerMask, float groundOffsetY = 0.02f)
    {
        velocity = initialVelocity;
        groundLayer = groundLayerMask;
        groundOffset = groundOffsetY;
        elapsed = 0f;
        active = true;
    }

    private void Update()
    {
        if (!active) return;

        float dt = Time.deltaTime;
        elapsed += dt;

        if (elapsed >= maxDuration)
        {
            TrySnapToGround();
            active = false;
            enabled = false;
            return;
        }

        velocity += Vector3.down * gravity * dt;
        transform.position += velocity * dt;

        // 낙하 중일 때만 지면 체크
        if (velocity.y <= 0f)
        {
            if (TrySnapToGround())
            {
                active = false;
                enabled = false;
            }
        }
    }

    private bool TrySnapToGround()
    {
        LayerMask mask = groundLayer;
        if (mask == 0) mask = Physics.DefaultRaycastLayers;

        Vector3 origin = transform.position + Vector3.up * 0.2f;
        float maxDist = 2f;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDist, mask, QueryTriggerInteraction.Ignore))
        {
            if (hit.distance < 0.3f)
            {
                transform.position = hit.point + Vector3.up * groundOffset;
                return true;
            }
        }

        return false;
    }
}
