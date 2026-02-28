using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[CustomEditor(typeof(CharacterSelectionController))]
public class CharacterSelectionControllerEditor : Editor
{
    private SerializedProperty charactersProp;
    private SerializedProperty portraitSlotSizeProp;
    private SerializedProperty portraitAreaBgColorProp;

    private void OnEnable()
    {
        charactersProp = serializedObject.FindProperty("characters");
        portraitSlotSizeProp = serializedObject.FindProperty("portraitSlotSize");
        portraitAreaBgColorProp = serializedObject.FindProperty("portraitAreaBgColor");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawDefaultInspector();

        EditorGUILayout.Space(16);
        EditorGUILayout.LabelField("초상화 Placeholder (에디터 모드 게임뷰)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("캐릭터 개수를 바꾼 뒤 아래 버튼을 누르면 씬의 placeholder가 갱신됩니다. 게임뷰에서 위치를 확인·조정할 수 있습니다.", MessageType.None);

        var ctrl = target as CharacterSelectionController;
        if (ctrl != null && GUILayout.Button("초상화 Placeholder 갱신"))
        {
            Undo.RecordObject(ctrl.transform, "Refresh Portrait Placeholder");
            SetupSceneTransitions.SetupPortraitPlaceholder(ctrl);
            EditorSceneManager.MarkSceneDirty(ctrl.gameObject.scene);
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Inspector 미리보기", EditorStyles.miniBoldLabel);

        if (charactersProp != null && charactersProp.isArray)
        {
            var slotSize = portraitSlotSizeProp != null ? portraitSlotSizeProp.vector2Value : new Vector2(120, 120);
            var bgColor = portraitAreaBgColorProp != null ? portraitAreaBgColorProp.colorValue : new Color(0.2f, 0.2f, 0.25f, 0.6f);

            var rect = GUILayoutUtility.GetRect(0, 120);
            var areaRect = new Rect(rect.x, rect.y, rect.width * 0.5f, rect.height);
            EditorGUI.DrawRect(areaRect, bgColor);
            EditorGUI.LabelField(new Rect(areaRect.x + 4, areaRect.y + 4, areaRect.width - 8, 18), "초상화 영역 (가로 배치)", EditorStyles.miniLabel);

            float x = areaRect.x + 12;
            float y = areaRect.y + 24;
            int count = charactersProp.arraySize;
            for (int i = 0; i < count; i++)
            {
                var slotRect = new Rect(x, y, Mathf.Min(72, slotSize.x * 0.6f), Mathf.Min(72, slotSize.y * 0.6f));
                EditorGUI.DrawRect(slotRect, new Color(0.4f, 0.4f, 0.5f, 0.8f));
                EditorGUI.LabelField(slotRect, $"{i + 1}\n{(int)slotSize.x}×{(int)slotSize.y}", EditorStyles.centeredGreyMiniLabel);

                x += slotRect.width + 8;
                if (x + 80 > areaRect.xMax) break;
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
