#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerBodyPartSlots))]
public class PlayerBodyPartSlotsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var slots = (PlayerBodyPartSlots)target;
        if (slots == null) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("에디터 미리보기", EditorStyles.boldLabel);

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Play 중에는 런타임으로 파츠가 붙습니다.", MessageType.Info);
            return;
        }

        EditorGUILayout.HelpBox(
            "파츠는 자동으로 붙지 않습니다. Prefab 수정 창에서 「미리보기 갱신」을 누르세요.\n" +
            "미리보기는 이름에 [TEMP_Preview]_ 가 붙고, 수정·저장되지 않도록 잠깁니다.\n" +
            "프리팹 저장 직전에 자동으로 제거됩니다.",
            MessageType.Info);

        bool canPreview = slots.IsEditorPreviewAllowed(out string blockReason);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!canPreview))
            {
                if (GUILayout.Button("미리보기 갱신"))
                    slots.RefreshEditorPreview();
            }

            if (GUILayout.Button("미리보기 제거"))
                slots.ClearEditorPreview();
        }

        if (!canPreview && !string.IsNullOrEmpty(blockReason))
            EditorGUILayout.HelpBox(blockReason, MessageType.None);

        EditorGUILayout.Space();
        if (GUILayout.Button("고아 미리보기 전부 제거"))
            PlayerBodyPartSlots.ClearAllEditorPreviewOrphansInOpenScenes();
    }
}
#endif
