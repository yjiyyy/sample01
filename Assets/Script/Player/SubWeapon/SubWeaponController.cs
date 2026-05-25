using UnityEngine;

/// <summary>
/// 플레이어 보조무기 궤도 및 슬롯(0~4) 관리. 업그레이드 등에서 넘긴 프리팹만 붙입니다.
/// - Root 본을 중심으로 회전, 궤적 지름/회전 속도 조절 가능
/// - 비어 있지 않은 슬롯만 360°/N 간격으로 배치
/// </summary>
[DisallowMultipleComponent]
public class SubWeaponController : MonoBehaviour
{
    private const int MaxSlots = 5;

    [Header("궤도 설정")]
    [Tooltip("궤도의 지름 (월드 단위)")]
    [SerializeField] private float orbitDiameter = 2f;
    [Tooltip("회전 속도 (도/초, deg/s). Time.deltaTime 적용됨")]
    [SerializeField] private float rotationSpeedDegPerSec = 90f;
    [Tooltip("궤도 중심(플레이어)으로부터의 Y 오프셋. 양수면 위로 올라감")]
    [SerializeField] private float orbitHeightOffset = 0f;

    [Header("궤도 중심 본")]
    [Tooltip("궤도 중심이 될 본 이름. 비어있으면 플레이어 루트 사용")]
    [SerializeField] private string orbitCenterBoneName = "Root_Dummy";

    private Transform orbitCenter;
    private readonly GameObject[] slotInstances = new GameObject[MaxSlots];
    private float baseAngleDeg;

    public int MaxCount => MaxSlots;

    private void Awake()
    {
        orbitCenter = FindOrbitCenter();
    }

    private void LateUpdate()
    {
        ApplyOrbitPositions();
    }

    private Transform FindOrbitCenter()
    {
        Transform root = transform.root;
        if (root == null) root = transform;

        var rootDummy = PlayerEquipmentController.FindRootDummy(root);
        if (rootDummy != null) return rootDummy;

        if (!string.IsNullOrEmpty(orbitCenterBoneName))
        {
            var found = FindDeepChild(root, orbitCenterBoneName);
            if (found != null) return found;
        }

        return root;
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var t = FindDeepChild(parent.GetChild(i), name);
            if (t != null) return t;
        }
        return null;
    }

    private void ApplyOrbitPositions()
    {
        if (orbitCenter == null) return;

        int n = 0;
        for (int i = 0; i < MaxSlots; i++)
        {
            if (slotInstances[i] != null)
                n++;
        }

        if (n == 0) return;

        float radius = orbitDiameter * 0.5f;
        float angleStep = 360f / n;
        float dt = Time.deltaTime;

        baseAngleDeg += rotationSpeedDegPerSec * dt;
        if (baseAngleDeg >= 360f) baseAngleDeg -= 360f;
        if (baseAngleDeg < 0f) baseAngleDeg += 360f;

        Vector3 centerPos = orbitCenter.position + Vector3.up * orbitHeightOffset;
        Vector3 right = Vector3.right;
        Vector3 forward = Vector3.forward;

        int k = 0;
        for (int i = 0; i < MaxSlots; i++)
        {
            if (slotInstances[i] == null)
                continue;

            float angleDeg = baseAngleDeg + angleStep * k;
            k++;
            float rad = angleDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            Vector3 offset = (right * cos + forward * sin) * radius;
            slotInstances[i].transform.position = centerPos + offset;
        }
    }

    /// <summary>
    /// 슬롯 인덱스(0~4)에 보조무기 프리팹을 장착하거나 해제합니다. null이면 해당 슬롯만 비웁니다.
    /// </summary>
    public void SetCompanionPrefab(int slotIndex, GameObject prefabOrNull)
    {
        if (slotIndex < 0 || slotIndex >= MaxSlots)
            return;

        ClearSlotInternal(slotIndex);
        if (prefabOrNull == null)
            return;

        Transform parent = orbitCenter != null ? orbitCenter : transform;
        GameObject inst = Instantiate(prefabOrNull, parent);
        inst.name = $"{prefabOrNull.name}_SubSlot{slotIndex}";
        slotInstances[slotIndex] = inst;

        var beh = inst.GetComponent<SubWeaponBehavior>();
        if (beh != null)
            beh.NotifyAttached();
    }

    public GameObject GetCompanionInstance(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MaxSlots)
            return null;
        return slotInstances[slotIndex];
    }

    public void ClearAllCompanionSlots()
    {
        for (int i = 0; i < MaxSlots; i++)
            ClearSlotInternal(i);
    }

    private void ClearSlotInternal(int slotIndex)
    {
        if (slotInstances[slotIndex] == null)
            return;

        Destroy(slotInstances[slotIndex]);
        slotInstances[slotIndex] = null;
    }
}
