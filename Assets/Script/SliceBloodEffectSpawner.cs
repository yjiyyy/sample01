using UnityEngine;

/// <summary>
/// 슬라이스 시 FX_Blood_부위_01(잘린 쪽), FX_Blood_부위_02(몸통 쪽)에서 BloodGushEffect를 발동합니다.
/// 캐릭터 루트에 부착하고, bloodGushPrefab만 인스펙터에서 할당하면 됩니다.
/// FX 방향 = 더미의 X축(transform.right)
/// </summary>
public class SliceBloodEffectSpawner : MonoBehaviour
{
    [Header("슬라이스 피 이펙트")]
    [Tooltip("발동할 피 뿜는 이펙트 프리팹. 비어 있으면 스폰하지 않음.")]
    public GameObject bloodGushPrefab;

    /// <summary>
    /// sliceRoot(절단된 부위 루트)에 대응하는 FX 더미에서 이펙트 발동.
    /// sliceRoot의 bone 이름으로 부위를 자동 판별합니다.
    /// </summary>
    public void SpawnBloodAtSlice(Transform sliceRoot)
    {
        if (bloodGushPrefab == null || sliceRoot == null) return;

        string partSuffix = GetPartSuffixFromBone(sliceRoot.name);
        if (string.IsNullOrEmpty(partSuffix)) return;

        Transform root = transform.root;
        string name01 = "FX_Blood_" + partSuffix + "01";
        string name02 = "FX_Blood_" + partSuffix + "02";

        Transform dummy01 = FindInChildren(sliceRoot, name01);
        Transform dummy02 = FindInRootExcluding(root, name02, sliceRoot);

        SpawnAt(dummy01);
        SpawnAt(dummy02);
    }

    private static string GetPartSuffixFromBone(string boneName)
    {
        if (string.IsNullOrEmpty(boneName)) return null;
        if (boneName.Contains("Head")) return "Head";
        if (boneName.Contains("L UpperArm") || boneName.Contains("L Upper Arm")) return "L_Arm";
        if (boneName.Contains("R UpperArm") || boneName.Contains("R Upper Arm")) return "R_Arm";
        if (boneName.Contains("L Thigh")) return "L_Leg";
        if (boneName.Contains("R Thigh")) return "R_Leg";
        return null;
    }

    private static Transform FindInChildren(Transform root, string name)
    {
        if (root == null) return null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t != null && t.name == name) return t;
        }
        return null;
    }

    private static Transform FindInRootExcluding(Transform root, string name, Transform excludeUnder)
    {
        if (root == null) return null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == null || t.name != name) continue;
            if (excludeUnder != null && IsUnder(t, excludeUnder)) continue;
            return t;
        }
        return null;
    }

    private static bool IsUnder(Transform t, Transform potentialAncestor)
    {
        var p = t.parent;
        while (p != null)
        {
            if (p == potentialAncestor) return true;
            p = p.parent;
        }
        return false;
    }

    private void SpawnAt(Transform dummy)
    {
        if (dummy == null || bloodGushPrefab == null) return;

        Vector3 pos = dummy.position;
        // FX 방향 = 더미의 X축(transform.right)
        Quaternion rot = Quaternion.LookRotation(dummy.right);

        var go = Instantiate(bloodGushPrefab, pos, rot);
        if (go != null)
            go.transform.SetParent(dummy, worldPositionStays: true);  // 더미 하위로 들어가 신체 부위를 따라감
    }
}
