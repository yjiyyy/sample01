using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LobbyEnvironment))]
public class LobbyEnvironmentEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var env = (LobbyEnvironment)target;
        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("조명 테스트", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "플레이 중 키보드 1 / 2 / 3으로 분위기를 천천히 바꿀 수 있습니다.\n색과 밝기만 바뀌고 그림자 방향은 그대로입니다.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("1 회색 스튜디오"))
                Apply(env, 0);
            if (GUILayout.Button("2 빨간 분위기"))
                Apply(env, 1);
            if (GUILayout.Button("3 어두운 분위기"))
                Apply(env, 2);
        }
    }

    private static void Apply(LobbyEnvironment env, int index)
    {
        Undo.RecordObject(env, "Apply Lobby Lighting Preset");
        env.ApplyPreset(index);
        EditorUtility.SetDirty(env);
    }
}
