using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(CharacterSelectionController))]
public class CharacterSelectionControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var preview = ((CharacterSelectionController)target).GetComponent<CharacterSelectionScenePreview>();
        if (preview == null)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "Play 전 Scene View 미리보기: CharacterSelectionScenePreview 컴포넌트를 추가하면 " +
                "프리팹 필드에 넣은 캐릭터를 스폰 포인트에서 바로 볼 수 있습니다.",
                MessageType.Info);

            if (GUILayout.Button("씬 미리보기 컴포넌트 추가"))
            {
                var controller = (CharacterSelectionController)target;
                Undo.AddComponent<CharacterSelectionScenePreview>(controller.gameObject);
                EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            }
        }

        EditorGUILayout.Space(12);
        EditorGUILayout.HelpBox(
            "Play 전 Scene View에서 UI 깊이가 맞게 보여야 합니다.\n" +
            "Tools → Apply Character Selection Canvas Layering 으로도 적용할 수 있습니다.",
            MessageType.Info);

        if (GUILayout.Button("캔버스 레이어링 적용 (Play 전 미리보기)"))
        {
            CharacterSelectionEditModePreview.ApplyToOpenCharacterSelectionScene();
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "씬 UI 레이아웃은 Tools → Setup Character Selection Layout 메뉴로 구성합니다.",
            MessageType.Info);

        if (GUILayout.Button("캐릭터 선택 레이아웃 구성"))
        {
            SetupCharacterSelectionLayout.ApplyToOpenScene();
            if (target is CharacterSelectionController ctrl)
                EditorSceneManager.MarkSceneDirty(ctrl.gameObject.scene);
        }
    }
}

[CustomEditor(typeof(CharacterSelectionScenePreview))]
public class CharacterSelectionScenePreviewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var preview = (CharacterSelectionScenePreview)target;
        if (preview == null)
            return;

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "Preview Prefab에 PC_Pre_* 등 프리팹을 넣으면 Play 없이 Scene View에서 바로 보입니다.\n" +
            "미리보기 오브젝트는 씬에 저장되지 않습니다.",
            MessageType.Info);

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Play 중에는 런타임 캐릭터 표시를 사용합니다.", MessageType.None);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("미리보기 갱신"))
                preview.RefreshPreviewNow();

            if (GUILayout.Button("미리보기 제거"))
            {
                preview.ClearPreview();
                EditorSceneManager.MarkSceneDirty(preview.gameObject.scene);
            }
        }
    }
}
