#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DiabloStyleCamera))]
public class DiabloStyleCameraEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            "Pitch: 낮을수록 뒤에서, 90에 가까울수록 탑뷰\n" +
            "Distance: 캐릭터와 카메라 유지 거리\n" +
            "Follow Point: 캐릭터 자식 빈 오브젝트를 넣으면 추적 기준점으로 사용\n" +
            "Target Local Offset: Follow Point가 없을 때 캐릭터 기준 오프셋",
            MessageType.Info);
    }

    private void OnSceneGUI()
    {
        var camera = (DiabloStyleCamera)target;

        Vector3 anchor = camera.ResolveAnchorForGizmo();
        Vector3 camPos = anchor + camera.GetCameraOffset();

        Handles.color = new Color(0.3f, 0.8f, 1f, 0.9f);
        Handles.DrawLine(camPos, anchor);
        Handles.DrawWireDisc(anchor, Vector3.up, 0.25f);
    }
}
#endif
