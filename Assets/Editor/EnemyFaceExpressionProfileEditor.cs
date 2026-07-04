#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemyFaceExpressionProfile))]
public class EnemyFaceExpressionProfileEditor : Editor
{
    private SerializedProperty _groupsProp;
    private SerializedProperty _faceRendererProp;

    private void OnEnable()
    {
        _groupsProp = serializedObject.FindProperty("groups");
        _faceRendererProp = serializedObject.FindProperty("faceRenderer");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var profile = (EnemyFaceExpressionProfile)target;
        profile.EnsureFaceRenderer();

        EditorGUILayout.HelpBox(
            "텍스처 그룹마다 담당 상황을 지정하세요.\n" +
            "「기본(All)」 그룹 하나를 두면, 지정되지 않은 상황은 그 텍스처를 사용합니다.",
            MessageType.Info);

        EditorGUILayout.PropertyField(_faceRendererProp, new GUIContent("M_Face Renderer"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("표정 텍스처 그룹", EditorStyles.boldLabel);

        if (_groupsProp != null)
        {
            for (int i = 0; i < _groupsProp.arraySize; i++)
                DrawGroup(_groupsProp.GetArrayElementAtIndex(i), i);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("그룹 추가"))
            {
                _groupsProp.arraySize++;
                var newGroup = _groupsProp.GetArrayElementAtIndex(_groupsProp.arraySize - 1);
                ClearGroup(newGroup);
            }

            using (new EditorGUI.DisabledScope(_groupsProp == null || _groupsProp.arraySize <= 1))
            {
                if (GUILayout.Button("마지막 그룹 삭제"))
                    _groupsProp.arraySize--;
            }
        }

        DrawValidationWarnings();

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawGroup(SerializedProperty groupProp, int index)
    {
        var textureProp = groupProp.FindPropertyRelative("texture");
        var defaultProp = groupProp.FindPropertyRelative("isDefaultForAll");
        var situationsProp = groupProp.FindPropertyRelative("situations");

        string title = defaultProp.boolValue
            ? $"그룹 {index + 1} — 기본(All)"
            : $"그룹 {index + 1}";

        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(textureProp, new GUIContent("Texture"));

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(defaultProp, new GUIContent("기본(All)"));
        if (EditorGUI.EndChangeCheck() && defaultProp.boolValue)
            situationsProp.ClearArray();

        if (!defaultProp.boolValue)
            DrawSituationsMask(situationsProp);

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4);
    }

    private static void DrawSituationsMask(SerializedProperty situationsProp)
    {
        EditorGUILayout.LabelField("담당 상황", EditorStyles.miniBoldLabel);

        var values = (EnemyFaceSituation[])Enum.GetValues(typeof(EnemyFaceSituation));
        var current = new HashSet<EnemyFaceSituation>();
        for (int i = 0; i < situationsProp.arraySize; i++)
            current.Add((EnemyFaceSituation)situationsProp.GetArrayElementAtIndex(i).enumValueIndex);

        EditorGUILayout.BeginHorizontal();
        int col = 0;
        foreach (EnemyFaceSituation value in values)
        {
            bool selected = current.Contains(value);
            bool next = GUILayout.Toggle(selected, value.ToString(), GUI.skin.button, GUILayout.MinWidth(72f));
            if (next != selected)
            {
                if (next)
                    AddSituation(situationsProp, value);
                else
                    RemoveSituation(situationsProp, value);
            }

            col++;
            if (col >= 3)
            {
                col = 0;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private static void AddSituation(SerializedProperty situationsProp, EnemyFaceSituation value)
    {
        for (int i = 0; i < situationsProp.arraySize; i++)
        {
            if ((EnemyFaceSituation)situationsProp.GetArrayElementAtIndex(i).enumValueIndex == value)
                return;
        }

        situationsProp.arraySize++;
        situationsProp.GetArrayElementAtIndex(situationsProp.arraySize - 1).enumValueIndex = (int)value;
    }

    private static void RemoveSituation(SerializedProperty situationsProp, EnemyFaceSituation value)
    {
        for (int i = situationsProp.arraySize - 1; i >= 0; i--)
        {
            if ((EnemyFaceSituation)situationsProp.GetArrayElementAtIndex(i).enumValueIndex != value)
                continue;

            situationsProp.DeleteArrayElementAtIndex(i);
        }
    }

    private void DrawValidationWarnings()
    {
        if (_groupsProp == null) return;

        int defaultCount = 0;
        var assigned = new HashSet<EnemyFaceSituation>();

        for (int i = 0; i < _groupsProp.arraySize; i++)
        {
            var group = _groupsProp.GetArrayElementAtIndex(i);
            if (group.FindPropertyRelative("isDefaultForAll").boolValue)
                defaultCount++;

            var situations = group.FindPropertyRelative("situations");
            for (int s = 0; s < situations.arraySize; s++)
            {
                var situation = (EnemyFaceSituation)situations.GetArrayElementAtIndex(s).enumValueIndex;
                if (!assigned.Add(situation))
                {
                    EditorGUILayout.HelpBox(
                        $"'{situation}' 상황이 여러 그룹에 중복 지정되어 있습니다. 그룹당 한 번만 지정하세요.",
                        MessageType.Warning);
                }
            }
        }

        if (defaultCount == 0)
        {
            EditorGUILayout.HelpBox(
                "기본(All) 그룹이 없습니다. 지정되지 않은 상황은 표정이 바뀌지 않을 수 있습니다.",
                MessageType.Warning);
        }
        else if (defaultCount > 1)
        {
            EditorGUILayout.HelpBox(
                "기본(All) 그룹이 여러 개입니다. 하나만 켜 두는 것을 권장합니다.",
                MessageType.Warning);
        }
    }

    private static void ClearGroup(SerializedProperty groupProp)
    {
        groupProp.FindPropertyRelative("texture").objectReferenceValue = null;
        groupProp.FindPropertyRelative("isDefaultForAll").boolValue = false;
        groupProp.FindPropertyRelative("situations").ClearArray();
    }
}
#endif
