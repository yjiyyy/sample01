using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 보조무기 궤도 및 슬롯 관리.
/// - Root 본을 중심으로 회전, 궤적 지름/회전 속도 조절 가능
/// - 보조무기 개수에 따라 360°/N 간격 자동 배치
/// - AddSubWeapon / RemoveSubWeapon 으로 런타임 증감 지원
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
    [SerializeField] private string orbitCenterBoneName = "Root_dummy";

    [Header("초기 보조무기 (에디터)")]
    [Tooltip("게임 시작 시 자동 장착할 보조무기 SO. 빈 슬롯은 null")]
    [SerializeField] private SubWeaponDataSO[] initialSlots = new SubWeaponDataSO[MaxSlots];

    private Transform orbitCenter;
    private readonly List<SubWeaponDataSO> slots = new List<SubWeaponDataSO>(MaxSlots);
    private readonly List<GameObject> instances = new List<GameObject>(MaxSlots);
    private float baseAngleDeg;

    public int Count => slots.Count;
    public int MaxCount => MaxSlots;

    private void Awake()
    {
        orbitCenter = FindOrbitCenter();

        // 초기 슬롯 반영
        if (initialSlots != null)
        {
            for (int i = 0; i < Mathf.Min(initialSlots.Length, MaxSlots); i++)
            {
                if (initialSlots[i] != null)
                    AddSubWeaponInternal(initialSlots[i]);
            }
        }
    }

    private void LateUpdate()
    {
        ApplyOrbitPositions();
    }

    private Transform FindOrbitCenter()
    {
        Transform root = transform.root;
        if (root == null) root = transform;

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
        int n = instances.Count;
        if (n == 0) return;

        float radius = orbitDiameter * 0.5f;
        float angleStep = 360f / n;
        float dt = Time.deltaTime;

        baseAngleDeg += rotationSpeedDegPerSec * dt;
        if (baseAngleDeg >= 360f) baseAngleDeg -= 360f;
        if (baseAngleDeg < 0f) baseAngleDeg += 360f;

        // 중심: 플레이어 위치 + 높이 오프셋 (위치는 따라가지만 회전은 따라가지 않음)
        Vector3 centerPos = orbitCenter.position + Vector3.up * orbitHeightOffset;
        // 월드 XZ 평면 기준으로 궤도 계산 (플레이어 회전 무관)
        Vector3 right = Vector3.right;
        Vector3 forward = Vector3.forward;

        for (int i = 0; i < n; i++)
        {
            float angleDeg = baseAngleDeg + angleStep * i;
            float rad = angleDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            Vector3 offset = (right * cos + forward * sin) * radius;
            instances[i].transform.position = centerPos + offset;
        }
    }

    /// <summary>
    /// 보조무기 추가. 빈 슬롯에 추가. 최대 5개 초과 시 false.
    /// </summary>
    public bool AddSubWeapon(SubWeaponDataSO so)
    {
        if (so == null || so.prefab == null)
        {
            Debug.LogWarning("[SubWeaponController] AddSubWeapon: SO 또는 prefab이 null입니다.");
            return false;
        }
        if (slots.Count >= MaxSlots)
        {
            Debug.LogWarning("[SubWeaponController] 최대 개수(5)에 도달했습니다.");
            return false;
        }
        return AddSubWeaponInternal(so);
    }

    private bool AddSubWeaponInternal(SubWeaponDataSO so)
    {
        slots.Add(so);
        var inst = Instantiate(so.prefab, orbitCenter != null ? orbitCenter : transform);
        inst.name = so.prefab.name + "_" + instances.Count;
        var beh = inst.GetComponent<SubWeaponBehavior>();
        if (beh != null) beh.ApplyData(so);
        instances.Add(inst);
        return true;
    }

    /// <summary>
    /// 인덱스로 보조무기 제거.
    /// </summary>
    public bool RemoveSubWeaponAt(int index)
    {
        if (index < 0 || index >= instances.Count) return false;
        if (instances[index] != null) Destroy(instances[index]);
        instances.RemoveAt(index);
        slots.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// SO로 보조무기 제거. 첫 번째 일치 항목 제거.
    /// </summary>
    public bool RemoveSubWeapon(SubWeaponDataSO so)
    {
        int idx = slots.IndexOf(so);
        return idx >= 0 && RemoveSubWeaponAt(idx);
    }

    /// <summary>
    /// 모든 보조무기 제거.
    /// </summary>
    public void ClearAll()
    {
        for (int i = instances.Count - 1; i >= 0; i--)
        {
            if (instances[i] != null) Destroy(instances[i]);
        }
        instances.Clear();
        slots.Clear();
    }
}
