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

        if (settings.fxBloodDummies == null || settings.fxBloodDummies.Length == 0)
        {
            if (GUILayout.Button("FX Blood 더미 초기화 (기본 10개)"))
            {
                Undo.RecordObject(settings, "FX Blood Dummies Init");
                settings.fxBloodDummies = RagdollBuildSettings.GetDefaultFXBloodDummies();
                EditorUtility.SetDirty(settings);
            }
        }
        EditorGUILayout.Space(4);

        DrawDefaultInspector();

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
