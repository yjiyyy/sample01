using UnityEngine;

/// <summary>
/// 드랍 아이템: 플레이어 <see cref="PlayerResources.PickupMagnetRadius"/> 안으로 들어오면
/// 플레이어에게 이동 후 흡수·삭제. <see cref="ItemDropArc"/> 착지 후에만 자석 판정(공중에서는 대기).
/// </summary>
[DisallowMultipleComponent]
public class PickupItem : MonoBehaviour
{
    [SerializeField] private PickupType pickupType = PickupType.Money;
    [SerializeField] private int amount = 1;

    [Header("흡수 이동")]
    [Tooltip("자석에 걸린 뒤 플레이어를 향해 이동하는 속도 (m/s, Time.deltaTime 기준).")]
    [SerializeField] private float absorbMoveSpeed = 14f;

    [Tooltip("이 거리 이하로 가까워지면 흡수 완료 후 삭제.")]
    [SerializeField] private float absorbArriveDistance = 0.35f;

    private Transform _player;
    private bool _arcLanded;

    private void Start()
    {
        CachePlayerIfNeeded();
    }

    private void Update()
    {
        if (!TryFinishWaitingForArc())
            return;

        var res = PlayerResources.Instance;
        if (res == null)
        {
            CachePlayerIfNeeded();
            res = PlayerResources.Instance;
        }
        if (res == null || _player == null)
            return;

        float flatDist = FlatDistance(transform.position, _player.position);
        float magnetR = res.PickupMagnetRadius;

        if (flatDist > magnetR)
            return;

        // 자석 구간: 플레이어 쪽으로 이동
        Vector3 flatTarget = _player.position;
        Vector3 flatSelf = transform.position;
        flatTarget.y = flatSelf.y;

        Vector3 next = Vector3.MoveTowards(
            flatSelf,
            flatTarget,
            absorbMoveSpeed * Time.deltaTime);

        transform.position = next;

        if (FlatDistance(transform.position, _player.position) <= absorbArriveDistance)
        {
            ApplyPickup(res);
            Destroy(gameObject);
        }
    }

    private bool TryFinishWaitingForArc()
    {
        if (_arcLanded)
            return true;

        // EnemyDie가 드랍 직후 AddComponent<ItemDropArc> 하므로 첫 프레임에 없을 수 있음
        if (!TryGetComponent<ItemDropArc>(out var arc))
        {
            _arcLanded = true;
            return true;
        }

        if (!arc.enabled)
            _arcLanded = true;

        return _arcLanded;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private void ApplyPickup(PlayerResources res)
    {
        if (amount <= 0) return;

        switch (pickupType)
        {
            case PickupType.Money:
                res.AddMoney(amount);
                break;
            case PickupType.Gem:
                res.AddGem(amount);
                break;
        }
    }

    private void CachePlayerIfNeeded()
    {
        if (_player != null) return;
        if (PlayerResources.Instance != null)
        {
            _player = PlayerResources.Instance.transform;
            return;
        }
        var go = GameObject.FindGameObjectWithTag("Player");
        if (go != null)
            _player = go.transform;
    }
}
