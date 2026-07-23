using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// EnemyConfig.overrideController가 비어 있을 때 사용할 기본 Animator Controller(E_Animator).
/// </summary>
public static class EnemyAnimatorDefaults
{
    private const string ControllerGuid = "0c585f06e6c97534cac89d91d9e0726d";

    private static RuntimeAnimatorController _cached;

    public static RuntimeAnimatorController GetDefaultController()
    {
        if (_cached != null)
            return _cached;

#if UNITY_EDITOR
        string path = AssetDatabase.GUIDToAssetPath(ControllerGuid);
        if (!string.IsNullOrEmpty(path))
            _cached = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
#endif

        if (_cached == null && Application.isPlaying)
            Debug.LogWarning("[EnemyAnimatorDefaults] E_Animator를 찾지 못했습니다.");

        return _cached;
    }
}
