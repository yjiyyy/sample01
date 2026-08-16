using UnityEngine;

/// <summary>
/// 에디터 미리보기로 붙인 PC 파츠 표시용.
/// 이름 접두사·마커로 식별하며, 저장 직전/창 닫을 때 제거됩니다.
/// </summary>
[DisallowMultipleComponent]
[ExecuteAlways]
internal class PlayerBodyPartPreviewMarker : MonoBehaviour
{
    /// <summary>미리보기 오브젝트 이름 접두사. 이 문자열이 있으면 임시 미리보기로 간주합니다.</summary>
    public const string NamePrefix = "[TEMP_Preview]_";

    public static bool IsPreviewObject(Transform t)
    {
        if (t == null) return false;
        if (t.GetComponent<PlayerBodyPartPreviewMarker>() != null) return true;
        return t.name != null && t.name.StartsWith(NamePrefix, System.StringComparison.Ordinal);
    }

    public static bool IsUnderPreview(Transform t)
    {
        while (t != null)
        {
            if (IsPreviewObject(t)) return true;
            t = t.parent;
        }
        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        EnsureMarked(gameObject);
    }

    /// <summary>이름·hideFlags·마커를 미리보기용으로 고정합니다.</summary>
    public static void EnsureMarked(GameObject root)
    {
        if (root == null) return;

        if (!root.name.StartsWith(NamePrefix, System.StringComparison.Ordinal))
            root.name = NamePrefix + root.name;

        ApplyDontSaveFlagsRecursive(root.transform);

        if (root.GetComponent<PlayerBodyPartPreviewMarker>() == null)
            root.AddComponent<PlayerBodyPartPreviewMarker>();
    }

    private static void ApplyDontSaveFlagsRecursive(Transform root)
    {
        var transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            var go = transforms[i].gameObject;
            go.hideFlags |= HideFlags.DontSaveInEditor | HideFlags.NotEditable | HideFlags.DontSaveInBuild;
        }
    }
#endif
}
