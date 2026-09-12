using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StageSelectPrototype))]
public sealed class StageSelectPrototypeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();

        if (GUILayout.Button("Apply Reference Style"))
            StageSelectVisualPolish.ApplyStyle((StageSelectPrototype)target);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Preview Selected Stage"))
            ((StageSelectPrototype)target).ShowSelectedStageForEditor();
        if (GUILayout.Button("Preview Overview"))
            ((StageSelectPrototype)target).ShowSelectedOverviewForEditor();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "아래 버튼은 왼쪽 아래 뒤로 가기 버튼만 추가하거나 갱신합니다. " +
            "지도와 목록은 그대로 두므로, 손으로 조정한 배치가 초기화되지 않습니다.",
            MessageType.Info);
        if (GUILayout.Button("Add / Update Back Button"))
        {
            StageSelectPrototype prototype = (StageSelectPrototype)target;
            Undo.RegisterFullObjectHierarchyUndo(prototype.gameObject, "Add Back Button");
            prototype.EnsureBackButtonForEditor();
            EditorUtility.SetDirty(prototype);
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Stage Count를 바꾼 뒤 아래 버튼을 누르면 목록과 핀이 다시 만들어집니다. " +
            "이름이 비어 있거나 Unlocked가 꺼진 항목은 빈 비활성 버튼으로 표시됩니다.",
            MessageType.Info);
        if (GUILayout.Button("Rebuild Stage Select UI"))
        {
            StageSelectPrototype prototype = (StageSelectPrototype)target;
            Undo.RegisterFullObjectHierarchyUndo(prototype.gameObject, "Rebuild Stage Select UI");
            prototype.Rebuild();
            EditorUtility.SetDirty(prototype);
        }
    }

    private void OnSceneGUI()
    {
        StageSelectPrototype prototype = (StageSelectPrototype)target;
        Transform tile = prototype.transform.Find("StageSelectRoot/MapViewport/MapContent/MapTile_C");
        if (tile is not RectTransform tileRect) return;

        for (int stage = 1; stage <= prototype.StageCount; stage++)
        {
            StageSelectPrototype.StageEntry entry = prototype.Stages[stage - 1];
            if (!entry.unlocked) continue;

            Handles.color = entry.completed ? Color.green : Color.red;
            Vector2 local = new(
                (entry.mapX - tileRect.pivot.x) * tileRect.rect.width,
                (entry.mapY - tileRect.pivot.y) * tileRect.rect.height);
            Vector3 oldPosition = tileRect.TransformPoint(local);
            float size = HandleUtility.GetHandleSize(oldPosition) * .08f;
            EditorGUI.BeginChangeCheck();
            Vector3 newPosition = Handles.FreeMoveHandle(oldPosition, size, Vector3.zero, Handles.DotHandleCap);
            Handles.Label(oldPosition, $" {stage:00}");
            if (!EditorGUI.EndChangeCheck()) continue;

            Undo.RecordObject(prototype, "Move Stage Pin");
            local = tileRect.InverseTransformPoint(newPosition);
            Vector2 normalized = new(
                local.x / tileRect.rect.width + tileRect.pivot.x,
                local.y / tileRect.rect.height + tileRect.pivot.y);
            prototype.SetPinPositionFromEditor(stage, normalized);
            EditorUtility.SetDirty(prototype);
        }
    }
}
