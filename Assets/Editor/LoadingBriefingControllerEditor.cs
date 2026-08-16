using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(LoadingBriefingController))]
public class LoadingBriefingControllerEditor : Editor
{
    private int previewCutIndex;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        var controller = (LoadingBriefingController)target;
        int cutCount = controller.CutCount;

        EditorGUILayout.Space(16);
        EditorGUILayout.LabelField("컷 구도 미리보기 / 저장", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1) 컷 인덱스를 고르고「이미지에 적용」→ Scene/Game 뷰에서 위치·크기 조절\n" +
            "2)「현재 상태를 컷에 저장」→ 이미지/스프라이트/대사(현재 언어 칸)가 그 컷에 기록됩니다.\n" +
            "재생: Fade In → 타이핑(홀드 시 2배) → Post Text Hold → Fade Out",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(cutCount <= 0))
        {
            previewCutIndex = EditorGUILayout.IntSlider("미리보기 컷 인덱스", previewCutIndex, 0, Mathf.Max(0, cutCount - 1));

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("이미지에 적용"))
            {
                Undo.RecordObject(controller, "Apply Briefing Cut");
                if (controller.BackgroundImageRect != null)
                    Undo.RecordObject(controller.BackgroundImageRect, "Apply Briefing Cut Rect");
                if (controller.BackgroundImage != null)
                    Undo.RecordObject(controller.BackgroundImage, "Apply Briefing Cut Image");
                if (controller.BriefingText != null)
                    Undo.RecordObject(controller.BriefingText, "Apply Briefing Cut Text");

                controller.ApplyCutToImage(previewCutIndex);
                MarkDirty(controller);
            }

            if (GUILayout.Button("현재 상태를 컷에 저장"))
            {
                Undo.RecordObject(controller, "Capture Briefing Cut");
                controller.CaptureImageToCut(previewCutIndex);
                MarkDirty(controller);
            }
            EditorGUILayout.EndHorizontal();
        }

        if (cutCount <= 0)
            EditorGUILayout.HelpBox("Cuts에 항목을 먼저 추가하세요.", MessageType.Warning);
    }

    private static void MarkDirty(LoadingBriefingController controller)
    {
        EditorUtility.SetDirty(controller);
        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
    }
}
