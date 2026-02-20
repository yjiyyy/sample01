#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RagdollBuildSettings))]
public class RagdollBuildSettingsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var settings = (RagdollBuildSettings)target;

        EditorGUILayout.Space(4);
        if (settings.boneOverrides == null || settings.boneOverrides.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "boneOverrides가 비어 있습니다. BIP 랙돌 빌드를 사용하려면 boneOverrides에 값을 채워야 합니다.",
                MessageType.Warning);
        }
        EditorGUILayout.Space(4);

        DrawDefaultInspector();

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
