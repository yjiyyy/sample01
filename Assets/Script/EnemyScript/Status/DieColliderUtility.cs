using UnityEngine;

/// <summary>
/// 무기/파츠 프리팹의 죽음 연출용 Collider(DieCollider) 식별·활성화.
/// MonoBehaviour가 아니며 프리팹에 붙일 필요 없음 — 코드에서 자동 호출.
/// 프리팹에는 이름이 "DieCollider"인 자식 + non-trigger Collider만 추가하면 됩니다.
/// </summary>
public static class DieColliderUtility
{
    public const string DieColliderObjectName = "DieCollider";
    public const string PartsLayerName = "Parts";

    public static int PartsLayer
    {
        get
        {
            int layer = LayerMask.NameToLayer(PartsLayerName);
            return layer >= 0 ? layer : 0;
        }
    }

    public static bool IsDieCollider(Collider col)
    {
        return col != null && col.gameObject.name == DieColliderObjectName;
    }

    public static void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null) return;
        root.gameObject.layer = layer;
        foreach (Transform child in root)
            SetLayerRecursively(child, layer);
    }

    public static void ApplyPartsLayer(Transform root)
    {
        SetLayerRecursively(root, PartsLayer);
    }

    public static void SetDieCollidersEnabled(Transform root, bool enabled)
    {
        if (root == null) return;
        foreach (var col in root.GetComponentsInChildren<Collider>(true))
        {
            if (!IsDieCollider(col)) continue;
            if (enabled)
                col.gameObject.SetActive(true);
            col.enabled = enabled;
        }
    }

    /// <summary>평소 비활성: DieCollider + 공격 Trigger(HitBox 등).</summary>
    public static void DisablePartCollidersForLife(Transform root)
    {
        if (root == null) return;
        foreach (var col in root.GetComponentsInChildren<Collider>(true))
        {
            if (col == null) continue;
            if (IsDieCollider(col) || col.isTrigger)
                col.enabled = false;
        }
    }
}
